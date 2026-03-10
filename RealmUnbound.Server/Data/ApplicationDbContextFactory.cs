using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RealmUnbound.Server.Data;

/// <summary>
/// Provides design-time construction of <see cref="ApplicationDbContext"/> for EF tooling
/// (migrations, scaffolding) without needing a running host or configuration secrets.
/// Uses SQLite so the tool works on any dev machine with no external services.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new ApplicationDbContext(options);
    }
}
