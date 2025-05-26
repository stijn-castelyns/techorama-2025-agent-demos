using AgentDemos.Agents.Plugins.DataVisualization;
using AgentDemos.Agents.Plugins.SQL;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Plugins.Core.CodeInterpreter;

namespace AgentDemos.Agents;

public static class U2UAgentFactory
{
  private const string SQL_PROMPT = """
      **Database Query Expert System**
      <purpose>
          You are a specialized SQL Server query architect with deep knowledge of Microsoft T-SQL syntax.
          Your primary function is to construct precise, optimized SQL queries through systematic exploration
          of database schema and data patterns.
      </purpose>

      <operational-protocol>
          1. **Schema Analysis First**
          - Begin every task by calling DescribeDatabase to understand tables, columns, and relationships
          - Identify primary/foreign keys, data types, and constraints before query construction

          2. **Data Pattern Exploration**
          - Use SampleTableData to examine actual data values and distributions
          - Verify date formats, string patterns, and numeric ranges through sampling

          3. **Iterative Validation**
          - Always test queries with RunTestSqlQuery before final execution
          - Analyze error messages carefully and modify approach accordingly
          - Validate result structure matches user requirements

          4. **Precision Execution**
          - Only use RunFinalSqlQuery when:
            * Schema understanding is complete
            * Test queries return expected results
            * SQL syntax is validated
            * Result set matches request parameters
            * If the user asks to persist the results, make sure to output the unaltered full path where the data is stored.

          5. **Tool Usage Requirements**
          - Always populate 'reasoning' parameter with detailed justification
          - Specify exact database/schema/table names from schema analysis
          - Handle NULLs and type conversions explicitly
          - Prefer CTEs over nested subqueries for readability
      </operational-protocol>

      <error-protocol>
          * On SQL errors:
            1. Analyze error message structure
            2. Check object names against DescribeDatabase results
            3. Verify data type compatibility
            4. Test problematic subqueries in isolation
            5. Report error details in reasoning before retrying
      </error-protocol>

      <performance-rules>
          * Use appropriate indexing hints from schema analysis
          * Prefer EXISTS over IN for subqueries
          * Limit result sets through WHERE clauses
          * Use pagination for large datasets
          * Avoid SELECT * in final queries
      </performance-rules>
      """;
  public static ChatCompletionAgent CreateSqlAgent(Kernel kernel, string? modelId = null, string? serviceId = null)
  {
    ThrowIfKernelHasPlugins(kernel);

    var executionSettings = GetPromptExecutionSettings(modelId, serviceId);

    if(executionSettings is null)
    {
      executionSettings = new PromptExecutionSettings()
      {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        ModelId = "gpt-4.1",
        ServiceId = "gpt-4.1-service"
      };
    }

    SqlPlugin sqlPlugin = kernel.GetRequiredService<SqlPlugin>();
    kernel.Plugins.AddFromObject(sqlPlugin);

    ChatCompletionAgent sqlAgent = new()
    {
      Name = "SqlAgent",
      Instructions = SQL_PROMPT,
      Description = "An agent specialized in querying a Microsoft SQL Server Database",
      Kernel = kernel,
      Arguments = new KernelArguments(executionSettings),
      InstructionsRole = AuthorRole.System
    };

    return sqlAgent;
  }

  public static IServiceCollection AddSqlAgentServices(this IServiceCollection services)
  {
    services.AddScoped<SqlPlugin>();
    return services;
  }

  public static ChatCompletionAgent CreateDataAnalysisAgent(Kernel kernel, string? modelId = null, string? serviceId = null)
  {
    ThrowIfKernelHasPlugins(kernel);

    var executionSettings = GetPromptExecutionSettings(modelId, serviceId);

    if (executionSettings is null)
    {
      executionSettings = new PromptExecutionSettings()
      {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        ModelId = "gpt-4.1",
        ServiceId = "gpt-4.1-service"
      };
    }

    var config = kernel.GetRequiredService<IConfiguration>();

    SessionsPythonPlugin ciPlugin = kernel.GetRequiredService<SessionsPythonPlugin>();

    kernel.Plugins.AddFromObject(ciPlugin);
    kernel.AutoFunctionInvocationFilters.Add(new PersistImageToWWWWRoot());

    ChatCompletionAgent agent = new()
    {
      Name = "DataAnalysisAgent",
      Description = "An agent specialized in csv dataset analysis and plot generation",
      Instructions = """
        <purpose>
          You are a specialized data analysis assistant.
          Your primary function is to analyze data and provide insights.
        </purpose>
        <operational-protocol>
          - When the location of a relevant file/dataset is mentioned, always upload it!
          - Make sure to always download images/plots you generate
          - Make sure to always create an appropriate plot based on the data you are analyzing
          - After downloading, always report the url where the user can find the image and 
            make sure to output the image url in markdown format, so that it can be rendered in the chat.
          </DataAnalysis>
        """,
      Kernel = kernel,
      Arguments = new KernelArguments(executionSettings),
      InstructionsRole = AuthorRole.System
    };

    return agent;
  }

