using Beep.KocAiCommunity.Worker;

var builder = Host.CreateApplicationBuilder(args);

// The durable job queue, outbox dispatcher, experiment sink, and health monitor
// are added from Phase 10 onward. This Phase 01 skeleton hosts a heartbeat only.
builder.Services.AddHostedService<HeartbeatService>();

var host = builder.Build();
host.Run();
