using System.ComponentModel;
using System.Text.Json;
using CWM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace CWM.RoslynNavigator.Tools;

[McpServerToolType]
public static class FindOverridesTool
{
    [McpServerTool(Name = "find_overrides"), Description("Find all overrides of a virtual or abstract method across the solution. Useful for understanding polymorphic behavior.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("The virtual or abstract method name to find overrides for")] string methodName,
        [Description("Optional: containing class name to disambiguate")] string? className = null,
        [Description("Maximum results to return. TotalFound in the response reports the full count; re-query with a higher value if it exceeds Count.")] int maxResults = 50,
        CancellationToken ct = default)
    {
        var notReady = await workspace.EnsureReadyOrStatusAsync(ct);
        if (notReady is not null) return notReady;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new OverridesResult([], 0, 0));

        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, methodName, ct: ct);

        if (symbol is not null && className is not null && symbol.ContainingType?.Name != className)
        {
            var allSymbols = await SymbolResolver.FindSymbolsByNameAsync(workspace, methodName, ct: ct);
            symbol = allSymbols.FirstOrDefault(s => s.ContainingType?.Name == className) ?? symbol;
        }

        if (symbol is null)
            return JsonSerializer.Serialize(new OverridesResult([], 0, 0));

        var overrides = await SymbolFinder.FindOverridesAsync(symbol, solution, cancellationToken: ct);

        var all = new List<OverrideInfo>();
        foreach (var overrideSymbol in overrides)
        {
            var location = SymbolResolver.GetLocation(overrideSymbol);
            if (location.HasValue)
            {
                all.Add(new OverrideInfo(
                    Method: overrideSymbol.Name,
                    ContainingType: overrideSymbol.ContainingType?.Name ?? "unknown",
                    File: workspace.ToRelativePath(location.Value.File),
                    Line: location.Value.Line));
            }
        }

        var results = all.Take(Math.Max(1, maxResults)).ToList();

        return JsonSerializer.Serialize(new OverridesResult(results, results.Count, all.Count));
    }
}
