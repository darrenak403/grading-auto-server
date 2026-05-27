using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace GradingSystem.Application.Services;

public class LabSubmissionService(IUnitOfWork uow, IConfiguration config) : ILabSubmissionService
{
    private readonly string _basePath = config["Storage:BasePath"] ?? "/storage";

    public async Task<IEnumerable<LabSubmissionDto>> ListAsync(Guid? assignmentId = null, CancellationToken ct = default)
    {
        var all = assignmentId.HasValue
            ? await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == assignmentId.Value)
            : await uow.LabSubmissions.GetAllAsync();
        return all.OrderBy(s => s.StudentCode).Select(Map);
    }

    public async Task<LabSubmissionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await uow.LabSubmissions.GetByIdAsync(id);
        return s is null ? null : Map(s);
    }

    public async Task<LabBatchUploadResult> BatchUploadAsync(Guid assignmentId, IEnumerable<LabUploadFile> files, CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"LabAssignment '{assignmentId}' not found.");

        var dir = Path.Combine(_basePath, "lab-submissions", assignmentId.ToString());
        Directory.CreateDirectory(dir);

        var created = new List<LabSubmissionDto>();
        var warnings = new List<string>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var savedPaths = new List<string>();

        try
        {
        foreach (var file in files)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
            if (!nameWithoutExt.Contains('_'))
            {
                warnings.Add($"Skipped '{file.FileName}': filename must contain '_' separator (e.g. Lab1_SE180234.zip)");
                continue;
            }
            var studentCode = nameWithoutExt.Split('_')[1];
            if (string.IsNullOrWhiteSpace(studentCode))
            {
                warnings.Add($"Skipped '{file.FileName}': could not extract student code — expected format Lab1_SE180234.zip");
                continue;
            }
            if (!seenCodes.Add(studentCode))
            {
                warnings.Add($"Skipped '{file.FileName}': duplicate student code '{studentCode}' in this batch.");
                continue;
            }
            var safeFileName = Path.GetFileName(file.FileName);
            var savePath = Path.Combine(dir, safeFileName);
            // Guard against path traversal after combining
            if (!Path.GetFullPath(savePath).StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new BadRequestException($"Invalid filename '{file.FileName}'.");
            using (var fs = new FileStream(savePath, FileMode.Create))
                await file.Content.CopyToAsync(fs, ct);
            savedPaths.Add(savePath);

            var submission = new LabSubmission
            {
                LabAssignmentId = assignmentId,
                StudentCode = studentCode,
                OriginalFileName = file.FileName,
                FilePath = savePath
            };
            await uow.LabSubmissions.AddAsync(submission);
            created.Add(Map(submission));
        }

        if (created.Count > 0)
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var p in savedPaths)
                if (File.Exists(p)) File.Delete(p);
            throw;
        }

        return new LabBatchUploadResult { Created = created, Warnings = warnings };
    }

    public async Task RegradeAsync(Guid id, CancellationToken ct = default)
    {
        var submission = await uow.LabSubmissions.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabSubmission '{id}' not found.");

        // Cancel any active jobs so the new job won't be skipped
        var activeJobs = (await uow.LabGradingJobs.FindAsync(j =>
            j.LabSubmissionId == id &&
            (j.Status == LabGradingJobStatus.Pending || j.Status == LabGradingJobStatus.Running)))
            .ToList();

        foreach (var job in activeJobs)
        {
            job.Status = LabGradingJobStatus.Failed;
            job.ErrorMessage = "Cancelled — regrade requested.";
            job.FinishedAt = DateTime.UtcNow;
            uow.LabGradingJobs.Update(job);
        }

        submission.Status = LabSubmissionStatus.Pending;
        submission.UpdatedAt = DateTime.UtcNow;
        uow.LabSubmissions.Update(submission);

        await uow.LabGradingJobs.AddAsync(new LabGradingJob { LabSubmissionId = id });
        await uow.SaveChangesAsync(ct);
    }

    public async Task<int> RegradeAllAsync(Guid assignmentId, CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"LabAssignment '{assignmentId}' not found.");

        var submissions = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == assignmentId)).ToList();

        foreach (var submission in submissions)
        {
            var activeJobs = (await uow.LabGradingJobs.FindAsync(j =>
                j.LabSubmissionId == submission.Id &&
                (j.Status == LabGradingJobStatus.Pending || j.Status == LabGradingJobStatus.Running)))
                .ToList();

            foreach (var job in activeJobs)
            {
                job.Status = LabGradingJobStatus.Failed;
                job.ErrorMessage = "Cancelled — regrade all requested.";
                job.FinishedAt = DateTime.UtcNow;
                uow.LabGradingJobs.Update(job);
            }

            submission.Status = LabSubmissionStatus.Pending;
            submission.UpdatedAt = DateTime.UtcNow;
            uow.LabSubmissions.Update(submission);

            await uow.LabGradingJobs.AddAsync(new LabGradingJob { LabSubmissionId = submission.Id });
        }

        await uow.SaveChangesAsync(ct);
        return submissions.Count;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var submission = await uow.LabSubmissions.GetByIdAsync(id)
            ?? throw new NotFoundException($"LabSubmission '{id}' not found.");
        if (File.Exists(submission.FilePath))
            File.Delete(submission.FilePath);
        uow.LabSubmissions.Remove(submission);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteAllByAssignmentAsync(Guid assignmentId, CancellationToken ct = default)
    {
        _ = await uow.LabAssignments.GetByIdAsync(assignmentId)
            ?? throw new NotFoundException($"LabAssignment '{assignmentId}' not found.");
        var submissions = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == assignmentId)).ToList();
        foreach (var s in submissions)
        {
            if (File.Exists(s.FilePath))
                File.Delete(s.FilePath);
            uow.LabSubmissions.Remove(s);
        }
        if (submissions.Count > 0)
            await uow.SaveChangesAsync(ct);
        return submissions.Count;
    }

    private static LabSubmissionDto Map(LabSubmission s) => new()
    {
        Id = s.Id,
        LabAssignmentId = s.LabAssignmentId,
        StudentCode = s.StudentCode,
        OriginalFileName = s.OriginalFileName,
        Status = s.Status.ToString(),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };
}
