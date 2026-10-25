// The API's managed identity: an Azure-managed account the container signs in with, so there are no passwords
// or keys for the registry, Key Vault or the database. "User-assigned" means it exists on its own, so it can be
// given permissions before the container app is created.

param location string
param tags object

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-sitehealth-api'
  location: location
  tags: tags
}

output id string = identity.id
output name string = identity.name
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
