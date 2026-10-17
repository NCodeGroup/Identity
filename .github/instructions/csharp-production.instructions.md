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
- 👁 **Request-abort cancellation is a disconnect, not an error.** An `OperationCanceledException` whose token is the
  request's `HttpContext.RequestAborted` means the client went away; at an endpoint/middleware boundary rethrow it so
  the framework abandons the request — never convert it into a rendered error response, a retry, or an audit write (the
  aborted token would fail those writes anyway).
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
- ⚠️ **`T?` under a `where T : notnull` constraint is NOT `Nullable<T>` for a value type** — it is the
  non-nullable `T`, so `field is not null` / `field is null` on such a member is always `true` / `false` for value
  types and silently misdetects "has a value". Never use a `T?` field (or the `x is null` test on one) as the
  presence flag for an optional value-type slot under a `notnull` constraint; track presence with an explicit `bool`
  flag (or constrain the API to `where T : struct` so `T?` really is `Nullable<T>`). This bit the settings lattice:
  see [ADR-0010](../../docs/adr/0010-supported-settings-unset-means-unrestricted.md).
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
- 👁 **A persisted-at-rest payload is schema-versioned and evolves additively.** A domain payload serialized into a
  stored JSON blob (the `PersistedGrant<TPayload>.Payload` path) is written through the versioned seam
  `IPersistedPayloadSerializer` ([ADR-0113](../../docs/adr/0113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md)),
  not a bare `JsonSerializer` call. Evolve the DTO **additively**: a new member is optional (nullable or defaulted),
  **never `required`** (`System.Text.Json` enforces `required` on read, breaking every older row); enums stay
  string-valued, never ordinal; a member is never renamed, removed, or retyped in place. A **non-additive** change bumps
  `[PersistedPayloadVersion(N)]` on the payload and ships one `FromVersion → FromVersion + 1` `IPersistedPayloadUpcaster`.
  A long-lived payload that is rewritten in place (`UpdatePayloadAsync`) also carries the `[JsonExtensionData]` bag above
  so a mixed-version node cannot drop members on a read-modify-write. Payload members never begin with `$` (reserved for
  the envelope).
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
  `dotnet format analyzers <project> --diagnostics RS0016 RS0017`; it reliably **adds** the missing entries (`RS0016`)
  but does **not** reliably remove the stale ones (`RS0017`), so delete by hand the entries for symbols that moved or
  were removed, then rebuild to confirm — rather than editing dozens of lines by hand. A `record` whose only `required`
  members are inherited has **no** public parameterless constructor on its surface, so it carries no
  `….<Type>() -> void` entry. Entries are a **set**, so exact sort order is not required for the build, though the file
  is kept sorted. A C# 14 `extension(Receiver)` member needs **two** entries — the `….extension(Receiver!)` container
  line **and** the member line — so when hand-editing (every `DefaultRegistration` method is such a member), add both.
- 👁 **Required inputs are constructor parameters; optional inputs are `init` properties.** Never add an optional value
  as a new constructor parameter — once shipped that is a binary-breaking signature change. Add it as an `init`
  property, model "unspecified" as nullable, and resolve the default **inside the library**.
- 👁 **An evolving set of optional selection criteria is a DTO, not positional parameters.** When a method takes a
  growing set of optional filters (the way `IGrantStore` kept gaining `sessionId`, then `grantType`), model them as a
  dedicated `readonly record struct` with **init-only, non-`required`** properties (e.g. `GrantFilter`), constructed by
  object initializer — so a new criterion is a source-compatible add, not a breaking signature change. The
  `(cursor, limit)` pagination pair follows the same shape via the shared `PageRequest` DTO (every store `GetPageAsync`
  takes `PageRequest`), so pagination can grow (e.g. a sort direction) non-breakingly too. Keep **required identity
  keys** positional. See [ADR-0099](../../docs/adr/0099-forward-compatible-evolving-parameters.md).
- 👁 **Annotate a reflection-/DI-/serializer-consumed public type with `[PublicAPI]`** (JetBrains.Annotations) — the
  house pattern across the packages. Members of a public type touched only by the JSON serializer, the options binder,
  or the DI container have no in-repo call site, so the unused-member analyzers flag them; `[PublicAPI]` states the
  intent and silences the noise without a null-forgiving or pragma escape hatch. **Do not apply `[PublicAPI]` to an
  internal type** — an internal implementation is reached through in-assembly call sites (its interface, its DI
  registration), so the analyzers do not flag it and the annotation would misstate it as public surface.

