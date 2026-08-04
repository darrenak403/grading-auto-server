using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PRN232.LMS.Repositories.Context;
using PRN232.LMS.Repositories.Implementations;
using PRN232.LMS.Repositories.Interfaces;
using PRN232.LMS.Services.Implementations;
using PRN232.LMS.Services.Interfaces;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<LmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// ─── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ISemesterRepository,  SemesterRepository>();
builder.Services.AddScoped<ICourseRepository,    CourseRepository>();
builder.Services.AddScoped<ISubjectRepository,   SubjectRepository>();
builder.Services.AddScoped<IStudentRepository,   StudentRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();

// ─── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ISemesterService,  SemesterService>();
builder.Services.AddScoped<ICourseService,    CourseService>();
builder.Services.AddScoped<ISubjectService,   SubjectService>();
builder.Services.AddScoped<IStudentService,   StudentService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

// ─── Controllers ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ─── Swagger / OpenAPI ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "PRN232 - LMS REST API",
        Version     = "v1",
        Description = "Learning Management System RESTful API - PRN232 Lab 1"
    });

    // Include XML comments for Swagger documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ─── Auto-migrate & seed database ─────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
    db.Database.Migrate();
}

// ─── Middleware ────────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "PRN232 LMS API v1");
    c.RoutePrefix = string.Empty; // Swagger at root URL
    c.DocumentTitle = "PRN232 - LMS API Documentation";
});

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
