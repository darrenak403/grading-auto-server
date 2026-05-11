namespace GradingSystem.Domain.Entities;

public class TestCase : BaseEntity
{
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public string Name { get; set; } = string.Empty;          // tên hiển thị cột Excel, ví dụ "GET list", "POST create"
    public string HttpMethod { get; set; } = string.Empty;   // GET, POST, …
    public string UrlTemplate { get; set; } = string.Empty;  // /api/products/{id}
    public string? InputJson { get; set; }                   // request body / query params
    public string ExpectJson { get; set; } = string.Empty;   // {"status":200,"isArray":true,...}
    public decimal Score { get; set; }
    public int Order { get; set; }
}