## 3. File & code organization

- ✅ **One type per file.** An interface and its implementation are two files. (A generic interface and its
  strongly-typed sibling of the same name — `IFoo` + `IFoo<T>` — may share a file, as they are one contract in two arities.)
- 👁 **The single canonical implementation of an `IFoo` that a consumer resolves through DI and never names directly
  is named `DefaultFoo`** — strip the `I`, prepend `Default` (`IJoseSerializer` → `DefaultJoseSerializer`,
  `IJsonWebTokenService` → `DefaultJsonWebTokenService`, `ITokenService` → `DefaultTokenService`). This is the ASP.NET
  Core idiom (`DefaultHttpContextFactory`): it keeps the bare concept name free, separates interface from
  implementation, and signals a replaceable default. The rule is about the **implementation**, which therefore stays
  `internal` (§2) and is reached only through the interface; the `IFoo` itself may be `public` or `internal` — that is
  what "regardless of the interface's visibility" means. **Three carve-outs, when the class is _not_ "the one canonical
  DI impl":** (a) one of several variants named for what makes it specific (`DefaultAuthorizationCodeGrantHandler`,
  `DefaultClientCredentialsGrantHandler` — each a distinct `ITokenGrantHandler`); (b) an implementation of a
  **framework** interface follows that framework's own pattern name; (c) a **public, developer-facing
  data / message / model / result type a consumer constructs and names directly** keeps the bare concept name
  (`AuthorizationTicket`, `TokenRequest`, `ResourceNode` — never `Default*`), even when it implements an `IFoo` seam.
  See [ADR-0006](../../docs/adr/0006-single-canonical-impl-is-defaultfoo.md).
- 👁 **No nested types**, with two exceptions: (a) a private nested type that must **close over the enclosing type's
  generic type parameter**; (b) a nested `static` class that groups a related **constant vocabulary** (only `const`
  fields — no behavior, no state). Everything else is its own top-level file.
- 👁 **A type's folder is decided by _what the type is_, organized by purpose.** Group interfaces/contracts, options,
  events, developer-facing models, extension-method classes, and internal infrastructure so a reader can find "the
  serializers" or "the endpoints" by folder. Public headline entry points a consumer uses by name live at the package
  root. A single-purpose package (all abstractions) stays flat with no subfolders. **Avoid a vague catch-all folder /
  namespace (`Logic`, `Helpers`, `Misc`, `Common`) — it is a grab-bag that grows without cohesion.** Name the group for
  the purpose it serves (`KeyTypes`, `Collections`, `KeyMaterial`, `Algorithms`, `Stores`); if a bucket has accreted
  several unrelated purposes, split it (as the secret-key `…Secrets.Logic` bucket was split — see
  [`CHANGELOG.md`](../../CHANGELOG.md)).
