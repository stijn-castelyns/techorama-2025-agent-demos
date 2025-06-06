﻿using AgentDemos.Agents;
using AgentDemos.Agents.Plugins.CourseRecommendation;
using AgentDemos.Infra.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = Host.CreateApplicationBuilder();

builder.AddServiceDefaults();
builder.AddSqlServerClient(connectionName: "northwind");

IConfigurationRoot config = new ConfigurationBuilder()
                                .AddUserSecrets<Program>()
                                .Build();

builder.Services.AddSingleton<IConfiguration>(config);
builder.Services.AddAzureOpenAIChatCompletion(endpoint: config["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: config["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "gpt-4.1-mini");

builder.Services.AddAzureOpenAITextEmbeddingGeneration(endpoint: config["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: config["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "text-embedding-3-small");

builder.Services.AddCourseRecommendationAgentServices();
builder.Services.AddDataAnalysisAgentServices();
builder.Services.AddSqlAgentServices();

builder.AddSqlServerClient(connectionName: "northwind");

var app = builder.Build();

app.Start();

Kernel kernel = new Kernel(builder.Services.BuildServiceProvider());

//ChatCompletionAgent agent = U2UAgentFactory.CreateDataAnalysisCCAgent(kernel);
//ChatCompletionAgent agent = U2UAgentFactory.CreateCourseRecommendationAgent(kernel);
//ChatCompletionAgent agent = U2UAgentFactory.CreateSqlAgent(kernel);
ChatCompletionAgent agent = U2UAgentFactory.CreateReportingAgent(kernel);
//Agent agent = await U2UAgentFactory.CreateDataAnalysisAgent(kernel);

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
  await foreach (ChatMessageContent response in agent.InvokeAsync(chatThread, new AgentInvokeOptions()
  {
    OnIntermediateMessage = (message) =>
    {
      
      return Task.CompletedTask;
    },
  }))
  {
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("AI > " + response.ToString());
  }
  Console.ResetColor();
}

Console.ResetColor();
Console.WriteLine("Conversation ended.");

Console.ReadLine();