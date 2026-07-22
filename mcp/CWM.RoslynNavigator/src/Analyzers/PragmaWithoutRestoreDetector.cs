using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// AP008: Detects #pragma warning disable without a corresponding #pragma warning restore.
/// Unbounded pragmas suppress warnings for the entire file, hiding potential issues.
/// </summary>
internal sealed class PragmaWithoutRestoreDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var filePath = tree.FilePath ?? "unknown";
        var root = tree.GetRoot(ct);

        // Collect all pragma directives
        var disables = new List<(string Code, int Line, Location Location)>();
        var restores = new HashSet<string>(StringComparer.Ordinal);

        foreach (var trivia in root.DescendantTrivia())
        {
            ct.ThrowIfCancellationRequested();

            if (!trivia.IsKind(SyntaxKind.PragmaWarningDirectiveTrivia))
                continue;

            var directive = trivia.GetStructure() as Microsoft.CodeAnalysis.CSharp.Syntax.PragmaWarningDirectiveTriviaSyntax;
            if (directive is null)
                continue;

            var isDisable = directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);

            foreach (var errorCode in directive.ErrorCodes)
            {
                var code = errorCode.ToString().Trim();
                if (isDisable)
                {
                    var line = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    disables.Add((code, line, trivia.GetLocation()));
                }
                else
                {
                    restores.Add(code);
                }
            }
        }

        // Report disables without matching restores
        foreach (var (code, line, _) in disables)
        {
            if (restores.Contains(code))
                continue;

            yield return new AntiPatternViolation(
                Id: "AP008",
                Severity: AntiPatternSeverity.Warning,
                Message: $"#pragma warning disable {code} has no matching restore",
                File: filePath,
                Line: line,
                Snippet: $"#pragma warning disable {code}",
                Suggestion: $"Add #pragma warning restore {code} after the affected code");
        }
    }
}
