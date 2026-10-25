<#
.SYNOPSIS
    Deploys SiteHealth to Azure: infrastructure, the API container, its database user, and the dashboard.

.DESCRIPTION
    Run it from the repository root after `az login`:

        powershell -ExecutionPolicy Bypass -File infra\deploy.ps1

    (-ExecutionPolicy Bypass applies to this one run only; it doesn't change your machine's settings.)

    It's safe to run again: every step checks what already exists. Steps:
      1. Check you're signed in and confirm the subscription.
      2. Deploy the infrastructure without the API (Bicep, pass 1).
      3. Give the API's managed identity its own database user.
      4. Build the API image with dotnet publish and push it to the registry.
      5. Deploy again with the API (pass 2) and wait until it's healthy.
      6. Build the dashboard for the API's address and upload it to Static Web Apps.

.PARAMETER EnableAi
    Give the API the Anthropic API key. Add it to Key Vault first (see infra\README.md).

.PARAMETER Yes
    Don't ask for confirmation before deploying.
#>
[CmdletBinding()]
param(
    [string]$ResourceGroup = 'rg-sitehealth',
    [string]$Location = 'centralindia',
    [int]$BudgetAmount = 10,
    [switch]$EnableAi,
    [switch]$SkipWeb,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$template = Join-Path $PSScriptRoot 'main.bicep'

# Runs the Azure CLI and stops the script if it fails. (PowerShell doesn't stop on a failing program by itself.)
function Az {
    $output = & az @args
    if ($LASTEXITCODE -ne 0) { throw "az $($args[0]) $($args[1]) failed (exit code $LASTEXITCODE)." }
    return $output
}

function Step([string]$text) { Write-Host "`n== $text" -ForegroundColor Cyan }

# --- 1. Who and where -------------------------------------------------------------------------------------------
Step 'Checking your Azure sign-in'
$account = Az account show --query '{name:name, id:id, user:user.name}' -o json | ConvertFrom-Json
$deployerId = Az ad signed-in-user show --query id -o tsv
$deployerLogin = Az ad signed-in-user show --query userPrincipalName -o tsv
Write-Host "Subscription: $($account.name) ($($account.id))"
Write-Host "Signed in as: $($account.user)"
Write-Host "Resource group: $ResourceGroup in $Location, budget $BudgetAmount per month"

if (-not $Yes) {
    $answer = Read-Host 'This creates resources in that subscription, some of which cost money. Continue? (y/n)'
    if ($answer -ne 'y') { Write-Host 'Stopped. Nothing was changed.'; exit 1 }
}

Az group create --name $ResourceGroup --location $Location --tags app=sitehealth -o none

$commonParams = @(
    "deployerObjectId=$deployerId",
    "deployerLogin=$deployerLogin",
    "budgetEmail=$($account.user)",
    "budgetAmount=$BudgetAmount"
)

# --- 2. Infrastructure, pass 1 ----------------------------------------------------------------------------------
Step 'Deploying the infrastructure (pass 1 of 2, a few minutes the first time)'
$outputs = Az deployment group create --resource-group $ResourceGroup --name sitehealth-infra `
    --template-file $template --parameters @commonParams deployApi=false `
    --query properties.outputs -o json | ConvertFrom-Json
$out = @{}
foreach ($p in $outputs.PSObject.Properties) { $out[$p.Name] = $p.Value.value }

# --- 3. Database user for the API --------------------------------------------------------------------------------
Step "Giving the API's identity access to the database"

# Creates a contained database user for the managed identity, matched by its client ID (the SID), so SQL doesn't
# need to look it up in Entra ID. Read, write, and ddladmin so EF Core migrations can create tables. Safe to rerun.
$grantSql = @'
DECLARE @sid nvarchar(100) = CONVERT(nvarchar(100), CONVERT(varbinary(16), CONVERT(uniqueidentifier, @clientId)), 1);
DECLARE @sql nvarchar(max) = N'';
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @name)
    SET @sql += N'CREATE USER ' + QUOTENAME(@name) + N' WITH SID = ' + @sid + N', TYPE = E; ';
SET @sql += N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@name) + N'; '
          + N'ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@name) + N'; '
          + N'ALTER ROLE db_ddladmin ADD MEMBER ' + QUOTENAME(@name) + N';';
EXEC sp_executesql @sql;
'@

