using AgentDemos.Agents.Plugins.SQL.Models;
using ConsoleTables;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Data;
using System.Text;
using System.Text.Json;

namespace AgentDemos.Agents.Plugins.SQL;

public class SqlPlugin(SqlConnection sqlConnection, ILogger<SqlPlugin> logger)
{
  private readonly SqlConnection _sqlConnection = sqlConnection;
  private readonly ILogger<SqlPlugin> _logger = logger;

  [KernelFunction, Description("Loads the table definitions and relationships of a specific Microsoft Sql Server database")]
  public async Task<string> ExtractSchemaForLlmAsync()
  {
    var schema = new DatabaseSchema();

    await _sqlConnection.OpenAsync();

    // Get all tables
    schema.Tables = await GetTablesAsync(_sqlConnection);

    // Get all columns for each table
    foreach (var table in schema.Tables)
    {
      table.Columns = await GetColumnsForTableAsync(_sqlConnection, table.Schema, table.Name);
    }

    // Get all relationships
    schema.Relationships = await GetRelationshipsAsync(_sqlConnection);

    // Get all indexes
    foreach (var table in schema.Tables)
    {
      table.Indexes = await GetIndexesForTableAsync(_sqlConnection, table.Schema, table.Name);
    }

    // Get all stored procedures
    schema.StoredProcedures = await GetStoredProceduresAsync(_sqlConnection);

    // Get all views
    schema.Views = await GetViewsAsync(_sqlConnection);

    string stringSchema = FormatSchemaForLlm(schema);
    Console.WriteLine($"SAMPLING TABLE: \n{stringSchema}");

    await _sqlConnection.CloseAsync();

    return stringSchema;
  }

  private async Task<List<TableInfo>> GetTablesAsync(SqlConnection connection)
  {
    var tables = new List<TableInfo>();

    const string sql = @"
                SELECT 
                    t.name AS TableName,
                    s.name AS SchemaName,
                    ISNULL(ep.value, '') AS TableDescription
                FROM 
                    sys.tables t
                INNER JOIN 
                    sys.schemas s ON t.schema_id = s.schema_id
                LEFT JOIN 
                    sys.extended_properties ep ON ep.major_id = t.object_id 
                    AND ep.minor_id = 0 
                    AND ep.name = 'MS_Description'
                ORDER BY 
                    s.name, t.name";

    using (var command = new SqlCommand(sql, connection))
    using (var reader = await command.ExecuteReaderAsync())
    {
      while (await reader.ReadAsync())
      {
        tables.Add(new Models.TableInfo
        {
          Name = reader["TableName"].ToString(),
          Schema = reader["SchemaName"].ToString(),
          Description = reader["TableDescription"].ToString()
        });
      }
    }

    return tables;
  }

