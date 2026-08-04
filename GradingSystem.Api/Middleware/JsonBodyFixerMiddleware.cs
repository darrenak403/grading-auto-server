using System.Text;

namespace GradingSystem.Api.Middleware;

/// <summary>
/// Pre-processes JSON request bodies where AI tools generate unescaped JSON objects
/// as string values for inputJson / expectJson fields.
///
/// Handles the pattern AI commonly produces:
///   "expectJson": "{"success":true}"   ← invalid JSON (unescaped inner quotes)
///
/// Transforms it to valid JSON before ASP.NET Core parses the body:
///   "expectJson": {"success":true}
/// </summary>
public class JsonBodyFixerMiddleware(RequestDelegate next)
{
    private static readonly string[] TargetFields = ["\"inputJson\"", "\"expectJson\""];

    public async Task InvokeAsync(HttpContext context)
    {
        if ((context.Request.Method == "POST" || context.Request.Method == "PUT") &&
            context.Request.ContentType?.Contains("application/json") == true &&
            context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            var fixed_ = FixBrokenJsonStrings(body);
            if (!ReferenceEquals(fixed_, body))
            {
                var bytes = Encoding.UTF8.GetBytes(fixed_);
                context.Request.Body = new MemoryStream(bytes);
                context.Request.ContentLength = bytes.Length;
            }
            else
            {
                context.Request.Body.Seek(0, SeekOrigin.Begin);
            }
        }

        await next(context);
    }

    internal static string FixBrokenJsonStrings(string json)
    {
        bool hasTarget = false;
        foreach (var t in TargetFields)
            if (json.Contains(t, StringComparison.Ordinal)) { hasTarget = true; break; }
        if (!hasTarget) return json;

        var result = new StringBuilder(json.Length);
        int pos = 0;

        while (pos < json.Length)
        {
            int nextIdx = -1;
            string? nextField = null;
            foreach (var field in TargetFields)
            {
                int idx = json.IndexOf(field, pos, StringComparison.Ordinal);
                if (idx >= 0 && (nextIdx < 0 || idx < nextIdx))
                {
                    nextIdx = idx;
                    nextField = field;
                }
            }

            if (nextIdx < 0)
            {
                result.Append(json, pos, json.Length - pos);
                break;
            }

            // Append text up to and including the matched field name
            result.Append(json, pos, nextIdx - pos + nextField!.Length);
            pos = nextIdx + nextField.Length;

            // Copy whitespace + ':' + whitespace verbatim
            while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                result.Append(json[pos++]);
            if (pos < json.Length && json[pos] == ':')
                result.Append(json[pos++]);
            while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                result.Append(json[pos++]);

            // Detect broken pattern: opening " immediately followed by { or [
            if (pos + 1 < json.Length && json[pos] == '"' &&
                (json[pos + 1] == '{' || json[pos + 1] == '['))
            {
                pos++; // skip the opening outer "
                char open = json[pos];
                char close = open == '{' ? '}' : ']';
                int depth = 0;
                bool inStr = false;
                bool esc = false;

                while (pos < json.Length)
                {
                    char c = json[pos];

                    if (esc) { result.Append(c); pos++; esc = false; continue; }
                    if (c == '\\' && inStr) { result.Append(c); pos++; esc = true; continue; }

                    if (c == '"')
                    {
                        if (inStr)
                        {
                            inStr = false;
                            result.Append(c); pos++;
                        }
                        else if (depth == 0)
                        {
                            // Closing outer " of the broken string wrapper — skip it
                            pos++; break;
                        }
                        else
                        {
                            inStr = true;
                            result.Append(c); pos++;
                        }
                        continue;
                    }

                    if (!inStr)
                    {
                        if (c == open) depth++;
                        else if (c == close)
                        {
                            depth--;
                            if (depth == 0)
                            {
                                result.Append(c); pos++;
                                // Skip trailing outer " if present
                                if (pos < json.Length && json[pos] == '"') pos++;
                                break;
                            }
                        }
                    }

                    result.Append(c); pos++;
                }
            }
            // else: value is null / proper string / number — leave as-is; next
            // iteration will continue from current pos (after the colon)
        }

        return result.ToString();
    }
}
