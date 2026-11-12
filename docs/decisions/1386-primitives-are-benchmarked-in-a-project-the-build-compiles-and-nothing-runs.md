# 1386. Primitives are benchmarked in a project the build compiles and nothing runs

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md), [project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md)

## Context

[project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md) rule 7
requires a benchmark to justify an optimization of a hot-path primitive, and
[ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)
holds the buffer and hot-path rules the primitives follow. The field, scalar and group arithmetic of
`NCode.Identity.Jose.EdDsa` is such a primitive ([#470](https://github.com/NCodeGroup/identity/issues/470)). Those types
are `internal`, and the public `EdDsaSignatureAlgorithm` cannot isolate one operation such as a field multiplication,
so a measurement needs a project that sees the internals without shipping them.

## Decision

1. **A package's benchmarks are a BenchmarkDotNet console project.** It is `benchmarks/<Package>.Benchmarks/`, listed
   in `NCode.Identity.slnx` under `/benchmarks/`. `benchmarks/Directory.Build.props` sets `IsPackable` to false, so it
   never packs. `NCode.Identity.Jose.EdDsa.Benchmarks` is such a project.

2. **The build compiles it and nothing runs it.** The solution build, and so the Definition of Done, compiles the
   project, so a primitive's signature change that breaks it fails the build. Nothing runs it, because `dotnet test`
   skips a project that is not a test project and `build/affected-tests.ps1` never maps `benchmarks/`.

3. **The measured package grants `InternalsVisibleTo` to its `.Benchmarks` project.** `AssemblyInfo.cs` of
   `NCode.Identity.Jose.EdDsa` carries the grant. A benchmark project is a non-shipping verification project like a test
   project, so the rule of [project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md)
   holds: production assemblies never see each other's internals.

4. **Every benchmark class has one shape.**
   - A class is `public`, unsealed (BenchmarkDotNet derives from it) and `[MemoryDiagnoser]`.
   - An input that a method takes by `in` or fills by `out` is a private field, because a ref target cannot be a
     property, which also exempts it from `NCID0001`.
   - A result goes to a private `Result` auto-property, because a public method cannot return an `internal` type.
   - Inputs come from a `Random` with a fixed seed.
   - `Main` returns `1` when a benchmark fails, so `--job Dry` is a smoke check.

5. **An optimization quotes its measurement.** A pull request that optimizes a primitive quotes the before and after
   tables of
   `dotnet run -c Release --project benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks -- --filter '*<Class>*'` from the
   same machine. The numbers come from a developer machine, never from CI.

## Options considered

- **Benchmarks in the `.Tests` project.** Rejected: they would run under the test host in a Debug build, which
  BenchmarkDotNet refuses.
- **Linking the primitive sources into the benchmark project.** Rejected: it measures a second compilation that can
  drift from the shipped one.
- **Measuring only through `EdDsaSignatureAlgorithm`.** Rejected: it cannot isolate a field or group operation.
- **Running the benchmarks in CI.** Rejected: shared runners give noisy numbers that prove nothing, at minutes per run.
- **A separate benchmark project the build compiles and nothing runs.** Chosen: it sees the internals through
  `InternalsVisibleTo`, measures the shipped compilation, and stays current because the build compiles it.

## Consequences

- The benchmark project breaks the build when a primitive's signature changes, so it is kept current.
- Benchmark results are evidence a developer produces on one machine for one pull request, never a gate.
- The benchmark project never packs and grants production assemblies no access to each other's internals.

## References

- [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md),
  [project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md),
  [project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md)
- Issue [#470](https://github.com/NCodeGroup/identity/issues/470)
- `benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks/`, `benchmarks/Directory.Build.props` and `AssemblyInfo.cs` in
  `NCode.Identity.Jose.EdDsa`
