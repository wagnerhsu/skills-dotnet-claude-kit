using System.ComponentModel;
using System.Text.Json;
using CWM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace CWM.RoslynNavigator.Tools;

[McpServerToolType]
public static class FindDeadCodeTool
{
    [McpServerTool(Name = "find_dead_code"), Description("Find unused types, methods, and properties across the solution. Identifies symbols with zero references that are not public API entry points, interface implementations, or overrides.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Scope: 'file', 'project', or 'solution'")] string scope = "solution",
        [Description("Optional: file path or project name depending on scope")] string? path = null,
        [Description("Kind filter: 'type', 'method', 'property', or 'all'")] string kind = "all",
        [Description("Maximum results to return")] int maxResults = 50,
        CancellationToken ct = default)
    {
        var notReady = await workspace.EnsureReadyOrStatusAsync(ct);
        if (notReady is not null) return notReady;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new DeadCodeResult([], 0, 0));

        var candidates = new List<(ISymbol Symbol, string File, int Line)>();

        var projects = GetProjectsForScope(solution, scope, path);

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(project.Id, ct);
            if (compilation is null) continue;

            var trees = scope == "file" && path is not null
                ? compilation.SyntaxTrees.Where(t => t.FilePath?.Contains(path, StringComparison.OrdinalIgnoreCase) == true)
                : compilation.SyntaxTrees;

            foreach (var tree in trees)
            {
                ct.ThrowIfCancellationRequested();

                var semanticModel = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                foreach (var node in root.DescendantNodes())
                {
                    var symbol = semanticModel.GetDeclaredSymbol(node, ct);
                    if (symbol is null) continue;

                    if (!MatchesKindFilter(symbol, kind)) continue;
                    if (ShouldSkip(symbol)) continue;

                    var location = SymbolResolver.GetLocation(symbol);
                    if (location.HasValue)
                        candidates.Add((symbol, location.Value.File, location.Value.Line));
                }
            }
        }

        // Deduplicate by display string
        var unique = candidates
            .DistinctBy(c => c.Symbol.ToDisplayString())
            .ToList();

        // Pre-collect all source texts for fast name-based pre-filter
        var sourceTexts = new Dictionary<SyntaxTree, string>();
        foreach (var proj in projects)
        {
            var comp = await workspace.GetCompilationAsync(proj.Id, ct);
            if (comp is null) continue;
            foreach (var tree in comp.SyntaxTrees)
                sourceTexts.TryAdd(tree, (await tree.GetTextAsync(ct)).ToString());
        }

        // Check references for each candidate
        var deadCode = new List<DeadCodeInfo>();
        var totalFound = 0;

        foreach (var (symbol, symbolFile, symbolLine) in unique)
        {
            ct.ThrowIfCancellationRequested();

            // Fast pre-filter: if symbol name appears in other files, likely not dead
            var declaringTree = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
            var likelyReferenced = sourceTexts.Any(kvp =>
                kvp.Key != declaringTree &&
                kvp.Value.Contains(symbol.Name, StringComparison.Ordinal));

            if (likelyReferenced) continue;

            // Expensive but necessary for remaining candidates
            var references = await SymbolFinder.FindReferencesAsync(symbol, solution, ct);
            var refCount = references.Sum(r => r.Locations.Count());

            if (refCount == 0)
            {
                totalFound++;
                if (deadCode.Count < maxResults)
                {
                    deadCode.Add(new DeadCodeInfo(
                        Name: symbol.Name,
                        Kind: SymbolResolver.GetKindString(symbol),
                        File: workspace.ToRelativePath(symbolFile),
                        Line: symbolLine,
                        ContainingType: symbol.ContainingType?.Name));
                }
            }
        }

        return JsonSerializer.Serialize(new DeadCodeResult(deadCode, deadCode.Count, totalFound));
    }

    private static bool MatchesKindFilter(ISymbol symbol, string kind) => kind.ToLowerInvariant() switch
    {
        "type" or "class" => symbol is INamedTypeSymbol,
        "method" => symbol is IMethodSymbol,
        "property" => symbol is IPropertySymbol,
        "all" => symbol is INamedTypeSymbol or IMethodSymbol or IPropertySymbol,
        _ => symbol is INamedTypeSymbol or IMethodSymbol or IPropertySymbol
    };

    private static bool ShouldSkip(ISymbol symbol)
    {
        // Skip constructors
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor })
            return true;

        // Skip property accessors
        if (symbol is IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet })
            return true;

        // Skip interface implementations
        if (symbol.ContainingType is not null)
        {
            foreach (var iface in symbol.ContainingType.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    var impl = symbol.ContainingType.FindImplementationForInterfaceMember(member);
                    if (SymbolEqualityComparer.Default.Equals(impl, symbol))
                        return true;
                }
            }
        }

        // Skip overrides
        if (symbol is IMethodSymbol { IsOverride: true })
            return true;

        if (symbol is IPropertySymbol { IsOverride: true })
            return true;

        // Skip symbols with certain attributes (entry points, test classes, etc.)
        var attributes = symbol.GetAttributes();
        foreach (var attr in attributes)
        {
            var attrName = attr.AttributeClass?.Name ?? "";
            if (attrName is "Fact" or "Theory" or "Test" or "TestMethod"
                or "ApiController" or "McpServerToolType" or "McpServerTool")
                return true;
        }

        // Skip types containing Main method (entry points)
        if (symbol is INamedTypeSymbol type)
        {
            if (type.GetMembers("Main").Length > 0)
                return true;

            // Skip enums and delegates
            if (type.TypeKind is TypeKind.Enum or TypeKind.Delegate)
                return true;
        }

        // Skip non-private members that could be used externally
        // Only flag internal/private dead code
        if (symbol.DeclaredAccessibility == Accessibility.Public)
            return true;

        return false;
    }

    private static IEnumerable<Project> GetProjectsForScope(Solution solution, string scope, string? path) => scope switch
    {
        "project" when path is not null => solution.Projects.Where(p =>
            p.Name.Equals(path, StringComparison.OrdinalIgnoreCase)),
        "file" => solution.Projects,
        _ => solution.Projects
    };

}
