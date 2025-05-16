using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentDemos.Agents.Plugins.SQL;

public class DapperConnectionProvider
{
  private readonly IConfiguration _configuration;
  private readonly string _connectionString;

  public DapperConnectionProvider(IConfiguration configuration)
  {
    _configuration = configuration;
    _connectionString = _configuration.GetConnectionString("NorthwindDb")!;
  }

  public IDbConnection Connect() => new SqlConnection(_connectionString);
}
