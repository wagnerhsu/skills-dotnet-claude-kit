using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CWM.RoslynNavigator;

/// <summary>
/// Shared utility for resolving symbol names to ISymbol instances across all projects in the solution.
/// Supports disambiguation by file path and line number.
/// </summary>
public static class SymbolResolver
{
    /// <summary>
    /// Finds all symbols matching the given name across the entire solution.
    /// </summary>
    public static async Task<IReadOnlyList<ISymbol>> FindSymbolsByNameAsync(
        WorkspaceManager workspace,
        string name,
        string? kindFilter = null,
        CancellationToken ct = default)
    {
        var solution = workspace.GetSolution();
        if (solution is null) return [];

        var results = new List<ISymbol>();

        foreach (var projectId in solution.ProjectIds)
        {
            var compilation = await workspace.GetCompilationAsync(projectId, ct);
            if (compilation is null) continue;

            var symbols = compilation.GetSymbolsWithName(name, SymbolFilter.All, ct);

            foreach (var symbol in symbols)
            {
                if (kindFilter is not null && !MatchesKind(symbol, kindFilter))
                    continue;

                results.Add(symbol);
            }
        }

        return results.Distinct(SymbolEqualityComparer.Default).ToList();
    }

    /// <summary>
    /// Finds a single symbol by name, optionally disambiguated by file and line.
    /// </summary>
    public static async Task<ISymbol?> ResolveSymbolAsync(
        WorkspaceManager workspace,
        string name,
        string? file = null,
        int? line = null,
        CancellationToken ct = default)
    {
        var symbols = await FindSymbolsByNameAsync(workspace, name, ct: ct);

        if (symbols.Count == 0)
            return null;

        if (symbols.Count == 1)
            return symbols[0];

        // Disambiguate by file
        if (file is not null)
        {
            var byFile = symbols.Where(s =>
                s.DeclaringSyntaxReferences.Any(r =>
                    r.SyntaxTree.FilePath.EndsWith(file, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (byFile.Count == 1)
                return byFile[0];

            if (byFile.Count > 1 && line.HasValue)
            {
                // Further disambiguate by line
                return byFile.FirstOrDefault(s =>
                    s.DeclaringSyntaxReferences.Any(r =>
                        r.SyntaxTree.GetLineSpan(r.Span).StartLinePosition.Line + 1 == line.Value));
            }

            if (byFile.Count > 0)
                return byFile[0];
        }

        return symbols[0];
    }

    /// <summary>
    /// Gets the file path and line number for a symbol's declaration.
    /// </summary>
    public static (string File, int Line)? GetLocation(ISymbol symbol)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is null) return null;

        var lineSpan = syntaxRef.SyntaxTree.GetLineSpan(syntaxRef.Span);
        var filePath = syntaxRef.SyntaxTree.FilePath;
        var lineNumber = lineSpan.StartLinePosition.Line + 1; // 1-based

        return (filePath, lineNumber);
    }

    /// <summary>
    /// Gets a short snippet of the source text around a location.
    /// </summary>
    public static async Task<string> GetSnippetAsync(
        Document document,
        int position,
        CancellationToken ct = default)
    {
        var text = await document.GetTextAsync(ct);
        var line = text.Lines.GetLineFromPosition(position);
        return line.ToString().Trim();
    }

    /// <summary>
    /// Maps a symbol kind string to a Roslyn SymbolKind check.
    /// </summary>
    public static bool MatchesKind(ISymbol symbol, string kind) => kind.ToLowerInvariant() switch
    {
        "type" or "class" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Class },
        "interface" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Interface },
        "struct" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct },
        "enum" => symbol is INamedTypeSymbol { TypeKind: TypeKind.Enum },
        "record" => symbol is INamedTypeSymbol { IsRecord: true },
        "method" => symbol is IMethodSymbol,
        "property" => symbol is IPropertySymbol,
        "field" => symbol is IFieldSymbol,
        "event" => symbol is IEventSymbol,
        "namespace" => symbol is INamespaceSymbol,
        "any" or "" or null => true,
        _ => symbol.Kind.ToString().Equals(kind, StringComparison.OrdinalIgnoreCase)
    };

    /// <summary>
    /// Gets a human-readable kind string for a symbol.
    /// </summary>
    public static string GetKindString(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol { IsRecord: true } nts => nts.TypeKind == TypeKind.Struct ? "record struct" : "record",
        INamedTypeSymbol nts => nts.TypeKind.ToString().ToLowerInvariant(),
        IMethodSymbol => "method",
        IPropertySymbol => "property",
        IFieldSymbol => "field",
        IEventSymbol => "event",
        INamespaceSymbol => "namespace",
        _ => symbol.Kind.ToString().ToLowerInvariant()
    };
}
