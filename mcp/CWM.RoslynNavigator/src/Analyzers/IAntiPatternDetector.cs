using Microsoft.CodeAnalysis;

namespace CWM.RoslynNavigator.Analyzers;

/// <summary>
/// Severity levels for anti-pattern violations.
/// </summary>
internal enum AntiPatternSeverity
{
    Warning,
    Error
}

/// <summary>
/// A single anti-pattern violation found in source code.
/// </summary>
internal sealed record AntiPatternViolation(
    string Id,
    AntiPatternSeverity Severity,
    string Message,
    string File,
    int Line,
    string Snippet,
    string Suggestion);

/// <summary>
/// Detects specific .NET anti-patterns using Roslyn analysis.
/// Syntax detectors operate on <see cref="SyntaxTree"/> only (fast, no compilation needed).
/// Semantic detectors require a <see cref="SemanticModel"/> for type resolution.
/// </summary>
internal interface IAntiPatternDetector
{
    /// <summary>
    /// Whether this detector requires a <see cref="SemanticModel"/> (semantic) or only a <see cref="SyntaxTree"/> (syntax).
    /// </summary>
    bool RequiresSemanticModel { get; }

    /// <summary>
    /// Analyze a syntax tree for anti-pattern violations.
    /// </summary>
    /// <param name="tree">The syntax tree to analyze.</param>
    /// <param name="model">The semantic model, or null for syntax-only detectors.</param>
    /// <param name="ct">Cancellation token.</param>
    IEnumerable<AntiPatternViolation> Detect(SyntaxTree tree, SemanticModel? model, CancellationToken ct);
}
