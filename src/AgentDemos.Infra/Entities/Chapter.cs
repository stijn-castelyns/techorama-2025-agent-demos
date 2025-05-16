using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;

namespace AgentDemos.Infra.Entities;

public class Chapter
{
  public int Id { get; set; }

  public Course Course { get; set; }
  public string CourseCode { get; set; }

  public string Title { get; set; }
  public float[] TitleVector { get; set; }

  public string Overview { get; set; }

  public float[] OverviewVector { get; set; }
}
