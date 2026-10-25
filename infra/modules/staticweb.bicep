// Azure Static Web Apps (Free): serves the React dashboard from a global CDN. Files are uploaded by
// infra/deploy.ps1 with a deployment token, so no GitHub link is needed here.

@description('Static Web Apps only exists in a few regions. The dashboard is served worldwide either way; this is just where its settings live.')
param location string
param tags object

resource site 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-sitehealth'
  location: location
  tags: tags
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

output name string = site.name
output hostname string = site.properties.defaultHostname
