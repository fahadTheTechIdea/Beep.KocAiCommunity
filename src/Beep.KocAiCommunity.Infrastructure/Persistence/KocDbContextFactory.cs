using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Beep.KocAiCommunity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF tooling (<c>dotnet ef migrations</c>). Uses SQLite so migrations
/// can be generated and applied without a running SQL Server. Production SQL Server migrations
/// are a separate assembly (planned).
/// </summary>
public sealed class KocDbContextFactory : IDesignTimeDbContextFactory<KocDbContext>
{
    public KocDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KocDbContext>()
            .UseSqlite("Data Source=koc-design.db")
            .Options;

        return new KocDbContext(options);
    }
}
