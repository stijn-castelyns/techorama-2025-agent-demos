using AgentDemos.Infra.Entities;
using AgentDemos.Infra.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.ComponentModel;
using System.Text;


namespace AgentDemos.Agents.Plugins;
public class CourseRecommendationPlugin
(
  U2UTrainingDb _trainingDb,
  ITextEmbeddingGenerationService _embeddingGenerator
)
{
  [KernelFunction, Description("Search for courses based on a course overview")]
  public async Task<string> SearchOverviewsAsync(CourseSearchRequest courseSearchRequest)
  {
    ReadOnlyMemory<float> courseOverviewVector = await _embeddingGenerator.GenerateEmbeddingAsync(courseSearchRequest.Description);
    float[] courseOverviewFloatVector = courseOverviewVector.ToArray();

    List<Course> courses = await _trainingDb
      .Courses
      .OrderBy(course => EF.Functions.VectorDistance("cosine", course.SummaryVector, courseOverviewFloatVector))
      .Where(course => course.Duration >= courseSearchRequest.MinNumberOfDays && course.Duration <= courseSearchRequest.MaxNumberOfDays)
      .Take(courseSearchRequest.NrOfCoursesToReturn)
      .ToListAsync();

    StringBuilder stringBuilder = new StringBuilder();
    stringBuilder.AppendLine("<Courses>");
    foreach (var course in courses)
    {
      stringBuilder.AppendLine("  <Course>");
      stringBuilder.AppendLine($"    <Code>{course.Code}</Code>");
      stringBuilder.AppendLine($"    <Title>{course.Title}</Title>");
      stringBuilder.AppendLine($"    <Summary>{course.Summary}</Summary>");
      stringBuilder.AppendLine($"    <Audience>{course.Audience}</Audience>");
      stringBuilder.AppendLine($"    <WebPageUrl>https://www.u2u.be/cc/{course.Code}</WebPageUrl>");
      stringBuilder.AppendLine($"    <Duration-In-Days>{course.Duration}</Duration-In-Days>");
      stringBuilder.AppendLine("  </Course>");
    }
    stringBuilder.AppendLine("</Courses>");

    return stringBuilder.ToString();
  }

  [KernelFunction, Description("Search/retrieve chapter descriptions for a given U2U course based on its course code")]
  public async Task<string> SearchChaptersForCourseAsync(ChapterInCourseSearchRequest chapterInCourseSearchRequest)
  {
    List<Chapter> chapters;
    if (string.IsNullOrWhiteSpace(chapterInCourseSearchRequest.Description))
    {
      chapters = await _trainingDb
        .Chapters
        .Where(chapter => chapter.CourseCode == chapterInCourseSearchRequest.CourseCode)
        .ToListAsync();
    }
    else
    {
      ReadOnlyMemory<float> chapterDescriptionVector = await _embeddingGenerator.GenerateEmbeddingAsync(chapterInCourseSearchRequest.Description);
      float[] chapterDescriptionFloatVector = chapterDescriptionVector.ToArray();
      chapters = await _trainingDb
        .Chapters
        .Where(chapter => chapter.CourseCode == chapterInCourseSearchRequest.CourseCode)
        .OrderBy(chapter => EF.Functions.VectorDistance("cosine", chapter.OverviewVector, chapterDescriptionFloatVector))
        .Take(chapterInCourseSearchRequest.NrOfChaptersToReturn)
        .ToListAsync();
    }

    // Format chapters as pseudo XML
    StringBuilder stringBuilder = new StringBuilder();
    stringBuilder.AppendLine("<Chapters>");
    foreach (var chapter in chapters)
    {
      stringBuilder.AppendLine("  <Chapter>");
      stringBuilder.AppendLine($"    <Id>{chapter.Id}</Id>");
      stringBuilder.AppendLine($"    <Title>{chapter.Title}</Title>");
      stringBuilder.AppendLine($"    <Overview>{chapter.Overview}</Overview>");
      stringBuilder.AppendLine("  </Chapter>");
    }
    stringBuilder.AppendLine("</Chapters>");

    return stringBuilder.ToString();
  }
}

public class CourseSearchRequest
{
  [Description("Course overview to search for based on the users description")]
  public required string Description { get; set; }
  [Description("Minimum number of course days")]
  public int MinNumberOfDays { get; set; } = 0;
  [Description("Maximum number of course days")]
  public int MaxNumberOfDays { get; set; } = 5;
  [Description("Number of courses to return")]
  public int NrOfCoursesToReturn { get; set; } = 5;
  [Description("Reasoning behind the course recommendation")]
  public string Reasoning { get; set; }
}

public class ChapterInCourseSearchRequest
{
  [Description("Course code of course for which to retrieve chapters. DO NOT COME UP WITH THIS YOURSELF!")]
  public required string CourseCode { get; set; }
  [Description("Chapter description to search on. DO NOT FILL IN THIS PARAMETER IF THE USER WANTS TO SEE ALL CHAPTERS FOR A GIVEN COURSE!")]
  public string? Description { get; set; }
  [Description("Number of chapters to return")]
  public int NrOfChaptersToReturn { get; set; } = 3;
  [Description("Reasoning behind the chapter description")]
  public string Reasoning { get; set; }
}