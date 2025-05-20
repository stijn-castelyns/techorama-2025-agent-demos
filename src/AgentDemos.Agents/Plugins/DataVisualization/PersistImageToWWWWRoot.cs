using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentDemos.Agents.Plugins.DataVisualization;
internal class PersistImageToWWWWRoot : IAutoFunctionInvocationFilter
{
  private string _rootPath = "C:\\Users\\StijnCastelyns\\Documents\\Techorama\\2025\\techorama-2025-agent-demos\\src\\AgentDemos.WebUI\\wwwroot\\query_images\\";
  public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
  {
    await next(context);
    
    if(context.Function.PluginName == "SessionsPythonPlugin" 
    && context.Function.Name == "DownloadFile")
    {
      byte[]? fileBytes = context.Result.GetValue<byte[]>();
      string fileName = $"{Guid.NewGuid().ToString()}.png";
      string? fullPath = Path.Combine(_rootPath, fileName);
      File.WriteAllBytes(fullPath, fileBytes);
      context.Result = new FunctionResult(context.Function, $"Image persisted to: https://localhost:7297/query_images/{fileName}");
    }
  }
}
