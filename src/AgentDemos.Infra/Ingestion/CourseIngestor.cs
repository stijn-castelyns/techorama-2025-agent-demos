using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgentDemos.Infra.Entities;
using System.Xml;
using HtmlAgilityPack;
using AgentDemos.Infra.Infra;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using System.Text.Json;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

namespace AgentDemos.Infra.Ingestion;

public class CourseIngestor
  (
    U2UTrainingDb _u2UTrainingDb,
    IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator,
    Kernel _kernel
  )
{
  public async Task IngestCourseAsync(string courseCode, string html)
  {
    Course course = await ExtractCourseContentAsync(courseCode, html);
    await _u2UTrainingDb.Courses.AddAsync(course);
    await _u2UTrainingDb.SaveChangesAsync();
  }

  public async Task<Course> ExtractCourseContentAsync(string courseCode, string html)
  {
    // Load the HTML document using HtmlAgilityPack
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    // Extract course title from the <title> element in the head.
    string courseTitle = doc.DocumentNode.SelectSingleNode("//head/title")?.InnerText.Trim()
                         ?? "Unknown Course";

    // Extract the number of days from the meta tag.
    var metaDaysNode = doc.DocumentNode.SelectSingleNode("//meta[@name='NumberOfDays']");
    string daysContent = metaDaysNode?.GetAttributeValue("content", "0") ?? "0";
    int numberOfDays = 0;
    int.TryParse(daysContent, out numberOfDays);

    // Locate the body element (ensuring we only process the main content)
    var body = doc.DocumentNode.SelectSingleNode("//body");

    CourseExtractionGeneration courseExtractionGeneration = await CourseExtractionGenerationAsync(html);

    Course course = new Course
    {
      Code = courseCode,
      Title = courseTitle,
      Duration = numberOfDays,
      Audience = courseExtractionGeneration.Audience,
      Summary = courseExtractionGeneration.Description,
      AudienceVector = new float[1536],
      SummaryVector = new float[1536],
      Chapters = new List<Chapter>(),
    };
    
    ReadOnlyMemory<float> audienceEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(courseExtractionGeneration.Audience);

    audienceEmbedding.CopyTo(course.AudienceVector);

    ReadOnlyMemory<float> summaryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(courseExtractionGeneration.Description);

    summaryEmbedding.CopyTo(course.SummaryVector);


    // Each chapter is assumed to start with an <h4> element.
    var h4Nodes = body.SelectNodes(".//h4");
    if (h4Nodes != null)
    {
      foreach (var h4 in h4Nodes)
      {
        string chapterTitle = h4.InnerText.Trim();
        var overviewBuilder = new StringBuilder();

        // Start the overview with the chapter header.
        overviewBuilder.AppendLine(h4.OuterHtml);

        // Include all following sibling nodes until the next <h4> is found.
        for (var node = h4.NextSibling; node != null; node = node.NextSibling)
        {
          // Process only element nodes or non-empty text nodes.
          if (node.NodeType == HtmlNodeType.Element)
          {
            if (node.Name.Equals("h4", StringComparison.OrdinalIgnoreCase))
              break;

            overviewBuilder.AppendLine(node.OuterHtml);
          }
          else if (node.NodeType == HtmlNodeType.Text && !string.IsNullOrWhiteSpace(node.InnerText))
          {
            overviewBuilder.AppendLine(node.InnerText.Trim());
          }
        }

        // Create a new Chapter instance.
        var chapter = new Chapter
        {
          Title = chapterTitle,
          Overview = overviewBuilder.ToString(),
          OverviewVector = new float[1536],
          TitleVector = new float[1536],
        };

        var chapterOverviewEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(chapter.Overview);

        chapterOverviewEmbedding.CopyTo(chapter.OverviewVector);

        var chapterTitleEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(chapter.Title);  
        chapterTitleEmbedding.CopyTo(chapter.TitleVector);

        course.Chapters.Add(chapter);
      }
    }

    // Return a Course object containing the title, number of days, and chapters.
    return course;
  }

  record CourseExtractionGeneration(string Description, string Audience);

  private async Task<CourseExtractionGeneration> CourseExtractionGenerationAsync(string courseHtml)
  {
    OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new OpenAIPromptExecutionSettings
    {
      ResponseFormat = typeof(CourseExtractionGeneration),
    };

    KernelArguments kernelArguments = new KernelArguments(openAIPromptExecutionSettings);

    string generateCourseExtractionPromptTemplate = $""""
      <message role="system">  
        # Instructions
        Based on the user provided course HTML, extract a course description and audience.
        Respond in JSON format

        # Course description definition
        - The course description is a 2-3 sentence summary of the course content

        # Audience definition
        - The audience is the intended group of people who the course is designed for
        - If there is no explicit mention of the audience, you can assume the audience based on the course content
      </message>

      <message role="user">
        {courseHtml}
      </message>
      """";

    string? courseExtractionString = await _kernel.InvokePromptAsync<string>(generateCourseExtractionPromptTemplate, kernelArguments, templateFormat: "handlebars", promptTemplateFactory: new HandlebarsPromptTemplateFactory());
    CourseExtractionGeneration? courseExtraction = JsonSerializer.Deserialize<CourseExtractionGeneration>(courseExtractionString);

    return courseExtraction;
  }
}


