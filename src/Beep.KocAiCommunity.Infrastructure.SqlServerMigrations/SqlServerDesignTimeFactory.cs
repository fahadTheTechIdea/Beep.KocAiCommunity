using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Beep.KocAiCommunity.Infrastructure.SqlServerMigrations;

/// <summary>
/// Design-time factory for generating SQL Server migrations. The connection string is a
/// placeholder — <c>dotnet ef migrations add</c> only needs the provider + model, not a live server.
/// </summary>
public sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<KocDbContext>
{
    public KocDbContext CreateDbContext(string[] args)
    {
        var assemblyName = typeof(SqlServerDesignTimeFactory).Assembly.GetName().Name!;
        var options = new DbContextOptionsBuilder<KocDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=koc;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly(assemblyName))
            .Options;

        return new KocDbContext(options);
    }
}
