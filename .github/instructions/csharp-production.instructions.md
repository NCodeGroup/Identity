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
- 👁 **Validate untrusted text as written, not only as parsed.** Anchor a validating regular expression with `\z`,
  never `$`: .NET's `$` also matches before a trailing newline, so `^[a-z]+$` accepts `"acme\n"`. When the value is an
  identifier compared byte for byte — an issuer, a redirect URI — check the raw string as well as the parsed `Uri`:
  `Uri` trims surrounding whitespace and reports an empty query (`?`) or fragment (`#`) as absent, so a string it
  accepts can still differ from the one clients must match (`TenantAddressRules.TryParseIssuer`). Carry such an
  identifier as a `string` wherever it is stored, serialized, compared, or delivered, and parse a `Uri` only to inspect
  a part: `Uri` equality ignores the case of the host, the user information, and the fragment, and `AbsoluteUri`,
  `ToString()`, `UriBuilder`, and `UriJsonConverter` normalize it (`myapp://callback` becomes `myapp://callback/`)
  ([ADR-0203](../../docs/adr/0203-redirect-uris-are-matched-exactly.md)).
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
- 👁 **A policy that a replaceable public service must apply is itself a public seam.** When a public interface a
  consumer may replace (`ITokenService`) has to enforce a security policy, put the policy behind its own public
  interface with an internal default (`IAccessTokenPolicyService`, `IClientJwtCredentialSelector`), so a replacement
  resolves the policy instead of reimplementing it or silently skipping it; keeping it internal makes the secure path
  unreachable for exactly the code that needs it. See
  [ADR-0158](../../docs/adr/0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md) and
  [ADR-0181](../../docs/adr/0181-every-issuer-of-a-jwt-addressed-to-a-client-resolves-one-public-credential-selector.md).
- 👁 **A token the server issued is validated only through `IIssuedTokenValidator`.** Never build
  `JwtValidationParameters` for an access token, ID token, or `id_token_hint` the tenant minted. The seam states the
  tenant's algorithm allow-lists, keys, `typ`, issuer, lifetime, and session check in one place, and hand-built copies
  are how introspection came to report ID tokens as active access tokens and ignore revoked sessions. A caller adds
  only what it owns — the audiences it requires and the decryption keys of the resource server it acts for — through
  `AccessTokenValidationOptions`. See
  [ADR-0167](../../docs/adr/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md).
- 👁 **Restrict a caller to some endpoints with opt-in endpoint metadata that fails closed.** When a kind of client may
  use only some endpoints, refuse it everywhere by default where it is resolved, and let each permitted endpoint opt in
  through route metadata (`AcceptResourceServerClients()` adds `IOpenIdEndpointResourceServerClientMetadata`, which
  `DefaultOpenIdClientResolver` checks). A per-endpoint deny check would be missing from the next endpoint someone
  adds, and a request with no endpoint, such as background work, must resolve nothing. See
  [ADR-0168](../../docs/adr/0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md).
- 👁 **Keep CORS off a front-channel endpoint with `WithOpenIdFrontChannel()`, never CORS metadata.** An endpoint the
  user agent navigates to (authorization, continue, end-session, the interactive surface) carries
  `IOpenIdEndpointFrontChannelMetadata`, and the CORS middleware is branched around it (`UseDeveloperCors`); the same
  call routes a CORS preflight to the endpoint, which refuses it, since routing's own 405 endpoint has no marker, and a
  preflight that selects no endpoint (an unmapped method) gets no policy either. Do not use `DisableCors()` /
  `DisableCorsAttribute` or any other `ICorsMetadata`: the endpoint middleware throws for an endpoint carrying CORS
  metadata when no CORS middleware ran, so it fails every request a production host without CORS serves. See [ADR-0109](../../docs/adr/0109-fapi-behavioral-rules-and-discovery-validation-details.md).
- 👁 **Extensibility is the default: a vocabulary is open unless it is provably closed.** The family exists to be
  extended — by consumers and by sibling packages — without editing it, so every set of named values (kinds, types,
  modes, scopes, formats, methods, reasons, statuses, plans) is **open** by default and modeled as:
  - **Names** — `public const string` values in a static class (`OpenIdSettingNames`, `OpenIdSettingScopes`,
    `OpenIdConstants.GrantTypes`), compared ordinally and documented as the **known** values, never as all of them.
  - **Registered data or strategies**, when a member carries behavior or relationships (a rule, a parent, an
    implementation) — a descriptor contributed through `ICollectionDataSource<T>` or a strategy collection from DI
    ([ADR-0001](../../docs/adr/0001-mediator-vs-dependency-injection-logic-classes.md)), resolved by name through a
    catalog that validates the whole set when it is composed (duplicate names, dangling references, cycles) — the
    pattern of `SettingDescriptor`, `SettingScopeDescriptor`, `ISecurityProfile`, and the client-authentication
    handlers. Input naming an unregistered member is **rejected** (the vocabulary of _registered_ values is closed,
    [ADR-0139](../../docs/adr/0139-setting-names-are-a-closed-vocabulary.md)), never ignored.

  An **`enum` is allowed only when the reviewer can state all three:** (1) the members are fixed by something that
  cannot grow without this library changing — a finite enumeration of an external spec that the library implements in
  full (`RequestObjectSource`), the outcome set of one library-owned operation's contract (`PasswordVerificationResult`,
  `OutboundHttpReadResult`), or a mirror of a framework type (`ObjectJson*Handling`); (2) no consumer, deployment, or
  sibling package could reasonably need another member — ask **"could a package that is not this one ever need to add
  one?"**, never "does this package own today's members?"; and (3) a new member would require a change to this library
  anyway. Whatever the representation:
  - 🚫 **No `[Flags]` for a vocabulary that may grow** — its bit space and the meaning of every combination are closed.
  - 🚫 **No relationships among members encoded in code.** A `switch`, a bit mask, or a hard-coded chain describing a
    vocabulary's hierarchy, compatibility, or order is lock-in even when the names are strings; make the relationship
    registered data and let the catalog derive it.
  - 🚫 **No exhaustive `switch` over an open vocabulary to choose behavior** — dispatch to the strategy registered for
    the name.
  - 🚫 **No trait inferred from a member's name.** A suffix or prefix test such as `EndsWith(".jwt")` is a
    hard-coded relationship too. The registered strategy declares the trait
    (`IAuthorizationResponseModeHandler.SecuredResponseMode`), and the catalog validates it when composed
    ([ADR-0201](../../docs/adr/0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md)).
  - A persisted or wire value is its string name, never an ordinal, so an older node that meets a newer member fails
    closed by name instead of misreading a number.

  The lesson behind the rule: setting scopes were first a `[Flags]` enum of the server, tenant, client, and resource
  server, with their cascade hard-coded in a `switch`, judged closed because the library owned all four entities — which
  shut out every extension that stores settings for an entity of its own. They are registered `SettingScopeDescriptor`
  data. See [ADR-0150](../../docs/adr/0150-vocabularies-are-open-unless-provably-closed.md).
