# Techorama 2025 Agent Demos

This repository contains demonstration applications showcasing AI agent capabilities using Azure OpenAI, Semantic Kernel, and .NET Aspire.

## Overview

This demo application includes:

- AI agent functionalities with Semantic Kernel
- SQL data analysis capabilities
- Course recommendation capabilities
- Dynamic session management with Azure Container Apps
- Integration with multiple AI models (Azure OpenAI and Google Gemini)

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Azure subscription
- Google AI API key (for Gemini models)
- Microsoft Entra ID (formerly Azure AD) account with access to create resources

## Deployment

### 1. Deploy Azure Resources

Click the "Deploy to Azure" button above or deploy using Azure CLI:

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fstijn-castelyns%2Ftechorama-2025-agent-demos%2Frefs%2Fheads%2Fmain%2Finfra%2Farm-build%2Fmain.json)

```powershell
$userPrincipalId = (az ad signed-in-user show --query id -o tsv)
az deployment group create --resource-group <YOUR_RESOURCE_GROUP_NAME> --template-file ./infra/arm-build/main.json --parameters userPrincipalId=$userPrincipalId
```

The deployment creates:
- Azure OpenAI service with GPT-4.1 and GPT-4.1-mini models
- Azure Container Apps environment for dynamic sessions

### 2. Configure User Secrets

After successful deployment, use the output values to configure your user secrets:

```powershell
# Create user secrets for the WebUI project
cd src/AgentDemos.WebUI
dotnet user-secrets init
dotnet user-secrets set "AzureOpenAIAIF:Endpoint" "<openAIServiceEndpoint>"
dotnet user-secrets set "AzureOpenAIAIF:AzureKeyCredential" "<openAIServicePrimaryKey>"
dotnet user-secrets set "GoogleGemini:Key" "<your-google-gemini-api-key>"
dotnet user-secrets set "AzureContainerApps:ManagementEndpoint" "<acaDynSessionsManagementEndpoint>"
```

## Change WWWRoot path
Change the path in `src/AgentDemos.Agents/Plugins/DataVisualization/PersistImageToWWWWRoot.cs` to your own path. Ran out of time, this could have been cleaner :(

## Running the Application

Use .NET Aspire to run the application locally:

```powershell
cd src/AgentDemos.AppHost
dotnet run
```

This will:
1. Start a SQL Server container
2. Initialize the Northwind database
3. Launch the WebUI application
4. Open the .NET Aspire dashboard in your browser

Access the application at: `https://localhost:5001` (or the port specified in your launchSettings.json)

## Solution Structure

- `AgentDemos.AppHost`: .NET Aspire application host
- `AgentDemos.WebUI`: Web interface for the agent demos
- `AgentDemos.Agents`: Core agent functionality and plugins
- `AgentDemos.Infra`: Infrastructure and data components
- `AgentDemos.ConsoleTests`: Console application for testing
- `AgentDemos.ServiceDefaults`: Shared service configurations

## Demos

### SQL Workflow
Sample questions:
```text
How have our monthly sales evolved over the lifetime of our company?
```

```text
Hoe zijn onze maandelijkse verkoopscijfers geevolueerd over het bestaan van ons bedrijf?
```

```text
Who is our best sales person in total number of sales?
```

### SQL Agent
Sample questions:
```text
How have our monthly sales evolved over the lifetime of our company?
```

```text
What is the monthly Year-over-Year (YoY) percentage change in gross profit for each product category, and which categories show the most significant growth or decline over the past three full years?
```

```text
Which products are most frequently purchased together in the same order? Can we identify top 3 product bundles for each product category that could be promoted?
```

```text
How does the performance (average sales, number of orders per employee) of employees correlate with their manager? Are there specific managers whose teams consistently outperform others?
```

### Data Analysis Agent
Sample questions:
```text
Model the population evolution of rabbits, and show the results in a graph
```

### Reporting Agent
Sample questions:
```text
How have the monthly sales of our top 3 sales people evolved over the lifetime of our company? Include both tables and graphs
```

## Troubleshooting

### Common Issues

1. **Connection Issues with Azure OpenAI**: Verify your endpoint and API key in user secrets
2. **Docker Issues**: Ensure Docker Desktop is running
3. **SQL Server Connection**: Check if SQL Server container is running (`docker ps`)

### Logs

Application logs are available in the .NET Aspire dashboard.