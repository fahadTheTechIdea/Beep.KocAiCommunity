using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Infrastructure;
using Beep.KocAiCommunity.Infrastructure.Jobs;
using Beep.KocAiCommunity.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Persistence + all Infrastructure services (includes the durable job queue).
builder.Services.AddKocInfrastructure(builder.Configuration);

// ML.NET AutoML trainer (the ML project is only referenced by the executable hosts).
builder.Services.AddScoped<Beep.KocAiCommunity.Application.ML.IMlTrainer, Beep.KocAiCommunity.ML.AutoMlTrainer>();

// Prediction pool singleton so registry eviction (retire/rollback) has a pool to talk to.
builder.Services.AddSingleton<Beep.KocAiCommunity.Application.ML.IPredictionPool, Beep.KocAiCommunity.ML.AutoMlPredictionPool>();

// Job handlers, the per-job processor, and the execution loop.
builder.Services.AddScoped<IJobHandler, ModelTrainingJobHandler>();
builder.Services.AddScoped<IJobHandler, Beep.KocAiCommunity.Infrastructure.Experiments.ExperimentTrainingJobHandler>();
builder.Services.AddScoped<IJobHandler, WorkflowRunJobHandler>();
builder.Services.AddScoped<JobProcessor>();
builder.Services.AddHostedService<JobExecutionService>();

var host = builder.Build();
host.Run();
