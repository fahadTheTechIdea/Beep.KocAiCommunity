var builder = DistributedApplication.CreateBuilder(args);

// Local dev (aspire run) uses the API's built-in SQLite file — no container, no Docker, instant
// start. SQL Server is provisioned only when publishing, or on an explicit opt-in
// (`UseSqlServer=true`) for testing the production-shaped stack locally.
var useSqlServer = builder.ExecutionContext.IsPublishMode
    || string.Equals(builder.Configuration["UseSqlServer"], "true", StringComparison.OrdinalIgnoreCase);

var api = builder.AddProject<Projects.Beep_KocAiCommunity_Api>("api")
    .WithHttpHealthCheck("/health");

var worker = builder.AddProject<Projects.Beep_KocAiCommunity_Worker>("worker");

if (useSqlServer)
{
    // Persistent container + data volume so the database survives restarts and the image is reused.
    var sql = builder.AddSqlServer("sql")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume();
    var kocDb = sql.AddDatabase("kocdb");

    api.WithReference(kocDb).WaitFor(kocDb).WithEnvironment("Database__Provider", "SqlServer");
    worker.WithReference(kocDb).WaitFor(kocDb).WithEnvironment("Database__Provider", "SqlServer");
}

// Web is the only externally reachable endpoint; it talks to the API over HTTP.
builder.AddProject<Projects.Beep_KocAiCommunity_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
