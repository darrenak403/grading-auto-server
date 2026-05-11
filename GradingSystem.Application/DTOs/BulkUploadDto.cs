namespace GradingSystem.Application.DTOs;

public class BulkUploadResultDto
{
    public int Parsed { get; set; }
    public int Created { get; set; }
    public int Missing { get; set; }
    public List<string> Errors { get; set; } = [];
}
