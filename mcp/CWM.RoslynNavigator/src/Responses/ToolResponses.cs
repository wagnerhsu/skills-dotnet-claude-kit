namespace CWM.RoslynNavigator.Responses;

// Token-optimized response records for MCP tools.
// All responses use minimal property names and avoid unnecessary nesting.
// List-returning tools follow a shared truncation contract: Count is the number of
// entries returned, TotalFound the number that matched — re-query with a higher
// maxResults when TotalFound exceeds Count.
// Note: intentionally many records in one file — these are pure DTOs and a file per
// two-line record would hurt navigability more than it helps.

internal sealed record SymbolLocation(string Name, string Kind, string File, int Line, string Namespace);

internal sealed record SymbolSearchResult(List<SymbolLocation> Symbols, int Count, int TotalFound);

internal sealed record ReferenceLocation(string File, int Line, string Snippet, string Kind);

internal sealed record ReferencesResult(List<ReferenceLocation> References, int Count, int TotalFound);

internal sealed record ImplementationInfo(string Type, string File, int Line);

internal sealed record ImplementationsResult(List<ImplementationInfo> Implementations, int Count, int TotalFound);

internal sealed record TypeHierarchyResult(
    List<string> BaseTypes,
    List<string> Interfaces,
    List<string> DerivedTypes,
    int TotalDerived);

internal sealed record ProjectInfo(
    string Name,
    string Path,
    string TargetFramework,
    List<string> References);

internal sealed record ProjectGraphResult(string Solution, List<ProjectInfo> Projects);

internal sealed record MemberInfo(string Kind, string Signature, string Accessibility);

internal sealed record PublicApiResult(string Type, List<MemberInfo> Members, int Count, int TotalFound);

internal sealed record DiagnosticInfo(string Id, string Severity, string Message, string File, int Line);

internal sealed record DiagnosticsResult(
    List<DiagnosticInfo> Diagnostics,
    int Count,
    int TotalFound,
    int Errors,
    int Warnings,
    int Info);

internal sealed record StatusResponse(string State, string Message);

internal sealed record CallerInfo(string Method, string ContainingType, string File, int Line);

internal sealed record CallersResult(List<CallerInfo> Callers, int Count, int TotalFound);

internal sealed record OverrideInfo(string Method, string ContainingType, string File, int Line);

internal sealed record OverridesResult(List<OverrideInfo> Overrides, int Count, int TotalFound);

internal sealed record ParameterDetail(string Name, string Type, string? DefaultValue);

internal sealed record SymbolDetail(
    string Name,
    string Kind,
    string Signature,
    string? ReturnType,
    string Accessibility,
    string? Namespace,
    List<ParameterDetail>? Parameters,
    string? XmlDoc,
    string File,
    int Line);

internal sealed record AntiPatternInfo(
    string Id,
    string Severity,
    string Message,
    string File,
    int Line,
    string Snippet,
    string Suggestion);

internal sealed record AntiPatternsResult(List<AntiPatternInfo> Violations, int Count, int TotalFound);

// Dead code detection
internal sealed record DeadCodeInfo(string Name, string Kind, string File, int Line, string? ContainingType);

internal sealed record DeadCodeResult(List<DeadCodeInfo> Symbols, int Count, int TotalFound);

// Circular dependency detection
internal sealed record CircularDependencyChain(List<string> Chain, string Level);

internal sealed record CircularDependenciesResult(List<CircularDependencyChain> Cycles, int Count);

// Dependency graph
internal sealed record DependencyNode(string Symbol, string ContainingType, string File, int Line, int Depth);

internal sealed record DependencyGraphResult(
    string RootSymbol,
    List<DependencyNode> Dependencies,
    int TotalNodes,
    bool Truncated);

// Test coverage map
internal sealed record TestCoverageEntry(string Type, string File, bool HasTests, string? TestFile);

internal sealed record TestCoverageMapResult(List<TestCoverageEntry> Coverage, int TotalTypes, int TestedTypes, int Percentage);

// Symbol source
internal sealed record SymbolSourceResult(
    string Name,
    string Kind,
    string File,
    int StartLine,
    int EndLine,
    string Source,
    bool Truncated);

// File outline
internal sealed record MemberOutline(string Kind, string Signature, int Line);

internal sealed record TypeOutline(
    string Name,
    string Kind,
    int Line,
    List<MemberOutline> Members,
    List<TypeOutline> NestedTypes);

internal sealed record FileOutlineResult(
    string File,
    string? Namespace,
    int UsingCount,
    List<TypeOutline> Types,
    int Count,
    int TotalFound);

// NuGet packages
internal sealed record PackageRef(string Id, string? Version);

internal sealed record ProjectPackages(string Name, string TargetFramework, bool Cpm, List<PackageRef> Packages);

internal sealed record NugetPackagesResult(List<ProjectPackages> Projects, int Count, int TotalFound);

// Endpoint map
internal sealed record EndpointEntry(string Method, string Route, string Auth, string Kind, string File, int Line);

internal sealed record EndpointMapResult(List<EndpointEntry> Endpoints, int Count, int TotalFound);

// DI registrations
internal sealed record DiRegistration(
    string Service,
    string Implementation,
    string Lifetime,
    bool Keyed,
    bool TryAdd,
    string File,
    int Line);

internal sealed record DiDuplicate(string Service, int Count);

internal sealed record DiCaptiveRisk(string Service, string Implementation, string DependsOn, string File, int Line);

internal sealed record DiRegistrationsResult(
    List<DiRegistration> Registrations,
    List<DiDuplicate> Duplicates,
    List<DiCaptiveRisk> CaptiveRisks,
    int Count,
    int TotalFound);
