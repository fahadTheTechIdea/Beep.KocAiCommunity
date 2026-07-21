using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// Boots the API over a shared in-memory SQLite database (kept alive by a held-open connection),
/// with the outbox dispatcher disabled and the test authentication scheme installed. Seeds a
/// small KOC org tree used by the endpoint tests.
/// </summary>
public sealed class KocApiFactory : WebApplicationFactory<Program>
{
    // Unique per factory instance so parallel test classes get isolated databases.
    private readonly string _connString = $"Data Source=file:kocapitests-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;
    private bool _seeded;

    public KocApiFactory() => _keepAlive = new SqliteConnection(_connString);

    public Guid Company { get; private set; }
    public Guid G1 { get; private set; }
    public Guid T1 { get; private set; }
    public Guid T2 { get; private set; }
    public Guid T9 { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:kocdb"] = _connString,
                ["Outbox:DispatcherEnabled"] = "false",
                ["Seed:Enabled"] = "false",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Authoritatively replace the DbContext with our held-open in-memory connection —
            // config-based overrides are read too early by AddKocInfrastructure to take effect.
            var toRemove = services
                .Where(d => d.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal) == true)
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            // Use the shared-cache connection string (not the single held connection) so each
            // request scope opens its own connection to the same in-memory database — avoids
            // contention/disposal races on a shared connection object. _keepAlive keeps it alive.
            services.AddDbContext<KocDbContext>(options => options.UseSqlite(_connString));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public HttpClient CreateClientAs(string? sub, params string[] roles) =>
        CreateClientAs(sub, competitionCreator: true, roles);

    /// <summary>
    /// Creates an authenticated test client. By default the user is granted Company-scope
    /// competition-creation (competition creation now requires a grant); pass
    /// <paramref name="competitionCreator"/> = false to test the ungranted / capped paths.
    /// </summary>
    public HttpClient CreateClientAs(string? sub, bool competitionCreator, params string[] roles)
    {
        EnsureSeeded();
        if (sub is not null && competitionCreator)
        {
            GrantCompetitionCreator(sub, VisibilityScope.Company);
        }

        var client = CreateClient();
        if (sub is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
            if (roles.Length > 0)
            {
                client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(',', roles));
            }
        }

        return client;
    }

    /// <summary>Grants (or updates) a user's competition-creation capability at the given max scope.</summary>
    public void GrantCompetitionCreator(string sub, VisibilityScope max)
    {
        lock (_seedLock)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
            var grant = db.CompetitionCreatorGrants.FirstOrDefault(g => g.UserId == sub);
            if (grant is null)
            {
                db.CompetitionCreatorGrants.Add(new Domain.Authorization.CompetitionCreatorGrant
                {
                    UserId = sub,
                    MaxScope = max,
                    GrantedByUserId = "test-seed",
                    CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                });
            }
            else
            {
                grant.MaxScope = max;
            }

            db.SaveChanges();
        }
    }

    private readonly object _seedLock = new();

    private void EnsureSeeded()
    {
        lock (_seedLock)
        {
            if (_seeded)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
            db.Database.EnsureCreated();

            // Idempotent: seed once even if the shared in-memory database already holds data.
            if (!db.OrgUnits.Any())
            {
                var company = Unit("KOC", OrgUnitType.Company, null, "/koc", "ceo");
                var d1 = Unit("Exploration", OrgUnitType.Directorate, company.Id, "/koc/d1", "dceo1");
                var g1 = Unit("Subsurface", OrgUnitType.Group, d1.Id, "/koc/d1/g1", "mgr1");
                var t1 = Unit("Reservoir Analytics", OrgUnitType.Team, g1.Id, "/koc/d1/g1/t1", "lead1");
                var t2 = Unit("Production Eng", OrgUnitType.Team, g1.Id, "/koc/d1/g1/t2");
                var d2 = Unit("Operations", OrgUnitType.Directorate, company.Id, "/koc/d2");
                var t9 = Unit("Facilities", OrgUnitType.Team, d2.Id, "/koc/d2/t9");

                db.OrgUnits.AddRange(company, d1, g1, t1, t2, d2, t9);
                db.OrgMemberships.AddRange(
                    Member("emp1", t1.Id, PositionLevel.Employee),
                    Member("emp2", t2.Id, PositionLevel.Employee),
                    Member("mgr1", g1.Id, PositionLevel.Manager),
                    Member("empOther", t9.Id, PositionLevel.Employee));
                db.SaveChanges();

                Beep.KocAiCommunity.Infrastructure.Learning.LearningSeeder.SeedTracksAsync(db).GetAwaiter().GetResult();
            }

            // Resolve ids by path so they always match the persisted rows.
            Company = db.OrgUnits.Single(u => u.Path == "/koc").Id;
            G1 = db.OrgUnits.Single(u => u.Path == "/koc/d1/g1").Id;
            T1 = db.OrgUnits.Single(u => u.Path == "/koc/d1/g1/t1").Id;
            T2 = db.OrgUnits.Single(u => u.Path == "/koc/d1/g1/t2").Id;
            T9 = db.OrgUnits.Single(u => u.Path == "/koc/d2/t9").Id;
            _seeded = true;
        }
    }

    private static OrgUnit Unit(string name, OrgUnitType type, Guid? parent, string path, string? leader = null) => new()
    {
        Name = name,
        Type = type,
        ParentId = parent,
        Path = path,
        LeaderUserId = leader,
        CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static OrgMembership Member(string userId, Guid orgUnitId, PositionLevel level) => new()
    {
        UserId = userId,
        OrgUnitId = orgUnitId,
        PositionLevel = level,
        IsPrimary = true,
        FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
