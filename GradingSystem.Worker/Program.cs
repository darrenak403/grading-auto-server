using GradingSystem.Infrastructure.Extensions;
using GradingSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using GradingSystem.Worker.Consumers;
using GradingSystem.Worker.Options;
using GradingSystem.Worker.Services;
using GradingSystem.Worker.Workers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(builder.Configuration["Storage:BasePath"]))
        throw new InvalidOperationException("Storage:BasePath must be configured for Production (e.g. /storage in Docker).");

    if (string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"]))
        throw new InvalidOperationException("RabbitMQ:Host must be configured for Production.");
}
else if (string.IsNullOrWhiteSpace(builder.Configuration["Storage:BasePath"]))
{
    var solutionRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."));
    builder.Configuration["Storage:BasePath"] = Path.Combine(solutionRoot, "storage");
}

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection("Worker"));

builder.Services.Configure<StorageCleanupOptions>(
    builder.Configuration.GetSection("StorageCleanup"));

builder.Services.AddHttpClient();

builder.Services.AddSingleton<ArtifactRunner>();
builder.Services.AddSingleton<TestRunner>();
builder.Services.AddSingleton<ExportRunner>();
builder.Services.AddSingleton<GradingPipeline>();

var workerOpts = builder.Configuration.GetSection("Worker").Get<WorkerOptions>() ?? new WorkerOptions();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<GradeJobConsumer>().Endpoint(e =>
    {
        e.PrefetchCount = workerOpts.MaxConcurrentJobs;
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq://localhost",
            h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHostedService<GradingWorker>();
builder.Services.AddHostedService<StorageCleanupWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GradingDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
