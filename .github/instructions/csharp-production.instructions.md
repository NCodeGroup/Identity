---
description: "NCode.Identity production C# conventions — async, public-API discipline, file/code organization, extension methods, build gates, clean code, testability, and provenance."
applyTo: "src/**/*.cs"
---

# NCode.Identity coding conventions (production)

The **authoritative source** for production coding standards in this repository, mirrored for orientation in
[`AGENTS.md`](../../AGENTS.md). Test-project rules live in
[`csharp-testing.instructions.md`](csharp-testing.instructions.md). The `.editorconfig`, CSharpier, and analyzers
enforce the machine-checkable subset. When a convention changes, change it **here** first. The _reasoning_ behind a
non-obvious rule or seam lives in an ADR under [`docs/adr/`](../../docs/adr/) — link to it rather than inlining the
rationale.

**Legend** — how each rule is enforced:

- ✅ **Tool-enforced** — the build / [`build/dod.ps1`](../../build/dod.ps1) fails if broken (analyzer, CSharpier, or compiler).
- 👁 **Review-only** — not yet machine-checkable; confirmed in code review.
- ⏳ **Rolling out** — the tooling is wired but the existing surface is still being brought into compliance; treat as 👁 today.

---

## 1. Language & async

- 👁 Use `ValueTask` / `ValueTask<T>` for async methods (not `Task`), with an `Async` suffix, and a
  `CancellationToken` as the **last** parameter. `Task` is allowed only at framework boundaries the library does not
  own — an ASP.NET Core middleware `InvokeAsync`, a minimal-API endpoint handler, or a mediator handler whose contract
  returns `Task`.
- ✅ **No sync-over-async.** Blocking on a `Task`/`ValueTask` — `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`,
  `Task.WaitAll` / `Task.WaitAny` — is banned by `BannedApiAnalyzers` (`RS0030`, see
  [`BannedSymbols.txt`](../../BannedSymbols.txt)). Always `await`.
- 👁 **Fire-and-forget offloads use `Task.Run`, never `Task.Factory.StartNew` with an `async` delegate.** `StartNew`
  over an `async` lambda returns an unobserved `Task<Task>` and drops the inner task's exceptions; `Task.Run` unwraps it.
  Prefer not offloading at all — offload only when the work must leave the calling thread (e.g. disposing an evicted
  cache entry off the eviction callback): `_ = Task.Run(async () => await x.DisposeAsync());`.
- ✅ **No `ConfigureAwait(false)`.** This is an ASP.NET-Core-oriented library, and ASP.NET Core installs no
  `SynchronizationContext` (nor a custom `TaskScheduler`), so `ConfigureAwait(false)` is a runtime no-op — the
  deadlock/marshalling reason for it belonged to classic ASP.NET (Framework) and UI apps. `CA2007` is disabled, not required.
- ✅ **Prefer C# 12+ collection expressions** over `new[] { … }` / `new List<T> { … }` / `Array.Empty<T>()`:
  `byte[] data = [1, 2, 3];`, `List<string> names = ["a", "b"];`, `int[] empty = [];`. Cast when a target type needs
  it for generic inference, e.g. `(byte[])[1, 2, 3]` for a `ReadOnlyMemory<T>` parameter.
- ✅ **Use `const` for compile-time literal values** of any type instead of `var`/`readonly` locals:
  `const string Expected = "hello";`, `const int Count = 5;`, `const char Separator = ',';`.
- ✅ **Use UTF-8 string literals (`u8`)** when building `byte[]`/`ReadOnlySpan<byte>` from valid UTF-8 text:
  `"Hello"u8.ToArray()`. Use explicit byte arrays only for invalid UTF-8 sequences or error-condition tests.
- ✅ Nullable reference types are **on** and nullable warnings are **errors**. 🚫 The **null-forgiving operator (`!`)
  is banned** — always restructure so the compiler proves non-null (`?? throw`, `[MemberNotNullWhen]`, an
  `is { } local` pattern, early-out narrowing) instead of asserting it. Even though the analyzer accepts a `// !`
  justification comment, **do not use that escape hatch**: eliminate the `!`, never annotate it.