  private async Task<List<ColumnInfo>> GetColumnsForTableAsync(SqlConnection connection, string schemaName, string tableName)
  {
    var columns = new List<ColumnInfo>();

    const string sql = @"
                SELECT 
                    c.name AS ColumnName,
                    t.name AS DataType,
                    c.max_length AS MaxLength,
                    c.precision AS Precision,
                    c.scale AS Scale,
                    c.is_nullable AS IsNullable,
                    CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                    CASE WHEN fk.parent_column_id IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey,
                    ISNULL(ep.value, '') AS ColumnDescription,
                    c.is_identity AS IsIdentity,
                    c.column_id AS ColumnOrder,
                    CASE WHEN dc.definition IS NOT NULL THEN dc.definition ELSE '' END AS DefaultValue
                FROM 
                    sys.columns c
                INNER JOIN 
                    sys.tables tb ON c.object_id = tb.object_id
                INNER JOIN 
                    sys.schemas s ON tb.schema_id = s.schema_id
                INNER JOIN 
                    sys.types t ON c.user_type_id = t.user_type_id
                LEFT JOIN 
                    sys.indexes pk_idx ON tb.object_id = pk_idx.object_id AND pk_idx.is_primary_key = 1
                LEFT JOIN 
                    sys.index_columns pk ON pk.object_id = pk_idx.object_id AND pk.index_id = pk_idx.index_id AND pk.column_id = c.column_id
                LEFT JOIN 
                    sys.foreign_key_columns fk ON fk.parent_object_id = tb.object_id AND fk.parent_column_id = c.column_id
                LEFT JOIN 
                    sys.extended_properties ep ON ep.major_id = tb.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description'
                LEFT JOIN
                    sys.default_constraints dc ON c.default_object_id = dc.object_id
                WHERE 
                    s.name = @SchemaName AND tb.name = @TableName
                ORDER BY 
                    c.column_id";

    using (var command = new SqlCommand(sql, connection))
    {
      command.Parameters.AddWithValue("@SchemaName", schemaName);
      command.Parameters.AddWithValue("@TableName", tableName);

      using (var reader = await command.ExecuteReaderAsync())
      {
        while (await reader.ReadAsync())
        {
          columns.Add(new ColumnInfo
          {
            Name = reader["ColumnName"].ToString(),
            DataType = reader["DataType"].ToString(),
            MaxLength = Convert.ToInt32(reader["MaxLength"]),
            Precision = Convert.ToByte(reader["Precision"]),
            Scale = Convert.ToByte(reader["Scale"]),
            IsNullable = Convert.ToBoolean(reader["IsNullable"]),
            IsPrimaryKey = Convert.ToBoolean(reader["IsPrimaryKey"]),
            IsForeignKey = Convert.ToBoolean(reader["IsForeignKey"]),
            Description = reader["ColumnDescription"].ToString(),
            IsIdentity = Convert.ToBoolean(reader["IsIdentity"]),
            ColumnOrder = Convert.ToInt32(reader["ColumnOrder"]),
            DefaultValue = reader["DefaultValue"].ToString()
          });
        }
      }
    }

    // For foreign key columns, get the referenced table and column
    foreach (var column in columns.Where(c => c.IsForeignKey))
    {
      const string fkSql = @"
                    SELECT 
                        rs.name AS ReferencedSchemaName,
                        rt.name AS ReferencedTableName,
                        rc.name AS ReferencedColumnName
                    FROM 
                        sys.foreign_key_columns fk
                    INNER JOIN 
                        sys.tables pt ON fk.parent_object_id = pt.object_id
                    INNER JOIN 
                        sys.schemas ps ON pt.schema_id = ps.schema_id
                    INNER JOIN 
                        sys.columns pc ON fk.parent_object_id = pc.object_id AND fk.parent_column_id = pc.column_id
                    INNER JOIN 
                        sys.tables rt ON fk.referenced_object_id = rt.object_id
                    INNER JOIN 
                        sys.schemas rs ON rt.schema_id = rs.schema_id
                    INNER JOIN 
                        sys.columns rc ON fk.referenced_object_id = rc.object_id AND fk.referenced_column_id = rc.column_id
                    WHERE 
                        ps.name = @SchemaName AND pt.name = @TableName AND pc.name = @ColumnName";

      using (var command = new SqlCommand(fkSql, connection))
      {
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@ColumnName", column.Name);

        using (var reader = await command.ExecuteReaderAsync())
        {
          if (await reader.ReadAsync())
          {
            column.ForeignKeyReference = new ForeignKeyReference
            {
              ReferencedSchema = reader["ReferencedSchemaName"].ToString(),
              ReferencedTable = reader["ReferencedTableName"].ToString(),
              ReferencedColumn = reader["ReferencedColumnName"].ToString()
            };
          }
        }
      }
    }

    return columns;
  }

  private async Task<List<RelationshipInfo>> GetRelationshipsAsync(SqlConnection connection)
  {
    var relationships = new List<RelationshipInfo>();

    const string sql = @"
                SELECT 
                    fk.name AS ForeignKeyName,
                    ps.name AS ParentSchemaName,
                    pt.name AS ParentTableName,
                    pc.name AS ParentColumnName,
                    rs.name AS ReferencedSchemaName,
                    rt.name AS ReferencedTableName,
                    rc.name AS ReferencedColumnName,
                    fk.delete_referential_action AS DeleteAction,
                    fk.update_referential_action AS UpdateAction
                FROM 
                    sys.foreign_keys fk
                INNER JOIN 
                    sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN 
                    sys.tables pt ON fkc.parent_object_id = pt.object_id
                INNER JOIN 
                    sys.schemas ps ON pt.schema_id = ps.schema_id
                INNER JOIN 
                    sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
                INNER JOIN 
                    sys.tables rt ON fkc.referenced_object_id = rt.object_id
                INNER JOIN 
                    sys.schemas rs ON rt.schema_id = rs.schema_id
                INNER JOIN 
                    sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
                ORDER BY 
                    ps.name, pt.name, pc.name";

    using (var command = new SqlCommand(sql, connection))
    using (var reader = await command.ExecuteReaderAsync())
    {
      while (await reader.ReadAsync())
      {
        relationships.Add(new RelationshipInfo
        {
          ForeignKeyName = reader["ForeignKeyName"].ToString(),
          ParentTable = new TableReference
          {
            Schema = reader["ParentSchemaName"].ToString(),
            Table = reader["ParentTableName"].ToString(),
            Column = reader["ParentColumnName"].ToString()
          },
          ReferencedTable = new TableReference
          {
            Schema = reader["ReferencedSchemaName"].ToString(),
            Table = reader["ReferencedTableName"].ToString(),
            Column = reader["ReferencedColumnName"].ToString()
          },
          DeleteAction = GetReferentialActionName(Convert.ToInt32(reader["DeleteAction"])),
          UpdateAction = GetReferentialActionName(Convert.ToInt32(reader["UpdateAction"]))
        });
      }
    }

    return relationships;
  }

