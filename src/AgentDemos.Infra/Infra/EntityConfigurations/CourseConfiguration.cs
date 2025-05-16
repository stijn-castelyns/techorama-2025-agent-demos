using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgentDemos.Infra.Entities;

namespace AgentDemos.Infra.Infra.EntityConfigurations;

class CourseConfiguration : IEntityTypeConfiguration<Course>
{
  public void Configure(EntityTypeBuilder<Course> course)
  {
    course.HasKey(c => c.Code);

    course.Property(c => c.Title)
          .IsRequired()
          .HasMaxLength(300);

    course.Property(c => c.Summary)
          .IsRequired();

    course.HasMany(c => c.Chapters)
          .WithOne(ch => ch.Course)
          .HasForeignKey(ch => ch.CourseCode)
          .OnDelete(DeleteBehavior.Cascade);

    course.Property(c => c.SummaryVector)
          .HasColumnType("vector(1536)");

    course.Property(c => c.AudienceVector)
          .HasColumnType("vector(1536)");
  }
}
