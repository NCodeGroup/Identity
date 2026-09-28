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
- ✅ Nullable reference types are **on** and nullable warnings are **errors**. ⏳ The **null-forgiving operator (`!`)
  is discouraged** — restructure so the compiler proves non-null (`?? throw`, `[MemberNotNullWhen]`, early-out
  narrowing) instead of asserting it.
- 👁 Prefer **auto-properties over fields** for state (static or instance). Use a field only when a property genuinely
  cannot express it (a `ref`/`Interlocked`/`fixed`/`stackalloc` target).
- 👁 Use **primary constructors** except where they can't work (for example when one member's initializer must
  reference another instance member).
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
- 👁 **Buffer by default; stream deliberately.** Reach for a lazy `IEnumerable<T>` / `IAsyncEnumerable<T>` only when the
  sequence is genuinely large/unbounded, I/O-backed, or usually short-circuited. Never return a deferred sequence that
  **captures request/ambient context** (it goes stale off-request — see §7), and never hand back as lazy a sequence a
  caller will enumerate more than once or need `Count` on.
- 👁 **No behavior-selecting flag arguments** (the "boolean trap"). A `bool`/`enum` parameter whose only job is to
  switch a method between two behaviors becomes **two intention-revealing methods**. A `bool` carried as data the
  method does not branch on is fine.
- 👁 **A configuration callback takes one growable type.** An `Action<T>` passed to a wire-up method uses a single
  sealed options/configurator `T`, never bare parameters — so a new configurable aspect is a **non-breaking
  property-add** on `T`, not a new callback parameter that breaks every caller.
- 👁 **No cross-assembly `InternalsVisibleTo` in production.** It is for **test projects only** (plus
  `DynamicProxyGenAssembly2` for mocking). Packages collaborate through **public** contracts, never internals. See
  [ADR-0004](../../docs/adr/0004-no-cross-assembly-internalsvisibleto.md).
- 👁 **Mark infrastructure-public types.** A type that is public **only** for cross-assembly collaboration — never for
  application code — is marked `[EditorBrowsable(EditorBrowsableState.Never)]` and its `<summary>` opens with the
  standard boilerplate: _"This API supports the NCode.Identity infrastructure and is not intended to be used directly
  from your code."_ (the EF Core / ASP.NET Core pattern).
- ⏳ The public surface is deliberate and tracked by `PublicApiAnalyzers` (`RS0016` / `RS0017` / `RS0024`): a public
  change not recorded in the sibling `PublicAPI.{Shipped,Unshipped}.txt` fails the build. A breaking change is a
  **major** version bump. See [ADR-0005](../../docs/adr/0005-public-api-surface-is-tracked-and-evolves-compatibly.md).
- 👁 **Required inputs are constructor parameters; optional inputs are `init` properties.** Never add an optional value
  as a new constructor parameter — once shipped that is a binary-breaking signature change. Add it as an `init`
  property, model "unspecified" as nullable, and resolve the default **inside the library**.

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
- 👁 **Choose the DI shape deliberately** per [ADR-0001](../../docs/adr/0001-mediator-vs-dependency-injection-logic-classes.md):
  a **service** (`TryAddSingleton<IFoo, DefaultFoo>`) for a capability you call for an answer; a **strategy collection**
  (`TryAddEnumerable`) for many implementations the caller iterates or selects; the **mediator** (`ICommandHandler<T>`)
  only for a genuine pipeline seam where the sender must stay ignorant of the handlers.

## 5. Formatting & build gates

- ⏳ **CSharpier** formatting (`dotnet csharpier check .`, wired into [`build/dod.ps1`](../../build/dod.ps1)). Run
  `dotnet csharpier format .` before committing. CSharpier 1.x formats XML too; generated output is excluded via
  [`.csharpierignore`](../../.csharpierignore).
- ⏳ **Code-style analyzers run on build.** `EnforceCodeStyleInBuild` makes the `IDExxxx` rules execute during
  `dotnet build`; combined with warnings-as-errors, rules set to `warning` in [`.editorconfig`](../../.editorconfig)
  become build errors on shipping code.
- ✅ **Banned APIs** (`Microsoft.CodeAnalysis.BannedApiAnalyzers`, `RS0030`) from
  [`BannedSymbols.txt`](../../BannedSymbols.txt) — no `Newtonsoft.Json` (use `System.Text.Json`) and no
  sync-over-async. Add house rules to that file as they emerge.
- ⏳ **Warnings-as-errors**, and **NuGet vulnerability advisories (`NU190x`) as errors in CI**.
- ✅ The whole gate is [`build/dod.ps1`](../../build/dod.ps1) — the single Definition of Done that CI runs.

## 6. Clean Code / Refactoring (review-only)

Drawn from _Clean Code_ (R. C. Martin) and _Refactoring_ (Fowler / Beck). All 👁:

- Small functions that do **one thing** at a single level of abstraction.
- **Few parameters** (0–2; avoid >3); a recurring parameter group (data clump) becomes a type.
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
  facade + `internal sealed` forwarding impl, registered in DI and injected, so callers become unit-testable by
  mocking the facade. Mark the passthrough impl `[ExcludeFromCodeCoverage]`.
- ✅/👁 **`internal` for test access.** Each package exposes its internals to its `.Tests` project via
  `InternalsVisibleTo` **plus `DynamicProxyGenAssembly2`** (so Moq/Castle can proxy `internal` types and members).
  Keep test-only reach `internal`, never widen the public surface for tests.
- 👁 **Test the unit through its public surface; privates are covered transitively.** A `private` method is not a seam;
  drive it through the public method that uses it. A `private` branch the public tests cannot reach is dead code to
  delete. If a private grows complex enough to isolate, **extract a collaborator** behind an interface and test that.
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
