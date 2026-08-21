using Doctorly.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Doctorly.Infrastructure.Persistence;

public sealed class DoctorlyDbContext(DbContextOptions<DoctorlyDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventRevision> EventRevisions => Set<EventRevision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DoctorlyDbContext).Assembly);
    }
}
