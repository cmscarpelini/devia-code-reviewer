using System.Text;
using DevIa.Domain.Reviews;

namespace DevIa.Application.Reviews;

/// <summary>
/// Builds a rejection justification from the AI assessment (summary + findings) for the case
/// where a reviewer rejects without writing one and consents to using the AI analysis instead
/// (SPEC-0003 extension). The result is persisted as the verdict justification and therefore
/// also becomes the GitHub PR comment, so it is formatted as Markdown.
/// </summary>
public static class AiAnalysisJustification
{
    public static string Build(Review review)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "_No reviewer justification was provided; the DevIA (AI) analysis below is used as the rejection rationale._");
        sb.AppendLine();
        sb.AppendLine($"**Summary:** {review.Summary ?? "No summary available."}");

        var findings = review.Findings
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.FilePath)
            .ToList();

        sb.AppendLine();
        sb.AppendLine($"**Findings ({findings.Count}):**");

        if (findings.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("_None reported._");
        }
        else
        {
            foreach (var f in findings)
            {
                var location = f.Line is { } line ? $"{f.FilePath}:{line}" : f.FilePath;
                sb.AppendLine();
                sb.AppendLine($"- **[{f.Severity}/{f.Category}]** {f.Title} — `{location}`");
                sb.AppendLine($"  {f.Description}");
                if (!string.IsNullOrWhiteSpace(f.Suggestion))
                    sb.AppendLine($"  💡 {f.Suggestion}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
