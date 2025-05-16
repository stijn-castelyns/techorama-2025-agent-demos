namespace AgentDemos.Agents.Plugins.SQL.Models;

  public class TableInfo
  {
    public string Name { get; set; }
    public string Schema { get; set; }
    public string Description { get; set; }
    public List<ColumnInfo> Columns { get; set; } = new List<ColumnInfo>();
    public List<IndexInfo> Indexes { get; set; } = new List<IndexInfo>();
  }