- ✅ **The _public_ namespace is a stable contract and does not have to mirror folders.** `IDE0130` /
  namespace-match-folder is opted out in [`.editorconfig`](../../.editorconfig), and `RootNamespace` pins a package's
  base namespace, so you can reorganize public types across folders **without churning their namespace** — the public
  surface stays flat under the package root (public options are the established exception: an `Options/` folder carries a
  `.Options` sub-namespace). **Internal implementation types, by contrast, carry no contract, so prefer traditional
  folder-matching organization for them** — when you group internals into a folder, give them the matching
  sub-namespace (e.g. `…Logging` for `Logging/`, `…InProcess` for a built-in strategy's `InProcess/` folder) so the
  namespace reflects the structure. Reserve the flat-namespace rule for the public surface, where stability matters;
  do not flatten internal helpers that live in a dedicated folder.
- 👁 **Interface names state the role; the namespace supplies the brand.** Name an interface for its role
  (`ITokenGrantHandler`, `IEndpointProvider`, `IClaimsService`) and add a qualifier only when the bare role noun would
  collide with a well-known BCL/framework type or be too generic to stand alone.

## 4. Extension methods & DI registration

- 👁 **A host `Program.cs` is composition-only — reusable or developer-only startup code never lives there.** This is
  the single most-repeated review miss, so treat it as a hard rule. The `Playground` and every reference host must read
  as a flat list of turnkey calls (`serviceCollection.AddX(...)`, `app.UseX()`, `app.MapX()`) — **no** reusable logic,
  policy builders, config-section parsing, `new`-ing of options, loops, consts, or dev/test helpers inline. **Tripwire:**
  the moment host startup needs more than a single wiring call — an `if`, a `foreach`, a CORS/auth policy builder, a
  `new`, reading a configuration section — stop and move that logic into the **owning package** as an `Add*`/`Use*`/`Map*`
  extension, then call the one-liner from the host. Developer/dev-only helpers for the OpenID server live beside their
  siblings in [`NCode.Identity.Server/DevelopmentEnvironment/`](../../src/NCode.Identity.Server/DevelopmentEnvironment)
  (`AddDeveloperKeys`/`GetDeveloperKeyMode`, `AddDeveloperCors`/`UseDeveloperCors`) and are exposed through the server's
  turnkey surface so a host opts in with one call. Default to "it goes in the package"; only the genuinely host-specific,
  unsafe-to-default inputs called out below (database provider/connection string, production signing key) stay in
  `Program.cs`.
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
- 👁 **Build a protocol endpoint to the canonical shape** per [ADR-0086](../../docs/adr/0086-canonical-openid-endpoint-shape.md):
  a **thin** `IEndpointProvider` whose `HandleRouteAsync` only orchestrates — verb/content-type guard →
  `IClientAuthenticationService.AuthenticateClientAsync` → load a **strongly-typed** `OpenIdMessage<T>` request
  (`FooRequest.Load(env, formData)` with `KnownParameter`-backed properties, registered via `AddMessageFactory<T>()`;
  **never** hand-rolled `form.TryGetValue(...)`) → dispatch mediator commands → render. Validation is a
  `ValidateFooRequestCommand : ICommand` + `ICommandHandler` (High priority) that **throws**
  `errorFactory.X().WithStatusCode(...).AsException()`; each replaceable protocol step (subject resolution, grant
  issuance, the processing tail) is a `DoFooCommand : ICommand<FooDisposition>` + `ICommandResponseHandler` whose
  **default handler reports “unsupported/undecided”** (reuse `AuthenticateSubjectDisposition` where it fits) so a
  capability package replaces it via `TryAddSingleton`. A side-effecting **transport** (notification, back-channel
  delivery) stays an injectable service invoked **from** a handler, not a mediator command. Exemplars: the token,
  end-session, and CIBA ([ADR-0085](../../docs/adr/0085-ciba-grant.md)) endpoints; the device-authorization endpoint is
  the pre-convention outlier (backlog `F18`), **not** a template to copy.
- 👁 **A package registers its own services from `DefaultRegistration.cs`** (the class is **always** `DefaultRegistration`)
  using `extension(IServiceBuilder<TMarker>)` + `TryAdd*` so a consumer can override a default, per
  [ADR-0069](../../docs/adr/0069-registration-library-compile-time-marker-nesting.md). **The receiver marker is the
  compile-time prerequisite:** a package root's `AddXLibrary()` extends its prerequisite library's builder
  (`extension(IServiceBuilder<IdentityLibrary>) { AddOpenIdCoreLibrary() }`), and an **opt-in bolt-on capability**
  extends the marker of the library it augments (`AddLocalAccountManagement()` on `IServiceBuilder<OpenIdManagementLibrary>`,
  `AddInteractiveAuthentication()` on `IServiceBuilder<OpenIdAuthenticationLibrary>`) — **never** a naked
  `IServiceCollection`, so it is compile-time-impossible to add a capability without its framework. A library marker a
  cross-package bolt-on must name lives in that library's `.Abstractions` (a marker is an empty `readonly struct`
  implementing `IRegistrationMarker<TMarker>` — its type identity is the whole signal, with no `DisplayName` or
  `ConfigureMethod` data to keep in sync). **A second litmus governs which `.Abstractions`
  packages own a marker: a registerable library _layer_ does (minted by its `AddXLibrary()`), and a dependency-inverted
  _seam that other registrations build on_ owns a _presence marker_ minted by every implementation.** Two seams own one:
  the persistence framework owns `StoreManagerLibrary` (`NCode.Persistence.Abstractions`, minted by
  `AddEntityFrameworkCorePersistence`), and the local-account store owns `LocalAccountLibrary`
  (per [ADR-0060](../../docs/adr/0060-local-account-one-store-seam-and-dependency-inverted-authentication.md)): each store
  registration (`AddLocalAccountEntityFramework`, `AddAspNetIdentityLocalAccountStore`, a custom store) receives
  `IServiceBuilder<StoreManagerLibrary>` and **returns** `IServiceBuilder<LocalAccountLibrary>`, and the account bolt-ons
  take it as a **required `IServiceBuilder<LocalAccountLibrary>` parameter** (their receiver stays the OpenId library they
  extend), so a capability cannot even be written without a
  store (the accepted cost: a custom store must mint/return it too). Pure contract/DTO abstractions, cross-library helpers,
  foundational marker-less infrastructure, and **the interactive credential verifier** (registered after its surface, so
  there is no builder to thread and a missing verifier surfaces at resolution) own **none** — a compile-time seam marker
  applies exactly when the seam is registered **before** its dependent. A **larger package organized into subfolders**
  registers each folder from its own `DefaultRegistration.cs` and may introduce **additional registration markers — most
  often `internal`, though not required to be — to scope a nested folder**, created with `builder.NewBuilder<TChildMarker>()`
  (e.g. `Endpoints` → `OpenIdAuthenticationEndpoints`). **The litmus for a naked `IServiceCollection` receiver is
  "would this registration do the right thing registered alone?":** a registration that **replaces, hooks, or consumes
  another marker-bearing library's services** is a bolt-on and **must** gate on that library's `IServiceBuilder<TMarker>`
  (e.g. the dev-host `AddDeveloperKeys` / `AddDeveloperInteractiveSeed`, which replace the server's tenant factory and
  hook its command handlers, extend `IServiceBuilder<IdentityServer>` — never the bare collection). Only a chain **root**
  with no prerequisite (`AddIdentityLibrary`, `AddSecretsLibrary`, `AddEventServices` which mints `EventsLibrary`, and the
  persistence-framework root `AddEntityFrameworkCorePersistence` which mints `StoreManagerLibrary`), a **generic utility
  helper**, or **foundational marker-less cross-cutting infrastructure** (`AddPropertyBag`, `AddDataProtection`) takes a
  naked `IServiceCollection`; a dependency-inverted store instead receives its framework seam's builder
  (`IServiceBuilder<StoreManagerLibrary>`), and the event opt-ins (`AddEventHandler`, `AddBackgroundEventDelivery`) receive
  `IServiceBuilder<EventsLibrary>`. A **cross-library helper** invoked from more
  than one library's builder (so it cannot name a single marker) takes the **non-generic `IServiceBuilder`** receiver —
  stronger than a naked collection (the caller must hold _some_ library builder) but library-agnostic (e.g.
  `AddMessageFactory<TMessage>`, `AddEndpointProvider<T>`, `AddEndpointGroup<T>`); never pin such a helper to one marker
  or drop it to a naked collection. Register a stateless capability as a singleton; register
  a long-running loop as an `AddHostedService<T>` over a `BackgroundService`. Wire-up stays out of the type that does
  the work, so the work stays constructor-injectable and testable.
