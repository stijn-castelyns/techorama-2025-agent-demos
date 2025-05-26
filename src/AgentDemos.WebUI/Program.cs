using AgentDemos.Agents;
using AgentDemos.WebUI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

builder.Services.AddAzureOpenAIChatCompletion(endpoint: builder.Configuration["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: builder.Configuration["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "gpt-4.1");

builder.Services.AddAzureOpenAIChatCompletion(endpoint: builder.Configuration["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: builder.Configuration["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "gpt-4.1",
                                              serviceId: "gpt-4.1-service");

builder.Services.AddAzureOpenAIChatCompletion(endpoint: builder.Configuration["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: builder.Configuration["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "gpt-4.1-mini",
                                              serviceId: "gpt-4.1-mini-service");

builder.Services.AddAzureOpenAIChatCompletion(endpoint: builder.Configuration["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: builder.Configuration["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "gpt-4o",
                                              serviceId: "gpt-4o-service");

builder.Services.AddGoogleAIGeminiChatCompletion(modelId: "gemini-2.5-pro-preview-05-06", 
                                                 apiKey: builder.Configuration["GoogleGemini:Key"]!, 
                                                 serviceId: "gemini-2.5-pro-preview-05-06-service");

builder.Services.AddGoogleAIGeminiChatCompletion(modelId: "gemini-2.5-flash-preview-05-20",
                                                 apiKey: builder.Configuration["GoogleGemini:Key"]!,
                                                 serviceId: "gemini-2.5-flash-preview-05-20-service");

builder.AddSqlServerClient(connectionName: "northwind");

builder.Services.AddSqlAgentServices();
builder.Services.AddDataAnalysisAgentServices();

builder.Services.AddScoped<Kernel>((serv) =>
{
  Kernel kernel = new Kernel(serv);
  return kernel;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Error", createScopeForErrors: true);
  // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
  app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
