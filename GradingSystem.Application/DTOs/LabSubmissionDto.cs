namespace GradingSystem.Application.DTOs;

public record AdjustLabScoreRequest(Guid ResultId, decimal Score, string Reason);
public record LabUploadFile(string FileName, Stream Content);

public class LabSubmissionDto
{
    public Guid Id { get; init; }
    public Guid LabAssignmentId { get; init; }
    public string StudentCode { get; init; } = null!;
    public string OriginalFileName { get; init; } = null!;
    public string Status { get; init; } = null!;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public class LabBatchUploadResult
{
    public List<LabSubmissionDto> Created { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
}
