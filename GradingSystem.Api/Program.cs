using System.Reflection;
using Asp.Versioning;
using GradingSystem.Api.Middleware;
using GradingSystem.Infrastructure.Extensions;
using GradingSystem.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 200 * 1024 * 1024); // 200 MB

if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(builder.Configuration["Storage:BasePath"]))
        throw new InvalidOperationException("Storage:BasePath must be configured for Production (e.g. /storage in Docker).");

    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length == 0 || origins.All(string.IsNullOrWhiteSpace))
        throw new InvalidOperationException("Cors:AllowedOrigins must list at least one origin in Production.");

    if (string.IsNullOrWhiteSpace(builder.Configuration["RabbitMQ:Host"]))
        throw new InvalidOperationException("RabbitMQ:Host must be configured for Production.");
}
else if (string.IsNullOrWhiteSpace(builder.Configuration["Storage:BasePath"]))
{
    var solutionRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."));
    builder.Configuration["Storage:BasePath"] = Path.Combine(solutionRoot, "storage");
}

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApiVersioning(opt =>
{
    opt.DefaultApiVersion = new ApiVersion(1, 0);
    opt.AssumeDefaultVersionWhenUnspecified = true;
    opt.ReportApiVersions = true;
    opt.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(opt =>
{
    opt.GroupNameFormat = "'v'VVV";
    opt.SubstituteApiVersionInUrl = true;
});

if (!builder.Environment.IsProduction())
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Grading System API", Version = "v1" });
        c.SwaggerDoc("v2", new OpenApiInfo { Title = "Grading System API", Version = "v2" });

        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
    });
}

builder.Services.AddMassTransit(x =>
{
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

if (builder.Environment.IsProduction())
{
    var origins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .Where(o => !string.IsNullOrWhiteSpace(o))
        .Select(o => o.Trim())
        .Distinct()
        .ToArray();
    builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
}
else
{
    builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GradingDbContext>();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "v2");
        c.RoutePrefix = string.Empty;
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
