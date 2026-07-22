using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// AP006: Detects string interpolation or concatenation in logging method calls.
/// Interpolated strings bypass structured logging, allocate even when the log level is disabled,
/// and prevent log aggregation tools from grouping related messages.
/// </summary>
internal sealed class LoggingInterpolationDetector : IAntiPatternDetector
{
    private static readonly HashSet<string> LogMethods = new(StringComparer.Ordinal)
    {
        "LogTrace", "LogDebug", "LogInformation",
        "LogWarning", "LogError", "LogCritical"
    };

    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath ?? "unknown";

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            // Match logger.LogXxx(...) or LogXxx(...)
            string? methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => null
            };

            if (methodName is null || !LogMethods.Contains(methodName))
                continue;

            // Check arguments for interpolated strings or string concatenation
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (arg.Expression is InterpolatedStringExpressionSyntax interpolated)
                {
                    var line = interpolated.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var snippet = interpolated.ToString();
                    if (snippet.Length > 60) snippet = snippet[..57] + "...";

                    yield return new AntiPatternViolation(
                        Id: "AP006",
                        Severity: AntiPatternSeverity.Warning,
                        Message: $"String interpolation in {methodName}() bypasses structured logging",
                        File: filePath,
                        Line: line,
                        Snippet: snippet,
                        Suggestion: $"Use message template: {methodName}(\"Message {{Param}}\", value)");
                }
                else if (arg.Expression is BinaryExpressionSyntax binary && ContainsStringConcat(binary))
                {
                    var line = binary.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var snippet = binary.ToString();
                    if (snippet.Length > 60) snippet = snippet[..57] + "...";

                    yield return new AntiPatternViolation(
                        Id: "AP006",
                        Severity: AntiPatternSeverity.Warning,
                        Message: $"String concatenation in {methodName}() bypasses structured logging",
                        File: filePath,
                        Line: line,
                        Snippet: snippet,
                        Suggestion: $"Use message template: {methodName}(\"Message {{Param}}\", value)");
                }
            }
        }
    }

    private static bool ContainsStringConcat(BinaryExpressionSyntax binary)
    {
        // Check if this is a + operation involving at least one string literal
        if (binary.OperatorToken.Text != "+")
            return false;

        return binary.Left is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax
            || binary.Right is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax
            || (binary.Left is BinaryExpressionSyntax leftBinary && ContainsStringConcat(leftBinary));
    }
}
