using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentDemos.Agents.Services;
public class ModelSelector(string modelName) : IAIServiceSelector, IChatClientSelector
{
  private readonly string _modelName = modelName;

  private bool TrySelect<T>(
      Kernel kernel, KernelFunction function, KernelArguments arguments,
      [NotNullWhen(true)] out T? service, out PromptExecutionSettings? serviceSettings) where T : class
  {
    foreach (var serviceToCheck in kernel.GetAllServices<T>())
    {
      string? serviceModelId = null;
      string? endpoint = null;

      if (serviceToCheck is IAIService aiService)
      {
        serviceModelId = aiService.GetModelId();
        endpoint = aiService.GetEndpoint();
      }
      else if (serviceToCheck is IChatClient chatClient)
      {
        var metadata = chatClient.GetService<ChatClientMetadata>();
        serviceModelId = metadata?.DefaultModelId;
        endpoint = metadata?.ProviderUri?.ToString();
      }

      if (!string.IsNullOrEmpty(serviceModelId) && serviceModelId.Equals(_modelName, StringComparison.OrdinalIgnoreCase))
      {
        service = serviceToCheck;
        serviceSettings = new OpenAIPromptExecutionSettings();
        return true;
      }
    }

    service = null;
    serviceSettings = null;
    return false;
  }

  public bool TrySelectAIService<T>(
      Kernel kernel,
      KernelFunction function,
      KernelArguments arguments,
      [NotNullWhen(true)] out T? service,
      out PromptExecutionSettings? serviceSettings) where T : class, IAIService
      => TrySelect(kernel, function, arguments, out service, out serviceSettings);

  public bool TrySelectChatClient<T>(
      Kernel kernel,
      KernelFunction function,
      KernelArguments arguments,
      [NotNullWhen(true)] out T? service,
      out PromptExecutionSettings? serviceSettings) where T : class, IChatClient
      => TrySelect(kernel, function, arguments, out service, out serviceSettings);
}