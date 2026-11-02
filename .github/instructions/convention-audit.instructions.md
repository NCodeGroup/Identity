---
description: "NCode.Identity's own checks for /audit-conventions: the tested-or-excluded coverage sweep and the review-only sweeps of production code. Not applied automatically; the audit reads it because AGENTS.md lists it."
---

# Convention audit: NCode.Identity's checks

`/audit-conventions`, from the project template, loads `AGENTS.md`, every instruction file it lists and the decision
records, runs the gate, sweeps the review-only rules and ranks the findings. These are this repository's sweeps for it.
The general C# rules are the project template's [csharp.instructions.md](csharp.instructions.md) and
[csharp-tests.instructions.md](csharp-tests.instructions.md); this repository's own are in
[csharp-production.instructions.md](csharp-production.instructions.md) and
[csharp-testing.instructions.md](csharp-testing.instructions.md), where every rule, a pointer to a template rule
included, is tagged ✅ tool-enforced, 👁 review-only or ⏳ rolling out: spend the manual effort on the 👁 rules.

## Tested-or-excluded coverage

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

## Review-only sweeps of production code

Scan production code only (test projects are exempt from several rules). Check each rule set:

- **§1 language/async** — every _own_ async method returns `ValueTask`/`ValueTask<T>`, has an `Async` suffix, and takes
  `CancellationToken` **last**. `Task` is allowed only at framework boundaries (middleware `InvokeAsync`, minimal-API
  handlers, mediator handlers). Collection expressions, `const` for literals, and `u8` for UTF-8 bytes. Fields only for
  `ref`/`Interlocked`/`fixed`/`stackalloc`; everything else an auto-property. Constants/statics at the top.
- **§2 public API** — vocabularies are **open by default** (ADR-1150): every `enum` must pass the three-part closed test
  (members fixed outside the package's control or by its own operation contract; no other package could need a member;
  a new member would need a library change anyway), and an open vocabulary is `public const string` names plus
  registered descriptors/strategies — never `[Flags]`, never a `switch`/mask encoding relationships among members; no
  secret material on the surface; no behavior-selecting `bool`/`enum` flag arguments; data/options/event types are
  `sealed` with concrete collections; **no cross-assembly `InternalsVisibleTo`** (only `*Tests`, `*.Benchmarks` + `DynamicProxyGenAssembly2`).
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
- `enum ` on a public surface (apply the ADR-1150 closed test to each) · `[Flags]` · a `switch` over a vocabulary's
  members that encodes their relationships · `IList<` returns · `bool ` parameters that switch behavior · `async void` · empty
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

**Exhaustive sweeps are deterministic.** A claim that a whole class of defect is absent (for example "no get-only
property's summary says _Gets or sets_") comes from a script or grep that lists every candidate with `file:line`, never
from an exploration subagent — those under-report and will answer "0 / clean". PowerShell `-match` is case-insensitive;
use `-cmatch` for casing checks (a summary that starts lower-case, "a id" vs "an ID"). The documentation defects the
compiler reports as errors — `CS1570` (malformed XML), `CS1572` / `CS1573` (a wrong or missing `<param>`), `CS1574` (a
bad `cref`) — are absent once the Definition of Done passes, so spend manual effort on the review-only classes: a
_Gets or sets_ summary that disagrees with its accessor, terse or copy-pasted summaries, and grammar. To compare a
summary with its accessor, read past the `///` and attribute lines to the declaration, and classify `{ get; }`,
`{ get; init; }`, and `=>` as read-only and `set;` as mutable.
