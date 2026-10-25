// SiteHealth on Azure: everything in one resource group.
//
//   Browser ──► Static Web Apps (dashboard) ──► Container Apps (API + worker) ──► Azure SQL (free offer)
//                                                      │ managed identity
//                                                      ├──► Container Registry (images)
//                                                      ├──► Key Vault (Anthropic API key)
//                                                      └──► Application Insights (telemetry)
//
// Deployed by infra/deploy.ps1 in two passes: first without the API (deployApi = false), because its image and
// database user don't exist yet; then again with the image (deployApi = true). See infra/README.md.

targetScope = 'resourceGroup'

param location string = resourceGroup().location

@description('Static Web Apps is only offered in some regions; East Asia is the closest to Central India.')
param staticWebAppLocation string = 'eastasia'

@description('Object ID of the person deploying (az ad signed-in-user show --query id). Becomes SQL admin and Key Vault secrets officer.')
param deployerObjectId string

@description('Sign-in name of the person deploying, e.g. someone@example.com.')
param deployerLogin string

@description('Where budget warnings go.')
param budgetEmail string

@description('Monthly budget for the resource group, in the billing currency.')
param budgetAmount int = 10

@description('Create or update the API container app. False on the very first pass.')
param deployApi bool = false

@description('The API image to run, e.g. acrsitehealthxyz.azurecr.io/sitehealth-api:1a2b3c4. Required when deployApi is true.')
param apiImage string = ''

@description('Give the API the Anthropic API key from Key Vault. Add the secret "anthropic-api-key" first.')
param enableAi bool = false

// A short, stable suffix from the resource group's ID, for names that must be unique across all of Azure
var suffix = uniqueString(resourceGroup().id)
var tags = { app: 'sitehealth' }

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: { location: location, tags: tags }
}

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: { location: location, tags: tags }
}

module registry 'modules/registry.bicep' = {
  name: 'registry'
  params: { location: location, tags: tags, suffix: suffix, pullPrincipalId: identity.outputs.principalId }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    tags: tags
    suffix: suffix
    readerPrincipalId: identity.outputs.principalId
    adminPrincipalId: deployerObjectId
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    tags: tags
    suffix: suffix
    adminObjectId: deployerObjectId
    adminLogin: deployerLogin
  }
}

module staticWeb 'modules/staticweb.bicep' = {
  name: 'staticweb'
  params: { location: staticWebAppLocation, tags: tags }
}

module budget 'modules/budget.bicep' = {
  name: 'budget'
  params: { amount: budgetAmount, contactEmail: budgetEmail }
}

// Uses outputs from registry and keyVault, so Bicep creates it after them, including their role assignments:
// the identity can pull the image and read the secret by the time the container starts.
module api 'modules/containerapp.bicep' = if (deployApi) {
  name: 'containerapp'
  params: {
    location: location
    tags: tags
    logAnalyticsName: monitoring.outputs.logAnalyticsName
    image: apiImage
    registryServer: registry.outputs.loginServer
    identityId: identity.outputs.id
    identityClientId: identity.outputs.clientId
    sqlServerFqdn: sql.outputs.serverFqdn
    databaseName: sql.outputs.databaseName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    dashboardOrigin: 'https://${staticWeb.outputs.hostname}'
    enableAi: enableAi
    keyVaultUri: keyVault.outputs.uri
  }
}

output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer
output keyVaultName string = keyVault.outputs.name
output sqlServerName string = sql.outputs.serverName
output sqlServerFqdn string = sql.outputs.serverFqdn
output databaseName string = sql.outputs.databaseName
output apiIdentityName string = identity.outputs.name
output apiIdentityClientId string = identity.outputs.clientId
output staticWebAppName string = staticWeb.outputs.name
output dashboardUrl string = 'https://${staticWeb.outputs.hostname}'
output apiUrl string = deployApi ? api!.outputs.url : ''