  private string GetReferentialActionName(int action)
  {
    switch (action)
    {
      case 0: return "NO_ACTION";
      case 1: return "CASCADE";
      case 2: return "SET_NULL";
      case 3: return "SET_DEFAULT";
      default: return "UNKNOWN";
    }
  }

  private async Task<List<IndexInfo>> GetIndexesForTableAsync(SqlConnection connection, string schemaName, string tableName)
  {
    var indexes = new List<IndexInfo>();

    const string sql = @"
                SELECT 
                    i.name AS IndexName,
                    i.is_unique AS IsUnique,
                    i.is_primary_key AS IsPrimaryKey,
                    i.type_desc AS IndexType,
                    STUFF((
                        SELECT ', ' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END
                        FROM sys.index_columns ic
                        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                        ORDER BY ic.key_ordinal
                        FOR XML PATH('')
                    ), 1, 2, '') AS IndexColumns
                FROM 
                    sys.indexes i
                INNER JOIN 
                    sys.tables t ON i.object_id = t.object_id
                INNER JOIN 
                    sys.schemas s ON t.schema_id = s.schema_id
                WHERE 
                    s.name = @SchemaName AND t.name = @TableName AND i.is_hypothetical = 0
                ORDER BY 
                    i.name";

    using (var command = new SqlCommand(sql, connection))
    {
      command.Parameters.AddWithValue("@SchemaName", schemaName);
      command.Parameters.AddWithValue("@TableName", tableName);

      using (var reader = await command.ExecuteReaderAsync())
      {
        while (await reader.ReadAsync())
        {
          indexes.Add(new IndexInfo
          {
            Name = reader["IndexName"].ToString(),
            IsUnique = Convert.ToBoolean(reader["IsUnique"]),
            IsPrimaryKey = Convert.ToBoolean(reader["IsPrimaryKey"]),
            Type = reader["IndexType"].ToString(),
            Columns = reader["IndexColumns"].ToString()
          });
        }
      }
    }

    return indexes;
  }

  private async Task<List<StoredProcedureInfo>> GetStoredProceduresAsync(SqlConnection connection)
  {
    var procedures = new List<StoredProcedureInfo>();

    const string sql = @"
                SELECT 
                    p.name AS ProcedureName,
                    s.name AS SchemaName,
                    OBJECT_DEFINITION(p.object_id) AS ProcedureDefinition,
                    ISNULL(ep.value, '') AS ProcedureDescription
                FROM 
                    sys.procedures p
                INNER JOIN 
                    sys.schemas s ON p.schema_id = s.schema_id
                LEFT JOIN 
                    sys.extended_properties ep ON ep.major_id = p.object_id 
                    AND ep.minor_id = 0 
                    AND ep.name = 'MS_Description'
                ORDER BY 
                    s.name, p.name";

    using (var command = new SqlCommand(sql, connection))
    using (var reader = await command.ExecuteReaderAsync())
    {
      while (await reader.ReadAsync())
      {
        procedures.Add(new StoredProcedureInfo
        {
          Name = reader["ProcedureName"].ToString(),
          Schema = reader["SchemaName"].ToString(),
          Definition = reader["ProcedureDefinition"].ToString(),
          Description = reader["ProcedureDescription"].ToString()
        });
      }
    }

    return procedures;
  }

