using AgentDemos.Agents;
using AgentDemos.Agents.Plugins;
using AgentDemos.Infra.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

var appBuilder = Host.CreateApplicationBuilder();

appBuilder.AddServiceDefaults();

var app = appBuilder.Build();

app.Start();

IConfigurationRoot config = new ConfigurationBuilder()
                                .AddUserSecrets<Program>()
                                .Build();

IKernelBuilder builder = Kernel.CreateBuilder();

builder.Services.AddAzureOpenAIChatCompletion(endpoint: config["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: config["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "gpt-4.1");

builder.Services.AddAzureOpenAITextEmbeddingGeneration(endpoint: config["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: config["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "text-embedding-3-small");

builder.Services.AddDbContext<U2UTrainingDb>(options =>
{
  options.UseSqlServer(config.GetConnectionString("U2UTrainingDb"), opts =>
  {
    opts.UseVectorSearch();
  });
});

builder.Plugins.AddFromType<CourseRecommendationPlugin>();

Kernel kernel = builder.Build();

ChatCompletionAgent courseRecommendationAgent = U2UAgentFactory.CreateCourseRecommendationAgent(kernel);

ChatHistory chat = [];
ChatHistoryAgentThread chatThread = new ChatHistoryAgentThread(chat);

Console.WriteLine("Start chatting with the Course Recommendation Agent! (type 'exit' to quit)\n");

// Conversation loop
while (true)
{
  Console.ForegroundColor = ConsoleColor.White;
  Console.Write("User > ");
  string? userInput = Console.ReadLine();

  if (string.IsNullOrWhiteSpace(userInput) || userInput.Trim().ToLower() == "/exit")
    break;

  if (userInput.Trim().ToLower() == "/clear")
  {
    chatThread.ChatHistory.Clear();
    Console.WriteLine("Chat History Reset!\n");
    continue;
  }
  
  chat.Add(new ChatMessageContent(AuthorRole.User, userInput));

  // Generate the agent response(s)
  await foreach (ChatMessageContent response in courseRecommendationAgent.InvokeAsync(chatThread))
  {
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("AI > " + response.ToString());
  }
  Console.ResetColor();
}

Console.ResetColor();
Console.WriteLine("Conversation ended.");

Console.ReadLine();