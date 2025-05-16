namespace AgentDemos.Agents.Plugins.SQL.Models;


public class DatabaseSchema
{
  public List<TableInfo> Tables { get; set; } = new List<TableInfo>();
  public List<RelationshipInfo> Relationships { get; set; } = new List<RelationshipInfo>();
  public List<ViewInfo> Views { get; set; } = new List<ViewInfo>();
  public List<StoredProcedureInfo> StoredProcedures { get; set; } = new List<StoredProcedureInfo>();
}
