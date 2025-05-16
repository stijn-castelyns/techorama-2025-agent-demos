using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentDemos.Infra.Infra;

public class U2UTrainingDbContextFactory : IDesignTimeDbContextFactory<U2UTrainingDb>
{
  public U2UTrainingDb CreateDbContext(string[] args)
  {
    IConfigurationBuilder configurationBuilder = new ConfigurationBuilder().AddUserSecrets<U2UTrainingDb>();

    IConfiguration config = configurationBuilder.Build();

    string connectionString = config.GetConnectionString("U2UTrainingDb")!;

    DbContextOptionsBuilder<U2UTrainingDb> dbContextOptionsBuilder = new();

    dbContextOptionsBuilder.UseSqlServer(connectionString, options =>
    {
      options.UseVectorSearch();
    });

    return new U2UTrainingDb(dbContextOptionsBuilder.Options);
  }
}
