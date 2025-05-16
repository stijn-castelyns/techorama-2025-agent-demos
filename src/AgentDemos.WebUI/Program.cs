using AgentDemos.Agents;
using AgentDemos.Agents.Plugins.CourseRecommendation;
using AgentDemos.Infra.Infra;
using AgentDemos.WebUI.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

builder.Services.AddAzureOpenAIChatCompletion(endpoint: builder.Configuration["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: builder.Configuration["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "gpt-4.1");

builder.Services.AddAzureOpenAITextEmbeddingGeneration(endpoint: builder.Configuration["AzureOpenAIAIF:Endpoint"]!,
                                              apiKey: builder.Configuration["AzureOpenAIAIF:AzureKeyCredential"]!,
                                              deploymentName: "text-embedding-3-small");

builder.Services.AddSqlAgentServices();
builder.Services.AddCourseRecommendationAgentServices();

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
