using AgentDemos.Agents.Plugins.CourseRecommendation;
using AgentDemos.Agents.Plugins.SQL;
using AgentDemos.Infra.Infra;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Assistants;
using OpenAI.Files;
using System.ClientModel;
using static Dapper.SqlMapper;

namespace AgentDemos.Agents;

public static class U2UAgentFactory
{
  private const string COURSE_RECOMMENDATION_PROMPT = """
    <purpose>
      You are an IT-training catalogue specialist.  
      Your task is to propose the most relevant U2U courses.
      Always well-structured answers in plain language.
    </purpose>

    <operational-protocol>
      1. <AnalyseUserRequest>
           • Extract key technologies, skill level, audience type,  
             preferred duration, and prerequisites from the user text.
           • If the user does not mention a specific technology, continue
             with the search, but add a note in reasoning.
           • Make sure the user mentions their skill level in the topics
             they are interested in, if not, ask them to clarify.
           • Create an “overview” paragraph to feed into the plugin.
         </AnalyseUserRequest>

      2. <InitialSearch>
           • Call SearchOverviewsAsync once, setting  
             MinNumberOfDays and MaxNumberOfDays exactly to what
             the user requested (or respectively 1 and 5 if omitted).  
           • Pass NrOfCoursesToReturn = 5.
           • Populate reasoning with: overview text, chosen day-range,
             and why these parameters meet the request.
         </InitialSearch>

      3. <EvaluateResults>
           • Check each Summary and Audience against the user need, if it
             seems that the plugin returned irrelevant results,  
             then:  
               – do not mention the courses to the user,  
               – if no courses are returned, remove the duration filter and re-run search.
         </EvaluateResults>

      4. <ReturnResults>
       • If one or more relevant courses are identified based on the <EvaluateResults> step:
         – **Identify the single MOST relevant course.**
         – **Present the Top Recommendation:**
           * Start with a clear heading, for example: "## Top Recommendation for You"
           * **Course Title & Url:** Display as "[Course Title](WebPageUrl)"
           * **Why it's a good match:** Briefly explain why this course is the top recommendation, linking it directly to the user's stated needs, key technologies, and skill level.
           * **Summary:** Provide the full summary of the course.
           * **Audience:** Describe the intended audience for this course.
           * **Duration:** State the course duration (e.g., "Duration: [Duration-In-Days] days").
           * **More Info:** Provide the `WebPageUrl` (e.g., "Learn more and enroll: [WebPageUrl]").

         – **List Other Potentially Relevant Courses (if any: be critical here, only show courses that address the user's need):**
           * If other relevant courses were found, present them under a heading like: "### Other Courses You Might Find Interesting"
           * For each of these courses:
             * **Course Title with Url and Course Length:** Display as "[Course Title](WebPageUrl) (Duration-In-Days)"
             * **Brief Relevance:** Provide a single, concise sentence explaining why this course might still be of interest. This sentence should highlight how it relates to the user's request or how it offers an alternative (e.g., "This course also covers [Key Technology] but is targeted at a [different skill level/audience], which could be suitable if you're looking for [specific alternative focus]." or "Consider this option if you're interested in a [shorter/longer] duration focusing on [related topic]." or "While the primary focus differs, this course touches upon [User's Interest Area B] which you also mentioned.").
           * Use bullet points for each of these additional courses to ensure clarity.

       • If no relevant courses are found after all search attempts (including any retries with broader filters as per <error-protocol>):
         – Apologise to the user clearly and politely.
         – Briefly explain that no courses perfectly matched their specific criteria (e.g., "I couldn't find any U2U courses that precisely match your request for [mention key criteria like technology, skill level, duration if specific].").
         – Suggest that the user try rephrasing their request, providing more details, or broadening their criteria (e.g., "You could try rephrasing your needs, or perhaps we can explore options with a different duration or focus?").

       • **General Formatting Guidelines for Output:**
         – Use clear, plain language.
         – Structure the response logically with headings and bullet points for easy readability.
         – Ensure all mentioned course details (Title, Code, Summary, Audience, WebPageUrl, Duration-In-Days) are taken directly from the plugin output and are not hallucinated.
    </ReturnResults>

      5. <SearchChapters>
           • Once the user has shown interest in a course and requests more detailed info about it,  
             call SearchChaptersForCourseAsync with the course code: 
                - If the user asks for specific or most relevant topics in a course, call the SearchChaptersForCourseAsync once for each topic and search the description based on what the user is looking for.
                - If the user does not mention specific topics, call the SearchChaptersForCourseAsync once for the course code without a chapter description, which will return all chapters.
         </SearchChapters>

      6. <ToolUsage>
           • Always fill the “reasoning” argument.  
           • Never hallucinate courses or chapters not present in plugin output.
    </operational-protocol>

    <irrelevant-results-definition>
      * A course is deemed irrelevant if:  
        – the course prerequisites do not match the user profile,  
        – if applicable: the programming languge in the user request does not match that of the course
    </irrelevant-results-definition>

    <error-protocol>
      * If plugin returns no XML, or malformed XML:  
        – Log the issue in reasoning;  
        – Retry once with broader filters;  
        – If still failing, apologise and ask the user to rephrase.
    </error-protocol>

    <performance-rules>
      * Retries capped at three search calls per user request.  
    </performance-rules>
    """;
  public static ChatCompletionAgent CreateCourseRecommendationAgent(Kernel kernel)
  {
    ThrowIfKernelHasPlugins(kernel);
    CourseRecommendationPlugin courseRecommendationPlugin = kernel.GetRequiredService<CourseRecommendationPlugin>();
    kernel.Plugins.AddFromObject(courseRecommendationPlugin);
    ChatCompletionAgent courseRecommendationAgent = new ChatCompletionAgent()
    {
      Name = "CourseRecommendationAgent",
      Instructions = COURSE_RECOMMENDATION_PROMPT,
      Kernel = kernel,
      Arguments = new KernelArguments(
          new PromptExecutionSettings()
          {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
          }),
      InstructionsRole = AuthorRole.System
    };

    return courseRecommendationAgent;
  }

