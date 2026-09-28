---
description: "Audit the NCode.Identity codebase against its coding-convention instructions — sweep for violations, run the Definition of Done, verify tested-or-excluded coverage, and produce a ranked findings report. Use for a full conventions/compliance review."
name: "Audit conventions"
agent: "agent"
---

Run a full **convention audit** of this repository against its authoritative rules — the scoped instruction files
[`csharp-production.instructions.md`](../instructions/csharp-production.instructions.md) and
[`csharp-testing.instructions.md`](../instructions/csharp-testing.instructions.md), mirrored for orientation in
[AGENTS.md](../../AGENTS.md). Produce a **ranked findings report**. Do **not** change code unless I explicitly ask —
audit first, offer fixes second.

## 1. Load the rules

Read the [production](../instructions/csharp-production.instructions.md) and
[testing](../instructions/csharp-testing.instructions.md) instruction files in full. They are authoritative. Every
rule is tagged ✅ tool-enforced / 👁 review-only / ⏳ rolling out. The ✅ subset is validated by the build, so spend
your manual effort on the 👁 rules.

## 2. Confirm the machine-enforced subset is green

Run the Definition of Done — it covers CSharpier, the analyzers, banned APIs + sync-over-async, and `CA1708`:

```pwsh
./build/dod.ps1
```

Then verify **tested-or-excluded** (§7): every non-`[ExcludeFromCodeCoverage]` type should be exercised by tests.

```pwsh
Remove-Item -Recurse -Force ./TestResults -ErrorAction SilentlyContinue
dotnet test NCode.Identity.slnx -c Debug --collect:"XPlat Code Coverage" --results-directory ./TestResults --nologo | Out-Null
$seen = @{}
Get-ChildItem ./TestResults -Recurse -Filter coverage.cobertura.xml | ForEach-Object {
    [xml]$x = Get-Content $_.FullName
    foreach ($c in $x.coverage.packages.package.classes.class) {
        $n = $c.name; $lr = [double]$c.'line-rate'
        if (-not $seen.ContainsKey($n) -or $lr -gt $seen[$n]) { $seen[$n] = $lr }
    }
}
$under = $seen.GetEnumerator() | Where-Object { $_.Value -lt 1.0 }
"classes: $($seen.Count); below 100%: $(@($under).Count)"
$under | Sort-Object Value, Name | ForEach-Object { "{0,6:P0}  {1}" -f $_.Value, $_.Key }
```

Any class that is **neither** tested **nor** annotated `[ExcludeFromCodeCoverage]` is a violation. (Coverlet reports
async bodies as separate `<...>d__N` entries, and only assemblies referenced by a test project appear — a package with
no `.Tests` project is invisible, which is itself a finding.)

## 3. Sweep production `.cs` for the review-only rules

Scan production code only (test projects are exempt from several rules). Check each rule set:

- **§1 language/async** — every _own_ async method returns `ValueTask`/`ValueTask<T>`, has an `Async` suffix, and takes
  `CancellationToken` **last**. `Task` is allowed only at framework boundaries (middleware `InvokeAsync`, minimal-API
  handlers, mediator handlers). Collection expressions, `const` for literals, and `u8` for UTF-8 bytes. Fields only for
  `ref`/`Interlocked`/`fixed`/`stackalloc`; everything else an auto-property. Constants/statics at the top.
- **§2 public API** — enums only for closed vocabularies (extensible ones are `public const string`); no secret material
  on the surface; no behavior-selecting `bool`/`enum` flag arguments; data/options/event types are `sealed` with
  concrete collections; **no cross-assembly `InternalsVisibleTo`** (only `*Tests` + `DynamicProxyGenAssembly2`).
- **§3 organization** — one type per file; **no nested types** except a private type closing over the enclosing generic
  parameter or a nested `static` constant-vocabulary class; the single canonical impl of `IFoo` is `DefaultFoo`;
  namespaces flat per package.
- **§6 clean code** — small single-purpose methods, ≤3 params, Command-Query Separation, no empty `catch`, no
  `async void`, intention-revealing names, no commented-out code, no repeated magic strings.
- **§7 testability** — **no captured request/ambient context in stored state** (a read-over-ambient type is a live view
  over `IHttpContextAccessor`); service-location only in the sanctioned singleton→scoped case; privates covered
  transitively; tested-or-excluded.

Fast mechanical checks (grep production `.cs`, adapt as needed):

- **Nested types**: a `class`/`struct`/`interface`/`enum`/`record` declaration indented inside another type.
- `enum ` on a public surface · `IList<` returns · `bool ` parameters that switch behavior · `async void` · empty
  `catch` · `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` (banned) · `Newtonsoft` (banned).
- **Primary-constructor candidates** (§1): grep `^\s+(public|internal|protected)\s+[A-Z]\w*\(` — one identifier
  immediately before `(` is a **constructor** (a method has `type name(`). Any type whose single ctor body is only
  `X = x;` assignments (or param-derived initializers) should be a primary constructor — **including** an `abstract`
  class with a `protected` pure-assignment ctor. Non-candidates: multiple ctors / `this()` delegation, a body with a
  guard/loop/branch or side-effecting call, a shared intermediate local used by ≥2 members, exceptions, records.
- **Unbuffered stored collections** (§2): a ctor/method that assigns a collection parameter (or DI `IEnumerable<T>`)
  straight to a field/property with no `.ToArray()` / `.ToFrozenSet()` / `.ToFrozenDictionary()` / `[.. x]`.
- **Before sealing, confirm it's a leaf** (§2): grep `:\s*TypeName\b` for real inheritance — a `TypeName? Bar { get; }`
  **property** is composition (has-a), not a base class, and is still sealable.
- `RequestServices` / `GetService` / `GetServices` — confirm each site is the sanctioned singleton service-location
  exception or `[ExcludeFromCodeCoverage]` framework glue.

## 4. Report

Deliver a **ranked** report — 🔴 violation / 🟡 worth fixing / 🟢 minor or conscious trade-off — each with the file,
the rule it touches, and a one-line fix. Affirm what is clean; if the codebase is compliant, say so plainly and don't
manufacture nitpicks. **Offer** to implement fixes; only edit code if I say yes.
