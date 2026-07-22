# Health Check — Grading Rubric Reference

Loaded by `/health-check` Step 2. This is the **canonical** grading system for
the kit — no other skill defines its own.

## Dimension Rubrics

### Dimension 1: Build Health

Tool: `dotnet build --no-restore` — metric: error count, warning count.

| Grade | Criteria |
|-------|----------|
| A | 0 errors, 0 warnings |
| B | 0 errors, 1-5 warnings |
| C | 0 errors, 6-15 warnings |
| D | 0 errors, 16-30 warnings |
| F | Any errors, or 30+ warnings |

### Dimension 2: Code Quality

Tool: MCP `detect_antipatterns(projectFilter: each project)` — metric: anti-patterns per 1K lines.

| Grade | Criteria |
|-------|----------|
| A | 0 anti-patterns |
| B | < 0.5 per 1K lines |
| C | 0.5 - 1.5 per 1K lines |
| D | 1.5 - 3.0 per 1K lines |
| F | > 3.0 per 1K lines |

Common findings: async void, sync-over-async, `new HttpClient()`, `DateTime.Now`,
broad catch blocks, string interpolation in logging, missing CancellationToken.

### Dimension 3: Architecture

Tools: MCP `get_project_graph` (dependency direction), `detect_circular_dependencies`
at both `scope: "projects"` and `scope: "types"`.

| Grade | Criteria |
|-------|----------|
| A | Correct dependency direction, 0 circular deps (project or type level) |
| B | Correct dependency direction, 1-2 type-level cycles (no project cycles) |
| C | 1-2 minor direction issues, or 3-5 type-level cycles |
| D | Project-level circular dependency, or significant layer violations |
| F | Multiple project-level cycles, no discernible architecture |

### Dimension 4: Test Coverage

Tool: MCP `get_test_coverage_map(projectFilter: each production project)` —
metric: % of production types with corresponding test classes.

| Grade | Criteria |
|-------|----------|
| A | 90%+ types have test classes |
| B | 75-89% |
| C | 50-74% |
| D | 25-49% |
| F | < 25% |

This is structural coverage (test class exists), not runtime line coverage. A
test class existing does not guarantee thorough testing, but its absence
guarantees none.

### Dimension 5: Dead Code

Tool: MCP `find_dead_code(scope: "solution", kind: "all", maxResults: 50)`.

| Grade | Criteria |
|-------|----------|
| A | 0-2 dead symbols |
| B | 3-8 |
| C | 9-15 |
| D | 16-25 |
| F | 25+ |

Some false positives are expected (reflection, DI conventions). Verify before
penalizing.

### Dimension 6: API Surface

Tools: MCP `get_public_api` (design review), `find_references` (overexposure).

| Grade | Criteria |
|-------|----------|
| A | Minimal public surface, proper return types, consistent naming |
| B | Mostly clean, 1-2 overexposed types |
| C | Several types expose internal details, inconsistent return types |
| D | Public APIs leak implementation, mixed return type patterns |
| F | No API design consideration, everything is public |

Check for: services that should be `internal`, methods returning `Task` instead
of `Task<Result<T>>` for fallible operations, mixed `TypedResults`/`IResult`,
public setters on types that should be immutable.

### Dimension 7: Security Posture

Tools: `dotnet list package --vulnerable --include-transitive`, MCP
`detect_antipatterns` (security patterns), scan for hardcoded secrets and
missing auth attributes.

| Grade | Criteria |
|-------|----------|
| A | 0 vulnerable packages, no hardcoded secrets, auth on all endpoints |
| B | 0 critical/high vulns, 1-2 low/medium vulns, clean auth |
| C | 1-2 medium vulns, or minor auth gaps |
| D | High-severity vuln, or missing auth on sensitive endpoints |
| F | Critical vuln, hardcoded secrets, or systemic auth gaps |

For a deep dive, run `/security-scan` — this dimension is a spot check.

### Dimension 8: Documentation

Scan: XML docs on public APIs; README existence and currency.

| Grade | Criteria |
|-------|----------|
| A | 90%+ public APIs have XML docs, README is comprehensive |
| B | 70-89% coverage, README covers basics |
| C | 50-69% coverage, README exists but sparse |
| D | < 50% coverage, minimal README |
| F | No XML docs, no README or severely outdated |

## GPA Calculation

Letter grades to points: A=4.0, B=3.0, C=2.0, D=1.0, F=0.0.
GPA = average across all 8 dimensions.

| GPA Range | Overall Assessment |
|-----------|--------------------|
| 3.5 - 4.0 | Excellent — production-ready, well-maintained |
| 3.0 - 3.4 | Good — solid foundation, minor improvements needed |
| 2.5 - 2.9 | Fair — functional but accumulating tech debt |
| 2.0 - 2.4 | Needs Work — significant improvements required |
| < 2.0 | Critical — major structural issues to address |

## Report Card Template

```markdown
## Project Health Report

**Project:** MyApp | **Date:** 2026-03-04 | **Assessed by:** Claude (MCP-assisted)

### Grades

| Dimension | Grade | Score | Key Finding |
|-----------|-------|-------|-------------|
| Build Health | A | 95 | 0 errors, 2 pre-existing warnings |
| Code Quality | B | 82 | 3 anti-patterns in 4.2K lines |
| Architecture | A | 92 | Clean dependency direction, 0 circular deps |
| Test Coverage | C | 68 | 34/50 production types have test classes |
| Dead Code | B | 85 | 5 unused methods identified |
| API Surface | B | 80 | 2 overexposed service types |
| Security | A | 94 | 0 vulnerable packages, auth coverage complete |
| Documentation | D | 55 | 12/30 public APIs have XML docs |

### Overall GPA: 3.0 (B-)

### Priority Recommendations

1. **Test Coverage (C -> B):** Add test classes for these 16 untested types:
   - `OrderService`, `PaymentProcessor`, `ShippingCalculator` (critical path)
   Estimated effort: 2-3 days

2. **Documentation (D -> C):** Add XML docs to the 8 endpoint classes first
   (user-facing), then the 10 public service interfaces.
   Estimated effort: 1 day

3. **Code Quality (B -> A):** Fix 3 anti-patterns:
   - `OrderService.cs:47` — `DateTime.Now` → `TimeProvider.GetUtcNow()`
   - `PaymentClient.cs:23` — `new HttpClient()` → `IHttpClientFactory`
   - `NotificationHandler.cs:12` — `async void` → `async Task`
   Estimated effort: 1 hour
```

Every recommendation must be specific (named types, file:line), prioritized,
and effort-estimated — "improve test coverage" is not actionable.

## Trend Tracking

If a previous health report exists, compare grades:

```markdown
### Trend

| Dimension | Previous | Current | Change |
|-----------|----------|---------|--------|
| Build Health | B | A | Improved — fixed 4 warnings |
| Code Quality | C | B | Improved — resolved 7 anti-patterns |
| Test Coverage | C | C | No change — still 68% |
```

Improving grades validate cleanup efforts; regressions flag where review
discipline slipped.
