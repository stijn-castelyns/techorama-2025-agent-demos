using AgentDemos.Agents;
using AgentDemos.Agents.Plugins;
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

builder.Services.AddDbContext<U2UTrainingDb>(options =>
{
  options.UseSqlServer(builder.Configuration.GetConnectionString("U2UTrainingDb"), opts =>
  {
    opts.UseVectorSearch();
  });
});

builder.Services.AddScoped<CourseRecommendationPlugin>();

builder.Services.AddScoped(serv =>
{
  KernelPluginCollection kernelFunctions = new KernelPluginCollection([KernelPluginFactory.CreateFromType<CourseRecommendationPlugin>(serviceProvider: serv)]);
  Kernel kernel = new Kernel(serv, kernelFunctions);
  return kernel;
});

builder.Services.AddScoped<CourseRecommendationAgent>();

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
