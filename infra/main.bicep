@description('The Object ID (Principal ID) of the user to whom the roles will be assigned. Found in Microsoft Entra ID for the user.')
param userPrincipalId string

module openai 'resources/openai.bicep' = {
  name: 'openai-deployment'
  params: {
    modelDeployments: [
      {
        name: 'gpt-4.1'
        modelName: 'gpt-4.1'
        modelVersion: '2025-04-14'
        capacity: 50
      }
      {
        name: 'gpt-4.1-mini'
        modelName: 'gpt-4.1-mini'
        modelVersion: '2025-04-14'
        capacity: 200
      }
    ]
  }
}

module aca_dyn_sessions 'resources/aca_dynamic_sessions.bicep' = {
  name: 'aca-dynamic-sessions-deployment'
  params: {
    userPrincipalId: userPrincipalId
  }
}

output openAIServiceEndpoint string = openai.outputs.openAIServiceEndpoint
output openAIServicePrimaryKey string = openai.outputs.openAIServicePrimaryKey
output acaDynSessionsManagementEndpoint string = aca_dyn_sessions.outputs.management_endpoint
