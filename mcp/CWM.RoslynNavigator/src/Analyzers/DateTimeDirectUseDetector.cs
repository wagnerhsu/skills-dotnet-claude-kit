using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// AP004: Detects direct use of DateTime.Now, DateTime.UtcNow, and DateTimeOffset.Now.
/// Use TimeProvider for testability and consistency.
/// </summary>
internal sealed class DateTimeDirectUseDetector : IAntiPatternDetector
{
    private static readonly HashSet<string> ForbiddenMembers = new(StringComparer.Ordinal)
    {
        "DateTime.Now",
        "DateTime.UtcNow",
        "DateTimeOffset.Now",
        "System.DateTime.Now",
        "System.DateTime.UtcNow",
        "System.DateTimeOffset.Now"
    };

    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath ?? "unknown";

        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var fullText = access.ToString();
            if (!ForbiddenMembers.Contains(fullText))
                continue;

            var line = access.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            yield return new AntiPatternViolation(
                Id: "AP004",
                Severity: AntiPatternSeverity.Warning,
                Message: $"Direct use of {fullText} is untestable and inconsistent across time zones",
                File: filePath,
                Line: line,
                Snippet: fullText,
                Suggestion: "Inject TimeProvider and use TimeProvider.GetUtcNow()");
        }
    }
}
