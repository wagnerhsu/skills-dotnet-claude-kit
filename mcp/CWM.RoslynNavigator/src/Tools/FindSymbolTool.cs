using System.ComponentModel;
using System.Text.Json;
using CWM.RoslynNavigator.Responses;
using ModelContextProtocol.Server;

namespace CWM.RoslynNavigator.Tools;

[McpServerToolType]
public static class FindSymbolTool
{
    [McpServerTool(Name = "find_symbol"), Description("Find where a type, method, or property is defined in the solution. Returns file path, line number, and namespace.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("The symbol name to search for (e.g., 'OrderRepository', 'CreateAsync')")] string name,
        [Description("Filter by kind: 'type', 'class', 'interface', 'struct', 'enum', 'record', 'method', 'property', 'field', or 'any'")] string kind = "any",
        CancellationToken ct = default)
    {
        var notReady = await workspace.EnsureReadyOrStatusAsync(ct);
        if (notReady is not null) return notReady;

        var symbols = await SymbolResolver.FindSymbolsByNameAsync(workspace, name, kind, ct);

        var results = symbols.Select(s =>
        {
            var location = SymbolResolver.GetLocation(s);
            return new SymbolLocation(
                Name: s.Name,
                Kind: SymbolResolver.GetKindString(s),
                File: location is { } loc ? workspace.ToRelativePath(loc.File) : "unknown",
                Line: location?.Line ?? 0,
                Namespace: s.ContainingNamespace?.ToDisplayString() ?? "global");
        }).ToList();

        return JsonSerializer.Serialize(new SymbolSearchResult(results));
    }
}
