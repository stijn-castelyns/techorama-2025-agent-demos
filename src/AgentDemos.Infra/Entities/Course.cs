using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentDemos.Infra.Entities;

public class Course
{
  public string Code { get; set; }
  public string Title { get; set; }
  public string Summary { get; set; }
  public float[] SummaryVector { get; set; }
  public string Audience { get; set; }
  public float[] AudienceVector { get; set; }
  public string? Domain { get; set; }
  public int Duration { get; set; }
  public List<Chapter> Chapters { get; set; }
}
