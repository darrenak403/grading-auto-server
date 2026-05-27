namespace PRN232.LMS.Services.Models;

/// <summary>Business model for Subject - used in Service layer processing</summary>
public class SubjectBusinessModel
{
    public int SubjectId { get; set; }
    public string SubjectCode { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public int Credit { get; set; }
}
