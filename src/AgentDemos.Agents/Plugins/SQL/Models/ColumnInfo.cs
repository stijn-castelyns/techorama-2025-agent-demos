namespace AgentDemos.Agents.Plugins.SQL.Models;

public class ColumnInfo
{
  public string Name { get; set; }
  public string DataType { get; set; }
  public int MaxLength { get; set; }
  public byte Precision { get; set; }
  public byte Scale { get; set; }
  public bool IsNullable { get; set; }
  public bool IsPrimaryKey { get; set; }
  public bool IsForeignKey { get; set; }
  public string Description { get; set; }
  public bool IsIdentity { get; set; }
  public int ColumnOrder { get; set; }
  public string DefaultValue { get; set; }
  public ForeignKeyReference ForeignKeyReference { get; set; }
}
