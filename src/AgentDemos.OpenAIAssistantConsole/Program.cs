using AgentDemos.Agents;
using AgentDemos.Agents.Plugins.CourseRecommendation;
using AgentDemos.Infra.Infra;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Assistants;
using OpenAI.Files;
using System.ClientModel;
using System.Diagnostics;

var appBuilder = Host.CreateApplicationBuilder();

appBuilder.AddServiceDefaults();

var app = appBuilder.Build();

app.Start();

IConfigurationRoot config = new ConfigurationBuilder()
                                .AddUserSecrets<Program>()
                                .Build();

IKernelBuilder builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IConfiguration>(config);
builder.Services.AddAzureOpenAIChatCompletion(endpoint: config["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: config["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "gpt-4.1");

builder.Services.AddCourseRecommendationAgentServices();

Kernel kernel = builder.Build();

OpenAIAssistantAgent agent = await U2UAgentFactory.CreateDataAnalysisAgent(kernel);

OpenAIAssistantAgentThread agentThread = new OpenAIAssistantAgentThread(agent.Client);

AzureOpenAIClient client = OpenAIAssistantAgent
   .CreateAzureOpenAIClient(apiKey: new ApiKeyCredential(config["AzureOpenAIAIF:AzureKeyCredential"]!),
                            endpoint: new Uri(config["AzureOpenAIAIF:Endpoint"]!));

Console.WriteLine("Start chatting with the Agent! (type 'exit' to quit)\n");

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
    await agentThread.DeleteAsync();
    agentThread = new OpenAIAssistantAgentThread(agent.Client);
    Console.WriteLine("Chat History Reset!\n");
    continue;
  }
  ChatMessageContent chatMessageContent = new ChatMessageContent(AuthorRole.User, userInput);
  // Generate the agent response(s)
  await foreach (AgentResponseItem<ChatMessageContent> response in agent.InvokeAsync(chatMessageContent, thread: agentThread))
  {
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("AI > " + response.Message.ToString());

    await DownloadResponseImageAsync(response.Message);

    agentThread = (OpenAIAssistantAgentThread)response.Thread;
  }
  Console.ResetColor();
}

Console.ResetColor();
Console.WriteLine("Conversation ended.");

Console.ReadLine();

async Task DownloadResponseContentAsync(ChatMessageContent message)
{
  OpenAIFileClient fileClient = client.GetOpenAIFileClient();

  foreach (KernelContent item in message.Items)
  {
    if (item is AnnotationContent annotation)
    {
      await DownloadFileContentAsync(fileClient, annotation.ReferenceId!);
    }
  }
}

async Task DownloadResponseImageAsync(ChatMessageContent message)
{
  OpenAIFileClient fileClient = client.GetOpenAIFileClient();

  foreach (KernelContent item in message.Items)
  {
    if (item is FileReferenceContent fileReference)
    {
      await DownloadFileContentAsync(fileClient, fileReference.FileId, launchViewer: true);
    }
  }
}

async Task DownloadFileContentAsync(OpenAIFileClient fileClient, string fileId, bool launchViewer = false)
{
  OpenAIFile fileInfo = fileClient.GetFile(fileId);
  if (fileInfo.Purpose == FilePurpose.AssistantsOutput)
  {
    string filePath = Path.Combine(Path.GetTempPath(), Path.GetFileName(fileInfo.Filename));
    if (launchViewer)
    {
      filePath = Path.ChangeExtension(filePath, ".png");
    }

    BinaryData content = await fileClient.DownloadFileAsync(fileId);
    File.WriteAllBytes(filePath, content.ToArray());
    Console.WriteLine($"  File #{fileId} saved to: {filePath}");

    if (launchViewer)
    {
      Process.Start(
          new ProcessStartInfo
          {
            FileName = "cmd.exe",
            Arguments = $"/C start {filePath}"
          });
    }
  }
}