- 👁 **The package that owns a consumer registers that consumer's ambient dependencies**, so composing the server
  (`AddIdentityServer()`) is turnkey and a host wires **no** framework plumbing. Register the framework services your
  endpoints/handlers depend on (routing, `IHttpClientFactory`, `AddAuthentication()`, a default `IIdGenerator<long>`,
  …) from the owning package via `TryAdd*`, as an **overridable default** rather than a host requirement; a host
  overrides by registering its own before the composition call (or last-wins after). Only genuinely host-specific,
  unsafe-to-default inputs stay host-only — the database provider/connection string and the production signing key (per
  [ADR-0002](../../docs/adr/0002-ephemeral-development-keys.md)). Dead host wiring that only "works" by accidentally
  pulling in a framework service (for example `AddControllers` dragging in authentication) is a smell: register the
  real dependency explicitly from the package that needs it.
- 👁 **A singleton service must be stateless — never memoize per-request state in an instance field.** A singleton is
  shared by every request, so a cached result on the instance (`_result ??= await …`) leaks one request's answer to all
  others — a correctness and security hole (a client-authentication result that sticks after the first call let any
  later request reuse it regardless of its credentials). Memoize per-request state on the **request** instead
  (`HttpContext.Items`, the `OpenIdContext`), or keep the service genuinely stateless. Making the service scoped is not
  a fix when a singleton consumer captures it (captive dependency re-singletons it), so prefer request-scoped storage.
