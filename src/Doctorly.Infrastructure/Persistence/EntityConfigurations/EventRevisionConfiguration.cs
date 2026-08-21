using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Doctorly.Infrastructure.Persistence.EntityConfigurations;

public sealed class EventRevisionConfiguration : IEntityTypeConfiguration<EventRevision>
{
    public void Configure(EntityTypeBuilder<EventRevision> builder)
    {
        builder.ToTable("EventRevisions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Snapshot).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(r => r.EventId);
    }
}
