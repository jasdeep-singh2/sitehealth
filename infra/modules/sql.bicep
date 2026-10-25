// Azure SQL: a logical server and one serverless database on the free offer.
// Sign-in is Microsoft Entra ID only: no SQL passwords exist. The person deploying is the server's Entra admin;
// the API gets its own database user (created by infra/deploy.ps1, since templates can't run SQL).

param location string
param tags object
param suffix string

@description('Object ID of the Entra user who will be the SQL server admin (the person deploying).')
param adminObjectId string

@description('Sign-in name of that user, e.g. someone@example.com.')
param adminLogin string

param databaseName string = 'sitehealth'

resource server 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: 'sql-sitehealth-${suffix}'
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      sid: adminObjectId
      tenantId: tenant().tenantId
      principalType: 'User'
    }
  }
}

// The free offer: up to 100,000 vCore seconds and 32 GB per month, one database per subscription.
// When the free amount runs out, the database pauses until next month instead of charging.
resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: server
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

// "Allow Azure services": lets Container Apps reach the server without a private network. It opens the
// firewall to any Azure address, but every connection still needs a valid Entra sign-in.
// A private network (VNet) is the stricter option for later.
resource allowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverName string = server.name
output serverFqdn string = server.properties.fullyQualifiedDomainName
output databaseName string = database.name