- ✅ **`Debug.Assert` is for internal invariants only — never to validate untrusted or external input.** It is
  compiled out in Release (`DEBUG`-gated), so any check written as an assertion silently disappears in shipped
  builds. Validating a parsed/decoded value (e.g. a `TryDecode`/`TryParse` result on caller- or wire-supplied data)
  with `Debug.Assert` is a security bug: the malformed value is accepted in Release. Use an explicit `if (!ok) throw`
  for anything derived from input; reserve `Debug.Assert` for facts the surrounding code already guarantees (a buffer
  length you just allocated, an unreachable `default` branch).
- 👁 Prefer **auto-properties over fields** for state (static or instance) — **including `private` members**. Use a
  field only for a low-level need a property cannot express (a `ref` / `Interlocked` / `fixed` / `stackalloc` target).
  **A `System.Threading.Lock` does _not_ qualify** — declare it as a get-only auto-property
  (`private Lock Gate { get; } = new();`): the property compiles to a `readonly` backing field and `lock(Gate)` still
  lowers to `Gate.EnterScope()`.
- 👁 **Pick the narrowest accessor shape:** `{ get; }` for read-only state, `{ get; init; }` for immutable DTOs, and
  `{ get; set; }` only when mutation is genuinely required. Use an **expression-bodied member** for a simple computed
  property (`internal TokenGeneratorSettings Settings => SettingsOrNull ??= LoadSettings();`).
- 👁 Use **primary constructors** wherever possible — including converting existing single-purpose constructors.
  When a constructor's body only assigns its parameters to members, hoist the parameters onto the type declaration
  and feed them directly into get-only / `init` auto-properties (`public Foo Bar { get; } = bar;`); delete the now-empty
  constructor and move its `<param>` doc tags onto the **type**. Keep a traditional constructor only when a primary one
  genuinely can't express the intent — a member initializer that must reference **another** instance member, multiple
  public constructors, or constructor-body logic that can't be reduced to member initializers (buffering a collection
  with `[.. param]` or a guard folded into an initializer is fine; branching, loops, or `out`/`ref` work is not).

- 👁 Put constants / statics / `static readonly` at the **top** of the file or class, before instance members.

## 2. Public API discipline

- 👁 **`public` is a deliberate decision, never a default.** Start every type and member at the **narrowest visibility
  that works** (`private`, then `internal`) and widen **only** with a conscious reason — a contract a consumer
  implements or resolves, a headline entry point, developer-facing data. "It compiled" or "a test needed it" are not
  reasons: test access uses `InternalsVisibleTo` (§7), not a widened surface, and a concrete implementation a consumer
  reaches through DI stays `internal` (the **interface** is the surface; the `DefaultFoo` impl is never named by
  consumers). The asymmetry is the argument — an accidental `internal` is a one-line widen, but an accidental `public`
  is a SemVer contract you must support and can only walk back with an `[Obsolete]` cycle across a **major** version.
- 👁 **Correct-by-default.** There is **no public API** to disable issuer / audience / signature / lifetime / algorithm
  validation, or to weaken a security control below its safe baseline. A knob may only supply values or swap a control
  for an equal-or-stronger one.
- 👁 **Enums for genuinely closed vocabularies; extensible vocabularies are string constants; no secret material on the
  surface.** A closed, non-extensible set is an `enum`. A vocabulary a consumer or sibling package must be able to
  **extend** (claim types, algorithm codes, grant types, `$type` discriminators) is a set of `public const string`
  values instead, so a new member is a non-breaking add rather than an enum edit. Never put secret material on the
  public surface.
- 👁 Data / options / context / event types are `sealed`, with `init` / get-only members and **concrete, buffered**
  collections (`List<T>`, not `IList<T>`) — AOT-friendly, cheap to re-enumerate, free of deferred-execution surprises.
  Interfaces appear only for a replaceable DI seam or a read-only slice.
