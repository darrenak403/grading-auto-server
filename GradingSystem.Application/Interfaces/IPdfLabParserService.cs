using GradingSystem.Application.Models;

namespace GradingSystem.Application.Interfaces;

public interface IPdfLabParserService
{
    Task<List<LabTestCaseDraft>> ParseAsync(string pdfPath, CancellationToken ct = default);
    Task<(bool Healthy, string Message)> CheckHealthAsync(CancellationToken ct = default);
}
