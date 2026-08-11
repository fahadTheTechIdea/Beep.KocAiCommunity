using Beep.KocAiCommunity.Application.Security;
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
public class KocApiFactory : WebApplicationFactory<Program>
{
    // Unique per factory instance so parallel test classes get isolated databases.
    private readonly string _connString = $"Data Source=file:kocapitests-{Guid.NewGuid():N}?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive;
    private bool _seeded;

    public KocApiFactory() => _keepAlive = new SqliteConnection(_connString);

    /// <summary>
    /// The sign-in mode the host boots in. Demo by default — the tests install their own authentication
    /// scheme, so what matters is that the mode is <em>pinned</em> rather than read from the developer's
    /// machine. <see cref="RealTokenApiFactory"/> overrides it to exercise real registration/sign-in.
    /// </summary>
    protected virtual string AuthMode => "KocEnvironment";

    /// <summary>A fixed signing key so issued tokens validate within the test host.</summary>
    protected const string TestSigningKey = "dGVzdC1zaWduaW5nLWtleS0zMi1ieXRlcy1sb25nISEhIQ==";

    public Guid Company { get; private set; }
    public Guid G1 { get; private set; }
    public Guid T1 { get; private set; }
    public Guid T2 { get; private set; }
    public Guid T9 { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive.Open();

        builder.UseEnvironment("Development");

        // Authentication is chosen while Program.cs runs, which is *before* ConfigureAppConfiguration
        // callbacks are applied (the same ordering that forces the DbContext swap below). UseSetting lands
        // in the host's configuration immediately, so these are visible in time.
        //
        // Pinning matters: without it the host would fall back to the first-run setup file in the
        // developer's own LocalApplicationData and the suite would depend on whichever mode that machine
        // happens to have chosen.
        builder.UseSetting("Auth:SignInWith", AuthMode);
        builder.UseSetting("Auth:TokenSigningKey", TestSigningKey);
        builder.UseSetting("Setup:File", Path.Combine(Path.GetTempPath(), $"koc-tests-{Guid.NewGuid():N}", "setup.json"));

        // The production default is 8 seconds, which is fine on an idle machine and not on this one:
        // the test assemblies run in parallel and both train, so an 8-second AutoML experiment can
        // finish without completing a single trial — ML.NET then throws and every training test in
        // flight fails. This buys headroom for the contention, not for the model.
        builder.UseSetting("Studio:TrainingSeconds", "25");

        // Negotiate needs Kestrel and this is a test server. It is a request handler, so it would run
        // on every call regardless of scheme and fail them all.
        builder.UseSetting("Auth:EnableNegotiate", "false");

        // No second listener: a test server has no real ports, and confining the API to one would make
        // every endpoint unroutable here. The in-process guard still applies.
        builder.UseSetting("Platform:InternalPort", "0");

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

            ConfigureAuthentication(services);
        });
    }

    /// <summary>
    /// Swaps in the header-driven test scheme so a test can act as any user. Overridden by
    /// <see cref="RealTokenApiFactory"/>, which keeps the host's real token validation so the
    /// register/sign-in path is exercised end to end rather than bypassed.
    /// </summary>
    protected virtual void ConfigureAuthentication(IServiceCollection services) =>
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
            options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
        }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

    public HttpClient CreateClientAs(string? sub, params string[] roles) =>
        CreateClientAs(sub, competitionCreator: true, roles);

    /// <summary>
    /// An unauthenticated client against a created schema — for endpoints that establish an identity
    /// themselves (registration and sign-in) rather than being handed one.
    /// </summary>
    public HttpClient CreateAnonymousClient()
    {
        EnsureSeeded();
        return CreateClient();
    }

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

        // Roles live in the platform's database, not in whatever the caller asserts — so a test that wants
        // to act as a Manager has to record that, exactly as an administrator would in the RBAC console.
        // The header only says *who* is calling.
        if (sub is not null)
        {
            GrantRoles(sub, roles.Length > 0 ? roles : [KocRoles.Employee]);
        }

        var client = CreateClient();
        if (sub is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        }

        return client;
    }

    /// <summary>
    /// A client identifying as someone the platform has <b>no record of</b> — a colleague arriving from
    /// the corporate directory for the first time. Deliberately seeds nothing, so first-arrival behaviour
    /// is exercised. <paramref name="claimedRoles"/> are roles the identity provider asserts, which the
    /// platform is expected to disregard.
    /// </summary>
    public HttpClient CreateClientAsUnknownUser(string sub, params string[] claimedRoles)
    {
        EnsureSeeded();
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
        if (claimedRoles.Length > 0)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(',', claimedRoles));
        }

        return client;
    }

    /// <summary>
    /// Records a user so they hold these roles, the way the admin console would — which means two
    /// different places. A <b>position</b> (Employee … CEO) is the person's place in the reporting line
    /// and lives on their org membership; a <b>function</b> role (PlatformAdmin, …) is a capability
    /// granted against the account. Tests still just say <c>CreateClientAs("sub", "Manager")</c>.
    /// </summary>
    public void GrantRoles(string sub, IReadOnlyList<string> roles)
    {
        lock (_seedLock)
        {
            using var scope = Services.CreateScope();
            var directory = scope.ServiceProvider.GetRequiredService<IKocUserDirectory>();
            var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

            directory.EnsureUserAsync(sub, sub, null).GetAwaiter().GetResult();

            var functions = roles.Where(r => !KocRoles.AllPositions.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
            directory.SetRolesAsync(sub, functions).GetAwaiter().GetResult();

            var position = roles.FirstOrDefault(r => KocRoles.AllPositions.Contains(r, StringComparer.OrdinalIgnoreCase));
            if (position is not null && Enum.TryParse<PositionLevel>(position, ignoreCase: true, out var level))
            {
                SetPosition(db, sub, level);
            }
        }
    }

    /// <summary>
    /// Puts a position on the user's primary org placement, creating one at the company root when the
    /// test hasn't placed them itself. Users the factory already seeds into the tree keep their unit —
    /// only the level changes — so org-scope expectations elsewhere are unaffected.
    /// </summary>
    private void SetPosition(KocDbContext db, string sub, PositionLevel level)
    {
        var membership = db.OrgMemberships.FirstOrDefault(m => m.UserId == sub && m.IsPrimary && m.ToUtc == null);
        if (membership is null)
        {
            membership = new OrgMembership
            {
                UserId = sub,
                OrgUnitId = Company,
                IsPrimary = true,
                FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            };
            db.OrgMemberships.Add(membership);
        }

        membership.PositionLevel = level;
        db.SaveChanges();
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

                // The seeded catalogue, so tests see what a real install serves. The translations go
                // last, after the rows they translate exist.
                Beep.KocAiCommunity.Infrastructure.Engagement.EngagementSeeder.SeedBadgesAsync(db).GetAwaiter().GetResult();
                Beep.KocAiCommunity.Infrastructure.Competitions.CompetitionCategorySeeder.SeedAsync(db).GetAwaiter().GetResult();

                // Competitions and their data ship with the platform too, so a test host has the same
                // arena a deployed one does — and the demo data has something to enter.
                var artifacts = scope.ServiceProvider.GetRequiredService<Beep.KocAiCommunity.Application.Storage.IArtifactService>();
                Beep.KocAiCommunity.Infrastructure.Competitions.CompetitionSeeder
                    .SeedCompetitionsAsync(db, artifacts).GetAwaiter().GetResult();

                Beep.KocAiCommunity.Infrastructure.Localization.ContentTranslationSeeder.SeedAsync(db).GetAwaiter().GetResult();
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