- 👁 **Name an `IServiceCollection` `serviceCollection`** (parameter, local, or extension receiver) and the builder
  property `ServiceCollection` — never `services` or `service`. The canonical `NCode.Registration` builder sets this
  shape; keep it uniform everywhere, including tests.

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
- ✅ **EF Core model changes ship with their migration.** Any edit to an entity, an `IEntityTypeConfiguration`, or a
  model contributor that changes a Playground context model (`OpenIdDbContext` or `DeveloperDataProtectionDbContext`)
  MUST be accompanied by a new migration (`dotnet ef migrations add <Name>` into the
  `NCode.Identity.OpenId.Playground.Database` project). The Definition of Done runs
  `dotnet ef migrations has-pending-model-changes` for both contexts and fails on un-migrated model drift, so it is
  caught pre-push and in CI, not at runtime. The dev database is Postgres applied by the migration worker, so migrations
  are authoritative — the gate is the reminder. See
  [ADR-0073](../../docs/adr/0073-postgres-dev-database-and-aspire-migration-worker.md).
- ✅ **Raw SQL in the EF model must quote identifiers.** Any hand-written SQL fragment in the model (`HasFilter`,
  `HasComputedColumnSql`, `HasDefaultValueSql`, …) MUST double-quote column/table names (e.g.
  `HasFilter("\"NormalizedDomainName\" IS NOT NULL")`). Postgres folds unquoted identifiers to lower case (the schema
  uses PascalCase), so an unquoted fragment compiles and passes the SQLite tests but fails at runtime against Postgres
  (`column "…" does not exist`). The drift gate cannot catch this — only applying the migration to Postgres does.
