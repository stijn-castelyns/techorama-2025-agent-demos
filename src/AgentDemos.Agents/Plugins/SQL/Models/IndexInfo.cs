namespace AgentDemos.Agents.Plugins.SQL.Models;

public class IndexInfo
{
  public string Name { get; set; }
  public bool IsUnique { get; set; }
  public bool IsPrimaryKey { get; set; }
  public string Type { get; set; }
  public string Columns { get; set; }
}