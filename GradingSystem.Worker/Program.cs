using GradingSystem.Infrastructure.Extensions;
using GradingSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using GradingSystem.Worker.Consumers;
using GradingSystem.Worker.Options;
using GradingSystem.Worker.Services;
using GradingSystem.Worker.Services.Lab;
using GradingSystem.Worker.Workers;
using MassTransit;
using OfficeOpenXml;

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

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

var storageBasePath = builder.Configuration["Storage:BasePath"];
if (!string.IsNullOrWhiteSpace(storageBasePath) && !Path.IsPathRooted(storageBasePath))
{
    builder.Configuration["Storage:BasePath"] = Path.GetFullPath(
        Path.Combine(builder.Environment.ContentRootPath, storageBasePath));
}

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection("Worker"));

builder.Services.Configure<PlaywrightOptions>(
    builder.Configuration.GetSection("Playwright"));

builder.Services.Configure<StorageCleanupOptions>(
    builder.Configuration.GetSection("StorageCleanup"));

builder.Services.AddHttpClient();

builder.Services.AddSingleton<ArtifactRunner>();
builder.Services.AddSingleton<TestRunner>();
builder.Services.AddSingleton<ExportRunner>();
builder.Services.AddSingleton<GradingPipeline>();

builder.Services.AddSingleton<DockerComposeRunner>();
builder.Services.AddSingleton<LabTestRunner>();
builder.Services.AddSingleton<SourceAnalyzer>();
builder.Services.AddSingleton<LabGradingPipeline>();

var workerOpts = builder.Configuration.GetSection("Worker").Get<WorkerOptions>() ?? new WorkerOptions();
var labMaxConcurrentJobs = Math.Max(1, workerOpts.LabMaxConcurrentJobs);

// MaxConcurrentJobs defaults to a CPU-aware value (cores - 1, min 1, max 8) when
// "Worker:MaxConcurrentJobs" is absent from config — leaving one core free since this
// host doubles as a dev/CI machine, not a dedicated grading server. An explicit value
// in config always wins, even if it happens to equal the old hardcoded default (3).
workerOpts.MaxConcurrentJobs = ConcurrencyCalculator.ResolveMaxConcurrentJobs(
    rawConfigValue: builder.Configuration["Worker:MaxConcurrentJobs"],
    configuredValue: workerOpts.MaxConcurrentJobs,
    processorCount: Environment.ProcessorCount);

Console.WriteLine($"[Worker] Effective MaxConcurrentJobs = {workerOpts.MaxConcurrentJobs} " +
    $"(ProcessorCount = {Environment.ProcessorCount})");

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<GradeJobConsumer>(c =>
    {
        c.UseConcurrentMessageLimit(workerOpts.MaxConcurrentJobs);
    }).Endpoint(e =>
    {
        e.PrefetchCount = workerOpts.MaxConcurrentJobs;
        e.ConcurrentMessageLimit = workerOpts.MaxConcurrentJobs;
    });
    x.AddConsumer<LabGradeJobConsumer>(c =>
    {
        c.UseConcurrentMessageLimit(labMaxConcurrentJobs);
    }).Endpoint(e =>
    {
        e.PrefetchCount = labMaxConcurrentJobs;
        e.ConcurrentMessageLimit = labMaxConcurrentJobs;
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
builder.Services.AddHostedService<LabGradingWorker>();
builder.Services.AddHostedService<StorageCleanupWorker>();
builder.Services.AddHostedService<DockerBuildCacheCleanupWorker>();
builder.Services.AddHostedService<DockerSystemCleanupWorker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GradingDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
