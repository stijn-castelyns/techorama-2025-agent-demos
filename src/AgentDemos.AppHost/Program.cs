var builder = DistributedApplication.CreateBuilder(args);


var sql = builder.AddSqlServer("sql")
                 .WithDataVolume();

var initScriptPath = Path.Join(Path.GetDirectoryName(typeof(Program).Assembly.Location), "Databases", "Northwind.sql");

var db = sql.AddDatabase("northwind")
  .WithCreationScript(File.ReadAllText(initScriptPath));

builder.AddProject<Projects.AgentDemos_ConsoleTests>("agentdemos-consoletests")
       .WithReference(db)
       .WaitFor(db); ;

builder.AddProject<Projects.AgentDemos_WebUI>("agentdemos-webui")
       .WithReference(db)
       .WaitFor(db);

builder.Build().Run();
