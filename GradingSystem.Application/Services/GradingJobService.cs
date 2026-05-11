using GradingSystem.Application.DTOs;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Services;

public class GradingJobService(IUnitOfWork unitOfWork) : IGradingJobService
{
    public async Task<GradingJobDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await unitOfWork.GradingJobs.GetByIdAsync(id);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<GradingJobDto>> GetBySubmissionIdAsync(
        Guid submissionId, CancellationToken ct = default)
    {
        var jobs = await unitOfWork.GradingJobs.FindAsync(j => j.SubmissionId == submissionId);
        return jobs.OrderByDescending(j => j.CreatedAt).Select(Map).ToList();
    }

    private static GradingJobDto Map(GradingJob e) => new()
    {
        Id = e.Id,
        SubmissionId = e.SubmissionId,
        Status = e.Status,
        ErrorMessage = e.ErrorMessage,
        StartedAt = e.StartedAt,
        FinishedAt = e.FinishedAt,
    };
}
