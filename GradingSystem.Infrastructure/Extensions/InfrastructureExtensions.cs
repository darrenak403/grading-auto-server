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

        return services;
    }
}
