using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Services;

public class ReviewNoteService(IUnitOfWork uow) : IReviewNoteService
{
    public async Task<ReviewNoteDto> UpsertAsync(Guid submissionId, UpdateReviewNoteRequest req, CancellationToken ct = default)
    {
        _ = await uow.Submissions.GetByIdAsync(submissionId)
            ?? throw new NotFoundException($"Submission '{submissionId}' not found.");

        if (string.IsNullOrWhiteSpace(req.Content))
            throw new BadRequestException("Content is required.");

        var existing = (await uow.ReviewNotes.FindAsync(n => n.SubmissionId == submissionId))
                       .FirstOrDefault();

        if (existing is null)
        {
            existing = new ReviewNote
            {
                SubmissionId = submissionId,
                Content      = req.Content.Trim(),
                ReviewedBy   = req.ReviewedBy?.Trim(),
            };
            await uow.ReviewNotes.AddAsync(existing);
        }
        else
        {
            existing.Content    = req.Content.Trim();
            existing.ReviewedBy = req.ReviewedBy?.Trim();
            uow.ReviewNotes.Update(existing);
        }

        await uow.SaveChangesAsync(ct);

        return Map(existing);
    }

    private static ReviewNoteDto Map(ReviewNote e) => new()
    {
        Id           = e.Id,
        SubmissionId = e.SubmissionId,
        Content      = e.Content,
        ReviewedBy   = e.ReviewedBy,
        CreatedAt    = e.CreatedAt,
    };
}
