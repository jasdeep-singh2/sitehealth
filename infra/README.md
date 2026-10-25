# Deploying SiteHealth to Azure

Everything lives in one resource group (`rg-sitehealth` by default), described by the Bicep templates in this
folder and deployed by `deploy.ps1`.

```text
Browser ──► Static Web Apps (dashboard) ──► Container Apps (API + worker) ──► Azure SQL (free offer)
                                                   │ managed identity: no passwords
                                                   ├──► Container Registry (images)
                                                   ├──► Key Vault (Anthropic API key)
                                                   └──► Application Insights (telemetry)
```

| Resource | Name | Notes |
|---|---|---|
| Container Apps | `ca-sitehealth-api` | 0.5 vCPU, 1 GiB. Scales to zero when idle, and to at most 1 copy (the queue lives in memory). |
| Azure SQL | `sql-sitehealth-<suffix>` / `sitehealth` | Serverless, free offer. Entra ID sign-in only: there are no SQL passwords. |
| Container Registry | `acrsitehealth<suffix>` | Basic. The API pulls with its managed identity. |
| Key Vault | `kv-sitehealth-<suffix>` | Holds `anthropic-api-key`. Role-based access. |
| Static Web Apps | `stapp-sitehealth` | Free. Region East Asia (Static Web Apps isn't offered in Central India; it's served worldwide anyway). |
| Log Analytics + Application Insights | `log-sitehealth`, `appi-sitehealth` | 30 days of logs, capped at 0.2 GB a day. |
| Managed identity | `id-sitehealth-api` | What the API signs in as. |
| Budget | `budget-sitehealth` | Emails at 80% of the monthly amount and when the forecast passes it. |

## What it costs

At learning-project traffic, roughly **US$5–7 a month**, almost all of it the Container Registry's Basic tier
(about $5). The free SQL offer, the Free Static Web App, Container Apps' monthly free grant and a small log cap
cover the rest. The budget emails you if spending heads past the amount you set (10 by default).

The SQL free offer is one database per subscription. If you already use it elsewhere, the deployment fails;
change `useFreeLimit` in `modules/sql.bicep` (and expect a cost) or remove the other one.

## First deployment

You need the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), the .NET 10 SDK and Node.js 24,
and an Azure subscription where you can create resources and assign roles (Owner, or Contributor plus
User Access Administrator).

1. Sign in, and pick the subscription if you have more than one:

   ```bash
   az login
   ```

   ```bash
   az account set --subscription "<name or id>"
   ```

2. From the repository root, run the deployment. It asks for confirmation before creating anything:

   ```bash
   powershell -ExecutionPolicy Bypass -File infra\deploy.ps1
   ```

   The first run takes about 10–15 minutes. It prints the dashboard and API addresses at the end.

`deploy.ps1` is safe to run again; that's also how to deploy a new version until GitHub Actions does it.

### Why two passes?

The container app can only start once its image is in the registry and its managed identity has a database
user. So the script deploys everything else first, creates the database user (templates can't run SQL), pushes
the image, then deploys again with the API. While creating the database user, it briefly allows your computer's
IP through the SQL firewall and removes it again afterwards.

## Switching on AI explanations

Add your Anthropic API key to Key Vault, then deploy with `-EnableAi`. The key never goes in a template,
a parameter or the repository:

```bash
az keyvault secret set --vault-name <kv-sitehealth-...> --name anthropic-api-key --value "<your key>"
```

```bash
powershell -ExecutionPolicy Bypass -File infra\deploy.ps1 -EnableAi
```

The Key Vault name is printed at the end of a deployment, or: `az keyvault list -g rg-sitehealth --query [].name -o tsv`.

## Useful commands

```bash
az containerapp logs show -g rg-sitehealth -n ca-sitehealth-api --follow
```

```bash
az containerapp revision list -g rg-sitehealth -n ca-sitehealth-api -o table
```

## Deleting everything

This removes every resource and stops all costs. Key Vault keeps a deleted vault for 7 days before its name can
be reused.

```bash
az group delete --name rg-sitehealth
```