- 👁 **Seal by derivation intent, not by visibility.** A public **data / options / context / event / result / DTO** type
  is `sealed` (above) — it is a leaf and sealing says so. A public type that is a **deliberate extension point** — an
  abstract base or a class consumers subclass (the Abstractions surface: `SecretKey`, `Algorithm`, `OpenIdContext`,
  `OpenIdClient`, `KnownParameter`, …) — is **not** sealed. A public **concrete leaf** with no extension point is
  sealed too; if it only needs to be _test_-mockable, demote it to `internal` (left unsealed, §7) or mock its
  interface — never keep it `public` **and** unsealed just for tests. `CA1852` (seal internal types) is **disabled**
  precisely so internal impls can stay unsealed for mocking; that exemption does not license unsealed public leaves.
- 👁 **Prefer a `readonly record struct` for a small, immutable value-like type** — an id, key, command, or disposition,
  the established shape across the family (`SettingKey<T>`, `UriDescriptor`, the `*Command` / `*Disposition` types) — so
  value equality and immutability come for free. Reach for a `class` / `record class` only for reference identity or a
  larger/mutable payload.
- 👁 **A JSON-deserialized DTO captures unknown fields** with a
  `[JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }` member, so an unrecognized
  wire member round-trips instead of being silently dropped.
- 👁 **Buffer by default; stream deliberately.** Reach for a lazy `IEnumerable<T>` / `IAsyncEnumerable<T>` only when the
  sequence is genuinely large/unbounded, I/O-backed, or usually short-circuited. Never return a deferred sequence that
  **captures request/ambient context** (it goes stale off-request — see §7), and never hand back as lazy a sequence a
  caller will enumerate more than once or need `Count` on.
