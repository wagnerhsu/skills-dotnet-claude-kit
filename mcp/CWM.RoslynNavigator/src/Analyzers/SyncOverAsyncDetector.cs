using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// AP002: Detects synchronous blocking over async code — .Result, .Wait(), .GetAwaiter().GetResult().
/// These cause deadlocks in ASP.NET Core and thread pool starvation.
/// </summary>
internal sealed class SyncOverAsyncDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath ?? "unknown";

        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var memberName = access.Name.Identifier.Text;
            string? pattern = null;
            string? snippet = null;

            if (memberName == "Result" && access.Name is SimpleNameSyntax)
            {
                pattern = ".Result";
                snippet = access.ToString();
            }
            else if (memberName == "Wait" && access.Parent is InvocationExpressionSyntax)
            {
                pattern = ".Wait()";
                snippet = access.Parent.ToString();
            }
            else if (memberName == "GetResult"
                && access.Expression is InvocationExpressionSyntax innerInvocation
                && innerInvocation.Expression is MemberAccessExpressionSyntax innerAccess
                && innerAccess.Name.Identifier.Text == "GetAwaiter")
            {
                pattern = ".GetAwaiter().GetResult()";
                snippet = access.Parent is InvocationExpressionSyntax inv ? inv.ToString() : access.ToString();
            }

            if (pattern is null)
                continue;

            var line = access.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var truncatedSnippet = snippet!.Length > 80 ? snippet[..77] + "..." : snippet;

            yield return new AntiPatternViolation(
                Id: "AP002",
                Severity: AntiPatternSeverity.Error,
                Message: $"Synchronous blocking via {pattern} causes deadlocks and thread pool starvation",
                File: filePath,
                Line: line,
                Snippet: truncatedSnippet,
                Suggestion: "Use await instead of synchronous blocking");
        }
    }
}
