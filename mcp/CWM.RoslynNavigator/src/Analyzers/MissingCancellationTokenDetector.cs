using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// AP009: Detects public async methods that don't accept a CancellationToken parameter.
/// Without CancellationToken, callers cannot cancel long-running operations.
/// Skips interface implementations (the interface defines the contract).
/// </summary>
internal sealed class MissingCancellationTokenDetector : IAntiPatternDetector
{
    public bool RequiresSemanticModel => true;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        if (model is null)
            yield break;

        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath ?? "unknown";

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            // Only public async methods returning Task/Task<T>
            if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
                continue;

            if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
                continue;

            // Skip async void (handled by AP001)
            if (method.ReturnType is PredefinedTypeSyntax predefined
                && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
                continue;

            // Skip if already has CancellationToken parameter
            if (method.ParameterList.Parameters.Any(p =>
                p.Type?.ToString() is "CancellationToken" or "System.Threading.CancellationToken"))
                continue;

            // Skip interface implementations
            var symbol = model.GetDeclaredSymbol(method, ct);
            if (symbol is null)
                continue;

            if (IsInterfaceImplementation(symbol))
                continue;

            // Skip overrides (base class defines the contract)
            if (symbol.IsOverride)
                continue;

            var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var snippet = $"public async {method.ReturnType} {method.Identifier.Text}(...)";

            yield return new AntiPatternViolation(
                Id: "AP009",
                Severity: AntiPatternSeverity.Warning,
                Message: $"Public async method '{method.Identifier.Text}' has no CancellationToken parameter",
                File: filePath,
                Line: line,
                Snippet: snippet,
                Suggestion: "Add CancellationToken ct = default as the last parameter");
        }
    }

    private static bool IsInterfaceImplementation(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
            return false;

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
            {
                var impl = containingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(impl, method))
                    return true;
            }
        }

        return false;
    }
}
