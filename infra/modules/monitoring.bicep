// Where logs and telemetry go: a Log Analytics workspace (the store) and Application Insights (the app view on top).

param location string
param tags object

@description('Stop collecting logs for the day after this many GB. Keeps a noisy bug from becoming a big bill.')
param dailyCapGb string = '0.2'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-sitehealth'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json(dailyCapGb) }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-sitehealth'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

output logAnalyticsName string = logs.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
