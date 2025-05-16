namespace AgentDemos.Agents.Plugins.SQL.Models;

public class ForeignKeyReference
{
  public string ReferencedSchema { get; set; }
  public string ReferencedTable { get; set; }
  public string ReferencedColumn { get; set; }
}
