using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// AP010: Detects EF Core read queries (via DbSet access) that lack AsNoTracking().
/// Tracked queries allocate change-tracking state that is unnecessary for read-only scenarios.
/// Skips chains containing .Select() (projections don't need tracking),
/// .Add/.Update/.Remove (mutation operations), or .AsNoTracking() already present.
/// </summary>
internal sealed class EfCoreNoTrackingDetector : IAntiPatternDetector
{
    private static readonly HashSet<string> MutationMethods = new(StringComparer.Ordinal)
    {
        "Add", "AddAsync", "AddRange", "AddRangeAsync",
        "Update", "UpdateRange",
        "Remove", "RemoveRange",
        "Attach", "AttachRange"
    };

    private static readonly HashSet<string> ReadTerminals = new(StringComparer.Ordinal)
    {
        "ToListAsync", "ToArrayAsync", "FirstAsync", "FirstOrDefaultAsync",
        "SingleAsync", "SingleOrDefaultAsync", "AnyAsync", "CountAsync",
        "LongCountAsync", "MinAsync", "MaxAsync", "SumAsync", "AverageAsync",
        "ToList", "ToArray", "First", "FirstOrDefault",
        "Single", "SingleOrDefault", "Any", "Count"
    };

    public bool RequiresSemanticModel => true;

    public IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct)
    {
        if (model is null)
            yield break;

        var root = tree.GetRoot(ct);
        var filePath = tree.FilePath ?? "unknown";

        // Find invocation chains that terminate in a read method
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (invocation.Expression is not MemberAccessExpressionSyntax terminalAccess)
                continue;

            var terminalMethod = terminalAccess.Name.Identifier.Text;
            if (!ReadTerminals.Contains(terminalMethod))
                continue;

            // Walk the invocation chain to collect all method names
            var chainMethods = CollectChainMethods(invocation);

            // Skip if chain already has AsNoTracking
            if (chainMethods.Any(m => m is "AsNoTracking" or "AsNoTrackingWithIdentityResolution"))
                continue;

            // Skip if chain has projection (Select)
            if (chainMethods.Contains("Select"))
                continue;

            // Skip if chain has mutation methods
            if (chainMethods.Any(MutationMethods.Contains))
                continue;

            // Check if chain starts from a DbSet<T> property
            var chainRoot = GetChainRoot(invocation);
            if (chainRoot is null)
                continue;

            var typeInfo = model.GetTypeInfo(chainRoot, ct);
            var typeName = typeInfo.Type?.ToDisplayString() ?? "";

            // Check for DbSet<T> or IQueryable<T> from EF Core
            if (!typeName.Contains("DbSet") && !IsEfCoreQueryable(typeInfo.Type))
                continue;

            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var snippet = invocation.ToString();
            if (snippet.Length > 80) snippet = snippet[..77] + "...";

            yield return new AntiPatternViolation(
                Id: "AP010",
                Severity: AntiPatternSeverity.Warning,
                Message: "EF Core read query without AsNoTracking() allocates unnecessary change-tracking state",
                File: filePath,
                Line: line,
                Snippet: snippet,
                Suggestion: "Add .AsNoTracking() before the terminal method for read-only queries");
        }
    }

    private static List<string> CollectChainMethods(ExpressionSyntax expression)
    {
        var methods = new List<string>();
        var current = expression;

        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax access)
            {
                methods.Add(access.Name.Identifier.Text);
                current = access.Expression;
            }
            else
            {
                break;
            }
        }

        return methods;
    }

    private static ExpressionSyntax? GetChainRoot(ExpressionSyntax expression)
    {
        var current = expression;

        while (current is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax access)
                current = access.Expression;
            else
                break;
        }

        return current;
    }

    private static bool IsEfCoreQueryable(ITypeSymbol? type)
    {
        if (type is null)
            return false;

        // Check if the type originates from Microsoft.EntityFrameworkCore
        return type.ContainingAssembly?.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true;
    }
}