  private async Task<List<ViewInfo>> GetViewsAsync(SqlConnection connection)
  {
    var views = new List<ViewInfo>();

    const string sql = @"
                SELECT 
                    v.name AS ViewName,
                    s.name AS SchemaName,
                    OBJECT_DEFINITION(v.object_id) AS ViewDefinition,
                    ISNULL(ep.value, '') AS ViewDescription
                FROM 
                    sys.views v
                INNER JOIN 
                    sys.schemas s ON v.schema_id = s.schema_id
                LEFT JOIN 
                    sys.extended_properties ep ON ep.major_id = v.object_id 
                    AND ep.minor_id = 0 
                    AND ep.name = 'MS_Description'
                ORDER BY 
                    s.name, v.name";

    using (var command = new SqlCommand(sql, connection))
    using (var reader = await command.ExecuteReaderAsync())
    {
      while (await reader.ReadAsync())
      {
        views.Add(new ViewInfo
        {
          Name = reader["ViewName"].ToString(),
          Schema = reader["SchemaName"].ToString(),
          Definition = reader["ViewDefinition"].ToString(),
          Description = reader["ViewDescription"].ToString()
        });
      }
    }

    return views;
  }

  /// <summary>
  /// Formats the database schema into a structured text format optimized for LLM understanding
  /// </summary>
  private string FormatSchemaForLlm(DatabaseSchema schema)
  {
    var sb = new StringBuilder();

    // Database overview section
    sb.AppendLine("# DATABASE SCHEMA");
    sb.AppendLine();

    // Tables section
    sb.AppendLine("## TABLES");
    sb.AppendLine();

    foreach (var table in schema.Tables)
    {
      sb.AppendLine($"### Table: {table.Schema}.{table.Name}");

      if (!string.IsNullOrEmpty(table.Description))
      {
        sb.AppendLine($"Description: {table.Description}");
      }

      sb.AppendLine();
      sb.AppendLine("| Column | Data Type | Nullable | PK | FK | Default | Description |");
      sb.AppendLine("|--------|-----------|----------|----|----|---------|-------------|");

      foreach (var column in table.Columns.OrderBy(c => c.ColumnOrder))
      {
        var dataType = column.DataType;

        // Format the data type with precision/scale/length as appropriate
        if (dataType == "varchar" || dataType == "nvarchar" || dataType == "char" || dataType == "nchar")
        {
          var length = column.MaxLength;
          if (dataType.StartsWith("n")) length /= 2;
          dataType += length == -1 ? "(MAX)" : $"({length})";
        }
        else if (dataType == "decimal" || dataType == "numeric")
        {
          dataType += $"({column.Precision},{column.Scale})";
        }

        var pkMark = column.IsPrimaryKey ? "✓" : "";
        var fkMark = column.IsForeignKey ? "✓" : "";
        var defaultValue = column.DefaultValue.Replace("((", "(").Replace("))", ")");

        sb.AppendLine($"| {column.Name} | {dataType} | {(column.IsNullable ? "YES" : "NO")} | {pkMark} | {fkMark} | {defaultValue} | {column.Description} |");
      }

      // Add foreign key details if any exist
      var fkColumns = table.Columns.Where(c => c.IsForeignKey && c.ForeignKeyReference != null).ToList();
      if (fkColumns.Any())
      {
        sb.AppendLine();
        sb.AppendLine("Foreign Keys:");

        foreach (var fkColumn in fkColumns)
        {
          var fk = fkColumn.ForeignKeyReference;
          sb.AppendLine($"- {fkColumn.Name} -> {fk.ReferencedSchema}.{fk.ReferencedTable}.{fk.ReferencedColumn}");
        }
      }

      // Add indexes if any exist
      if (table.Indexes.Any())
      {
        sb.AppendLine();
        sb.AppendLine("Indexes:");

        foreach (var index in table.Indexes)
        {
          var indexType = index.IsPrimaryKey ? "PRIMARY KEY" : index.IsUnique ? "UNIQUE" : "INDEX";
          sb.AppendLine($"- {index.Name} ({indexType}): {index.Columns}");
        }
      }

      sb.AppendLine();
    }

    // Relationships section
    if (schema.Relationships.Any())
    {
      sb.AppendLine("## RELATIONSHIPS");
      sb.AppendLine();

      foreach (var rel in schema.Relationships)
      {
        sb.AppendLine($"- {rel.ForeignKeyName}: {rel.ParentTable.Schema}.{rel.ParentTable.Table}.{rel.ParentTable.Column} -> " +
                     $"{rel.ReferencedTable.Schema}.{rel.ReferencedTable.Table}.{rel.ReferencedTable.Column}");
        sb.AppendLine($"  ON DELETE: {rel.DeleteAction}, ON UPDATE: {rel.UpdateAction}");
      }

      sb.AppendLine();
    }

    // Views section
    if (schema.Views.Any())
    {
      sb.AppendLine("## VIEWS");
      sb.AppendLine();

      foreach (var view in schema.Views)
      {
        sb.AppendLine($"### View: {view.Schema}.{view.Name}");

        if (!string.IsNullOrEmpty(view.Description))
        {
          sb.AppendLine($"Description: {view.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("```sql");
        sb.AppendLine(view.Definition);
        sb.AppendLine("```");
        sb.AppendLine();
      }
    }

    // Stored Procedures section (with summarized definitions to avoid excessive length)
    if (schema.StoredProcedures.Any())
    {
      sb.AppendLine("## STORED PROCEDURES");
      sb.AppendLine();

      foreach (var proc in schema.StoredProcedures)
      {
        sb.AppendLine($"### Procedure: {proc.Schema}.{proc.Name}");

        if (!string.IsNullOrEmpty(proc.Description))
        {
          sb.AppendLine($"Description: {proc.Description}");
        }

        // Extract and display parameters from the procedure definition
        var parameterSection = ExtractParametersFromProcedure(proc.Definition);
        if (!string.IsNullOrEmpty(parameterSection))
        {
          sb.AppendLine();
          sb.AppendLine("Parameters:");
          sb.AppendLine("```sql");
          sb.AppendLine(parameterSection);
          sb.AppendLine("```");
        }

        sb.AppendLine();
      }
    }

    //// JSON format for programmatic access
    //sb.AppendLine("## JSON REPRESENTATION");
    //sb.AppendLine();
    //sb.AppendLine("```json");
    //sb.AppendLine(JsonSerializer.Serialize(schema));
    //sb.AppendLine("```");

    return sb.ToString();
  }

  private string ExtractParametersFromProcedure(string procedureDefinition)
  {
    if (string.IsNullOrEmpty(procedureDefinition))
      return string.Empty;

    // Simple regex-like approach to extract parameters
    var startIndex = procedureDefinition.IndexOf("CREATE PROCEDURE", StringComparison.OrdinalIgnoreCase);
    if (startIndex < 0)
      startIndex = procedureDefinition.IndexOf("ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase);

    if (startIndex < 0)
      return string.Empty;

    var asIndex = procedureDefinition.IndexOf(" AS ", startIndex, StringComparison.OrdinalIgnoreCase);
    if (asIndex < 0)
      return string.Empty;

    var paramSection = procedureDefinition.Substring(startIndex, asIndex - startIndex);
    var openParenIndex = paramSection.IndexOf('(');

    if (openParenIndex >= 0)
    {
      var closeParenIndex = paramSection.LastIndexOf(')');
      if (closeParenIndex > openParenIndex)
      {
        return paramSection.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
      }
    }

    return string.Empty;
  }

  [KernelFunction, Description("Retrieves a sample of rows from a specific table in the database")]
  public string SampleTableData(
      [Description("Name of the database containing the table")] string databaseName,
      [Description("Schema of the table (e.g., 'dbo')")] string tableSchema,
      [Description("Name of the table to sample")] string tableName,
      [Description("Number of sample rows to retrieve")] int sampleSize,
      [Description("Reason why the sample is necessary")] string reasoning)
  {
    try
    {
      _logger.LogInformation($"SAMPLING TABLE: {reasoning}");
      string query = $"SELECT TOP {sampleSize} * FROM [{databaseName}].[{tableSchema}].[{tableName}]";

      Console.WriteLine($"\nExecuting Sample Query:\n{query}");
      IEnumerable<dynamic> result = _sqlConnection.Query(query);
      PrintTable(result);

      return FormatResultAsTable(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sampling table");
      return $"Error sampling table {databaseName}.{tableSchema}.{tableName}: {ex.Message}";
    }
  }

  [KernelFunction, Description("Executes a test SQL query with a row limit to verify correctness")]
  public string RunTestSqlQuery(
      [Description("The SQL query to test")] string sqlQuery,
      [Description("Maximum number of rows to return")] int maxRows,
      [Description("Reason why the test query is necessary")] string reasoning)
  {
    try
    {
      _logger.LogInformation($"TESTING QUERY: {reasoning}");

      Console.WriteLine($"\nExecuting Test Query:\n{sqlQuery}");
      IEnumerable<dynamic> result = _sqlConnection.Query(sqlQuery);
      PrintTable(result);

      return FormatResultAsTable(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Test query failed");
      return $"Test query execution failed: {ex.Message}";
    }
  }

  [KernelFunction, Description("Executes the final SQL query and returns all results. Optionally persists results to a CSV file.")]
  public string RunFinalSqlQuery(
    [Description("The final SQL query to execute")] string sqlQuery,
    [Description("Reason why the final query is necessary")] string reasoning,
    [Description("True if user requests to persist final query results to a CSV file")] bool persistResults)
  {
    try
    {
      _logger.LogInformation($"FINAL QUERY: {reasoning}");

      Console.WriteLine($"\nExecuting Final Query:\n{sqlQuery}");
      // Execute the query and buffer the results in a list, as IEnumerable<dynamic> might be deferred execution.
      // This is important if we need to iterate over it multiple times (once for CSV, once for table formatting).
      List<dynamic> result = _sqlConnection.Query(sqlQuery).AsList();
      PrintTable(result);

      string formattedResult = FormatResultAsTable(result);
      string persistMessage = "";

      if (persistResults)
      {
        if (result.Any())
        {
          try
          {
            string filePath = Path.Combine("C:\\Users\\StijnCastelyns\\Documents\\Techorama\\2025\\techorama-2025-agent-demos\\src\\AgentDemos.WebUI\\wwwroot\\query_results\\", $"query_results_{DateTime.Now:yyyyMMddHHmmssfff}.csv");
            SaveResultsToCsv(result, filePath);
            persistMessage = $"\nResults also saved to: {filePath}";
            _logger.LogInformation($"Query results successfully saved to {filePath}");
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Failed to save query results to CSV.");
            persistMessage = "\nFailed to save results to CSV: " + ex.Message;
          }
        }
        else
        {
          persistMessage = "\nNo results to save to CSV.";
        }
      }

      return formattedResult + persistMessage;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Final query failed");
      return $"Final query execution failed: {ex.Message}";
    }
  }

  private void SaveResultsToCsv(IEnumerable<dynamic> queryResult, string filePath)
  {
    if (queryResult == null || !queryResult.Any())
    {
      return;
    }

    var sb = new StringBuilder();
    var firstRow = (IDictionary<string, object>)queryResult.First();
    var columnNames = firstRow.Keys;

    // Add header row
    sb.AppendLine(string.Join(",", columnNames.Select(EscapeCsvValue)));

    // Add data rows
    foreach (var row in queryResult)
    {
      var rowDict = (IDictionary<string, object>)row;
      var values = rowDict.Values.Select(val => EscapeCsvValue(val?.ToString() ?? ""));
      sb.AppendLine(string.Join(",", values));
    }

    File.WriteAllText(filePath, sb.ToString());
  }

  private string EscapeCsvValue(string value)
  {
    if (string.IsNullOrEmpty(value))
    {
      return "";
    }
    // If the value contains a comma, double quote, or newline, enclose it in double quotes.
    if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
    {
      // Escape existing double quotes by doubling them up.
      return $"\"{value.Replace("\"", "\"\"")}\"";
    }
    return value;
  }

  private string FormatResultAsTable(IEnumerable<dynamic> queryResult)
  {
    if (queryResult == null || !queryResult.Any())
      return "No results found.";

    try
    {
      IDictionary<string, object> firstRow = (IDictionary<string, object>)queryResult.First();
      string[] columns = firstRow.Keys.ToArray();
      ConsoleTable table = new(columns);

      foreach (dynamic row in queryResult)
      {
        IDictionary<string, object> rowDict = (IDictionary<string, object>)row;
        table.AddRow(rowDict.Values.Select(v => v?.ToString()).ToArray());
      }

      return table.ToMarkDownString();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error formatting result");
      return $"Error formatting results: {ex.Message}";
    }
  }

  private void PrintTable(IEnumerable<dynamic> queryResult)
  {
    if (queryResult == null || !queryResult.Any())
    {
      Console.WriteLine("No results to display.");
      return;
    }

    try
    {
      IDictionary<string, object> firstRow = (IDictionary<string, object>)queryResult.First();
      string[] columns = firstRow.Keys.ToArray();
      ConsoleTable table = new(columns);

      foreach (dynamic row in queryResult)
      {
        IDictionary<string, object> rowDict = (IDictionary<string, object>)row;
        table.AddRow(rowDict.Values.Select(v => v?.ToString()).ToArray());
      }

      Console.WriteLine("\nQuery Results:");
      table.Write(Format.Alternative);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error printing table");
      Console.WriteLine($"Error displaying results: {ex.Message}");
    }
  }
}