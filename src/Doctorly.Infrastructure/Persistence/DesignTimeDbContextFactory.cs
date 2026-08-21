using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Doctorly.Infrastructure.Persistence;

// only used by `dotnet ef migrations add` - the Api project wires the real connection string at runtime
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DoctorlyDbContext>
{
    public DoctorlyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DoctorlyDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=55432;Database=doctorly;Username=doctorly;Password=doctorly");
        return new DoctorlyDbContext(optionsBuilder.Options);
    }
}
