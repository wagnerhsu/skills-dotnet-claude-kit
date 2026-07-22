using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// AP003: Detects direct <c>new HttpClient()</c> instantiation.
/// Direct instantiation causes socket exhaustion. Use IHttpClientFactory instead.
/// </summary>
internal sealed class HttpClientInstantiationDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath ?? "unknown";

        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var typeName = creation.Type.ToString();
            if (typeName is not ("HttpClient" or "System.Net.Http.HttpClient"))
                continue;

            var line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            yield return new AntiPatternViolation(
                Id: "AP003",
                Severity: AntiPatternSeverity.Warning,
                Message: "Direct HttpClient instantiation causes socket exhaustion under load",
                File: filePath,
                Line: line,
                Snippet: $"new {typeName}()",
                Suggestion: "Use IHttpClientFactory via dependency injection");
        }
    }
}
