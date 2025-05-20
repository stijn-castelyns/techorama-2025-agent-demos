using Azure.AI.OpenAI;
using Azure.Storage.Blobs; // Added for Blob Storage
using Azure.Storage.Blobs.Models;
using HandlebarsDotNet;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Files; // Added for FileUploadPurpose and OpenAIFile
using System;
using System.ComponentModel;
using System.IO; // Added for MemoryStream
using System.Threading.Tasks;

namespace AgentDemos.Agents.Plugins.DataVisualization;

public class DataVisualizationPlugin
{
  private readonly AzureOpenAIClient _azureOpenAIClient;
  private readonly BlobContainerClient _blobContainerClient; // Added BlobContainerClient

  // Modified constructor to accept BlobContainerClient
  public DataVisualizationPlugin(AzureOpenAIClient azureOpenAIClient, BlobContainerClient blobContainerClient)
  {
    _azureOpenAIClient = azureOpenAIClient ?? throw new ArgumentNullException(nameof(azureOpenAIClient));
    _blobContainerClient = blobContainerClient ?? throw new ArgumentNullException(nameof(blobContainerClient));
  }

  [KernelFunction, Description("Call this function whenever the user asks to persist images that were generated using code interpreter")]
  public async Task<string> PersistImagesAsync([Description("Descriptions of generated images")] List<string> imageDescriptions, ChatHistory? chatHistory = null)
  {
    ChatMessageContent lastAssistantMessage = chatHistory!.Last(m => m.Role == AuthorRole.Assistant && !m.Content.IsNullOrEmpty());
    List<string> imageUrls = await DownloadResponseImageAsync(lastAssistantMessage);

    return $"URLS: {string.Join(", ", imageUrls)}\nDESCRIPTIONS: {string.Join(", ", imageDescriptions)}";
  }

  private async Task<List<string>> DownloadResponseImageAsync(ChatMessageContent message)
  {
    OpenAIFileClient fileClient = _azureOpenAIClient.GetOpenAIFileClient();
    List<string> imageUrls = [];
    foreach (KernelContent item in message.Items)
    {
      if (item is FileReferenceContent fileReference)
      {
        OpenAIFile fileInfo = fileClient.GetFile(fileReference.FileId);

        if (fileInfo.Purpose == FilePurpose.AssistantsOutput)
        {
          BinaryData content = await fileClient.DownloadFileAsync(fileReference.FileId);

          BlobContentInfo? blob = await _blobContainerClient.UploadBlobAsync(fileInfo.Filename, content);

          imageUrls.Add($"https://{_blobContainerClient.AccountName}.blob.core.windows.net/{_blobContainerClient.Name}/{fileInfo.Filename}");
        }
      }
    }

    return imageUrls;
  }

  public async Task<string> DownloadDatasetAsync(string blobName)
  {
    if (string.IsNullOrWhiteSpace(blobName))
    {
      throw new ArgumentException("Blob name cannot be null or whitespace.", nameof(blobName));
    }

    BlobClient blobClient = _blobContainerClient.GetBlobClient(blobName);

    if (!await blobClient.ExistsAsync())
    {
      return $"Blob '{blobName}' not found in container '{_blobContainerClient.Name}'.";
    }

    // Download blob content to a memory stream
    using var memoryStream = new MemoryStream();
    await blobClient.DownloadToAsync(memoryStream);
    memoryStream.Position = 0; // Reset stream position to the beginning for reading

    // Get the OpenAI file client
    OpenAIFileClient openAIFileClient = _azureOpenAIClient.GetOpenAIFileClient();

    // Upload the file content from the memory stream to OpenAI Assistants
    // We use the original blobName as the desired filename for the assistant.
    OpenAIFile openAIFile = await openAIFileClient.UploadFileAsync(
        memoryStream,
        blobName, // This will be the filename seen by the assistant
        FileUploadPurpose.Assistants);

    return openAIFile.Id;
  }
}