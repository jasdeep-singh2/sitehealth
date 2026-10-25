// Key Vault: holds the Anthropic API key. Access is by Azure role (RBAC), not vault access policies:
// the API identity can read secrets, the person deploying can add them. The secret itself is never in a
// template or a parameter; it's added with `az keyvault secret set` (see infra/README.md).

param location string
param tags object
param suffix string

@description('Principal ID of the API identity, which may read secrets.')
param readerPrincipalId string

@description('Object ID of the person deploying, who may add and change secrets.')
param adminPrincipalId string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  // Vault names are global and at most 24 characters
  name: 'kv-sitehealth-${take(suffix, 8)}'
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    // Deleted vaults and secrets can be recovered for 7 days (the minimum), then the name is free again
    softDeleteRetentionInDays: 7
  }
}

var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var secretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource apiCanRead 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, readerPrincipalId, secretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource adminCanWrite 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, adminPrincipalId, secretsOfficerRoleId)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficerRoleId)
    principalId: adminPrincipalId
    principalType: 'User'
  }
}

output name string = vault.name
output uri string = vault.properties.vaultUri
