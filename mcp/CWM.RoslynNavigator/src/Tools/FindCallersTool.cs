using System.ComponentModel;
using System.Text.Json;
using CWM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Server;

namespace CWM.RoslynNavigator.Tools;

[McpServerToolType]
public static class FindCallersTool
{
    [McpServerTool(Name = "find_callers"), Description("Find all methods that call a specific method. Useful for impact analysis and understanding dependencies.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("The method name to find callers for")] string methodName,
        [Description("Optional: containing class name to disambiguate")] string? className = null,
        CancellationToken ct = default)
    {
        var notReady = await workspace.EnsureReadyOrStatusAsync(ct);
        if (notReady is not null) return notReady;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new CallersResult([], 0));

        var symbol = await SymbolResolver.ResolveSymbolAsync(workspace, methodName, ct: ct);

        // If className is provided and the resolved symbol isn't in that type, try to find a better match
        if (symbol is not null && className is not null && symbol.ContainingType?.Name != className)
        {
            var allSymbols = await SymbolResolver.FindSymbolsByNameAsync(workspace, methodName, ct: ct);
            symbol = allSymbols.FirstOrDefault(s => s.ContainingType?.Name == className) ?? symbol;
        }

        if (symbol is null)
            return JsonSerializer.Serialize(new CallersResult([], 0));

        var callers = await SymbolFinder.FindCallersAsync(symbol, solution, ct);

        var results = new List<CallerInfo>();
        foreach (var caller in callers)
        {
            if (!caller.IsDirect) continue;

            var location = SymbolResolver.GetLocation(caller.CallingSymbol);
            if (location.HasValue)
            {
                results.Add(new CallerInfo(
                    Method: caller.CallingSymbol.Name,
                    ContainingType: caller.CallingSymbol.ContainingType?.Name ?? "unknown",
                    File: workspace.ToRelativePath(location.Value.File),
                    Line: location.Value.Line));
            }
        }

        return JsonSerializer.Serialize(new CallersResult(results, results.Count));
    }
}
