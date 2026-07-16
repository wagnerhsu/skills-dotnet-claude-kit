using System.ComponentModel;
using System.Text.Json;
using CWM.RoslynNavigator.Analyzers;
using CWM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CWM.RoslynNavigator.Tools;

[McpServerToolType]
public static class DetectAntiPatternsTool
{
    private static readonly IAntiPatternDetector[] Detectors =
    [
        // Syntax detectors (fast, no compilation needed)
        new AsyncVoidDetector(),
        new SyncOverAsyncDetector(),
        new HttpClientInstantiationDetector(),
        new DateTimeDirectUseDetector(),
        new BroadCatchDetector(),
        new LoggingInterpolationDetector(),
        new PragmaWithoutRestoreDetector(),
        // Semantic detectors (require SemanticModel)
        new MissingCancellationTokenDetector(),
        new EfCoreNoTrackingDetector()
    ];

    [McpServerTool(Name = "detect_antipatterns"), Description("Detect .NET anti-patterns in source code using Roslyn analysis. Finds async void, sync-over-async, new HttpClient(), DateTime.Now, broad catch, logging interpolation, missing pragma restore, missing CancellationToken, and EF Core queries without AsNoTracking.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Filter to file (partial match on file path)")] string? file = null,
        [Description("Filter to project name")] string? projectFilter = null,
        [Description("Minimum severity: 'warning' (default) or 'error'")] string severity = "warning",
        [Description("Maximum results to return")] int maxResults = 100,
        CancellationToken ct = default)
    {
        var notReady = await workspace.EnsureReadyOrStatusAsync(ct);
        if (notReady is not null) return notReady;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new AntiPatternsResult([], 0, 0));

        var minSeverity = severity.Equals("error", StringComparison.OrdinalIgnoreCase)
            ? AntiPatternSeverity.Error
            : AntiPatternSeverity.Warning;

        var allViolations = new List<AntiPatternViolation>();

        var projects = GetFilteredProjects(solution, projectFilter);

        foreach (var project in projects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(project.Id, ct);
            if (compilation is null)
                continue;

            var trees = GetFilteredTrees(compilation, file);

            foreach (var tree in trees)
            {
                ct.ThrowIfCancellationRequested();

                SemanticModel? semanticModel = null;

                foreach (var detector in Detectors)
                {
                    if (detector.RequiresSemanticModel)
                    {
                        semanticModel ??= compilation.GetSemanticModel(tree);
                    }

                    var violations = detector.Detect(
                        tree,
                        detector.RequiresSemanticModel ? semanticModel : null,
                        ct);

                    foreach (var violation in violations)
                    {
                        if (violation.Severity >= minSeverity)
                            allViolations.Add(violation);
                    }
                }
            }
        }

        var totalFound = allViolations.Count;

        var result = allViolations
            .OrderByDescending(v => v.Severity)
            .ThenBy(v => v.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.Line)
            .Take(maxResults)
            .Select(v => new AntiPatternInfo(
                v.Id,
                v.Severity.ToString().ToLowerInvariant(),
                v.Message,
                workspace.ToRelativePath(v.File),
                v.Line,
                v.Snippet,
                v.Suggestion))
            .ToList();

        return JsonSerializer.Serialize(new AntiPatternsResult(result, result.Count, totalFound));
    }

    private static IEnumerable<Project> GetFilteredProjects(Solution solution, string? projectFilter)
    {
        if (projectFilter is null)
            return solution.Projects;

        return solution.Projects.Where(p =>
            p.Name.Equals(projectFilter, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<SyntaxTree> GetFilteredTrees(Compilation compilation, string? file)
    {
        if (file is null)
            return compilation.SyntaxTrees;

        return compilation.SyntaxTrees.Where(t =>
            t.FilePath?.Contains(file, StringComparison.OrdinalIgnoreCase) == true);
    }

}