- 👁 **Own every stored collection; pick the concrete type by usage.** A constructor or method that keeps a collection
  parameter — including a DI-injected `IEnumerable<T>` or a sequence a deferred closure will capture — **buffers it once
  into an owned, immutable copy**; never store the caller's reference through (they can mutate it, and a lazy sequence
  re-enumerates on every read). Choose the type by how it is read: a set-once membership/lookup table read many times is
  a `FrozenSet<T>` / `FrozenDictionary<K,V>` (concrete, immutable, O(1) — **not** `IReadOnlyDictionary<K,V>`, which is an
  interface); a set-once sequence you iterate is an `ImmutableArray<T>` (`Foo { get; } = [.. items];`); a genuinely
  mutable working set stays a `List<T>`. Prefer `params IEnumerable<T>` over `params T[]` (C# 13) and buffer inside.
- 👁 **Signatures speak in read-only interfaces; storage stays concrete.** A method **parameter** or a computed method
  **return value** that does not hand back ownership takes / returns the narrowest read-only interface
  (`IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `IReadOnlySet<T>`), so callers aren't coupled to a concrete type. This
  does **not** relax the rule above: a set-once collection **property** on a data type still exposes its concrete
  immutable type (`FrozenSet<T>` / `ImmutableArray<T>`), and the backing storage is always concrete. Locals stay
  concrete via `var` over an explicit initializer (`var map = new Dictionary<string, T>();`).
- 👁 **No behavior-selecting flag arguments** (the "boolean trap"). A `bool`/`enum` parameter whose only job is to
  switch a method between two behaviors becomes **two intention-revealing methods**. A `bool` carried as data the
  method does not branch on is fine.
- 👁 **A configuration callback takes one growable type.** An `Action<T>` passed to a wire-up method uses a single
  sealed options/configurator `T`, never bare parameters — so a new configurable aspect is a **non-breaking
  property-add** on `T`, not a new callback parameter that breaks every caller.
- 👁 **No cross-assembly `InternalsVisibleTo` in production.** It is for **test projects only** (plus
  `DynamicProxyGenAssembly2` for mocking). Packages collaborate through **public** contracts, never internals. See
  [ADR-0004](../../docs/adr/0004-no-cross-assembly-internalsvisibleto.md).
- 👁 **An implementation package references only `*.Abstractions` packages — never another implementation package.**
  Cross-implementation collaboration flows through an interface owned by an `*.Abstractions` package, resolved from DI;
  the concrete type is registered by its own package's `DefaultRegistration.cs` (§4) and referenced in exactly one
  place — the composition root (`AddIdentityServer()` on the `NCode.Registration` `IServiceBuilder<TMarker>` builder). A
  new capability shared by more than one implementation therefore ships as an abstraction/implementation **pair**. An
  implementation `.csproj` whose `ProjectReference` names a non-`Abstractions` package is a violation. See
  [ADR-0016](../../docs/adr/0016-implementation-packages-depend-only-on-abstractions.md).
- 👁 **Mark infrastructure-public types.** A type that is public **only** for cross-assembly collaboration — never for
  application code — is marked `[EditorBrowsable(EditorBrowsableState.Never)]` and its `<summary>` opens with the
  standard boilerplate: _"This API supports the NCode.Identity infrastructure and is not intended to be used directly
  from your code."_ (the EF Core / ASP.NET Core pattern).
- ⏳ The public surface is deliberate and tracked by `PublicApiAnalyzers` (`RS0016` / `RS0017` / `RS0024`): a public
  change not recorded in the sibling `PublicAPI.{Shipped,Unshipped}.txt` fails the build. A breaking change is a
  **major** version bump. See [ADR-0005](../../docs/adr/0005-public-api-surface-is-tracked-and-evolves-compatibly.md).
  After a large refactor (types moved/renamed, members added/removed), regenerate the entries with
  `dotnet format analyzers <project> --diagnostics RS0016 RS0017` — it applies the analyzer's add/remove code fixes to
  `PublicAPI.Unshipped.txt` in bulk; rebuild to confirm and hand-fix any leftovers rather than editing dozens of lines
  by hand. Entries are a **set**, so exact sort order is not required for the build, though the file is kept sorted.
- 👁 **Required inputs are constructor parameters; optional inputs are `init` properties.** Never add an optional value
  as a new constructor parameter — once shipped that is a binary-breaking signature change. Add it as an `init`
  property, model "unspecified" as nullable, and resolve the default **inside the library**.
- 👁 **Annotate a reflection-/DI-/serializer-consumed public type with `[PublicAPI]`** (JetBrains.Annotations) — the
  house pattern across the packages. Members of a public type touched only by the JSON serializer, the options binder,
  or the DI container have no in-repo call site, so the unused-member analyzers flag them; `[PublicAPI]` states the
  intent and silences the noise without a null-forgiving or pragma escape hatch. **Do not apply `[PublicAPI]` to an
  internal type** — an internal implementation is reached through in-assembly call sites (its interface, its DI
  registration), so the analyzers do not flag it and the annotation would misstate it as public surface.

## 3. File & code organization

- ✅ **One type per file.** An interface and its implementation are two files. (A generic interface and its
  strongly-typed sibling of the same name — `IFoo` + `IFoo<T>` — may share a file, as they are one contract in two arities.)
- 👁 **The single canonical implementation of an `IFoo` is named `DefaultFoo`** — strip the `I`, prepend `Default`
  (`IJoseSerializer` → `DefaultJoseSerializer`, `IJsonWebTokenService` → `DefaultJsonWebTokenService`,
  `ITokenService` → `DefaultTokenService`). This is the ASP.NET Core idiom (`DefaultHttpContextFactory`): it keeps the
  bare concept name free, separates interface from implementation, and signals a replaceable default. Applies
  regardless of the interface's visibility. **Two carve-outs, when the class is _not_ "the one canonical impl":**
  (a) one of several variants named for what makes it specific (`DefaultAuthorizationCodeGrantHandler`,
  `DefaultClientCredentialsGrantHandler` — each a distinct `ITokenGrantHandler`); (b) an implementation of a
  **framework** interface follows that framework's own pattern name. See
  [ADR-0006](../../docs/adr/0006-single-canonical-impl-is-defaultfoo.md).
- 👁 **No nested types**, with two exceptions: (a) a private nested type that must **close over the enclosing type's
  generic type parameter**; (b) a nested `static` class that groups a related **constant vocabulary** (only `const`
  fields — no behavior, no state). Everything else is its own top-level file.
- 👁 **A type's folder is decided by _what the type is_, organized by purpose.** Group interfaces/contracts, options,
  events, developer-facing models, extension-method classes, and internal infrastructure so a reader can find "the
  serializers" or "the endpoints" by folder. Public headline entry points a consumer uses by name live at the package
  root. A single-purpose package (all abstractions) stays flat with no subfolders.
- ✅ **Namespaces are organized independently of folders and stay flat per package** — the public namespace is a
  stable contract; folders are organization only. `IDE0130` / namespace-match-folder is opted out in
  [`.editorconfig`](../../.editorconfig). Use `RootNamespace` to pin a package's namespace.
- 👁 **Interface names state the role; the namespace supplies the brand.** Name an interface for its role
  (`ITokenGrantHandler`, `IEndpointProvider`, `IClaimsService`) and add a qualifier only when the bare role noun would
  collide with a well-known BCL/framework type or be too generic to stand alone.

## 4. Extension methods & DI registration

- ✅ Use the **C# 14 `extension` member syntax** (`extension(ReceiverType r) { … }` blocks), not classic
  `this`-parameter static methods — matching the `DefaultRegistration.cs` composition pattern
  (`extension(IServiceBuilder<IdentityLibrary> builder) { … }`).
- ✅ **One receiver-type per static class.** Multiple `extension(...)` blocks with different receiver types in one class
  trip `CA1708` — split them (which also fits one-type-per-file).
- 👁 Keep null-guards on the receiver (extension members can be invoked on `null`).
- 👁 **Name the receiver in a receiver-argument exception even though `CA2208` objects.** When an `extension(...)` member
  throws `ArgumentException` about its receiver, still pass `nameof(receiver)` — the receiver _is_ the offending argument.
  `CA2208` does not yet treat an extension-block receiver as a parameter, so wrap only those throws in a tight
  `#pragma warning disable/restore CA2208` with a one-line reason; never drop the `paramName` to silence it.
- 👁 **Choose the DI shape deliberately** per [ADR-0001](../../docs/adr/0001-mediator-vs-dependency-injection-logic-classes.md):
  a **service** (`TryAddSingleton<IFoo, DefaultFoo>`) for a capability you call for an answer; a **strategy collection**
  (`TryAddEnumerable`) for many implementations the caller iterates or selects; the **mediator** (`ICommandHandler<T>`)
  only for a genuine pipeline seam where the sender must stay ignorant of the handlers.
- 👁 **A package registers its own services from one `DefaultRegistration.cs`** `extension(IServiceBuilder<TMarker>)`
  block, using `TryAdd*` so a consumer can override a default. Register a stateless capability as a singleton; register
  a long-running loop as an `AddHostedService<T>` over a `BackgroundService`. Wire-up stays out of the type that does
  the work, so the work stays constructor-injectable and testable.

## 5. Formatting & build gates

- ⏳ **CSharpier** formatting (`dotnet csharpier check .`, wired into [`build/dod.ps1`](../../build/dod.ps1)). Run
  `dotnet csharpier format .` before committing. CSharpier 1.x formats XML too; generated output is excluded via
  [`.csharpierignore`](../../.csharpierignore). The house style CSharpier enforces: **file-scoped namespaces**,
  4-space indentation, and a **trailing comma** on every multi-line object / collection / enum member list.
- ⏳ **Code-style analyzers run on build.** `EnforceCodeStyleInBuild` makes the `IDExxxx` rules execute during
  `dotnet build`; combined with warnings-as-errors, rules set to `warning` in [`.editorconfig`](../../.editorconfig)
  become build errors on shipping code.
- ✅ **Banned APIs** (`Microsoft.CodeAnalysis.BannedApiAnalyzers`, `RS0030`) from
  [`BannedSymbols.txt`](../../BannedSymbols.txt) — no `Newtonsoft.Json` (use `System.Text.Json`), no legacy
  `JwtSecurityTokenHandler` / `JwtSecurityToken` (use `JsonWebTokenHandler`), and no
  sync-over-async. Add house rules to that file as they emerge.
- ⏳ **Warnings-as-errors**, and **NuGet vulnerability advisories (`NU190x`) as errors in CI**.
- ✅ The whole gate is [`build/dod.ps1`](../../build/dod.ps1) — the single Definition of Done that CI runs.
- 👁 **Run the full `dod.ps1` (a clean Release build over the whole solution) before calling a structural change done —
  an incremental Debug build of a single project can mask errors a clean build catches.** Notably, a `git mv` of a file
  that is open in the editor can be silently undone by the editor re-saving its stale buffer to the old path, leaving a
  duplicate type definition that only a clean/Release build (i.e. the DoD) surfaces.

## 6. Clean Code / Refactoring (review-only)

Drawn from _Clean Code_ (R. C. Martin) and _Refactoring_ (Fowler / Beck). All 👁:

- Small functions that do **one thing** at a single level of abstraction.
- **Few parameters** (0–2; avoid >3); a recurring parameter group (data clump) becomes a type. **I/O-channel
  parameters don't count toward the budget** — a trailing `CancellationToken` and a Try-pattern `out` result are
  plumbing, not data. A clump that mirrors a well-known BCL shape (e.g. `ClaimsIdentity(authenticationType,
nameType, roleType)`) is acceptable, and a private single-call-site helper isn't worth a parameter object.
- **Command–Query Separation** — a method acts _or_ answers, never both; a query does not mutate.
- **Don't pass or return `null`** where an empty/absent form exists (empty collections; model absence).
- **Intention-revealing, honest, searchable names** — no `Manager` / `Helper` catch-alls, no encodings.
- **No repeated magic string literals.** A string used in more than one place — a claim type, an algorithm code, a
  shared-dictionary key — is a named `const` in **one** place. Sentinels living in a shared dictionary
  (`HttpContext.Items`, `AuthenticationProperties.Items`) are namespaced with a package prefix so they cannot collide.
- **Exceptions, not error codes**, typed to the caller and carrying context; **never** an empty `catch`, never `async void`.
- **Comments justify _why_, never restate _what_.** No commented-out code (Git remembers).
- **Primitive Obsession** — model a concept that carries invariants as a type, not a bare `string`/`bool`/`int`.
- **Law of Demeter** — talk to immediate collaborators; avoid `a.B().C().D()` chains and Feature Envy.
- **Never nest a function call inside another call's arguments.** Hoist the inner call into an intention-revealing
  local first — `var value = GetValue(); Process(value, "foo");`, never `Process(GetValue(), "foo")` — so each step is
  named, debuggable, and readable.
- **Dispose what you own.** A local `IDisposable` / `IAsyncDisposable` gets a `using` / `await using` **declaration**
  (`using var x = …;`, not a nested block) unless ownership is deliberately transferred to a type that will dispose it.
  Never leave a `HttpResponseMessage`, `Stream`, `JsonDocument`, or `IHttpClientFactory`-created client undisposed.

## 7. Testability & unit-test-friendliness

Production code **must** be unit-test-friendly. These are 👁 review-only except the `InternalsVisibleTo` wiring.

- 👁 **Prefer instance members behind an interface, registered as a DI singleton**, over a `static` utility class — a
  singleton gives static-like performance and testability (tests mock the interface).
- 👁 **Minimize `static`.** Reserve it for pure functions with no swappable dependency, `const` / `static readonly`
  constants, an expensive immutable thread-safe shared instance, and extension-method entry points. **Never** static
  _mutable_ or _ambient_ state.
- 👁 **Never capture request/ambient context in stored state.** Do not hold a captured `HttpContext`, a request-scoped
  `IServiceProvider`, or a `ClaimsPrincipal` snapshot in a field or property — it goes stale and becomes a
  use-after-request footgun. Model a read-over-ambient type as a **live view** over `IHttpContextAccessor`, or take the
  request-scoped dependency as a **method parameter** / resolve it at the point of use.
- 👁 **Constructor-inject every dependency** (no `new`-ing a collaborator inside a method, no service-location) —
  **except** a singleton resolving collaborators it genuinely cannot inject (a scoped service, or one selected by an
  open generic type parameter at call time), which it resolves from `HttpContext.RequestServices` /
  `IServiceProvider` at the point of use and stays testable via a real `ServiceProvider`. When the collaborators are a
  closed set of singletons, constructor-inject `IEnumerable<T>` instead.
- 👁 **Facade un-mockable dependencies.** A static or framework/external dependency that cannot be easily mocked — the
  clock (`DateTimeOffset.UtcNow`), `Guid.NewGuid()`, crypto RNG, `Activity` — gets a thin `internal interface`
  facade + `internal sealed` forwarding impl **under a `Facades/` folder**, registered in DI and injected, so callers
  become unit-testable by mocking the facade. Mark the passthrough impl `[ExcludeFromCodeCoverage]` (it only delegates).
- 👁 **`internal virtual` is the sanctioned local override seam.** When extracting a collaborator behind an interface is
  overkill, mark a helper whose logic needs isolation — or a **factory method** that allocates an object handed to a
  dependency — `internal virtual`, so a test subclass can stub/override it (`DynamicProxyGenAssembly2` lets Moq proxy
  it). Reach for an extracted interface collaborator when the seam is reused or substantial; use `internal virtual` for
  a one-off override point.
- 👁 **Internal implementations stay unsealed so tests can mock them; public types are mocked through their interface.**
  A concrete `internal` impl is left **unsealed** (and its test-relevant members `internal virtual`) so Moq/Castle can
  subclass it — `CA1852` ("seal internal types") is therefore **disabled** ([`.editorconfig`](../../.editorconfig)):
  the micro devirtualization win loses to mockability. Do **not** reach for `public virtual` to make a public class
  mockable — a `public virtual` member is a permanent, SemVer-bound override point (fragile base class) and can let a
  subclass weaken a security control; mock the **interface** the class implements instead. `sealed` remains correct for
  data/options/event types (§2) and facade impls, where nothing needs to derive.
- ✅/👁 **`internal` for test access.** Each package exposes its internals to its `.Tests` project via
  `InternalsVisibleTo` **plus `DynamicProxyGenAssembly2`** (so Moq/Castle can proxy `internal` types and members).
  Keep test-only reach `internal`, never widen the public surface for tests.
- 👁 **Test the unit through its public surface; privates are covered transitively.** A `private` method is not a seam;
  drive it through the public method that uses it. A `private` branch the public tests cannot reach is dead code to
  delete. If a private grows complex enough to isolate, **extract a collaborator** behind an interface — or promote it
  to an `internal virtual` override seam (see above) — and test that.
- 👁 **Tested-or-excluded.** Every type/member that can carry `[ExcludeFromCodeCoverage]` is **either** exercised by
  tests **or** annotated with it (with a one-line reason when non-obvious). Exclude thin passthrough infrastructure
  (facades), pure DI/registration wiring, and framework-event adapters; **test** anything with behavior.

### Tests

Test-project conventions (xUnit, Moq `.Verifiable()`, `MockBehavior.Strict`, `Method_State_Expected` naming, `#region`
organization) live in [`csharp-testing.instructions.md`](csharp-testing.instructions.md).

## 8. Provenance & versioning

- ✅ Versioning is stamped by **Nerdbank.GitVersioning** (single root [`version.json`](../../version.json)), **lockstep**
  across the family. `AssemblyVersion` is pinned to `major` precision so patches/minors are drop-in within a major.
  See [ADR-0003](../../docs/adr/0003-central-package-management-and-lockstep-versioning.md).
- ✅ Packages are **source-linked to GitHub** and ship a portable symbol package (`.snupkg`) so consumers can step into
  the exact released source. See [ADR-0007](../../docs/adr/0007-provenance-sourcelink-and-symbols.md).
- ✅ Package **versions live centrally** in [`Directory.Packages.props`](../../Directory.Packages.props) (Central
  Package Management); a `PackageReference` never carries an inline `Version`.

## 9. Change intake (open source)

This is an open-source project. A change starts as a GitHub issue describing the problem or proposal; a design-affecting
change (semantics, public surface, validation/security posture) should reach agreement on the issue — and be recorded
as an ADR under [`docs/adr/`](../../docs/adr/) — before the pull request. Bug fixes, patches, and small changes go
straight to a PR. Record consumer-visible changes in [`CHANGELOG.md`](../../CHANGELOG.md) under `## [Unreleased]`.

## 10. XML documentation

- 👁 **New or changed API carries XML doc.** Any type or member you add — or whose signature/behavior you change — gets
  a `///` doc comment. Pre-existing undocumented code is grandfathered: do **not** add doc comments to code you are not
  otherwise touching, but when you rename or update a member, add or update its doc to match.
- 👁 **Prefer a substantive `<summary>`** that explains purpose, behavior, and context over a terse one-liner, and use
  `<param>` / `<returns>` / `<remarks>` where they add clarity.
- 👁 **A `<summary>` tag is multi-line** — the opening tag, content, and closing tag on separate lines, even for a short
  summary; never collapse them onto one line.
- 👁 **A property summary follows the Microsoft convention** — open with **"Gets or sets"**, **"Gets"**, or **"Sets"**
  according to its accessors.

## 11. Logging

- 👁 **Log through source-generated `[LoggerMessage]` methods**, never `ILogger.Log*` calls with an interpolated
  message — the generator gives allocation-free, strongly-typed, structured logging. Group the partial methods for a
  package in a `Logging/Log.cs` `internal static partial class Log`, written as **extension methods on `ILogger`**
  (`Logger.SubjectValidationFailed(reason)`), and mark the class `[ExcludeFromCodeCoverage]` (generated plumbing).
- 👁 **A message template is a constant with named placeholders** (`"… {DisplayName}"`), never string interpolation or
  concatenation; a value that also feeds an error/response is defined **once** (§6) and passed to the log method as a
  structured argument.
- 👁 **Event IDs are named `const`s in a per-package `Logging/EventIds.cs`** — an `internal static class EventIds`
  with a `private const int Base` set to the package's band (below) and one `public const int` per event defined as
  `Base + n`. **Each constant is named _exactly_ as the `[LoggerMessage]` method it identifies**
  (`EventIds.SubjectValidationFailed` ↔ `Log.SubjectValidationFailed(…)`), and the attribute references it
  (`EventId = EventIds.SubjectValidationFailed`) — never a bare magic number at the call site or in the attribute.
- 👁 **Every runtime package is pre-assigned a disjoint 1000-wide Event-ID band** in the registry below; a package
  starts using its band when it adds its first log. An `EventId` only has to be unique within an `ILogger<T>` category,
  but a family-wide band makes it **globally unique across packages**, so an operator's log filter or alert keys on a
  stable number regardless of which assembly emitted it. IDs are **append-only** — never renumber or reuse a shipped
  value (same discipline as enum members and the public API §2), and a band, once assigned, is never renumbered.
  `*.Abstractions` packages are pure contracts (§2) with no behavior, so they never log and get no band. A **new**
  runtime package added to the family claims the next free band and **adds a row here**:

    | Band            | Package                                             |
    | --------------- | --------------------------------------------------- |
    | `1000`–`1999`   | `NCode.Identity.OpenId.Core`                        |
    | `2000`–`2999`   | `NCode.Identity.OpenId.Authentication`              |
    | `3000`–`3999`   | `NCode.Identity.OpenId.Management`                  |
    | `4000`–`4999`   | `NCode.Identity.OpenId.Persistence`                 |
    | `5000`–`5999`   | `NCode.Identity.OpenId.Persistence.EntityFramework` |
    | `6000`–`6999`   | `NCode.Identity.Secrets`                            |
    | `7000`–`7999`   | `NCode.Identity.Secrets.Persistence`                |
    | `8000`–`8999`   | `NCode.Identity.Jose`                               |
    | `9000`–`9999`   | `NCode.Identity.JsonWebTokens`                      |
    | `10000`–`10999` | `NCode.Identity`                                    |
    | `11000`–`11999` | `NCode.Registration`                                |
    | `12000`–`12999` | `NCode.Identity.Server`                             |
    | `13000`–`13999` | `NCode.Identity.OpenId.Playground`                  |

## 12. Configuration & options

- 👁 **Configuration is a strongly-typed options class** in an `Options/` folder, bound with
  `services.Configure<T>(configuration.GetSection(...))` — never `IConfiguration` read ad hoc deep in the code.
- 👁 **Inject `IOptions<T>` for start-up-fixed configuration and `IOptionsMonitor<T>` for values that may change at
  runtime.** An options class is a data type (§2): `sealed`, `init` / get-only members, concrete collections,
  correct-by-default (§2 — no knob weakens a security control below its safe baseline).
