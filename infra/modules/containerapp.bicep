// Azure Container Apps: runs the API (and its background worker) from the image in the registry.

param location string
param tags object

param environmentName string = 'cae-sitehealth'
param logAnalyticsName string

@description('Full image name, e.g. acrsitehealthxyz.azurecr.io/sitehealth-api:1a2b3c4.')
param image string
param registryServer string

param identityId string
param identityClientId string

param sqlServerFqdn string
param databaseName string
param appInsightsConnectionString string

@description('The dashboard\'s address, allowed to call the API from browsers (CORS).')
param dashboardOrigin string

@description('Read the Anthropic API key from Key Vault. The secret "anthropic-api-key" must exist first.')
param enableAi bool
param keyVaultUri string

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsName
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// Signs in to SQL as the managed identity. No password: SqlClient gets a token from Azure for this identity.
// Connection Timeout 60: a paused serverless database can take up to a minute to wake.
var connectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${databaseName};Authentication=Active Directory Managed Identity;User Id=${identityClientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'

var aiSecrets = enableAi
  ? [
      {
        name: 'anthropic-api-key'
        keyVaultUrl: '${keyVaultUri}secrets/anthropic-api-key'
        identity: identityId
      }
    ]
  : []

var aiEnv = enableAi ? [{ name: 'ANTHROPIC_API_KEY', secretRef: 'anthropic-api-key' }] : []

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-sitehealth-api'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    environmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [{ server: registryServer, identity: identityId }]
      secrets: aiSecrets
    }
    template: {
      containers: [
        {
          name: 'api'
          image: image
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: concat(
            [
              { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
              { name: 'ConnectionStrings__SiteHealth', value: connectionString }
              { name: 'Database__MigrateOnStartup', value: 'true' }
              { name: 'Proxy__TrustForwardedHeaders', value: 'true' }
              { name: 'Cors__AllowedOrigins__0', value: dashboardOrigin }
              { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
            ],
            aiEnv
          )
          probes: [
            // Startup: up to 2.5 minutes for migrations and a sleeping database before the other probes begin
            {
              type: 'Startup'
              httpGet: { path: '/health', port: 8080 }
              periodSeconds: 5
              failureThreshold: 30
            }
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
              periodSeconds: 15
              failureThreshold: 4
            }
          ]
        }
      ]
      scale: {
        // 0: no cost while nobody uses it; the first request after a quiet spell takes a few seconds longer.
        // 1 at most: the scan queue and the rate-limit counters live in memory, so there must be one copy.
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

output url string = 'https://${api.properties.configuration.ingress.fqdn}'