  public static IServiceCollection AddCourseRecommendationAgentServices(this IServiceCollection services)
  {
    var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
    services.AddDbContext<U2UTrainingDb>(options =>
    {
      options.UseSqlServer(configuration.GetConnectionString("U2UTrainingDb"), opts =>
      {
        opts.UseVectorSearch();
      });
    });
    services.AddAzureOpenAITextEmbeddingGeneration(endpoint: configuration["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: configuration["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "text-embedding-3-small");
    services.AddScoped<CourseRecommendationPlugin>();

    return services;
  }

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
  public static ChatCompletionAgent CreateSqlAgent(Kernel kernel)
  {
    ThrowIfKernelHasPlugins(kernel);
    SqlPlugin sqlPlugin = kernel.GetRequiredService<SqlPlugin>();
    kernel.Plugins.AddFromObject(sqlPlugin);

    ChatCompletionAgent sqlAgent = new()
    {
      Name = "SqlAgent",
      Instructions = SQL_PROMPT,
      Kernel = kernel,
      Arguments = new KernelArguments(
          new OpenAIPromptExecutionSettings()
          {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0  // Reduce creativity for precise SQL generation

          }),
      InstructionsRole = AuthorRole.System
    };

    return sqlAgent;
  }

  public static IServiceCollection AddSqlAgentServices(this IServiceCollection services)
  {
    services.AddSingleton<DapperConnectionProvider>();
    services.AddScoped<SqlPlugin>();
    return services;
  }

  public static async Task<OpenAIAssistantAgent> CreateDataAnalysisAgent(Kernel kernel)
  {
    var config = kernel.GetRequiredService<IConfiguration>();
    
    AzureOpenAIClient client = OpenAIAssistantAgent
      .CreateAzureOpenAIClient(apiKey: new ApiKeyCredential(config["AzureOpenAIAIF:AzureKeyCredential"]!), 
                               endpoint: new Uri(config["AzureOpenAIAIF:Endpoint"]!));

    //OpenAIFileClient fileClient = client.GetOpenAIFileClient();
    
    //OpenAIFile fileDataCountryList = await fileClient.UploadFileAsync(@"https://people.sc.fsu.edu/~jburkardt/data/csv/crash_catalonia.csv", FileUploadPurpose.Assistants);
    
    AssistantClient assistantClient = client.GetAssistantClient();
    Assistant assistant =
        await assistantClient.CreateAssistantAsync(
            "gpt-4.1",
            name: "SampleAssistantAgent",
            instructions:
                    """
                        Always format response using markdown.
                        Always include a numerical index that starts at 1 for any lists or tables.
                        Always sort lists in ascending order.
                        """,
            enableCodeInterpreter: true);

    OpenAIAssistantAgent agent = new(assistant, assistantClient);

    return agent;
  }

  private static void ThrowIfKernelHasPlugins(Kernel kernel)
  {
    if(!(kernel is not null && kernel.Plugins.IsNullOrEmpty()))
    {
      throw new ArgumentException("Kernel should not have any plugins when an agent.");
    }
  }
}