- 👁 **No secret material on the public surface.**
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
  An `abstract override` member is recorded as `override abstract …`. `dotnet format analyzers` can rewrite a
  `PublicAPI.Unshipped.txt` it edits with CRLF line endings; the repository's files are LF, so restore LF before
  committing (`git diff --stat` shows the whole file changed when this happens).
- ⏳ **At most one overload per method name may carry optional parameters (`RS0026`).** The public-API analyzer fails
  the build when two overloads of the same name each declare a defaulted parameter (even when their other parameters
  differ in type, so there is no call ambiguity). When a family of overloads needs an optional tail argument (e.g. a
  `JsonSerializerOptions? jsonOptions`), do **not** give several of them `= null` defaults; express them as explicit
  no-argument / with-argument overload pairs (the trailing parameter is present but **not** defaulted), which reads
  unambiguously and satisfies the analyzer. `IJoseSerializer.Deserialize<T>` / `VerifyJws<T>` follow this shape.
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
  the purpose it serves (`KeyTypes`, `Collections`, `KeyMaterial`, `Algorithms`, `Stores`, `Pkce`, `OutboundHttp`,
  `ResourceServers`, `Cryptography`); if a bucket has accreted several unrelated purposes, split it (no `Logic`
  namespace remains in the family — see the backlog `D7` entry in [`CHANGELOG.md`](../../CHANGELOG.md)). A contract,
  its DTOs, and its `Default*` implementation share one purpose namespace across the `.Abstractions`/implementation
  pair, and that folder registers its own services (`AddPkceServices()`), never a catch-all registration. **An
  endpoint's own replaceable services live at that endpoint's root namespace** (`…Endpoints.Registration`, beside its
  handler and `DefaultRegistration`), and a service used by a single grant lives with that grant
  (`…Endpoints.Token.TokenExchange`) — never in a per-endpoint sub-bucket.
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
  the single most-repeated review miss, so treat it as a hard rule. The reference host and every other host must read
  as a flat list of turnkey calls (`serviceCollection.AddX(...)`, `app.UseX()`, `app.MapX()`) — **no** reusable logic,
  policy builders, config-section parsing, `new`-ing of options, loops, consts, or dev/test helpers inline. **Tripwire:**
  the moment host startup needs more than a single wiring call — an `if`, a `foreach`, a CORS/auth policy builder, a
  `new`, reading a configuration section — stop and move that logic into the **owning package** as an `Add*`/`Use*`/`Map*`
  extension, then call the one-liner from the host. Developer/dev-only helpers for the OpenID server live beside their
  siblings in [`NCode.Identity.Server/DevelopmentEnvironment/`](../../src/NCode.Identity.Server/DevelopmentEnvironment)
  (`AddDeveloperKeys`/`GetDeveloperKeysOptions`, `AddDeveloperCors`/`UseDeveloperCors`) and are exposed through the server's
  turnkey surface so a host opts in with one call. Default to "it goes in the package"; only the genuinely host-specific,
  unsafe-to-default inputs called out below (database provider/connection string, production signing key) stay in
  `Program.cs`. Kestrel's HTTPS defaults are one of them: `ConfigureHttpsDefaults` keeps only its last action, so a
  package that called it would silently replace, or be replaced by, the host's own TLS settings. A package never calls
  it; the host makes one `ConfigureKestrel(k => k.ConfigureHttpsDefaults(...))` call that holds every HTTPS default,
  such as the reference host's `AllowAnyClientCertificate()` for mutual TLS
  ([ADR-0202](../../docs/adr/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).
- ✅ Use the **C# 14 `extension` member syntax** (`extension(ReceiverType r) { … }` blocks), not classic
  `this`-parameter static methods — matching the `DefaultRegistration.cs` composition pattern
  (`extension(IServiceBuilder<IdentityLibrary> builder) { … }`).
- ✅ **One receiver-type per static class.** Multiple `extension(...)` blocks with different receiver types in one class
  trip `CA1708` — split them (which also fits one-type-per-file).
- 👁 **When two receiver interfaces are unrelated but a concrete type implements both, target their common base — do
  not write one overload per interface.** `IDictionary<,>` and `IReadOnlyDictionary<,>` don't derive from one another,
  yet a concrete `Dictionary<,>` implements both, so an overload on each makes every concrete-dictionary call site
  **`CS0121` ambiguous** — and splitting the two into separate classes does **not** help, because the ambiguity is
  between the methods, not their containers. Provide a **single** member on the common base
  (`extension(IEnumerable<KeyValuePair<TKey,TValue>>)`) and fast-path the dictionary receivers to an O(1) lookup inside
  (`switch` on `IReadOnlyDictionary<,>` / `IDictionary<,>`, else scan). Exemplar:
  `NCode.Json.ObjectGraphExtensions.TryGetValue<T>`.
- 👁 Keep null-guards on the receiver (extension members can be invoked on `null`).
- 👁 **Name the receiver in a receiver-argument exception even though `CA2208` objects.** When an `extension(...)` member
  throws `ArgumentException` about its receiver, still pass `nameof(receiver)` — the receiver _is_ the offending argument.
  `CA2208` does not yet treat an extension-block receiver as a parameter, so wrap only those throws in a tight
  `#pragma warning disable/restore CA2208` with a one-line reason; never drop the `paramName` to silence it.
- 👁 **Choose the DI shape deliberately** per [ADR-0001](../../docs/adr/0001-mediator-vs-dependency-injection-logic-classes.md):
  a **service** (`TryAddSingleton<IFoo, DefaultFoo>`) for a capability you call for an answer; a **strategy collection**
  (`TryAddEnumerable`) for many implementations the caller iterates or selects; the **mediator** (`ICommandHandler<T>`)
  only for a genuine pipeline seam where the sender must stay ignorant of the handlers. `TryAddEnumerable`
  keeps one registration per implementation type, an instance's type included, so each built-in strategy of an open
  vocabulary is its own class (the redirect URI kinds); several instances of one class keep only the first.
- 👁 **A relationship between a client's settings is a registered coherence rule.** When a value a client registers must
  be permitted by another of its effective settings, add an `IClientCoherenceRule` with `TryAddEnumerable`; every entry
  point that stores a client and every tenant or server settings change checks it through `IClientCoherenceValidator`;
  never add the check to one caller
  ([ADR-0206](../../docs/adr/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)).
- 👁 **Options validation never constructs a strategy that has dependencies.** `IOptions<OpenIdOptions>.Value` runs
  every `IValidateOptions<OpenIdOptions>`, which read the setting-descriptor catalog, so every setting data source, and
  every catalog it consults to name a domain (the client-authentication method descriptors, the redirect URI kinds, the
  security profiles), is built from dependency-free registrations; resolving a strategy with dependencies there builds
  its whole graph inside options validation and deadlocks composition when a constructor on that path reads `Value`.
  When a vocabulary's members are strategies, register a descriptor beside each one and check at composition that they
  correspond
  ([ADR-0207](../../docs/adr/0207-client-authentication-methods-are-named-by-registered-descriptors.md)).
- 👁 **Mediator handlers run in descending `ISupportMediatorPriority` order** — `DefaultMediatorPriorities.High` (100)
  first, `Low` (-100) last. When one handler depends on another's effect, state it with priorities, never with
  registration order. A handler that observes or repairs state for the rest runs `High` (the default validators, the
  development secret pruner), and a handler that consumes what the others leave runs `Low` (tenant key provisioning,
  [ADR-0169](../../docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md)).
- 👁 **A change other replicas must observe is announced, not only applied locally.** An in-process cache or
  materialized object is invalidated on every replica through `IReplicaBroadcaster`, with a registered
  `IReplicaBroadcastHandler` per channel. Apply the change locally first, publish after, and log rather than throw a
  failed publish once the write has committed
  ([ADR-0205](../../docs/adr/0205-replicas-announce-changes-through-a-broadcast-port.md)).
- 👁 **Work proportional to the deployment never runs inside a request.** A pass over every tenant or every client
  reads a settings projection (`GetSettingsPageAsync`, untracked, no secrets), skips when the change cannot affect it,
  and when it must act on each tenant it runs as an `IJobDispatcher` job (R21, R50).
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
- 👁 **Protocol endpoints are served under the tenant route.** The OpenID protocol group's prefix is the configured
  tenant route (`/{tenantId}`, a literal path, or empty), so an endpoint's own route is relative to the tenant; never
  hard-code an absolute protocol path into a published URL. Generate every URL that leaves the process with
  `GetTenantUriByName`, which takes the authority from the tenant and carries the tenant route's values over. Never
  hand the request's live `RouteValues` to a template binder — it consumes them; bind a copy. See
  [ADR-0138](../../docs/adr/0138-protocol-endpoints-are-served-under-the-tenant-route.md).
- 👁 **A client authenticates only with its registered method, and its type is that one fact.** The registered
  `token_endpoint_auth_method` (RFC 7591 §2; `client_secret_basic` when none is registered) is the only method that
  authenticates a client at every endpoint that authenticates clients, and `token_endpoint_auth_methods_supported` is
  only the server/tenant/profile **ceiling** over it — never a list of alternatives. Authenticate a client only through
  `IClientAuthenticationService`, which enforces this; a new `IClientAuthenticationHandler` authenticates only a client
  registered for its method (the `CommonClientAuthenticationHandler` base does). Anything that provisions a client
  registers its method explicitly — a public client registers `none`. The authorization endpoint **identifies** the
  client by `client_id` and never authenticates it, so a rule there reads the type the client is registered as —
  `IsRegisteredAsConfidential` — and the same validation reads the same fact when it runs at the pushed authorization
  request endpoint; an endpoint that authenticated the client (the token endpoint redeeming a grant) reads
  `OpenIdClient.IsConfidential`, which agrees with it. Never treat a client registered as confidential as having
  authenticated. See [ADR-0145](../../docs/adr/0145-a-client-authenticates-only-with-its-registered-method.md) and
  [ADR-0144](../../docs/adr/0144-authorization-codes-are-bound-to-pkce-by-default.md).
- 👁 **Bind configuration strictly and validate it when the host starts.** An options type bound from configuration is
  bound with the binder's `ErrorOnUnknownConfiguration` (through `NCode.Registration`'s `BindStrictly`, which names the
  section in the error) and registered with `ValidateOnStart`; a rule the binder cannot express is an
  `IValidateOptions<T>`. A key that maps to no option must stop the host — a misspelled or misplaced key is otherwise
  indistinguishable from an absent one. Bind a whole section rather than reading single keys (`configuration["A:B"]`),
  which   leave a misspelled sibling unnoticed; bind eagerly only when the value decides what gets registered. A collection
  option the binder fills starts **empty**: the binder appends the configured values to an array or list that already
  holds some, so a default set in the initializer can never be narrowed by configuration — apply the default when
  nothing is configured instead. A host's own identifiers are not a library default at all: a collection that would
  default to them (a seeded client's id and redirect URIs) has none, and the host's environment overlay declares the
  entries beside the configuration that publishes them
  ([ADR-0146](../../docs/adr/0146-a-development-seeder-registers-only-the-clients-its-configuration-declares.md)).
  Each section
  has one owner that declares its whole shape; an extension binds its own section rather than adding keys to one the
  server owns. Validate only what is in force (for example the selected tenant strategy's options), since a JSON overlay
  cannot delete a lower layer's key. **Validate a value the way it will be used:** a lenient parser can turn a typo into
  a different value instead of an error (`IPAddress.TryParse("10.0.0")` is `10.0.0.0`; `IPNetwork.TryParse` masks
  `10.0.0.4/8` to `10.0.0.0/8`; `Uri.TryCreate(value, UriKind.Absolute, …)` accepts an implicit file path — `/path` on
  Unix, `C:\path` on Windows — and trims surrounding whitespace, so an absolute URI must also start with its own
  `scheme:` and contain no whitespace), and a value compared verbatim (a CORS origin) must be in the form it is compared
  in — reject anything that parses to something other than what was written. A setting the server cannot honor in an
  environment (a development-only section in Production) is a startup error, not an inert key. See
  [ADR-0137](../../docs/adr/0137-configuration-binds-strictly-and-is-validated-at-startup.md),
  [ADR-0139](../../docs/adr/0139-setting-names-are-a-closed-vocabulary.md), and
  [ADR-0140](../../docs/adr/0140-setting-values-are-validated-against-their-domain.md).
- 👁 **Setting names are a closed vocabulary.** Every setting a server, tenant, client, or resource server carries has a
  registered `SettingDescriptor`, contributed through `ICollectionDataSource<SettingDescriptor>` by the package that
  reads or writes it; never read or write a setting through an ad-hoc `new SettingKey<T>("…")` without a descriptor.
  Settings are rejected wherever they enter — configuration on start, management writes (`ISettingSerializer.ValidateSettings`
  → `400`), and stored documents on read (fail closed) — so retiring, renaming, narrowing the scopes of a setting, or
  narrowing its domain ships with a data migration
  of the stored documents. That migration is an EF migration in `NCode.Identity.OpenId.Host.Database`
  (`dotnet ef migrations add`, then hand-written SQL in `Up`) that rewrites **every** settings table (`Servers`,
  `Tenants`, `Clients`, `ResourceServers`); it **translates** the setting's meaning into the current vocabulary rather
  than dropping it (dropping a restriction silently loosens the document), leaves a document whose value was already
  invalid untouched (it keeps failing closed), and gives each rewritten row a new `SettingsConcurrencyToken`. A value
  stripped from a list because the domain narrowed never leaves the list unset: an emptied list stays `[]`, which
  permits nothing, while an unset one is unrestricted (ADR-0010). The model
  drift gate cannot see a data migration, so verify it by applying it to a throwaway Postgres over seeded documents
  ([ADR-0142](../../docs/adr/0142-listed-response-types-are-the-only-front-channel-token-control.md) and the
  `RetireUnsafeJoseSettingValues` migration of
  [ADR-0155](../../docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md) are worked
  examples): run the Postgres image the AppHost uses (the `Aspire.Hosting.PostgreSQL` default tag) in a container on
  an unused port, `dotnet ef database update <previous migration> --connection "<that container>"` (the design-time
  factory's own connection string is a placeholder), seed rows with `psql`, update to the new migration and inspect
  them — a rewritten document and its new token, an untouched control row — then revert with
  `database update <previous migration>` to exercise `Down`, and remove the container. A value that is metadata
  rather than a runtime control (RFC 7591 `client_name`, `contacts`)
  is still a registered setting. See [ADR-0139](../../docs/adr/0139-setting-names-are-a-closed-vocabulary.md).
- 👁 **A setting declares its domain, not only its type.** Give every descriptor whose type admits values the server
  would not honor a `Rule` (`SettingRules.OneOf`/`EmptyOr`/`AtLeast`/`Between`/`Each`/`Must`): an enumerated string is a
  `OneOf`, a lifetime or interval is at least one second, a timeout is positive, a list applies `Each` to its values, and
  a domain defined by registered strategies (security profiles, algorithms, client-authentication handlers) consults
  them with a `Must` predicate or a `OneOf` built from their names. Check a shape the way the runtime uses the value (a
  URI written with its scheme, a host exactly as `Uri.Host` reports it). The rule is enforced at every entry point
  alongside the name and type — configuration, management writes, dynamic client registration and seeders (validate the
  document before storing it), and stored documents (fail closed); it is **not** checked by `ISettingCollection.Set`, so
  a code-set value is trusted and a descriptor's `Default` must satisfy its own rule (a composed-server test pins it).
  A protocol value whose parts are unordered — a `response_type` such as `code id_token` — is a **whole value** stored,
  advertised, and compared in **one canonical form** (parts in ordinal order, single-space separated), which the rule
  enumerates; wire input is canonicalized at the protocol boundary before it is compared, never by a lenient setting
  reader, and is never split back into its parts to be enforced piecewise.
  See [ADR-0140](../../docs/adr/0140-setting-values-are-validated-against-their-domain.md) and
  [ADR-0141](../../docs/adr/0141-response-types-are-whole-values.md).
- 👁 **A security profile binds every scope below the one that selects it.** Outside the live providers, resolve a
  scope's effective settings only through `IEffectiveSettingsResolver`. Use `Resolve(parent, scope, document)`, and
  `ResolveServerAsync` for the server's committed settings. Never hand-merge a deserialized document, and never take
  server settings from this replica's poll for a seed or a check. Only the server factory and the root settings source
  call the catalog directly
  ([ADR-0204](../../docs/adr/0204-a-scopes-effective-settings-are-resolved-by-one-service.md)).
  `security_profiles` accumulates down the cascade (`Union`), so enforcement evaluates the requirements of every profile
  in the effective settings and never picks "the" profile. A profile contributes its ceiling through
  `ISecurityProfileCeilingBuilder`, which accepts only a setting whose merge is a lattice operation
  (`SettingDescriptor.HasLatticeMerge`); a setting a profile must constrain gets a lattice merge, never a projection
  (`Replace`/`Keep`), which a child would override or which would ignore the ceiling. See
  [ADR-0148](../../docs/adr/0148-security-profiles-accumulate-down-the-settings-cascade.md).
- 👁 **A setting declares the scopes it applies to.** Every descriptor sets the required `AppliesTo` — the names of the
  scopes whose effective settings the runtime reads it from (`OpenIdSettingScopes.Tenant` for discovery and the
  tenant-wide services, `.Client` for the protocol endpoints, `.ResourceServer` for the bounds a resource server puts on
  the access tokens issued for it; a discoverable setting always includes the tenant). Scopes are registered data, not a fixed set: a
  `SettingScopeDescriptor` (a name and its parent) contributed through `ICollectionDataSource<SettingScopeDescriptor>`,
  so a package that stores settings for an entity of its own registers that entity's scope beneath the scope it
  inherits from and declares its settings for it. A document may carry a setting only at a scope that applies it or
  cascades into one that does (`ISettingScopeCollection.AppliesWithin`), so every entry point passes the document's
  scope name (`ISettingSerializer.ValidateSettings`/`DeserializeSettings` and `ISecurityProfileCatalog.Expand`), and a
  management effective-settings preview shows only the settings that apply within its scope. **Read a per-client
  control from the client's effective settings** (`openIdClient.Settings`), never from
  `openIdContext.Tenant.SettingsProvider` — the tenant's collection ignores the client's narrowing and the ceilings of
  the profiles the client selects. Adding a reader at a new scope widens `AppliesTo`; narrowing it ships with a data
  migration that removes the setting from the documents of the scopes it leaves. See
  [ADR-0149](../../docs/adr/0149-settings-declare-the-scopes-they-apply-to.md).
- 👁 **Sibling scopes combine; a cascade merges.** When two scopes that are not parent and child govern one operation —
  an access token is bound by its client **and** every resource server it targets — join their effective settings with
  `IReadOnlySettingCollection.Combine`, never `Merge`: it accepts only lattice merges, so the result binds both and does
  not depend on order, and it throws on a projection. A setting that applies to resource servers therefore merges with
  a lattice operation (a composed-server test pins it). Read every access-token setting from the token's governing
  settings (`AccessTokenPolicy.Settings` from `IAccessTokenPolicyService`), never from `openIdClient.Settings` alone,
  which ignores the targeted resource servers' bounds; and never fall back to a weaker or different credential when the
  combined bounds cannot be met — refuse (`invalid_request` when the request can avoid it, `server_error` when the
  configuration cannot be satisfied). See
  [ADR-0158](../../docs/adr/0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md).
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
- 👁 **Correctness never depends on process-local state.** The host runs as several interchangeable, disposable
  replicas ([ADR-0134](../../docs/adr/0134-cloud-native-operating-principles.md)), and any replica may serve the next
  request. State that must outlive a request or be seen by another replica goes to the database or to the shared-state
  port; in-memory state (`IMemoryCache`, static or singleton fields) is only for data that can be rebuilt on a miss, and
  must tolerate eviction and restarts. Never write durable data to the local filesystem.
- 👁 **A single-use or freshness check records into `IExpiringKeySet`, never a get-then-set.** A replay guard (a
  `jti`), a server-issued nonce, or any "accept once" record is an atomic add-if-absent on the shared-state port
  (`NCode.Identity.SharedState.Abstractions`), whose adapter the composition root chooses — in-process for one replica,
  Valkey for several. `IDistributedCache` cannot express it: a read followed by a write lets two concurrent requests
  both pass. Build the key with the tenant and a digest of the length-prefixed, request-supplied components
  (`ExpiringKeys.Create`), so tenants sharing a store never collide and an attacker-supplied value cannot make a key
  ambiguous or unbounded. A library never registers a host-wide cache. See
  [ADR-0164](../../docs/adr/0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md).
- 👁 **A count every replica must agree on increments `IExpiringCounterSet`, never a get-then-set.** A rate limit or a
  quota counts with the port's atomic `IncrementAsync`, which starts a counter for one fixed window; a read followed by
  a write loses concurrent requests. Read a count with `GetAsync` only for a counter this request does not add to (the
  previous window). Count what is requested, not what later succeeds: counting only an outcome needs a read before the
  work and an increment after it. See
  [ADR-0196](../../docs/adr/0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) and
  [ADR-0198](../../docs/adr/0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md).
- 👁 **Public metadata is unmetered, and a limit an endpoint enforces refuses through `RateLimitErrors.Refuse`.** An
  endpoint any anonymous caller or cache fetches (discovery, the JSON Web Key Set) is marked `WithOpenIdMetered(false)`,
  so it spends only the per-minute limits, never the tenant's daily quota or billable usage. `RateLimitEndpointFilter`
  records usage after the endpoint runs, so a `429` raised inside an endpoint must come from `RateLimitErrors.Refuse`,
  which marks the request refused; any other path bills the refused request. See
  [ADR-0198](../../docs/adr/0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) and
  [ADR-0199](../../docs/adr/0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md).
- 👁 **Name an `IServiceCollection` `serviceCollection`** (parameter, local, or extension receiver) and the builder
  property `ServiceCollection` — never `services` or `service`. The canonical `NCode.Registration` builder sets this
  shape; keep it uniform everywhere, including tests.

## 5. Formatting & build gates

- ⏳ **CSharpier** formatting (`dotnet csharpier check .`, wired into [`build/dod.ps1`](../../build/dod.ps1)). Run
  `dotnet csharpier format .` before committing, but always via the **repo-pinned** tool (`dotnet tool restore` first) —
  a globally-installed CSharpier can rewrite a constructor-initialized collection (`new T(args) { … }`) into the C# 14
  **preview** `[with(args), …]` collection-expression-arguments form, which fails the build under `LangVersion=latest`
  (CS8652). The gate is `dotnet csharpier check .` and it accepts the constructor form; if `format` ever emits
  `[with(…)]`, revert to the constructor form. CSharpier 1.x formats XML too; generated output is excluded via
  [`.csharpierignore`](../../.csharpierignore). The house style CSharpier enforces: **file-scoped namespaces**,
  4-space indentation, and a **trailing comma** on every multi-line object / collection / enum member list. Line
  endings are **LF** ([`.gitattributes`](../../.gitattributes), [`.editorconfig`](../../.editorconfig)), and the check
  fails a file with CRLF endings ("different line endings than formatting it would result in") — which a file created
  on Windows by a tool that ignores `.editorconfig` gets; `dotnet csharpier format <file>` fixes it.
- ⏳ **Code-style analyzers run on build.** `EnforceCodeStyleInBuild` makes the `IDExxxx` rules execute during
  `dotnet build`; combined with warnings-as-errors, rules set to `warning` in [`.editorconfig`](../../.editorconfig)
  become build errors on shipping code.
- ✅ **No unused `using` directives** — `IDE0005` is raised to `warning` solution-wide in
  [`.editorconfig`](../../.editorconfig), so an unused `using` fails the build. It is hidden by default under
  `AnalysisLevel` `latest-recommended`, and Roslyn only evaluates it when `GenerateDocumentationFile` is `true`
  (dotnet/roslyn#41640). Clean violations with `dotnet format style --diagnostics IDE0005`, then re-run CSharpier.
- ✅ **Banned APIs** (`Microsoft.CodeAnalysis.BannedApiAnalyzers`, `RS0030`) from
  [`BannedSymbols.txt`](../../BannedSymbols.txt) — no `Newtonsoft.Json` (use `System.Text.Json`), no legacy
  `JwtSecurityTokenHandler` / `JwtSecurityToken` (use `JsonWebTokenHandler`), and no
  sync-over-async. Add house rules to that file as they emerge.
- ⏳ **Warnings-as-errors in _every_ project — including tests and the host projects** — and **NuGet vulnerability
  advisories (`NU190x`) as errors in CI**. See
  [ADR-0127](../../docs/adr/0127-warnings-are-errors-in-every-project.md): the criterion is "code we maintain", not
  "code we ship". Relax an individual rule via a justified [`.editorconfig`](../../.editorconfig) entry or a tight
  `#pragma`; 🚫 never by setting `TreatWarningsAsErrors=false`.
- ✅ **EF Core model changes ship with their migration.** Any edit to an entity, an `IEntityTypeConfiguration`, or a
  model contributor that changes a host context model (`OpenIdDbContext` or `DeveloperDataProtectionDbContext`)
  MUST be accompanied by a new migration (`dotnet ef migrations add <Name>` into the
  `NCode.Identity.OpenId.Host.Database` project). The Definition of Done runs
  `dotnet ef migrations has-pending-model-changes` for both contexts and fails on un-migrated model drift, so it is
  caught pre-push and in CI, not at runtime. The dev database is Postgres applied by the migration worker, so migrations
  are authoritative — the gate is the reminder. `dotnet ef migrations add` writes its files with a UTF-8 BOM and
  re-saves `OpenIdDbContextModelSnapshot.cs` even when the model is unchanged; the repository's files are BOM-less
  (`charset = utf-8`), so strip the BOM from the generated migration and revert a snapshot whose only diff is the BOM.
  See [ADR-0073](../../docs/adr/0073-postgres-dev-database-and-aspire-migration-worker.md).
- ✅ **A migration only expands the schema.** The previous release serves while a migration runs and after a rollback,
  so a migration adds tables, nullable or defaulted columns, and indexes, and never drops, renames, or tightens what the
  previous release reads. `ExpandOnlyRuleTests` fails any other migration in the Definition of Done. A deliberate
  contract step, shipped a release after its expansion, carries `[ContractMigration("why")]`. Hand-written code in
  `NCode.Identity.OpenId.Host.Database` stays outside `Migrations/`, which `.editorconfig` treats as generated code. See
  [ADR-0184](../../docs/adr/0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md).
- ✅ **Raw SQL in the EF model must quote identifiers.** Any hand-written SQL fragment in the model (`HasFilter`,
  `HasComputedColumnSql`, `HasDefaultValueSql`, …) MUST double-quote column/table names (e.g.
  `HasFilter("\"NormalizedDomainName\" IS NOT NULL")`). Postgres folds unquoted identifiers to lower case (the schema
  uses PascalCase), so an unquoted fragment compiles and passes the SQLite tests but fails at runtime against Postgres
  (`column "…" does not exist`). The drift gate cannot catch this — only applying the migration to Postgres does.
- ✅ The whole gate is [`build/dod.ps1`](../../build/dod.ps1) — the single Definition of Done that CI runs.
- 👁 **`dod.ps1` prints its stage markers and the final `Definition of Done: PASSED` verdict via `Write-Host`,** which
  bypasses the PowerShell pipeline. `./build/dod.ps1 2>&1 | Out-File run.log` therefore captures the nested tool output
  but **silently drops the verdict line**, making a passing run look inconclusive. To capture everything, tee the
  console stream (`./build/dod.ps1 *>&1 | Tee-Object run.log`) or just read the verdict off the console.
- ⏳ **Local inner loop: `./build/dod.ps1 -Fast`.** It parallelizes project builds and test assemblies and skips the
  non-correctness stages (coverage collection + packing) for a quicker edit-run cycle, while keeping CSharpier,
  analyzers, and compile warnings-as-errors. It is the **local pre-push gate** (the push protocol's final check); the
  plain `./build/dod.ps1` is the CI-parity gate that CI runs.
- 👁 **Never run two `dotnet build` / `dotnet test` invocations concurrently in one clone.** They share every project's
  `obj/` output, and Nerdbank.GitVersioning rewrites `obj/<config>/<tfm>/<Project>.Version.cs.new` on each build, so
  concurrent runs fail with `MSB4018` (the file is in use by another process) — a spurious build error, not a code
  defect. Run test projects one after another, or let `./build/dod.ps1 -Fast` parallelize within a single invocation.
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
- **Read `JsonElement` members with the `NCode.Json.JsonElements` extension methods, not hand-rolled accessors.** Use
  `TryGetPropertyValue<T>` / `TryGetValue<T>` — which convert by `ValueKind` and never throw — in place of a
  `TryGetProperty(name, out var e) + e.ValueKind == JsonValueKind.X + e.GetString()/GetInt64()/…` chain (the bare
  `GetInt64()`/`GetString()` form also throws on a mismatched kind); `IsNullOrUndefined()` in place of a bare
  `ValueKind == JsonValueKind.Null` absence check; and `EmptyObject` / `OrEmptyObject()` in place of re-parsing `"{}"`.
  Reference the dependency-free **`NCode.Json`** package — the shared home for these helpers
  ([ADR-0056](../../docs/adr/0056-abstractions-split-into-framework-free-tiered-packages.md)).
- **Parse a date/time string with `NCode.Json.DateCoercion`, never a hand-rolled `DateTime(Offset).TryParse`.**
  `TryParseDateTimeOffset` / `TryParseDateTime` are the family's single lenient, invariant-culture date policy (zone-less
  → UTC, explicit offset honored, an out-of-range instant fails instead of clamping), so every package accepts the same
  formats and produces the same values. Apply it only where the value is **declared** to be a date (an explicit claim
  value type, a date-typed target) — **never** to sniff whether an arbitrary string "looks like" a date: lenient parsing
  turns `"1.2"` or `"10:30"` into dates and would rewrite them. A decoded JSON string claim stays a verbatim
  `ClaimValueTypes.String`. See [ADR-0121](../../docs/adr/0121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md).
- ✅ **Read an RFC 7519 `NumericDate` (`exp`, `nbf`, `iat`, `auth_time`, a Unix-seconds parameter) strictly and
  range-guarded** — `JsonElement.TryGetNumericDate` / `TryGetPropertyNumericDate` for JSON (a number only, integer or
  fractional; never a string), `DateCoercion.TryParseNumericDate` for a claim value or parameter text, and
  `DateCoercion.TryFromNumericDate` for a number already in hand. They return `false` instead of throwing, so wire input
  can never fault the caller; `DateTimeOffset.FromUnixTimeSeconds`/`FromUnixTimeMilliseconds` are **banned** (`RS0030`)
  because they throw on out-of-range input. A time claim that is **present but invalid fails closed** — never read it
  as absent. Saturate (don't overflow) when adding a margin to an instant near `DateTimeOffset.MaxValue`. See
  [ADR-0123](../../docs/adr/0123-numeric-date-is-read-strictly-and-range-guarded.md).
- **Stamp an instant that a JWT or a stored grant carries with `NCode.Time`'s whole-second helpers.** Use
  `TimeProvider.GetUtcNowWithPrecisionInSeconds()` (or `GetTimestampWithPrecisionInSeconds()` for ticks) and
  `WithPrecisionInSeconds()`, so a stored time equals the `NumericDate` it round-trips through. Reference the
  dependency-free **`NCode.Time`** package, the home for general time helpers. A helper with a method body never lives
  in an `*.Abstractions` package. See [ADR-0188](../../docs/adr/0188-general-time-helpers-live-in-a-dependency-free-package.md).
- **Bound a received JWT's lifetime through the lifetime requirement, never with ad-hoc `exp` arithmetic.** Cap a
  client-supplied JWT (assertion, request object) with `ValidateTokenLifeTime(o => o.MaxLifetime = …)`, fed from a
  `Min`-merged `*_max_lifetime` setting; it measures from the earliest asserted `iat`/`nbf` (or now plus skew), so the
  client cannot game it. A replay record for such a JWT is retained until `exp` plus the **effective** `clock_skew`, not
  a fixed margin. See [ADR-0124](../../docs/adr/0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md).
- 👁 **Validate a received JWT only through `IJwtValidator`, stating its whole policy in the parameters.** Set
  `ValidSignatureAlgorithms` (and, for an encrypted token, `ValidKeyManagementAlgorithms`,
  `ValidContentEncryptionAlgorithms`, and `ValidCompressionAlgorithms = []`) from the governing setting, read at a trust
  boundary with `GetPermittedAlgorithms` so an **unset list permits nothing** — never `TryGetValue`-unset-means-all
  there. Name the decryption keys with `UseDecryptionKeys` (the recipient's keys, never the signer's); the validator
  never infers them. Never re-check `alg` by hand after validation in place of the allow-list — a check after decoding
  runs after the cryptography it must precede; an early check that only picks a precise error code is fine alongside it.
  The validator accepts only signed compact JWTs (a JWE only as a Nested JWT); there is no opt-in for `alg:none`. See
  [ADR-0154](../../docs/adr/0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md).
- 👁 **Protect a JWT addressed to a client through `IClientJwtCredentialSelector`.** It signs with the algorithm the
  client registered (`*_signed_response_alg`) and encrypts to a public key the client published, within the client's
  effective `*_values_supported` ceilings, and fails with `server_error` rather than substitute an algorithm. Never pick
  "the first listed algorithm" for a client-addressed token, never encrypt one to a tenant or shared key, and never
  compress a JWT the server produces (RFC 8725 §3.6). Add a new kind of client-addressed JWT as a new
  `ClientJwtProtectionSettings` profile. See
  [ADR-0155](../../docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md).
- 👁 **Bring a client's key in through `IClientKeyImporter`, and read a client's keys through `IClientKeyResolver`.**
  A client's public keys come from one source — the keys stored for it or the keys it publishes at its `jwks_uri`,
  never both — and every entry point (dynamic registration, the `jwks_uri` fetch, the management API) imports a key
  under one policy: a public key of a registered key type, no private or symmetric member, a `use` / `key_ops` / `alg`
  that agree with each other and the key, and a `kid` unique among the client's keys. Never call `ISecretImporter`
  directly for a client's key, never read `OpenIdClient.SecretKeys` for a client's public keys (it ignores the
  `jwks_uri`), and never use a key's `kid` as its secret identifier: a `kid` is the owner's name for the key
  (`PersistedSecret.KeyId`), while a secret identifier is globally unique. Match a key type by its JSON Web Key `kty`
  through `ISecretKeyTypeCatalog.TryGetByJwkKeyType`, never a `switch` over `kty` values. See
  [ADR-0161](../../docs/adr/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md).
- 👁 **Read a client's certificate only through `IClientCertificateResolver`.** It is the one place that knows where the
  deployment's certificate comes from: the connection under direct TLS, and behind a proxy only the trusted forwarded
  header, decoded by the `IClientCertificateHeaderDecoder` registered for the configured format. Never read
  `HttpContext.Connection.ClientCertificate` or a forwarded header directly — behind a proxy the connection's certificate
  is the proxy's. A forwarded value that cannot be decoded is `invalid_request`, never an absent certificate: a
  present-but-malformed credential fails visibly instead of looking like one that was never sent. See
  [ADR-0172](../../docs/adr/0172-forwarded-client-certificates-are-decoded-by-a-registered-format.md).
- **Give a typed `Claim` its value in the declared type's XML Schema lexical form** — numbers via
  `value.ToString(CultureInfo.InvariantCulture)`, never culture-formatted (`"1,000"`). The JWT claim ⇄ JSON mapping
  (`ClaimValueMapper`, the single place a `Claim.ValueType` is interpreted) parses each value strictly in its declared
  type and emits a value outside it as a verbatim string. See
  [ADR-0122](../../docs/adr/0122-jwt-claim-values-map-to-json-by-declared-type.md).

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
  the renderer's credential location must match the method the client is registered for (the bootstrap administrator
  registers `client_secret_basic`, so its credentials go in the `Authorization` **header**). See
  [ADR-0046](../../docs/adr/0046-api-reference-openapi-document-and-scalar.md) and
  [ADR-0145](../../docs/adr/0145-a-client-authenticates-only-with-its-registered-method.md).
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

## 11. Logging & metrics

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
    | `13000`–`13999` | `NCode.Identity.OpenId.Host`                  |
    | `14000`–`14999` | `NCode.Identity.Events`                             |
    | `15000`–`15999` | `NCode.Identity.Dispatch`                           |
    | `16000`–`16999` | `NCode.Identity.SharedState`                        |
    | `17000`–`17999` | `NCode.Identity.SharedState.Valkey`                 |

### Metrics

- 👁 **Measure a signal once, through its owner**
  ([ADR-0197](../../docs/adr/0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md)): request
  count, latency, and status belong to ASP.NET Core's `http.server.request.duration` (enrich it through
  `IHttpMetricsTagsFeature`), outbound calls to `System.Net.Http`, and domain outcomes to the event seam's counter.
  Add a family instrument only for a mechanism that is none of these, such as rate limiting or shared state.
- 👁 **Create meters through `IMeterFactory`, never `new Meter(…)`**: one meter per package, named after the package
  as a public constant, with its instruments in one internal `…Metrics` singleton that call sites use. A meter name
  starts with `NCode.Identity.`, so the host's `AddOpenTelemetryMetrics` subscribes to it without a change.
- 👁 **An event adds metric dimensions by implementing `ISupportMetricAttributes`**, never through a counter at its
  call site; an endpoint tags the request through `OpenIdMetricAttributes.Add`.
- 👁 **Name instruments and attributes `ncode.identity.{area}.…`**, keep an OpenTelemetry semantic-convention name
  (`error.type`) where one exists, and use UCUM units: durations as histograms in seconds, counts as `{noun}`.
- 👁 **A metric attribute's value comes from a vocabulary the server controls**: a registered name, a server-chosen
  error code, or `_OTHER`. Never a tenant, client, subject, or address, and never a value echoed from the request;
  those belong to traces and logs.
- 👁 **Test every instrument with `MetricCollector<T>`**, asserting its name, unit, and attributes.
- 👁 **A new or renamed instrument or attribute updates [`docs/metrics.md`](../../docs/metrics.md)** in the same
  change; the catalog is the contract dashboards and alerts are built against.

## 12. Configuration & options

- 👁 **Configuration is a strongly-typed options class** in an `Options/` folder, bound with
  `services.Configure<T>(configuration.GetSection(...))` — never `IConfiguration` read ad hoc deep in the code.
- 👁 **Inject `IOptions<T>` for start-up-fixed configuration and `IOptionsMonitor<T>` for values that may change at
  runtime.** An options class is a data type (§2): `sealed`, `init` / get-only members, concrete collections,
  correct-by-default (§2 — no knob weakens a security control below its safe baseline).
- 👁 **A rule that validates configured data also validates the same data written at runtime.** When startup validation
  checks a value that another path can also write — the management API, dynamic registration, a seeder — both paths
  apply one implementation of the rule, shared through the seam that owns the concept (the tenant-selection seam judges a
  tenant's address for configuration and the management API alike). A check that exists only for configuration lets the
  runtime path store exactly what startup refuses. An options validator never resolves the services that read the
  options it validates; share a stateless rule set with them instead. See
  [ADR-0160](../../docs/adr/0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md).
- 👁 **Each strategy validates its own options.** An `IValidateOptions<T>` for a strategy's section is registered beside
  the strategy and skips when another strategy is selected; a shared validator never branches on the strategy code, so a
  strategy registered by another package brings its own validation (§2, open vocabularies).
- 👁 **A default for runtime-written data is derived through the seam that judges it, then judged and stored like an
  explicit value.** When configuration can supply a value a write omits — a tenant's issuer from its strategy's issuer
  template — every write path asks the owning seam for the default (`ITenantResolver.GetDefaultIssuerAsync`) before it
  validates, and persists the result, so a later configuration change never silently moves existing data. Never fill
  the gap at read time from the request or other ambient state: that is a second, unvalidated source of the same value.
  See [ADR-0165](../../docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md).
- 👁 **A configuration seeder names exactly the target it seeds.** A seeder that writes credentials or accounts into a
  tenant (`BootstrapAdmin`, `DeveloperInteractiveSeed`, `ConformanceSeed`) requires its `TenantId` whenever it has
  something to seed, and an unset target never means "every tenant": every tenant created at runtime would inherit the
  credential. Validate the requirement when the host starts. See
  [ADR-0166](../../docs/adr/0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md).