$firewallRule = 'deploy-script-client'
try {
    for ($attempt = 1; ; $attempt++) {
        # A fresh token each attempt; your own Entra sign-in is the server's admin
        $token = Az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
        $connection = New-Object System.Data.SqlClient.SqlConnection("Server=tcp:$($out.sqlServerFqdn),1433;Database=$($out.databaseName);Encrypt=True;Connect Timeout=60;")
        $connection.AccessToken = $token
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            $command.CommandText = $grantSql
            [void]$command.Parameters.AddWithValue('@name', $out.apiIdentityName)
            [void]$command.Parameters.AddWithValue('@clientId', $out.apiIdentityClientId)
            [void]$command.ExecuteNonQuery()
            Write-Host "Database user '$($out.apiIdentityName)' is ready."
            break
        }
        catch [System.Data.SqlClient.SqlException] {
            $message = $_.Exception.Message
            if ($attempt -ge 6) { throw }

            # The server's firewall only lets Azure in. SQL's refusal names this computer's public IP; allow it briefly.
            if ($message -match "IP address '([0-9a-fA-F\.:]+)'") {
                Write-Host "Allowing this computer ($($Matches[1])) through the SQL firewall for this step..."
                Az sql server firewall-rule create --resource-group $ResourceGroup --server $out.sqlServerName `
                    --name $firewallRule --start-ip-address $Matches[1] --end-ip-address $Matches[1] -o none
                Start-Sleep -Seconds 15  # firewall changes take a moment
            }
            else {
                # Most likely the serverless database waking up
                Write-Host "Database not ready yet ($($_.Exception.Number)); retrying in 20 seconds..."
                Start-Sleep -Seconds 20
            }
        }
        finally {
            $connection.Dispose()
        }
    }
}
finally {
    # Remove the temporary rule, so only Azure services can reach the server again
    & az sql server firewall-rule delete --resource-group $ResourceGroup --server $out.sqlServerName --name $firewallRule -o none 2>$null
}

# --- 4. API image -----------------------------------------------------------------------------------------------
Step 'Building the API image and pushing it to the registry'
$commit = (& git -C $repoRoot rev-parse --short HEAD).Trim()
$tag = "$commit-$(Get-Date -Format 'yyyyMMddHHmm')"
$image = "$($out.registryLoginServer)/sitehealth-api:$tag"

# dotnet publish pushes with a short-lived registry token, passed in environment variables for this step only
$registryToken = Az acr login --name $out.registryName --expose-token --query accessToken -o tsv
try {
    $env:DOTNET_CONTAINER_REGISTRY_UNAME = '00000000-0000-0000-0000-000000000000'
    $env:DOTNET_CONTAINER_REGISTRY_PWORD = $registryToken
    & dotnet publish (Join-Path $repoRoot 'src\SiteHealth.Api') -c Release -t:PublishContainer `
        "-p:ContainerRegistry=$($out.registryLoginServer)" "-p:ContainerImageTag=$tag"
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
}
finally {
    Remove-Item Env:DOTNET_CONTAINER_REGISTRY_UNAME, Env:DOTNET_CONTAINER_REGISTRY_PWORD -ErrorAction SilentlyContinue
}

# --- 5. Infrastructure, pass 2: the API --------------------------------------------------------------------------
if ($EnableAi) {
    & az keyvault secret show --vault-name $out.keyVaultName --name anthropic-api-key --query id -o tsv 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "-EnableAi needs the secret 'anthropic-api-key' in Key Vault '$($out.keyVaultName)'. See infra\README.md."
    }
}

Step 'Deploying the API (pass 2 of 2)'
$outputs = Az deployment group create --resource-group $ResourceGroup --name sitehealth-infra `
    --template-file $template --parameters @commonParams deployApi=true "apiImage=$image" "enableAi=$($EnableAi.IsPresent.ToString().ToLower())" `
    --query properties.outputs -o json | ConvertFrom-Json
$apiUrl = $outputs.apiUrl.value

Step "Waiting for the API to report ready at $apiUrl/health/ready"
$ready = $false
for ($i = 0; $i -lt 30 -and -not $ready; $i++) {
    try {
        $ready = (Invoke-WebRequest -Uri "$apiUrl/health/ready" -UseBasicParsing -TimeoutSec 30).StatusCode -eq 200
    }
    catch { Start-Sleep -Seconds 10 }
}
if (-not $ready) {
    Write-Warning "The API didn't report ready within 5 minutes. Check its logs: az containerapp logs show -g $ResourceGroup -n ca-sitehealth-api --follow"
}
else { Write-Host 'API is ready.' }

# --- 6. Dashboard ------------------------------------------------------------------------------------------------
if (-not $SkipWeb) {
    Step 'Building the dashboard and uploading it to Static Web Apps'
    Push-Location (Join-Path $repoRoot 'web')
    try {
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }

        # The dashboard calls the API at this address in production (see web/src/api/client.ts)
        $env:VITE_API_BASE_URL = $apiUrl
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'npm run build failed.' }

        # The SWA CLI reads the deployment token from this variable, so it never appears on a command line
        $env:SWA_CLI_DEPLOYMENT_TOKEN = Az staticwebapp secrets list --name $out.staticWebAppName --resource-group $ResourceGroup --query properties.apiKey -o tsv
        & npx.cmd --yes @azure/static-web-apps-cli deploy ./dist --env production
        if ($LASTEXITCODE -ne 0) { throw 'Uploading the dashboard failed.' }
    }
    finally {
        Remove-Item Env:VITE_API_BASE_URL, Env:SWA_CLI_DEPLOYMENT_TOKEN -ErrorAction SilentlyContinue
        Pop-Location
    }
}

Step 'Done'
Write-Host "Dashboard: $($out.dashboardUrl)"
Write-Host "API:       $apiUrl"
