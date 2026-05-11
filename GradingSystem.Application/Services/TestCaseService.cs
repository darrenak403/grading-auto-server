using System.Text.Json;
using System.Text.Json.Serialization;
using GradingSystem.Application.DTOs;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Application.Services;

public class TestCaseService(IUnitOfWork unitOfWork) : ITestCaseService
{
    private static readonly HashSet<string> AllowedHttpMethods = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS",
    };

    private static readonly JsonSerializerOptions SerializerOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<TestCaseDto>> CreateManyAsync(
        Guid questionId,
        IReadOnlyList<CreateTestCaseRequest> requests,
        CancellationToken ct = default)
    {
        if (requests.Count == 0)
            throw new BadRequestException("At least one test case is required.");

        var question = await unitOfWork.Questions.GetByIdAsync(questionId)
            ?? throw new NotFoundException($"Question '{questionId}' not found.");

        var totalScore = requests.Sum(r => r.Score);
        if (totalScore != question.MaxScore)
            throw new BadRequestException(
                $"Sum of test case scores ({totalScore}) must equal question MaxScore ({question.MaxScore}).");

        var created = new List<TestCaseDto>(requests.Count);

        foreach (var req in requests)
        {
            var normalizedMethod = req.HttpMethod.Trim().ToUpperInvariant();
            if (!AllowedHttpMethods.Contains(normalizedMethod))
                throw new BadRequestException(
                    $"HttpMethod '{req.HttpMethod}' is not supported. Allowed values: {string.Join(", ", AllowedHttpMethods)}.");

            if (req.Score > question.MaxScore)
                throw new BadRequestException(
                    $"Test case score ({req.Score}) exceeds question MaxScore ({question.MaxScore}).");

            var entity = new TestCase
            {
                QuestionId  = questionId,
                Name        = BuildName(normalizedMethod, req.UrlTemplate),
                HttpMethod  = normalizedMethod,
                UrlTemplate = req.UrlTemplate.Trim(),
                InputJson   = SerializeInput(req.Input),
                ExpectJson  = SerializeExpect(req),
                Score       = req.Score,
                Order       = req.Order,
            };

            await unitOfWork.TestCases.AddAsync(entity);
            created.Add(Map(entity));
        }

        await unitOfWork.SaveChangesAsync(ct);

        return created;
    }

    public async Task<IEnumerable<TestCaseDto>> GetByQuestionIdAsync(Guid questionId, CancellationToken ct = default)
    {
        var entities = await unitOfWork.TestCases.FindAsync(t => t.QuestionId == questionId);
        return entities.OrderBy(t => t.Order).ThenBy(t => t.CreatedAt).Select(Map);
    }

    public async Task<int> DeleteByQuestionIdAsync(Guid questionId, CancellationToken ct = default)
    {
        _ = await unitOfWork.Questions.GetByIdAsync(questionId)
            ?? throw new NotFoundException($"Question '{questionId}' not found.");

        var existing = (await unitOfWork.TestCases.FindAsync(t => t.QuestionId == questionId)).ToList();
        foreach (var tc in existing)
            unitOfWork.TestCases.Remove(tc);

        await unitOfWork.SaveChangesAsync(ct);
        return existing.Count;
    }

    public async Task<TestCaseDto> DeleteByIdAsync(Guid testCaseId, CancellationToken ct = default)
    {
        var entity = await unitOfWork.TestCases.GetByIdAsync(testCaseId)
            ?? throw new NotFoundException($"Test case '{testCaseId}' not found.");

        unitOfWork.TestCases.Remove(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    public async Task<TestCaseDto> UpdateAsync(Guid testCaseId, CreateTestCaseRequest request, CancellationToken ct = default)
    {
        var entity = await unitOfWork.TestCases.GetByIdAsync(testCaseId)
            ?? throw new NotFoundException($"Test case '{testCaseId}' not found.");

        var normalizedMethod = request.HttpMethod.Trim().ToUpperInvariant();
        if (!AllowedHttpMethods.Contains(normalizedMethod))
            throw new BadRequestException(
                $"HttpMethod '{request.HttpMethod}' is not supported. Allowed values: {string.Join(", ", AllowedHttpMethods)}.");

        entity.Name        = BuildName(normalizedMethod, request.UrlTemplate);
        entity.HttpMethod  = normalizedMethod;
        entity.UrlTemplate = request.UrlTemplate.Trim();
        entity.InputJson   = SerializeInput(request.Input);
        entity.ExpectJson  = SerializeExpect(request);
        entity.Score       = request.Score;
        entity.Order       = request.Order;

        unitOfWork.TestCases.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return Map(entity);
    }

    private static string BuildName(string httpMethod, string urlTemplate)
    {
        var generated = $"{httpMethod} {urlTemplate.Trim()}";
        return generated.Length <= 100 ? generated : generated[..100];
    }

    private static string? SerializeInput(JsonElement? input) =>
        input is { } el && el.ValueKind != JsonValueKind.Null && el.ValueKind != JsonValueKind.Undefined
            ? el.GetRawText()
            : null;

    private static string SerializeExpect(CreateTestCaseRequest req) =>
        JsonSerializer.Serialize(new
        {
            status           = req.ExpectedStatus,
            isArray          = req.IsArray,
            fields           = req.Fields,
            value            = req.Value,
            selector         = req.Selector,
            selectorText     = req.SelectorText,
            selectorMinCount = req.SelectorMinCount,
            body             = req.ExpectedBody,
            elementId        = req.ElementId,
            elementText      = req.ElementText,
            extract          = req.Extract,
        }, SerializerOpts);

    private static TestCaseDto Map(TestCase entity)
    {
        var dto = new TestCaseDto
        {
            Id          = entity.Id,
            QuestionId  = entity.QuestionId,
            Name        = entity.Name,
            HttpMethod  = entity.HttpMethod,
            UrlTemplate = entity.UrlTemplate,
            Score       = entity.Score,
            Order       = entity.Order,
        };

        if (entity.InputJson != null)
        {
            try { dto.Input = JsonDocument.Parse(entity.InputJson).RootElement.Clone(); }
            catch { /* ignore malformed stored JSON */ }
        }

        if (string.IsNullOrWhiteSpace(entity.ExpectJson)) return dto;

        try
        {
            using var doc = JsonDocument.Parse(entity.ExpectJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var s) && s.ValueKind != JsonValueKind.Null)
                dto.ExpectedStatus = s.GetInt32();

            if (root.TryGetProperty("isArray", out var a) && a.ValueKind != JsonValueKind.Null)
                dto.IsArray = a.GetBoolean();

            if (root.TryGetProperty("fields", out var f) && f.ValueKind == JsonValueKind.Array)
                dto.Fields = f.EnumerateArray().Select(x => x.GetString()!).ToList();

            if (root.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null)
                dto.Value = v.GetString();

            if (root.TryGetProperty("selector", out var sel) && sel.ValueKind != JsonValueKind.Null)
                dto.Selector = sel.GetString();

            if (root.TryGetProperty("selectorText", out var st) && st.ValueKind != JsonValueKind.Null)
                dto.SelectorText = st.GetString();

            if (root.TryGetProperty("selectorMinCount", out var smc) && smc.ValueKind != JsonValueKind.Null)
                dto.SelectorMinCount = smc.GetInt32();

            if (root.TryGetProperty("body", out var body) && body.ValueKind != JsonValueKind.Null)
                dto.ExpectedBody = body.Clone();

            if (root.TryGetProperty("elementId", out var eid) && eid.ValueKind != JsonValueKind.Null)
                dto.ElementId = eid.GetString();

            if (root.TryGetProperty("elementText", out var etxt) && etxt.ValueKind != JsonValueKind.Null)
                dto.ElementText = etxt.GetString();

            if (root.TryGetProperty("extract", out var ext) && ext.ValueKind == JsonValueKind.Object)
                dto.Extract = ext.Deserialize<Dictionary<string, string>>(SerializerOpts);
        }
        catch { /* ignore malformed stored JSON */ }

        return dto;
    }
}
