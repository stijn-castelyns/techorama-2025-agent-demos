@description('Location for all resources.')
param location string = 'swedencentral'

@description('The SKU for the Azure AI Services account.')
@allowed([
  'S0'
])
param sku string = 'S0'

@description('Array of model deployments. Each object should specify name, modelName, modelVersion, and capacity.')
param modelDeployments array

// Variables for unique naming based on the resource group
var uniqueSuffix = uniqueString(resourceGroup().id)
var aiServiceAccountName = toLower('aisvc${uniqueSuffix}') // Example: aisvcxxxxxxxxxxxxx (18 chars)
var customDomainName = toLower('aicd${uniqueSuffix}')     // Example: aicdxxxxxxxxxxxxx (18 chars)

// Azure AI Services Account
resource openAIService 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: aiServiceAccountName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: sku
  }
  kind: 'AIServices' // Changed from 'OpenAI' to 'AIServices' as 'OpenAI' is deprecated for kind
  properties: {
    customSubDomainName: customDomainName
    // Other properties like publicNetworkAccess can be added here if needed
  }
}

@batchSize(1)
// Azure OpenAI Model Deployments (using a loop)
resource modelDeploymentResources 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = [for (deployment, i) in modelDeployments: {
  parent: openAIService
  name: deployment.name
  properties: {
    model: {
      format: 'OpenAI'
      name: deployment.modelName
      version: deployment.modelVersion
    }
  }
  sku: {
    name: 'GlobalStandard' // Standard SKU for PTU (Provisioned Throughput Units)
    capacity: deployment.capacity
  }
}]

// Outputs
output openAIServiceEndpoint string = openAIService.properties.endpoint
output openAIServicePrimaryKey string = openAIService.listKeys().key1
