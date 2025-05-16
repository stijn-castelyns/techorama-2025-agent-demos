using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgentDemos.Infra.Entities;

namespace AgentDemos.Infra.Infra.EntityConfigurations;

public class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
  public void Configure(EntityTypeBuilder<Chapter> chapter)
  {
    // Set primary key
    chapter.HasKey(c => c.Id);

    // Configure Title property
    chapter.Property(c => c.Title)
        .IsRequired()
        .HasMaxLength(300);

    // Configure TitleVector - storing as a binary array for example
    chapter.Property(c => c.TitleVector)
        .HasColumnType("vector(1536)");

    // Configure OverviewVector - storing as a binary array for example
    chapter.Property(c => c.OverviewVector)
        .HasColumnType("vector(1536)");
  }
}
