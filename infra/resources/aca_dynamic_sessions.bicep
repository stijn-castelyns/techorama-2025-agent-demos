@description('The Object ID (Principal ID) of the user to whom the roles will be assigned. Found in Microsoft Entra ID for the user.')
param userPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)
var acaDynsessionsName = toLower('acads${uniqueSuffix}')

resource sessionPool 'Microsoft.App/sessionPools@2025-01-01' = {
  name: acaDynsessionsName
  location: 'North Europe'
  identity: {
    type: 'None'
  }
  properties: {
    poolManagementType: 'Dynamic'
    containerType: 'PythonLTS'
    scaleConfiguration: {
      maxConcurrentSessions: 10
    }
    dynamicPoolConfiguration: {
      lifecycleConfiguration: {
        lifecycleType: 'Timed'
        cooldownPeriodInSeconds: 300
      }
    }
    sessionNetworkConfiguration: {
      status: 'EgressDisabled'
    }
    managedIdentitySettings: []
  }
}

@description('Role Definition ID for Azure ContainerApps Session Executor.')
var acaSessionExecutorRoleDefId = '0fb8eba5-a2bb-4abe-b1c1-49dfad359bb0'

@description('Role Definition ID for Azure ContainerApps Session Contributor.')
var acaSessionContributorRoleDefId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'

// Role Assignment: Azure ContainerApps Session Executor
resource sessionExecutorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sessionPool.id, userPrincipalId, acaSessionExecutorRoleDefId)
  scope: sessionPool
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', acaSessionExecutorRoleDefId)
    principalId: userPrincipalId
    principalType: 'User' // Assuming the principal is a user. Change to 'ServicePrincipal' or 'Group' if needed.
  }
}

// Role Assignment: Azure ContainerApps Session Contributor
resource sessionContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sessionPool.id, userPrincipalId, acaSessionContributorRoleDefId)
  scope: sessionPool
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', acaSessionContributorRoleDefId)
    principalId: userPrincipalId
    principalType: 'User' // Assuming the principal is a user. Change to 'ServicePrincipal' or 'Group' if needed.
  }
}

output management_endpoint string = sessionPool.properties.poolManagementEndpoint
