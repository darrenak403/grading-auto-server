using GradingSystem.Application.DTOs;

namespace GradingSystem.Application.Interfaces;

public interface IReviewNoteService
{
    Task<ReviewNoteDto> UpsertAsync(Guid submissionId, UpdateReviewNoteRequest req, CancellationToken ct = default);
}
