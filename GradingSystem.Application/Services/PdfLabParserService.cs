using GradingSystem.Application.Interfaces;
using GradingSystem.Application.Models;

namespace GradingSystem.Application.Services;

// Stub — AWS Bedrock removed. Not registered in DI.
public sealed class PdfLabParserService : IPdfLabParserService
{
    public Task<List<LabTestCaseDraft>> ParseAsync(string pdfPath, CancellationToken ct = default)
        => Task.FromResult(new List<LabTestCaseDraft>());

    public Task<(bool Healthy, string Message)> CheckHealthAsync(CancellationToken ct = default)
        => Task.FromResult((false, "PDF parsing removed. Use batch import instead."));
}
