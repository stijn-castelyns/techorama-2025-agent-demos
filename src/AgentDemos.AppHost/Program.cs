var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AgentDemos_ConsoleTests>("agentdemos-consoletests");



builder.AddProject<Projects.AgentDemos_WebUI>("agentdemos-webui");



builder.Build().Run();