  public static IServiceCollection AddDataAnalysisAgentServices(this IServiceCollection services)
  {
    var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

    services.AddHttpClient();

    string? cachedToken = null;

    async Task<string> TokenProvider(CancellationToken cancellationToken)
    {
      if (cachedToken is null)
      {
        string resource = "https://dynamicsessions.io/.default";
        var credential = new DefaultAzureCredential();

        // Attempt to get the token
        var accessToken = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext([resource]), cancellationToken).ConfigureAwait(false);

        cachedToken = accessToken.Token;
      }

      return cachedToken;
    }

    var settings = new SessionsPythonSettings(
            sessionId: Guid.NewGuid().ToString(),
            endpoint: new Uri(config["ACASessionsPool:Endpoint"]!));

    services.AddSingleton((sp)
        => new SessionsPythonPlugin(
            settings,
            sp.GetRequiredService<IHttpClientFactory>(),
            TokenProvider,
            sp.GetRequiredService<ILoggerFactory>()));
    return services;
  }

  public static ChatCompletionAgent CreateReportingAgent(Kernel kernel, string? modelId = null, string? serviceId = null)
  {
    ThrowIfKernelHasPlugins(kernel);

    var executionSettings = GetPromptExecutionSettings(modelId, serviceId);

    if (executionSettings is null)
    {
      executionSettings = new PromptExecutionSettings()
      {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        ModelId = "gpt-4.1",
        ServiceId = "gpt-4.1-service"
      };
    }

    var agentsPlugin = KernelPluginFactory.CreateFromFunctions("AgentPlugin",
    [
        AgentKernelFunctionFactory.CreateFromAgent(CreateSqlAgent(kernel.Clone(), "gpt-4.1", "gpt-4.1-service")),
        AgentKernelFunctionFactory.CreateFromAgent(CreateDataAnalysisAgent(kernel.Clone(), "gpt-4.1", "gpt-4.1-service"))
    ]);

    kernel.Plugins.Add(agentsPlugin);

    ChatCompletionAgent reportingAgent = new()
    {
      Name = "ReportingAgent",
      Description = "An agent specialized in generating reports",
      Instructions = """
        <purpose>
          You are a specialized reporting assistant.
          Your primary function is to generate reports based on data analysis and SQL queries.
        </purpose>
        <operational-protocol>
          Delegate work appropriately to the SQL and Data Analysis agents.
          When delegating work to the Sql agent, make sure to mention that data needs to be persisted.
          When requesting the analysis from the Data Analysis agent, make sure to provide the absolute path to the dataset that needs to be analysed.
          When requesting the analysis from the Data Analysis agent, make sure to ask for relevant plots, and instruct the agent to download the plots as well
          This absolute path should normally be reported by the sql agent.
          Make sure to output markdown image links when the Data Analysis agent with url to online hosted image
          Make sure to always ask the sql agent to return the retrieved data in markdown format as well
        </operational-protocol>
        """,
      Kernel = kernel,
      Arguments = new KernelArguments(executionSettings),
      InstructionsRole = AuthorRole.System
    };

    return reportingAgent;
  }

  private static void ThrowIfKernelHasPlugins(Kernel kernel)
  {
    if (!(kernel is not null && kernel.Plugins.IsNullOrEmpty()))
    {
      throw new ArgumentException("Kernel should not have any plugins when an agent.");
    }
  }

  public static PromptExecutionSettings? GetPromptExecutionSettings(string? modelId, string? serviceId)
  {
    if(modelId is null && serviceId is null)
    {
      return null;
    }
    if(modelId.StartsWith("gemini"))
    {
      return new GeminiPromptExecutionSettings()
      {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions,
        ModelId = modelId,
        ServiceId = serviceId,
      };
    }
    return new PromptExecutionSettings()
    {
      FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
      ModelId = modelId,
      ServiceId = serviceId
    };
  }
}
