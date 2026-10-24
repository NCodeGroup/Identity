# 185. Coverage measures hand-written code and counts a line that runs as covered

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The Definition of Done collects .NET coverage with coverlet and merges it with ReportGenerator into one Cobertura
report. The job summary shows that report, and Codecov ingests the same file
([ADR-0143](0143-ci-presents-test-results-and-coverage-in-github.md)). Coverlet measures every source file in every
assembly a test loads, which includes code nobody writes by hand:

- source-generator output under `obj/`, such as the OpenAPI XML-comment support and `GeneratedRegex` implementations;
- EF Core migrations under `NCode.Identity.OpenId.Host.Database/Migrations/`, which `dotnet ef` scaffolds.

The generated files are build intermediates, not repository files. ReportGenerator warns that they do not exist, and
Codecov drops them silently, so the two figures disagree about which lines count. They also disagree about how a line
counts. ReportGenerator counts a line whose branches are only partly taken as covered, while Codecov counts it as a
miss by default. On the first CI run the job summary reported 35.8% over 75,871 coverable lines, and Codecov reported
52.8% over about 49,600.

## Decision

**The headline coverage figure measures hand-written code, and a line that runs counts as covered, in the job summary
and in Codecov alike.**

1. **Generated code is excluded where coverage is collected.** [`build/coverage.runsettings`](../../build/coverage.runsettings)
   configures coverlet to skip files under `obj/` and under `Migrations/`, and members marked `[GeneratedCode]`.
   Coverlet already skips `[ExcludeFromCodeCoverage]`. `build/dod.ps1` passes the file whenever it collects coverage.
   Both presenters read the reports it produces, so one setting scopes both.
2. **Hand-written code stays in scope wherever it lives.** The reference host and the host database project ship in
   the image that self-hosters and the hosted platform run
   ([ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md)), so their code counts. A low figure for the host
   reports a real gap. Assemblies that no test loads, such as the Aspire orchestrator, never enter the report.
3. **Codecov counts a partially covered line as a hit.** `codecov.yml` sets `parsers.cobertura.partials_as_hits`, which
   matches ReportGenerator's line coverage. Branch coverage stays in the report for the partial lines. The setting
   applies to the frontend upload too, which is also a ReportGenerator Cobertura file.

Hand-written code in a project that also holds scaffolded migrations stays outside `Migrations/`
([ADR-0184](0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md)).

## Options considered

- **Exclude the host assemblies from the headline.** This would raise the number by leaving out
  `NCode.Identity.OpenId.Host`. Rejected: the host is shipped code, so leaving it out hides a gap that matters.
- **Filter in ReportGenerator and in `codecov.yml` `ignore`.** Two filter lists in two syntaxes must be kept equal.
  Rejected: excluding at collection gives both presenters the same input from one list.
- **Exclude `[CompilerGenerated]` members.** Rejected: the compiler marks async state machines, lambdas, and iterator
  classes with it, so excluding it would drop the bodies of hand-written async methods and lambdas.

## Consequences

- The headline figure rises, because migrations and generator output leave the denominator. The first full run under
  this scope reports 83.6% line coverage over 38,091 coverable lines, with the reference host at 79.1%, and the merged
  report names no file under `obj/` or `Migrations/`. The two presenters now report the same scope.
- A test that runs migrations, such as `ExpandOnlyRuleTests`, no longer adds their scaffolded lines to the figure.
- `./build/dod.ps1 -Fast` collects no coverage, so the setting affects only the full gate that CI runs.
- A coverage threshold remains a separate policy decision.

## References

- [ADR-0143](0143-ci-presents-test-results-and-coverage-in-github.md): CI presents test results and coverage in GitHub.
- [`build/coverage.runsettings`](../../build/coverage.runsettings), [`build/dod.ps1`](../../build/dod.ps1), and
  [`codecov.yml`](../../codecov.yml).
- coverlet's `ExcludeByFile` and `ExcludeByAttribute` collector settings.
