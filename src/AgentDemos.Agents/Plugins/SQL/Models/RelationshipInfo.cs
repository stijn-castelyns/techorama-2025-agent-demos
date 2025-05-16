namespace AgentDemos.Agents.Plugins.SQL.Models;

public class RelationshipInfo
{
  public string ForeignKeyName { get; set; }
  public TableReference ParentTable { get; set; }
  public TableReference ReferencedTable { get; set; }
  public string DeleteAction { get; set; }
  public string UpdateAction { get; set; }
}