- ✅ The whole gate is [`build/dod.ps1`](../../build/dod.ps1) — the single Definition of Done that CI runs.
- ⏳ **Local inner loop: `./build/dod.ps1 -Fast`.** It parallelizes project builds and test assemblies and skips the
  non-correctness stages (coverage collection + packing) for a quicker edit-run cycle, while keeping CSharpier,
  analyzers, and compile warnings-as-errors. It is the **local pre-push gate** (the push protocol's final check); the
  plain `./build/dod.ps1` is the CI-parity gate that CI runs.
- 👁 **Run the full `dod.ps1` (no `-Fast`, a clean Release build over the whole solution) before calling a structural
  change done —
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
- **Push store filtering into the DBMS.** A reader that needs a **filtered** subset of a store's entities calls a
  discrete, intent-named store method (`FindEnabledByScopesAsync`, `GetEnabledIdentifiersAsync`, …) that pushes the
  predicate to the database — never page the whole set via `GetPageAsync` and filter in memory. Keep the store contract
  ORM-agnostic (no `IQueryable` / `Expression` in the interface), project to only the columns the caller needs, and
  reserve `GetPageAsync` for genuine **unfiltered** pagination. See
  [ADR-0080](../../docs/adr/0080-store-query-methods-push-filtering-to-dbms.md).
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
  **Prefer `JsonElement.Parse(string)` (.NET 9+) when you only need a detached `JsonElement`** — it owns no
  `JsonDocument` to dispose and is the concise replacement for `JsonDocument.Parse(json).RootElement.Clone()` and for
  `JsonSerializer.Deserialize<JsonElement>(json)`; keep `JsonDocument` only when you parse UTF-8 bytes (no
  `JsonElement.Parse` byte overload) or genuinely enumerate within a scoped `using`.

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
- 👁 **Documentation describes the timeless present, never the change.** A `///` or `//` comment states what the code
  _is_ and _does_, not how it came to be: no temporal or change-relative wording (`now`, `new`, `previously`,
  `no longer`, `used to`, `renamed`/`moved`, `rather than before`), and it **never names a removed or renamed
  artifact** — referring to a deleted or superseded type/member by its old identifier (`IFooOldThing`) is itself
  temporal content. Describing an anti-pattern (_what not to do_ and _why_) is fine; just name the rejected shape by its
  **role**, not by a no-longer-existing identifier. The same discipline applies to ADR prose (Context/Decision/
  Consequences describe the end-state design and its rationale, not a diff). History lives in Git and the `CHANGELOG`;
  the text must read correctly to someone who never saw the prior version.
- 👁 **A `<summary>` tag is multi-line** — the opening tag, content, and closing tag on separate lines, even for a short
  summary; never collapse them onto one line.
- 👁 **A property summary follows the Microsoft convention** — open with **"Gets or sets"**, **"Gets"**, or **"Sets"**
  according to its accessors. An **`init`-only** property (`{ get; init; }`) opens with **"Gets"**: it is read-only
  after construction, so it reads as a get-only property, not "Gets or sets". Reserve "Gets or sets" for a genuinely
  mutable `{ get; set; }` property.
- 👁 **Prose spells protocol terms the spec way, not the .NET-type way.** In `///` and `//` text write **"JSON Web
  Token (JWT)"** (and likewise JSON/JOSE/OAuth/OpenID in their canonical casing); do not carry the `JsonWebToken` type's
  Pascal casing into documentation. This applies only to prose — a `<see cref="JsonWebToken"/>` or an identifier keeps
  its real casing.
- 👁 **No internal citations in `///` doc comments.** A `///` comment ships to consumers — it lands in the package's
  XML-doc file (IntelliSense) and, for any type on an HTTP API surface, is surfaced into the generated **OpenAPI**
  document (.NET's XML-comment integration turns a DTO/property `<summary>` into a schema `description`). So a `///`
  comment must read as a clean, self-contained **public** description: never an ADR number (`ADR-0011`), a
  `<see href="../../../docs/adr/…">` link (a broken relative path for a consumer), or an internal implementation name
  (a persistence column, an internal type). Keep the behavioral prose; drop the citation. When the _why_ is worth
  recording in code, put the ADR reference in a **`//` code comment** (which never ships) — this is what "link to the
  ADR rather than inlining the rationale" (file header) means: the link lives in `//`, the ADR doc, or the `CHANGELOG`,
  **never** in consumer-facing `///`.
- 👁 **A minimal-API handler's `///` reaches its OpenAPI operation only under specific conditions.** `<summary>` /
  `<remarks>` become the operation description and `<param>` / `<response code="NNN">desc</response>` annotate it, but
  only when **all** hold: (1) the handler's **defining** project references `Microsoft.AspNetCore.OpenApi` and calls
  `AddOpenApi(...)` (which turns on the per-assembly XML-comment source generator); (2) the handler method is
  `internal` or more visible — a `private` handler's XML is silently ignored; and (3) the endpoint carries **no** fluent
  `.WithSummary()` / `.WithDescription()` — fluent metadata makes the generator skip that operation entirely (use
  fluent **or** XML, never both). A `<response code="NNN">` only _describes_ a response entry that already exists (the
  default `200`, or one declared via `.Produces`); to add a non-200 entry (`400`/`401`/`204`/`302`…) call
  `.Produces(StatusCodes.StatusNNN)` / `.Produces<T>()` in the map and let the `<response>` attach its description.
- 👁 **OpenAPI document contributions belong in the composition layer, not the host.** The document describes the
  server's own endpoints, so register document transformers (security schemes, tag groups, request/response examples)
  in the composition root's `AddOpenApi(...)`, and expose the document and the API-reference UI through the server's
  turnkey map extension — a host never re-declares them. See
  [ADR-0046](../../docs/adr/0046-api-reference-openapi-document-and-scalar.md).
- 👁 **Never embed secret material in a served OpenAPI document or renderer configuration.** A renderer may pre-fill
  credentials for a one-click login, but pre-filling a **secret** bakes it into the served config where anyone who can
  load the reference page can read it — pre-fill a secret **only in Development**. The login flow itself (for example
  OAuth2 `client_credentials`) must be wired in **every** environment so the production cold-start/bootstrap path works;
  only the secret pre-fill is environment-gated. See
  [ADR-0046](../../docs/adr/0046-api-reference-openapi-document-and-scalar.md) and
  [ADR-0011](../../docs/adr/0011-secret-management-api.md).
- 👁 **A renderer builds a `x-www-form-urlencoded` body from the schema, not from examples.** Optional schema
  properties render unchecked and are omitted from the request, and a named `example` is not applied to form fields. To
  make a field pre-checked and pre-filled, a document transformer marks it `required` and sets its schema `Default`;
  the renderer's credential location must match the endpoint (the token endpoint reads `client_secret_post` from the
  **body**). See [ADR-0046](../../docs/adr/0046-api-reference-openapi-document-and-scalar.md).
- 👁 **Handle credential and secret bytes in a pinned, zeroed-on-return buffer.** Encode a password or secret into a
  `SecureMemoryPool<byte>` rental via `SecureEncoding.UTF8` (`NCode.Buffers`) — never a raw `ArrayPool<byte>` +
  manual `CryptographicOperations.ZeroMemory`, and never a plain `Encoding.UTF8.GetBytes` that leaves the plaintext
  lingering on the GC heap. Encode/decode **once** at the earliest boundary and thread the sensitive payload as
  `ReadOnlyMemory<byte>` through the (async) call chain rather than re-encoding a `string` in a leaf method. A secret
  that enters from configuration (already an unavoidable managed `string`) is supplied **pre-encoded** (Base64Url) and
  decoded straight into the secure buffer, so a raw process-memory snapshot does not expose the plaintext directly. See
  [ADR-0011](../../docs/adr/0011-secret-management-api.md).
- 👁 **Prefer `Span<byte>` / `ReadOnlyMemory<byte>` / `IBufferWriter<byte>` over `byte[]` for binary and key material.**
  A producer writes into a caller-supplied writer — `void Export…<TWriter>(ref TWriter destination) where TWriter :
  IBufferWriter<byte>, allows ref struct` (as in `SymmetricSecretKey.ExportPrivateKey` /
  `AsymmetricSecretKey.ExportSubjectPublicKeyInfo`) — instead of returning a freshly-allocated `byte[]`; inputs are
  `ReadOnlySpan<byte>` / `ReadOnlyMemory<byte>`. Rent a pooled writer with
  `BufferFactory.CreatePooledBufferWriter(isSensitive:)` (`NCode.Buffers` over `Nerdbank.Streams`), not a bare
  `ArrayBufferWriter<byte>` — `isSensitive: true` for secrets (pinned, zeroed `SecureMemoryPool`), `false` for
  non-secret bytes — and flatten a multi-segment `Sequence<byte>` to a contiguous span via `GetSpanLease(isSensitive:)`,
  never manual concatenation. This is the general convention for all byte-array/memory/buffer usage; existing `byte[]`
  surfaces migrate incrementally. See [ADR-0116](../../docs/adr/0116-memory-allocations-and-buffer-operations.md).
- 👁 **Cryptographic and numeric hot-path primitives are optimized aggressively; readability is secondary there.**
  Inner loops of the math/crypto primitives (field, scalar, and group arithmetic; encoders; constant-time helpers)
  should use every applicable trick — `[MethodImpl(MethodImplOptions.AggressiveInlining |
  MethodImplOptions.AggressiveOptimization)]`, manual unrolling of tiny fixed-count (e.g. 4/5-limb) loops,
  `stackalloc`, `MemoryMarshal`/`Unsafe` `ref`-based **zero-copy** reinterpretation (`MemoryMarshal.Cast<byte, T>`,
  `MemoryMarshal.AsBytes`, with a JIT-constant `BitConverter.IsLittleEndian` guard for portability), and bounds-check
  elision — even when the result is dense and harder to read, to minimize allocations and branches. **Two constraints override speed and are never traded away:** (1) constant-time
  code must not gain a data-dependent branch or memory access from any optimization; (2) correctness is pinned by
  test vectors / differential tests that must stay green. This is a deliberate, scoped exception to the clean-code
  rules above, limited to performance-critical primitives. See
  [ADR-0116](../../docs/adr/0116-memory-allocations-and-buffer-operations.md).

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
    | `14000`–`14999` | `NCode.Identity.Events`                             |

## 12. Configuration & options

- 👁 **Configuration is a strongly-typed options class** in an `Options/` folder, bound with
  `services.Configure<T>(configuration.GetSection(...))` — never `IConfiguration` read ad hoc deep in the code.
- 👁 **Inject `IOptions<T>` for start-up-fixed configuration and `IOptionsMonitor<T>` for values that may change at
  runtime.** An options class is a data type (§2): `sealed`, `init` / get-only members, concrete collections,
  correct-by-default (§2 — no knob weakens a security control below its safe baseline).
