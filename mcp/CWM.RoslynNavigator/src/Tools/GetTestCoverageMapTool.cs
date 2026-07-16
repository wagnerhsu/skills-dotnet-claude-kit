using System.ComponentModel;
using System.Text.Json;
using CWM.RoslynNavigator.Responses;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace CWM.RoslynNavigator.Tools;

[McpServerToolType]
public static class GetTestCoverageMapTool
{
    private static readonly string[] TestFrameworkAssemblies =
    [
        "xunit.v3.core",
        "xunit.core",
        "nunit.framework",
        "Microsoft.VisualStudio.TestPlatform.TestFramework"
    ];

    [McpServerTool(Name = "get_test_coverage_map"), Description("Heuristic test coverage map: identifies which production types have corresponding test classes. Matches by naming convention (e.g., OrderService → OrderServiceTests). Not runtime coverage — structural analysis only.")]
    public static async Task<string> ExecuteAsync(
        WorkspaceManager workspace,
        [Description("Optional: production project name to check coverage for")] string? projectFilter = null,
        [Description("Maximum results to return")] int maxResults = 50,
        CancellationToken ct = default)
    {
        var notReady = await workspace.EnsureReadyOrStatusAsync(ct);
        if (notReady is not null) return notReady;

        var solution = workspace.GetSolution();
        if (solution is null)
            return JsonSerializer.Serialize(new TestCoverageMapResult([], 0, 0, 0));

        // Identify test projects vs production projects
        var testProjects = new List<Project>();
        var productionProjects = new List<Project>();

        foreach (var project in solution.Projects)
        {
            if (await IsTestProjectAsync(workspace, project, ct))
                testProjects.Add(project);
            else
                productionProjects.Add(project);
        }

        // Filter production projects if specified
        if (projectFilter is not null)
        {
            productionProjects = productionProjects
                .Where(p => p.Name.Equals(projectFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Collect all test type names from test projects
        var testTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var testTypeFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var testProject in testProjects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(testProject.Id, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                foreach (var node in root.DescendantNodes())
                {
                    var symbol = semanticModel.GetDeclaredSymbol(node, ct);
                    if (symbol is not INamedTypeSymbol typeSymbol) continue;
                    if (typeSymbol.TypeKind != TypeKind.Class) continue;

                    testTypeNames.Add(typeSymbol.Name);
                    var location = SymbolResolver.GetLocation(typeSymbol);
                    if (location.HasValue)
                        testTypeFiles.TryAdd(typeSymbol.Name, workspace.ToRelativePath(location.Value.File));
                }
            }
        }

        // Check each production type for matching test class
        var coverage = new List<TestCoverageEntry>();
        var totalTypes = 0;
        var testedTypes = 0;

        foreach (var project in productionProjects)
        {
            ct.ThrowIfCancellationRequested();

            var compilation = await workspace.GetCompilationAsync(project.Id, ct);
            if (compilation is null) continue;

            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                foreach (var node in root.DescendantNodes())
                {
                    var symbol = semanticModel.GetDeclaredSymbol(node, ct);
                    if (symbol is not INamedTypeSymbol typeSymbol) continue;
                    if (typeSymbol.TypeKind is not (TypeKind.Class or TypeKind.Struct)) continue;
                    if (typeSymbol.IsAbstract && typeSymbol.TypeKind == TypeKind.Class
                        && typeSymbol.GetMembers().All(m => m.IsAbstract || m.IsImplicitlyDeclared)) continue;

                    totalTypes++;

                    var typeName = typeSymbol.Name;
                    var (hasTests, testFile) = FindMatchingTestClass(typeName, testTypeNames, testTypeFiles);

                    if (hasTests)
                        testedTypes++;

                    if (coverage.Count < maxResults)
                    {
                        var location = SymbolResolver.GetLocation(typeSymbol);
                        coverage.Add(new TestCoverageEntry(
                            Type: typeName,
                            File: location.HasValue ? workspace.ToRelativePath(location.Value.File) : "unknown",
                            HasTests: hasTests,
                            TestFile: testFile));
                    }
                }
            }
        }

        var percentage = totalTypes > 0 ? (testedTypes * 100) / totalTypes : 0;
        return JsonSerializer.Serialize(new TestCoverageMapResult(coverage, totalTypes, testedTypes, percentage));
    }

    private static async Task<bool> IsTestProjectAsync(WorkspaceManager workspace, Project project, CancellationToken ct)
    {
        // Check by name convention
        if (project.Name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
            project.Name.EndsWith("Test", StringComparison.OrdinalIgnoreCase) ||
            project.Name.Contains(".Tests.", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for test framework references
        var compilation = await workspace.GetCompilationAsync(project.Id, ct);
        if (compilation is null) return false;

        foreach (var reference in compilation.ReferencedAssemblyNames)
        {
            foreach (var testAssembly in TestFrameworkAssemblies)
            {
                if (reference.Name.Equals(testAssembly, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static (bool HasTests, string? TestFile) FindMatchingTestClass(
        string typeName,
        HashSet<string> testTypeNames,
        Dictionary<string, string> testTypeFiles)
    {
        // Try common test naming conventions
        string[] testNamePatterns =
        [
            $"{typeName}Tests",
            $"{typeName}Test",
            $"{typeName}_Tests",
            $"{typeName}Specs",
            $"{typeName}Spec"
        ];

        foreach (var pattern in testNamePatterns)
        {
            if (testTypeNames.Contains(pattern))
            {
                testTypeFiles.TryGetValue(pattern, out var testFile);
                return (true, testFile);
            }
        }

        return (false, null);
    }

}
