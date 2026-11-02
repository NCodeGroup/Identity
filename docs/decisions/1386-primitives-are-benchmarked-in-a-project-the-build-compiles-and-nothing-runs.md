# 1386. Primitives are benchmarked in a project the build compiles and nothing runs

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md), [project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md), [ADR-1116](1116-memory-allocations-and-buffer-operations.md)

## Context

[ADR-1116](1116-memory-allocations-and-buffer-operations.md) decision 4 and
[project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md) rule 7
require a benchmark to justify an optimization of a hot-path primitive, such as the field, scalar and group arithmetic
of `NCode.Identity.Jose.EdDsa`, and there was none ([#470](https://github.com/NCodeGroup/Identity/issues/470)). The
primitives are `internal`, and the public `EdDsaSignatureAlgorithm` cannot isolate one operation such as a field
multiplication.

## Decision

1. **A package's benchmarks are a BenchmarkDotNet console project.** It is `benchmarks/<Package>.Benchmarks/` with
   `IsPackable=false`, listed in `NCode.Identity.slnx` under `/benchmarks/`. The solution build, and so the Definition
   of Done, compiles it; nothing runs it, because `dotnet test` skips a project that is not a test project and
   `build/affected-tests.ps1` maps only `src/` and `tests/`. The first is `NCode.Identity.Jose.EdDsa.Benchmarks`, with
   BenchmarkDotNet 0.15.8.

2. **The measured package grants `InternalsVisibleTo` to its `.Benchmarks` project.** A benchmark project is a
   non-shipping verification project like a test project, so the rule of
   [project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md) is unchanged: production
   assemblies never see each other's internals.

3. **Every benchmark class has one shape.** A class is `public`, unsealed (BenchmarkDotNet derives from it) and
   `[MemoryDiagnoser]`. An input that a method takes by `in` or fills by `out` is a private field, because a ref target
   cannot be a property. A result goes to a private `Result` auto-property, because a public method cannot return an
   `internal` type. Inputs come from a `Random` with a fixed seed. `Main` returns `1` when a benchmark fails, so
   `--job Dry` is a smoke check.

4. **An optimization quotes its measurement.** A pull request that optimizes a primitive quotes the before and after
   tables of `dotnet run -c Release --project benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks -- --filter '*<Class>*'`
   from the same machine.

## Options considered

- **Benchmarks in the `.Tests` project.** Rejected: they would run under the test host in a Debug build, which
  BenchmarkDotNet refuses.
- **Linking the primitive sources into the benchmark project.** Rejected: it measures a second compilation that can
  drift from the shipped one.
- **Measuring only through `EdDsaSignatureAlgorithm`.** Rejected: it cannot isolate a field or group operation.
- **Running the benchmarks in CI.** Rejected: shared runners give noisy numbers that prove nothing, at minutes per run.

## Consequences

- The benchmark project breaks the build when a primitive's signature changes, so it is kept current.
- The numbers come from a developer machine, never from CI.

## References

- [ADR-1116](1116-memory-allocations-and-buffer-operations.md),
  [project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md),
  [project-template ADR-0112](project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md)
- Issue [#470](https://github.com/NCodeGroup/Identity/issues/470)
- `benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks/` and `AssemblyInfo.cs` in `NCode.Identity.Jose.EdDsa`
