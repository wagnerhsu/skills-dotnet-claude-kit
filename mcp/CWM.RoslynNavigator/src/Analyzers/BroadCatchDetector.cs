using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// AP005: Detects broad catch(Exception) without specific handling.
/// AP007: Detects empty catch blocks that silently swallow errors.
/// Files named *Middleware* or *ExceptionHandler* are excluded (legitimate global handlers).
/// </summary>
internal sealed class BroadCatchDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => false;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        var filePath = tree.FilePath ?? "unknown";
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Skip global exception handler files
        if (fileName.Contains("Middleware", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("ExceptionHandler", StringComparison.OrdinalIgnoreCase))
            yield break;

        var root = tree.GetRoot(ct);

        foreach (var catchClause in root.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            var line = catchClause.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            // AP007: Empty catch blocks
            if (catchClause.Block.Statements.Count == 0)
            {
                var snippet = catchClause.Declaration is not null
                    ? $"catch ({catchClause.Declaration.Type})"
                    : "catch";

                yield return new AntiPatternViolation(
                    Id: "AP007",
                    Severity: AntiPatternSeverity.Error,
                    Message: "Empty catch block silently swallows errors",
                    File: filePath,
                    Line: line,
                    Snippet: $"{snippet} {{ }}",
                    Suggestion: "Log the exception or rethrow. If intentionally ignoring, add a comment explaining why");
                continue; // Don't also flag as AP005
            }

            // AP005: Broad catch(Exception)
            if (catchClause.Declaration is null)
            {
                yield return new AntiPatternViolation(
                    Id: "AP005",
                    Severity: AntiPatternSeverity.Warning,
                    Message: "Bare catch clause catches all exceptions including OutOfMemoryException",
                    File: filePath,
                    Line: line,
                    Snippet: "catch { ... }",
                    Suggestion: "Catch specific exception types relevant to the operation");
                continue;
            }

            var typeName = catchClause.Declaration.Type.ToString();
            if (typeName is "Exception" or "System.Exception")
            {
                yield return new AntiPatternViolation(
                    Id: "AP005",
                    Severity: AntiPatternSeverity.Warning,
                    Message: "catch(Exception) catches all exceptions including critical system exceptions",
                    File: filePath,
                    Line: line,
                    Snippet: $"catch ({typeName}) {{ ... }}",
                    Suggestion: "Catch specific exception types relevant to the operation");
            }
        }
    }
}
