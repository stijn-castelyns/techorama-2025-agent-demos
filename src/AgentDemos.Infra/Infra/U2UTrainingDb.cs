using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using AgentDemos.Infra.Entities;

namespace AgentDemos.Infra.Infra;

public class U2UTrainingDb : DbContext
{
  public DbSet<Course> Courses { get; set; }
  public DbSet<Chapter> Chapters { get; set; }

  public U2UTrainingDb(DbContextOptions dbContextOptions) : base(dbContextOptions)
  {
    
  }

  public U2UTrainingDb()
  {
    
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetAssembly(typeof(U2UTrainingDb))!);
  }
}
