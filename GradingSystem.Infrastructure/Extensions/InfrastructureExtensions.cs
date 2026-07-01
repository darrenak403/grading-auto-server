using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Services;
using GradingSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GradingSystem.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<GradingDbContext>(
            opt => opt.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IExamSessionService, ExamSessionService>();
        services.AddScoped<IBulkUploadService, BulkUploadService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<ITestCaseService, TestCaseService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IReviewNoteService, ReviewNoteService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IQuestionResultService, QuestionResultService>();
        services.AddScoped<IGradingJobService, GradingJobService>();

        // Lab grading
        services.AddScoped<ISemesterService, SemesterService>();
        services.AddScoped<ILabAssignmentService, LabAssignmentService>();
        services.AddScoped<ILabTestCaseService, LabTestCaseService>();
        services.AddScoped<ILabSubmissionService, LabSubmissionService>();
        services.AddScoped<ILabGradingResultService, LabGradingResultService>();

        // Supabase Integration
        services.Configure<GradingSystem.Application.Options.SupabaseOptions>(opt =>
        {
            configuration.GetSection("Supabase").Bind(opt);

            // Fallback to flat environment variables if not bound from section
            if (string.IsNullOrWhiteSpace(opt.Url))
            {
                opt.Url = configuration["SUPABASE_URL"] ?? configuration["SupabaseUrl"] ?? "";
            }
            if (string.IsNullOrWhiteSpace(opt.ServiceRoleKey))
            {
                opt.ServiceRoleKey = configuration["SUPABASE_SERVICE_ROLE_KEY"] ?? configuration["SupabaseServiceRoleKey"] ?? "";
            }
            var autoSyncStr = configuration["SUPABASE_AUTO_SYNC"] ?? configuration["SupabaseAutoSync"];
            if (!string.IsNullOrWhiteSpace(autoSyncStr) && bool.TryParse(autoSyncStr, out var autoSync))
            {
                opt.AutoSync = autoSync;
            }
        });

        services.AddHttpClient<ISupabaseSyncService, SupabaseSyncService>((sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GradingSystem.Application.Options.SupabaseOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.Url))
            {
                client.BaseAddress = new Uri(opts.Url);
            }
        });

        return services;
    }
}
