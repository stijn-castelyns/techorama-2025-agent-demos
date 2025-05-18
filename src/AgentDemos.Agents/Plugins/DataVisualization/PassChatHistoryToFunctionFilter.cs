using Azure;
using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using OpenAI.Files;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentDemos.Agents.Plugins.DataVisualization;
public class PassChatHistoryToFunctionFilter : IAutoFunctionInvocationFilter
{
  public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
  {
    if (context.Function.Name == nameof(DataVisualizationPlugin.GetImageDescriptionsAsync))
    {
      context.Arguments["chatHistory"] = context.ChatHistory;
    }
    await next(context);
  }
}
