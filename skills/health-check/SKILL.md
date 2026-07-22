---
name: health-check
description: >
  Multi-dimensional health assessment for .NET projects with letter grades (A-F)
  using Roslyn MCP tools. Evaluates 8 dimensions: build health, code quality,
  architecture, test coverage, dead code, API surface, security posture, and
  documentation. Produces a structured report card with actionable recommendations.
  Load this skill when: "health check", "how healthy is this", "project health",
  "code quality report", "grade this project", "assess codebase", "quality audit",
  "technical assessment", "codebase review", "report card".
---

# /health-check — 8-Dimension Project Assessment

## What

Runs a data-driven health assessment across 8 dimensions, each graded A-F with
the specific data points that produced the grade, and rolls them into a GPA.
Gut feeling is not a grade: every dimension uses MCP tools or CLI commands, and
every grade below A comes with specific, prioritized, effort-estimated fixes —
"add test classes for OrderService, PaymentProcessor, ShippingCalculator" is
actionable; "improve test coverage" is not.

This skill owns the **canonical grading system** for the kit. The full rubrics,
GPA scale, and report template live in `references/grading-rubric.md` — load
that file when running an assessment.

Tone is diagnostic, not punitive: a C grade is an improvement path, not a
failure.

## When

- Onboarding to an unfamiliar or new project — set the baseline
- "How healthy is this?", "grade this project", "codebase review", "report card"
- Pre-release quality gate, or monthly/quarterly maintenance review
- After a cleanup sprint (`/de-sloppify`) — re-grade to show progress
- Tech-debt prioritization — lowest grades get the next sprint's attention

## How

### Step 1: Choose Scope

| Scenario | Dimensions |
|----------|------------|
| Full assessment (onboarding, pre-release, monthly review) | All 8 |
| Quick health (mid-sprint checkpoint, before a demo, after a merge) | 1-4 only |
| After major refactor | 1 (Build), 3 (Architecture), 4 (Tests) |
| Post-dependency update | 1 (Build), 7 (Security) |
| After cleanup sprint | Re-grade only the cleaned dimensions |

### Step 2: Run the Dimensions

Read `references/grading-rubric.md` for the grade thresholds, then collect data
per dimension. For deep code-quality dimensions, delegate to the
`code-reviewer` agent with the `code-review` skill.

| # | Dimension | Data source |
|---|-----------|-------------|
| 1 | Build Health | `dotnet build --no-restore` — errors + warnings |
| 2 | Code Quality | MCP `detect_antipatterns` per project — findings per 1K lines |
| 3 | Architecture | MCP `get_project_graph` + `detect_circular_dependencies` (projects AND types) |
| 4 | Test Coverage | MCP `get_test_coverage_map` — % of types with test classes (structural, not line coverage) |
| 5 | Dead Code | MCP `find_dead_code(scope: "solution")` — verify false positives before penalizing |
| 6 | API Surface | MCP `get_public_api` + `find_references` — overexposure, return-type consistency |
| 7 | Security Posture | `dotnet list package --vulnerable --include-transitive` + secrets/auth spot check (deep dive: `/security-scan`) |
| 8 | Documentation | XML doc coverage on public APIs + README currency |

### Step 3: Grade and Aggregate

Apply the rubric thresholds exactly — never grade on a curve ("pretty good for
a project this size" is how standards erode; 15 warnings is a C regardless of
project size). Convert to GPA (A=4.0 … F=0.0, averaged across dimensions run).

### Step 4: Report

Produce the report card from the template in `references/grading-rubric.md`:
grades table with key findings, overall GPA, and priority recommendations —
each with named types/files, priority order, and effort estimates. If a
previous report exists, append the trend comparison table.

## Example

```
User: /health-check

Claude: Running full 8-dimension assessment...

| Dimension | Grade | Key Finding |
|-----------|-------|-------------|
| Build Health | A | 0 errors, 2 warnings |
| Code Quality | B | 3 anti-patterns in 4.2K lines (0.71/1K) |
| Architecture | A | Clean direction, 0 cycles |
| Test Coverage | C | 34/50 types have test classes (68%) |
| Dead Code | B | 5 unused methods |
| API Surface | B | 2 overexposed service types |
| Security | A | 0 vulnerable packages |
| Documentation | D | 12/30 public APIs documented |

Overall GPA: 3.0 (B-) — Good: solid foundation, minor improvements needed.

Priority: (1) test classes for OrderService, PaymentProcessor,
ShippingCalculator — critical path, ~2 days; (2) XML docs on the 8 endpoint
classes, ~1 day; (3) 3 anti-pattern fixes, ~1 hour.
```

## Related

- `references/grading-rubric.md` — canonical rubrics, GPA scale, report template
- `/de-sloppify` — cleanup pipeline for the issues a health check surfaces
- `/security-scan` — deep 6-layer scan behind Dimension 7
- `/code-review` — per-change review (this skill grades the whole project)
- `/verify` — pass/fail pipeline for a change set, not a graded assessment
