using Doctorly.Domain.Events;
using Doctorly.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Doctorly.Infrastructure.Persistence.EntityConfigurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Title).HasMaxLength(Event.MaxTitleLength).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(Event.MaxDescriptionLength).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // domain-owned optimistic concurrency token, readable from Application/DTOs
        // without a Postgres-specific concept (e.g. xmin) crossing the layer boundary
        builder.Property(e => e.Version).IsConcurrencyToken();

        builder.OwnsOne(e => e.TimeRange, tr =>
        {
            tr.Property(t => t.Start).HasColumnName("StartTime").IsRequired();
            tr.Property(t => t.End).HasColumnName("EndTime").IsRequired();
        });
        builder.Navigation(e => e.TimeRange).IsRequired();

        builder.OwnsMany(e => e.Attendees, a =>
        {
            a.ToTable("Attendees");
            a.WithOwner().HasForeignKey("EventId");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).ValueGeneratedNever();

            a.Property(x => x.Name).HasMaxLength(Attendee.MaxNameLength).IsRequired();
            a.Property(x => x.IsAttending);

            a.OwnsOne(x => x.Email, email =>
            {
                email.Property(v => v.Value).HasColumnName("Email").HasMaxLength(EmailAddress.MaxLength).IsRequired();
            });
            a.Navigation(x => x.Email).IsRequired();
        });
        builder.Navigation(e => e.Attendees).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.DomainEvents);
    }
}
