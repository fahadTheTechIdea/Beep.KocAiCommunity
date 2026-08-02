var builder = DistributedApplication.CreateBuilder(args);

// Local dev (aspire run) uses the web host's built-in SQLite file — no container, no Docker, instant
// start. SQL Server is provisioned only when publishing, or on an explicit opt-in
// (`UseSqlServer=true`) for testing the production-shaped stack locally.
var useSqlServer = builder.ExecutionContext.IsPublishMode
    || string.Equals(builder.Configuration["UseSqlServer"], "true", StringComparison.OrdinalIgnoreCase);

// One website. It carries the platform surface in-process — there has been no separate API project
// since 2026-08-02, and KOC Studio on the desktop reads the same database directly rather than
// through it.
var web = builder.AddProject<Projects.Beep_KocAiCommunity_Web>("web")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var worker = builder.AddProject<Projects.Beep_KocAiCommunity_Worker>("worker");

if (useSqlServer)
{
    // Persistent container + data volume so the database survives restarts and the image is reused.
    var sql = builder.AddSqlServer("sql")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume();
    var kocDb = sql.AddDatabase("kocdb");

    web.WithReference(kocDb).WaitFor(kocDb).WithEnvironment("Database__Provider", "SqlServer");
    worker.WithReference(kocDb).WaitFor(kocDb).WithEnvironment("Database__Provider", "SqlServer");
}

builder.Build().Run();
