---
description: "NCode.Identity production C# conventions — async, public-API discipline, file/code organization, extension methods, build gates, clean code, testability, and provenance."
applyTo: "src/**/*.cs,hosts/**/*.cs,analyzers/**/*.cs"
---

# NCode.Identity coding conventions (production)

NCode.Identity's own production coding rules. The project template's
[`csharp.instructions.md`](csharp.instructions.md) holds the general C# rules and applies to every `.cs` file; this
file keeps what the family adds to them or decides differently, and a bullet that names a template rule links its
section there. Where the two differ, this file wins. Test-project rules live in
[`csharp-testing.instructions.md`](csharp-testing.instructions.md). The `.editorconfig`, CSharpier, and analyzers
enforce the machine-checkable subset. When a family convention changes, change it **here** first. The _reasoning_
behind a non-obvious rule or seam lives in an ADR under [`docs/decisions/`](../../docs/decisions/) — link to it rather
than inlining the rationale.

**Legend** — how each rule is enforced:

- ✅ **Tool-enforced** — the build / [`build/dod.ps1`](../../build/dod.ps1) fails if broken (analyzer, CSharpier, or compiler).
- 👁 **Review-only** — not yet machine-checkable; confirmed in code review.
- ⏳ **Rolling out** — the tooling is wired but the existing surface is still being brought into compliance; treat as 👁 today.

---

## 1. Language & async

- ✅ **Async methods return `ValueTask`, end in `Async` and take the `CancellationToken` last** — template
  [Language and Async](csharp.instructions.md#language-and-async); `NCID0002` reports a method that returns `Task` or
  `Task<T>`
  ([ADR-1496](../../docs/decisions/1496-an-analyzer-reports-an-own-async-method-that-returns-task-or-lacks-a-trailing-cancellation-token.md)).
  A mediator handler whose contract returns `Task` is a framework boundary too.
- ✅ **Every async method of ours takes a `CancellationToken`, even when none of its awaited calls takes one** — such
  as one whose only asynchronous call is `IAuthorizationService.AuthorizeAsync`. The token is part of the method's
  contract, so a call added later honors it; a root call site with no token in hand passes `CancellationToken.None`.
  `NCID0003` reports an async method whose last parameter is not a `CancellationToken`
  ([ADR-1496](../../docs/decisions/1496-an-analyzer-reports-an-own-async-method-that-returns-task-or-lacks-a-trailing-cancellation-token.md)).
  The first exception is a method whose whole work follows a committed change and runs on its own bounded token
  through `PostCommitWork`, such as every `IManagementAuditRecorder` and `IPostCommitSecurityEffects` method: it takes
  no `CancellationToken`, so no caller can pass one that drops the effect
  ([ADR-1351](../../docs/decisions/1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)).
  The second is a method an ADR makes uncancellable: its whole awaited work runs on `CancellationToken.None` because a
  caller's abort must not drop it
  ([ADR-1291](../../docs/decisions/1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md),
  [ADR-1293](../../docs/decisions/1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md),
  [ADR-1308](../../docs/decisions/1308-a-refresh-lease-is-released-only-by-the-owner-token-its-winner-holds.md)).
  It takes no token either, since a parameter it ignores would misstate its cancellation. Each such type or wrapper
  carries a
  `[SuppressMessage("Design", "NCID0003", Justification = "...")]` whose justification cites the ADR. The analyzer
  skips, without a suppression, a method group handed to an ASP.NET Core API, an xUnit test method, a middleware
  `Invoke`/`InvokeAsync`, an entry point, `DisposeAsyncCore` and an override or implementation of a foreign member.
- ✅ **No sync-over-async** — template [Language and Async](csharp.instructions.md#language-and-async);
  [`build/BannedSymbols.txt`](../../build/BannedSymbols.txt) bans it in every project, tests included (`RS0030`).
- 👁 **Fire-and-forget offloads use `Task.Run`, never `Task.Factory.StartNew` with an `async` delegate** — template
  [Language and Async](csharp.instructions.md#language-and-async).
- ✅ **No `ConfigureAwait(false)`.** The family's libraries are ASP.NET-Core-oriented, so the template's
  [Language and Async](csharp.instructions.md#language-and-async) rule for code that runs only under ASP.NET Core
  applies to all of them; `CA2007` is disabled, not required.
- 👁 **Request-abort cancellation is a disconnect, not an error** — template
  [Language and Async](csharp.instructions.md#language-and-async).
- 👁 **A form body that cannot be read is the client's error.** `ReadFormAsync` throws `InvalidDataException` for a
  malformed body or one over the form limits, which the OpenID exception handler answers with `500`. An OpenID endpoint
  reads its form once, first — before client authentication — through `TryReadFormAsync` and answers
  `UnreadableForm.CreateError` (`400 invalid_request`) when it returns `null`; a later reader gets the cached form. Every
  package reads a form through that one method, `NCode.Identity.AspNetCore.FormRequestExtensions.TryReadFormAsync` (`null` for no
  form or an unreadable one) — the OpenID endpoints and the BFF alike — never through a copy of its own
  ([ADR-1500](../../docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
- 👁 **A background loop treats an `OperationCanceledException` as a failure unless its own stopping token is
  cancelled** — template [Language and Async](csharp.instructions.md#language-and-async) (`JobDispatchWorker`,
  [ADR-1242](../../docs/decisions/1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md)).
- ✅ **Collection expressions** — template [Collection Expressions](csharp.instructions.md#collection-expressions).
- ✅ **`const` for compile-time literal values** — template
  [Constants for Literals](csharp.instructions.md#constants-for-literals).
- ✅ **UTF-8 string literals (`u8`)** — template [UTF-8 String Literals](csharp.instructions.md#utf-8-string-literals);
  a fixed protocol label, such as an HKDF `info` or a hash prefix, is a `static ReadOnlySpan<byte>` property.
- ✅ **Nullable reference types are on, nullable warnings are errors, and the null-forgiving operator (`!`) is
  banned** — template [Nullability and Invariants](csharp.instructions.md#nullability-and-invariants).
- ⚠️ **`T?` under a `where T : notnull` constraint is not `Nullable<T>` for a value type** — template
  [Nullability and Invariants](csharp.instructions.md#nullability-and-invariants); it bit the settings lattice
  ([ADR-1010](../../docs/decisions/1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)).
- ✅ **`Debug.Assert` is for internal invariants only, never for input** — template
  [Nullability and Invariants](csharp.instructions.md#nullability-and-invariants).
- 👁 **Validate untrusted text as written, not only as parsed** — template
  [Nullability and Invariants](csharp.instructions.md#nullability-and-invariants). `TenantAddressRules.TryParseIssuer`
  checks an issuer's raw string, and an identifier compared byte for byte, such as a redirect URI, is never normalized
  through `UriBuilder` or `UriJsonConverter` either
  ([ADR-1203](../../docs/decisions/1203-redirect-uris-are-matched-exactly-as-written-and-their-kinds-are-narrowed-by-settings.md)).
- ✅ **Auto-properties over fields, `private` members and a `Lock` included** — template
  [Members and Constructors](csharp.instructions.md#members-and-constructors). `NCID0001`, from
  `analyzers/NCode.Identity.Analyzers`, fails the build of every project on a field that is not a `const`, a `ref` field, a
  `volatile` or fixed-size buffer field, a field of a `[StructLayout]` or `[InlineArray]` struct, or a field the code
  uses by reference (`ref`, `out` or `in`, `Interlocked`, `Volatile`, `&`, or an in-place call on a mutable struct)
  ([ADR-1486](../../docs/decisions/1486-the-repositorys-own-analyzer-project-fails-the-build-on-a-field-an-auto-property-could-replace.md)).
- 👁 **The narrowest accessor shape** — template
  [Members and Constructors](csharp.instructions.md#members-and-constructors).
- 👁 **Primary constructors wherever they can express the type**, converting single-purpose constructors too — template
  [Members and Constructors](csharp.instructions.md#members-and-constructors); buffering a collection with `[.. param]`
  or a guard folded into an initializer still fits a primary constructor.

- 👁 **Constants and statics at the top of the type** — template [Layout](csharp.instructions.md#layout).

## 2. Public API discipline

- 👁 **`public` is a deliberate decision, never a default** — template [Public API](csharp.instructions.md#public-api);
  the `DefaultFoo` implementation behind a public interface stays `internal` (§3).
- 👁 **Correct-by-default** — template [Security Defaults](csharp.instructions.md#security-defaults): no public API
  disables issuer, audience, signature, lifetime or algorithm validation.
- 👁 **A policy that a replaceable public service must apply is itself a public seam** — template
  [Public API](csharp.instructions.md#public-api): a replacement of `ITokenService` resolves
  `IAccessTokenPolicyService` and `IClientJwtCredentialSelector`
  ([ADR-1158](../../docs/decisions/1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md),
  [ADR-1181](../../docs/decisions/1181-every-issuer-of-a-jwt-addressed-to-a-client-resolves-one-public-credential-selector.md)).
- 👁 **A token the server issued is validated only through `IIssuedTokenValidator`.** Never build
  `JwtValidationParameters` for an access token, ID token, or `id_token_hint` the tenant minted. The seam states the
  tenant's algorithm allow-lists, keys, `typ`, issuer, lifetime, and session check in one place, and hand-built copies
  are how introspection came to report ID tokens as active access tokens and ignore revoked sessions. A caller adds
  only what it owns — the audiences it requires and the decryption keys of the resource server it acts for — through
  `AccessTokenValidationOptions`. See
  [ADR-1167](../../docs/decisions/1167-the-server-validates-the-tokens-it-issued-under-one-policy.md).
- 👁 **Restrict a caller to some endpoints with opt-in endpoint metadata that fails closed** — template
  [Security Defaults](csharp.instructions.md#security-defaults): `AcceptResourceServerClients()` adds
  `IOpenIdEndpointResourceServerClientMetadata`, which `DefaultOpenIdClientResolver` checks, and a request with no
  endpoint, such as background work, resolves no such client
  ([ADR-1168](../../docs/decisions/1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md)).
- 👁 **Keep CORS off a front-channel endpoint with `WithFrontChannel()`, never CORS metadata.** An endpoint the
  user agent navigates to (authorization, continue, end-session, the interactive surface) carries `NCode.Identity.AspNetCore`'s
  `IFrontChannelEndpointMetadata`, and the CORS middleware is branched around it (`UseDeveloperCors`); the same call
  routes a CORS preflight to the endpoint, which refuses it, since routing's own 405 endpoint has no marker, and a
  preflight that selects no endpoint (an unmapped method) gets no policy either. Do not use `DisableCors()` /
  `DisableCorsAttribute` or any other `ICorsMetadata`: the endpoint middleware throws for an endpoint carrying CORS
  metadata when no CORS middleware ran, so it fails every request a production host without CORS serves. See [ADR-1148](../../docs/decisions/1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md).
- 👁 **Extensibility is the default: a vocabulary is open unless it is provably closed.** The family exists to be
  extended — by consumers and by sibling packages — without editing it, so every set of named values (kinds, types,
  modes, scopes, formats, methods, reasons, statuses, plans) is **open** by default and modeled as:
    - **Names** — `public const string` values in a static class (`OpenIdSettingNames`, `OpenIdSettingScopes`,
      `OpenIdProtocol.GrantTypes`), compared ordinally and documented as the **known** values, never as all of them.
    - **Registered data or strategies**, when a member carries behavior or relationships (a rule, a parent, an
      implementation) — a descriptor contributed through `ICollectionDataSource<T>` or a strategy collection from DI
      ([ADR-1001](../../docs/decisions/1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)), resolved by name through a
      catalog that validates the whole set when it is composed (duplicate names, dangling references, cycles) — the
      pattern of `SettingDescriptor`, `SettingScopeDescriptor`, `ISecurityProfile`, and the client-authentication
      handlers. Input naming an unregistered member is **rejected** (the vocabulary of _registered_ values is closed,
      [ADR-1139](../../docs/decisions/1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)), never ignored.

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
      ([ADR-1201](../../docs/decisions/1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md)).
    - A persisted or wire value is its string name, never an ordinal, so an older node that meets a newer member fails
      closed by name instead of misreading a number.

    The lesson behind the rule: setting scopes were first a `[Flags]` enum of the deployment root, the tenant, the client
    and the resource server, with their cascade hard-coded in a `switch`, judged closed because the library owned all four entities — which
    shut out every extension that stores settings for an entity of its own. They are registered `SettingScopeDescriptor`
    data. See [ADR-1150](../../docs/decisions/1150-vocabularies-are-open-unless-provably-closed.md).

- 👁 **No secret material on the public surface** — template
  [Types and Collections](csharp.instructions.md#types-and-collections).
- 👁 **Data, options, context and event types are `sealed`**, with `init` or get-only members and concrete collections
  (`List<T>`, never `IList<T>`) — template [Types and Collections](csharp.instructions.md#types-and-collections).
- 👁 **Seal by derivation intent, not by visibility** — template
  [Types and Collections](csharp.instructions.md#types-and-collections). The Abstractions extension points
  (`SecretKey`, `Algorithm`, `OpenIdContext`, `OpenIdClient`, `KnownParameter`) stay unsealed.
- 👁 **A `readonly record struct` for a small, immutable value** — template
  [Types and Collections](csharp.instructions.md#types-and-collections) (`SettingKey<T>`, `UriDescriptor`, the
  `*Command` and `*Disposition` types).
- 👁 **A JSON-deserialized DTO captures unknown fields** with a
  `[JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }` member, so an unrecognized
  wire member round-trips instead of being silently dropped.
- 👁 **A persisted-at-rest payload is a schema-versioned envelope, and a stored document that lacks what the serving
  code writes fails closed** — template
  [Serialized and Stored Data](csharp.instructions.md#serialized-and-stored-data) and
  [project-template ADR-0108](../../docs/decisions/project-template/0108-persisted-documents-are-versioned-and-additive.md).
  A domain payload serialized into a stored JSON blob (the `PersistedGrant<TPayload>.Payload` path) is written through
  `IPersistedPayloadSerializer`
  ([ADR-1113](../../docs/decisions/1113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md)), not a bare
  `JsonSerializer` call, and its members never begin with `$`. A stored document that lacks what the serving code
  writes — the envelope, or a member the serving code always sets — is malformed: the reader throws rather than
  supplying a default or a fallback
  ([ADR-1395](../../docs/decisions/1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md)).
  A long-lived payload that is rewritten in place (`UpdatePayloadAsync`) carries the `[JsonExtensionData]` bag above so
  a read-modify-write never drops a member it does not model. Until the first stable release
  ([#535](https://github.com/NCodeGroup/identity/issues/535)), a member the serving code always writes is `required`;
  from it, a non-additive change bumps `[PersistedPayloadVersion(N)]` on the payload and ships one
  `FromVersion → FromVersion + 1` `IPersistedPayloadUpcaster`.
- 👁 **Buffer by default; stream deliberately** — template
  [Types and Collections](csharp.instructions.md#types-and-collections).
- 👁 **Own every stored collection; pick the concrete type by usage** — template
  [Types and Collections](csharp.instructions.md#types-and-collections).
- 👁 **Signatures speak in read-only interfaces; storage stays concrete** — template
  [Types and Collections](csharp.instructions.md#types-and-collections); a local stays concrete through `var` over an
  explicit initializer.
- 👁 **No behavior-selecting flag arguments** — template [Public API](csharp.instructions.md#public-api).
- 👁 **A configuration callback takes one growable type** — template [Public API](csharp.instructions.md#public-api).
- 👁 **No cross-assembly `InternalsVisibleTo` in production** — template [Public API](csharp.instructions.md#public-api)
  and [project-template ADR-0104](../../docs/decisions/project-template/0104-no-cross-assembly-internalsvisibleto.md).
- 👁 **An implementation package references only `*.Abstractions` packages — never another implementation package.**
  Cross-implementation collaboration flows through an interface owned by an `*.Abstractions` package, resolved from DI;
  the concrete type is registered by its own package's `DefaultRegistration.cs` (§4) and referenced in exactly one
  place — the composition root (`AddIdentityServer()` on the `NCode.Identity.Registration` `IServiceBuilder<TMarker>` builder). A
  new capability shared by more than one implementation therefore ships as an abstraction/implementation **pair**. An
  implementation `.csproj` whose `ProjectReference` names a non-`Abstractions` package is a violation. See
  [ADR-1500](../../docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md). A foundation package
  (`NCode.Identity.Json`, `NCode.Identity.Time`, `NCode.Identity.AspNetCore`) is the exception: any package may
  reference it ([ADR-1500](../../docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
- 👁 **Mark infrastructure-public types** — template [Public API](csharp.instructions.md#public-api), with the summary
  _"This API supports the NCode.Identity infrastructure and is not intended to be used directly from your code."_
- ⏳ The public surface is deliberate and tracked by `PublicApiAnalyzers` (`RS0016` / `RS0017` / `RS0024`): a public
  change not recorded in the sibling `PublicAPI.{Shipped,Unshipped}.txt` fails the build. A breaking change is a
  **major** version bump. See [ADR-1025](../../docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md).
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
- ⏳ **At most one overload per method name carries optional parameters (`RS0026`)** — template
  [Public API](csharp.instructions.md#public-api) (`IJoseSerializer.Deserialize<T>` and `VerifyJws<T>`).
- 👁 **Required inputs are constructor parameters; optional inputs are `init` properties** — template
  [Public API](csharp.instructions.md#public-api).
- 👁 **An evolving set of optional selection criteria is a DTO, not positional parameters** — template
  [Public API](csharp.instructions.md#public-api): `GrantFilter`, and the shared `PageRequest` that every store
  `GetPageAsync` takes ([ADR-1080](../../docs/decisions/1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md)).
- 👁 **Annotate a reflection-/DI-/serializer-consumed public type with `[PublicAPI]`** (JetBrains.Annotations) — the
  house pattern across the packages. Members of a public type touched only by the JSON serializer, the options binder,
  or the DI container have no in-repo call site, so the unused-member analyzers flag them; `[PublicAPI]` states the
  intent and silences the noise without a null-forgiving or pragma escape hatch. **Do not apply `[PublicAPI]` to an
  internal type** — an internal implementation is reached through in-assembly call sites (its interface, its DI
  registration), so the analyzers do not flag it and the annotation would misstate it as public surface.

## 3. File & code organization

- ✅ **One type per file** — template [File and Code Organization](csharp.instructions.md#file-and-code-organization).
- 👁 **The single canonical implementation of an `IFoo` is `DefaultFoo`** — template
  [File and Code Organization](csharp.instructions.md#file-and-code-organization) and
  [project-template ADR-0105](../../docs/decisions/project-template/0105-the-canonical-implementation-is-named-defaultfoo.md)
  (`IJoseSerializer` → `DefaultJoseSerializer`, `ITokenService` → `DefaultTokenService`).
- 👁 **No nested types**, except the template's two — template
  [File and Code Organization](csharp.instructions.md#file-and-code-organization).
- 👁 **A type's folder is decided by what the type is, organized by purpose** — template
  [File and Code Organization](csharp.instructions.md#file-and-code-organization). A single-purpose package (all
  abstractions) stays flat. Name a group for its purpose (`KeyTypes`, `Algorithms`, `Stores`, `Pkce`, `OutboundHttp`,
  `ResourceServers`, `Cryptography`); a contract, its DTOs and its `Default*` implementation share one purpose
  namespace across the `.Abstractions`/implementation pair, and that folder registers its own services
  (`AddPkceServices()`). An endpoint's own replaceable services live at that endpoint's root namespace
  (`…Endpoints.Registration`), and a service used by a single grant lives with that grant
  (`…Endpoints.Token.TokenExchange`).
- ✅ **The public namespace is a stable contract and does not have to mirror folders** — template
  [File and Code Organization](csharp.instructions.md#file-and-code-organization). `IDE0130` is opted out in
  [`.editorconfig`](../../.editorconfig) and `RootNamespace` pins a package's base namespace; public options are the
  exception, whose `Options/` folder carries a `.Options` sub-namespace.
- 👁 **Interface names state the role; the namespace supplies the brand** — template
  [File and Code Organization](csharp.instructions.md#file-and-code-organization).

## 4. Extension methods & DI registration

- 👁 **A host `Program.cs` is composition-only** — template
  [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods), and the
  most-repeated review miss here. Developer-only helpers for the OpenID server live beside their siblings in
  [`NCode.Identity.Server/DevelopmentEnvironment/`](../../src/NCode.Identity.Server/DevelopmentEnvironment)
  (`AddDeveloperKeyPruning`/`GetDeveloperKeysOptions`, `AddDeveloperCors`/`UseDeveloperCors`) behind the server's turnkey
  surface, the production signing key is a host input, and the host makes one
  `ConfigureKestrel(k => k.ConfigureHttpsDefaults(...))` call that holds every HTTPS default, such as the reference
  host's `AllowAnyClientCertificate()` for mutual TLS
  ([ADR-1202](../../docs/decisions/1202-fapi-1-0-advanced-is-a-registered-profile-and-only-a-request-objects-parameters-count.md)).
- 👁 **A host maps its probes with `MapHealthProbes()`** (`NCode.Identity.AspNetCore`), and a package tags a check a replica cannot serve without with `HealthCheckTags.Readiness` (`NCode.Identity.Registration`) ([ADR-1456](../../docs/decisions/1456-the-hosts-map-their-health-probes-through-one-general-helper.md)).
- ✅ **C# 14 `extension` member syntax** — template
  [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods)
  (`extension(IServiceBuilder<OpenIdCoreLibrary> builder) { … }` in `DefaultRegistration.cs`).
- ✅ **One receiver type per static class (`CA1708`)** — template
  [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods).
- 👁 **When two receiver interfaces are unrelated but a concrete type implements both, target their common base** —
  template [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods)
  (`NCode.Identity.Json.ObjectGraphExtensions.TryGetValue<T>`).
- 👁 **Null-guard the receiver** — template
  [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods).
- 👁 **Name the receiver in a receiver-argument exception even though `CA2208` objects** — template
  [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods).
- 👁 **Choose the DI shape deliberately** — template
  [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods) and
  [ADR-1001](../../docs/decisions/1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md). The mediator is
  `NCode.Mediator`'s `ICommandHandler<T>`, and each built-in strategy of an open vocabulary is its own class (the
  redirect URI kinds).
- 👁 **A relationship between a client's settings is a registered coherence rule.** When a value a client registers must
  be permitted by another of its effective settings, or may require one, add an `IClientCoherenceRule` with
  `TryAddEnumerable`. The rule reads a `ClientCoherenceContext` (the client's registered and its effective settings; a
  value a ceiling drops is visible only in the registered settings) and reports a missing companion with
  `ClientCoherenceViolation.Missing`. Every entry point that stores a client, a configuration seeder through
  `SeededClientValidator` included, and every platform, team or tenant settings change checks it with
  `IClientCoherenceValidator.Validate(tenantSettings, clientSettings)`; never add the check to one caller
  ([ADR-1206](../../docs/decisions/1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)). List a tenant's
  incoherent clients, in a report or a background pass, only through `IClientCoherenceReporter`, which checks one page
  under the tenant's scope and reports a document the rules cannot read as `IncoherentClient.DocumentError`
  ([ADR-1245](../../docs/decisions/1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md)).
  A value the client registers in a setting whose merge is a meet (`SettingDescriptor.HasMeetMerge`: `Min`, `And`,
  `Intersect`) must survive into its effective settings, so a ceiling that lowers it is a violation, never a silent
  clamp, while a join only raises a value to a parent's floor and is never reported
  ([ADR-1206](../../docs/decisions/1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)). A settings write that walks
  the stored clients logs and skips a document the settings vocabulary cannot honor and never counts it as a violation
  the change introduces
  ([ADR-1245](../../docs/decisions/1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md)).
- 👁 **Options validation never constructs a strategy that has dependencies.** `IOptions<OpenIdOptions>.Value` runs
  every `IValidateOptions<OpenIdOptions>`, which read the setting-descriptor catalog, so every setting data source, and
  every catalog it consults to name a domain (the client-authentication method descriptors, the redirect URI kinds, the
  security profiles), is built from dependency-free registrations; resolving a strategy with dependencies there builds
  its whole graph inside options validation and deadlocks composition when a constructor on that path reads `Value`.
  When a vocabulary's members are strategies, register a descriptor beside each one and check at composition that they
  correspond
  ([ADR-1207](../../docs/decisions/1207-client-authentication-methods-are-named-by-registered-descriptors.md)).
- 👁 **Mediator handlers run in descending `ISupportMediatorPriority` order** — `DefaultMediatorPriorities.High` (100)
  first, `Low` (-100) last. When one handler depends on another's effect, state it with priorities, never with
  registration order. A handler that observes or repairs state for the rest runs `High` (the default validators, the
  development secret pruner), and a handler that consumes what the others leave runs `Low` (tenant key provisioning,
  [ADR-1169](../../docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md)).
- 👁 **A change other replicas must observe is announced, not only applied locally.** An in-process cache or
  materialized object is invalidated on every replica through `IReplicaBroadcaster`, with a registered
  `IReplicaBroadcastHandler` per channel. Apply the change locally first, publish after, and log rather than throw a
  failed publish once the write has committed
  ([ADR-1205](../../docs/decisions/1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md)). A handler also implements
  `HandleMissedMessagesAsync`, which the Valkey subscriber calls once it subscribes and whenever its connection is
  restored: drop or read again everything a lost message on the channel could have made stale. When each cached entry
  records the store revision it was materialized from and every change the channel announces commits that revision, the
  handler revalidates instead: it compares each entry's revision with the store's, evicts only what changed, and drops
  everything only when it cannot read the revisions
  ([ADR-1416](../../docs/decisions/1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md)).
- 👁 **Work proportional to the deployment never runs inside a request.** A pass over every tenant or every client
  reads a settings projection (`GetSettingsPageAsync`, untracked, no secrets) and skips when the change cannot affect
  it. A pass that brings tenants or clients in line with their team's settings is a handler of
  `ReconcileTeamSettingsCommand`, which `TeamSettingsReconciliationService` sends per team and one replica runs in the
  background whenever the team's effective settings change
  ([ADR-1412](../../docs/decisions/1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md));
  walk the teams beneath a team only through `ITeamDescendantWalker`, which holds one page of child teams per level, and
  never read a whole subtree into memory
  ([ADR-1426](../../docs/decisions/1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md));
  never put such a pass on the in-process job dispatcher, whose bounded workers it would hold.
- 👁 **State derived from settings converges by reconciliation, not by replaying an event.** A background pass reads the
  current state and repairs what differs, so a pass that a restart interrupted is run again and nothing depends on a
  queued job that a restart loses. Record durably what the pass reconciled against — the input itself beside its
  fingerprint (`PersistedTeam.ReconciledSettings`, `ReconciledSettingsFingerprint`) — and let each handler compare
  it with the current input (`ReconcileTeamSettingsCommand.PreviousSettings`) to skip what the change cannot affect,
  reconciling everything when there is nothing to compare with. Mark the record stale in the unit of work that changes
  its input, discard the compared input when a second change arrives before the first was reconciled, write the
  record alone so it never overwrites a concurrent write, and take an `IExpiringKeySet` lease keyed by the input's
  version so one replica runs each pass
  ([ADR-1412](../../docs/decisions/1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)).
- 👁 **Build a protocol endpoint to the canonical shape** per [ADR-1086](../../docs/decisions/1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md):
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
  end-session, and CIBA ([ADR-1085](../../docs/decisions/1085-ciba-is-a-pending-request-in-the-grant-store-delivered-by-poll-ping-or-push.md)) endpoints; the device-authorization endpoint is
  the pre-convention outlier (issue [#56](https://github.com/NCodeGroup/identity/issues/56)), **not** a template to copy.
- 👁 **Protocol endpoints are served under the tenant route.** The OpenID protocol group's prefix is the configured
  tenant route (`/{tenantId}`, a literal path, or empty), so an endpoint's own route is relative to the tenant; never
  hard-code an absolute protocol path into a published URL. Generate every URL that leaves the process with
  `GetTenantUriByName`, which takes the authority from the tenant and carries the tenant route's values over. Never
  hand the request's live `RouteValues` to a template binder — it consumes them; bind a copy. See
  [ADR-1165](../../docs/decisions/1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md).
- 👁 **Minimal-API endpoints are bound by the Request Delegate Generator; a project with form-bound endpoints opts
  out.** `Directory.Build.props` generates every project's request delegates at compile time, and a handler shape the
  generator cannot produce fails the build — fix the shape, never suppress the diagnostic. A project with an endpoint
  that binds `[FromForm]`, `IFormFile` or `IFormCollection` sets
  `<EnableRequestDelegateGenerator>false</EnableRequestDelegateGenerator>`, because the generated form binding ignores
  a failed antiforgery validation and answers 500, and each such endpoint has an integration test that a POST without
  the antiforgery token answers 4xx and changes nothing (issue [#186](https://github.com/NCodeGroup/identity/issues/186) re-enables the generator). See
  [ADR-1262](../../docs/decisions/1262-minimal-api-request-delegates-are-generated-at-compile-time.md).
- 👁 **A client authenticates only with its registered method, and its type is that one fact.** The registered
  `token_endpoint_auth_method` (RFC 7591 §2; `client_secret_basic` when none is registered) is the only method that
  authenticates a client at every endpoint that authenticates clients, and `token_endpoint_auth_methods_supported` is
  only the platform/team/tenant/profile **ceiling** over it — never a list of alternatives. Authenticate a client only
  through `IClientAuthenticationService`, which enforces this; a new `IClientAuthenticationHandler` authenticates only a client
  registered for its method (the `CommonClientAuthenticationHandler` base does). Anything that provisions a client
  registers its method explicitly — a public client registers `none`. The authorization endpoint **identifies** the
  client by `client_id` and never authenticates it, so a rule there reads the type the client is registered as —
  `IsRegisteredAsConfidential` — and the same validation reads the same fact when it runs at the pushed authorization
  request endpoint; an endpoint that authenticated the client (the token endpoint redeeming a grant) reads
  `OpenIdClient.IsConfidential`, which agrees with it. Never treat a client registered as confidential as having
  authenticated. See [ADR-1145](../../docs/decisions/1145-a-client-authenticates-only-with-its-registered-method.md) and
  [ADR-1144](../../docs/decisions/1144-authorization-codes-are-bound-to-an-s256-pkce-challenge-unless-an-attested-client-uses-the-nonce.md).
- 👁 **Bind configuration strictly and validate it when the host starts** — template
  [Configuration and Options](csharp.instructions.md#configuration-and-options). Bind through `NCode.Identity.Registration`'s
  `BindStrictly`, which names the section in the error. A seeded client's id and redirect URIs have no library default:
  the host's environment overlay declares them beside the configuration that publishes them
  ([ADR-1370](../../docs/decisions/1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md)).
  An extension binds its own section rather than adding keys to one the server owns. Parse an absolute URI with
  `NCode.Identity.OpenId.AbsoluteUri.TryParse`, which requires its own `scheme:` and no whitespace, never with
  `Uri.TryCreate` alone, and keep a value compared verbatim, such as a CORS origin, in the form it is compared in. An
  options validator refuses an enum value that `Enum.IsDefined` rejects (`InProcessJobDispatcherOptionsValidator`,
  [ADR-1242](../../docs/decisions/1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md)). See
  [ADR-1137](../../docs/decisions/1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md),
  [ADR-1139](../../docs/decisions/1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md), and
  [ADR-1140](../../docs/decisions/1140-setting-values-are-validated-against-their-domain-not-only-their-type.md).
- 👁 **Setting names are a closed vocabulary.** Every setting a platform, team, tenant, client, or resource server carries has a
  registered `SettingDescriptor`, contributed through `ICollectionDataSource<SettingDescriptor>` by the package that
  reads or writes it; never read or write a setting through an ad-hoc `new SettingKey<T>("…")` without a descriptor.
  Settings are rejected wherever they enter — configuration on start, management writes (`ISettingSerializer.ValidateSettings`
  → `400`), and stored documents on read (fail closed). Until the first stable release
  ([#535](https://github.com/NCodeGroup/identity/issues/535)), retiring, renaming, narrowing the scopes of a setting, or
  narrowing its domain edits its descriptor and writes no data migration: each host database context has one
  re-scaffolded `InitialCreate` that holds no data, and a stored document that names an unregistered setting or holds a
  value outside its domain fails closed
  ([ADR-1180](../../docs/decisions/1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md)).
  A value that is metadata rather than a runtime control (RFC 7591 `client_name`, `contacts`) is still a registered
  setting. No descriptor stays registered for a name nothing reads, and a management request that loads a stored
  document naming an unregistered setting answers `409`. See
  [ADR-1139](../../docs/decisions/1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md).
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
  See [ADR-1140](../../docs/decisions/1140-setting-values-are-validated-against-their-domain-not-only-their-type.md) and
  [ADR-1141](../../docs/decisions/1141-a-response-type-is-a-whole-value-and-the-listed-values-alone-control-front-channel-tokens.md).
- 👁 **A security profile binds every scope below the one that selects it.** Outside the live providers, resolve a
  scope's effective settings only through `IEffectiveSettingsResolver`. Use `Resolve(parent, scope, document)`,
  the platform's committed one with `ResolvePlatformAsync`, and a team's effective settings with `ResolveTeamAsync`.
  Never hand-merge a deserialized document, and never take platform settings from this replica's poll for a seed or a
  check. Only the platform factory and the root settings source
  call the catalog directly
  ([ADR-1204](../../docs/decisions/1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md)).
  `security_profiles` accumulates down the cascade (`Union`), so enforcement evaluates the requirements of every profile
  in the effective settings and never picks "the" profile. A profile contributes its ceiling through
  `ISecurityProfileCeilingBuilder`, which accepts only a setting whose merge is a lattice operation
  (`SettingDescriptor.HasLatticeMerge`); a setting a profile must constrain gets a lattice merge, never a projection
  (`Replace`/`Keep`), which a child would override or which would ignore the ceiling. See
  [ADR-1148](../../docs/decisions/1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md).
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
  [ADR-1149](../../docs/decisions/1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md).
- 👁 **A capability the server advertises and a requirement a client registers are two settings.** Advertised support
  never forces a client. The requirement is a client setting with an `Or` floor, and a coherence rule refuses it where
  support is off (`tls_client_certificate_bound_access_tokens` and
  `require_tls_client_certificate_bound_access_tokens`; `dpop_signing_alg_values_supported` and
  `require_dpop_bound_access_tokens`). A requirement is named `require_` plus its RFC client metadata member
  (`tls_client_certificate_bound_access_tokens`, `dpop_bound_access_tokens`). See
  [ADR-1097](../../docs/decisions/1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md).
- 👁 **A value nobody chose sits beneath a security profile's default.** At the platform root the descriptor defaults, the
  computed `*_supported` values and the default-settings providers are the baseline passed to
  `ISecurityProfileCatalog.Expand(scopeSettings, OpenIdSettingScopes.Platform, baselineSettings)`, and only the
  configuration is the root's own settings, so a selected profile's default replaces the baseline and never a
  configured value. Every `Expand` names its scope, and a default applies only to a setting that applies within that
  scope. See
  [ADR-1148](../../docs/decisions/1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md).
- 👁 **Sibling scopes combine; a cascade merges.** When two scopes that are not parent and child govern one operation —
  an access token is bound by its client **and** every resource server it targets — join their effective settings with
  `IReadOnlySettingCollection.Combine`, never `Merge`: it accepts only lattice merges, so the result binds both and does
  not depend on order, and it throws on a projection. A setting that applies to resource servers therefore merges with
  a lattice operation (a composed-server test pins it). Read every access-token setting from the token's governing
  settings (`AccessTokenPolicy.Settings` from `IAccessTokenPolicyService`), never from `openIdClient.Settings` alone,
  which ignores the targeted resource servers' bounds; and never fall back to a weaker or different credential when the
  combined bounds cannot be met — refuse (`invalid_request` when the request can avoid it, `server_error` when the
  configuration cannot be satisfied). See
  [ADR-1158](../../docs/decisions/1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md).
- 👁 **A package registers its own services from `DefaultRegistration.cs`** (the class is **always** `DefaultRegistration`)
  using `extension(IServiceBuilder<TMarker>)` + `TryAdd*` so a consumer can override a default, per
  [ADR-1069](../../docs/decisions/1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md). **The receiver marker is the
  compile-time prerequisite:** a package root's `AddXLibrary()` extends its prerequisite library's builder
  (`extension(IServiceBuilder<OpenIdCoreLibrary>) { AddOpenIdManagementLibrary() }`), and an **opt-in bolt-on capability**
  extends the marker of the library it augments (`AddLocalAccountManagement()` on `IServiceBuilder<OpenIdManagementLibrary>`,
  `AddInteractiveAuthentication()` on `IServiceBuilder<OpenIdAuthenticationLibrary>`) — **never** a naked
  `IServiceCollection`, so it is compile-time-impossible to add a capability without its framework. A library marker a
  cross-package bolt-on must name lives in that library's `.Abstractions` (a marker is an empty `readonly struct`
  implementing `IRegistrationMarker<TMarker>` — its type identity is the whole signal, with no `DisplayName` or
  `ConfigureMethod` data to keep in sync). **A second litmus governs which `.Abstractions`
  packages own a marker: a registerable library _layer_ does (minted by its `AddXLibrary()`), and a dependency-inverted
  _seam that other registrations build on_ owns a _presence marker_ minted by every implementation.** Two seams own one:
  the persistence framework owns `StoreManagerLibrary` (`NCode.Identity.Persistence.Abstractions`, minted by
  `AddEntityFrameworkCorePersistence`), and the local-account store owns `LocalAccountLibrary`
  (per [ADR-1060](../../docs/decisions/1060-local-account-one-store-seam-and-dependency-inverted-authentication.md)): each store
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
  (e.g. the dev-host `AddDeveloperKeyPruning` / `AddDeveloperInteractiveSeed`, which hook the server's command handlers,
  extend `IServiceBuilder<IdentityServer>` — never the bare collection). Only a chain **root**
  with no prerequisite (`AddSettingsLibrary`, `AddClaimsLibrary`, `AddCryptographyLibrary`, `AddSecretsLibrary`, `AddEventServices` which mints `EventsLibrary`, and the
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
- 👁 **The package that owns a consumer registers that consumer's ambient dependencies** — template
  [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods), so composing
  the server (`AddIdentityServer()`) is turnkey: the owning package registers routing, `IHttpClientFactory`,
  `AddAuthentication()` and a default `IIdGenerator<long>` with `TryAdd*`. Only the database provider and connection
  string and the production signing key stay host inputs
  ([ADR-1370](../../docs/decisions/1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md)); host wiring that works only by pulling in a
  framework service by accident, such as `AddControllers` dragging in authentication, is a smell.
- 👁 **A singleton service is stateless** — template
  [DI Registration and Extension Methods](csharp.instructions.md#di-registration-and-extension-methods); a
  client-authentication result memoized on a singleton let a later request reuse it whatever its credentials. Keep
  per-request state on the request (`HttpContext.Items`, the `OpenIdContext`).
- 👁 **Correctness never depends on process-local state** — template
  [Distributed State and Background Work](csharp.instructions.md#distributed-state-and-background-work): the host runs
  as interchangeable replicas ([ADR-1134](../../docs/decisions/1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md)), and state
  another replica must see goes to the database or the shared-state port.
- 👁 **A hosted service whose `StopAsync` does work runs it once and never throws** — template
  [Distributed State and Background Work](csharp.instructions.md#distributed-state-and-background-work)
  (`TenantUsageFlushService.StopAsync`,
  [ADR-1199](../../docs/decisions/1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md)).
- 👁 **A hosted service that reads the store waits for the platform to be materialized.** It starts from
  `IMaterializedPlatformAccessor.GetPlatformAsync`, public so a package other than the core waits the same way, which
  completes once the replica materializes its platform: `TeamSettingsReconciliationService` materializes it when the
  replica starts and, when that fails because the database may still be migrating, the replica's first request does.
  An integration test that needs a persisted platform document sets `HostApplicationFactory.PersistedPlatformSettings`,
  which `PersistedPlatformSeedService` writes in `StartingAsync`, before any hosted service starts
  ([ADR-1412](../../docs/decisions/1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)).
- 👁 **A start check that needs the platform's protector is a hosted service after the warmup.** `ValidateOnStart` and
  every startup validator run before any hosted service starts, so before `PlatformSecretProtectorWarmup` loads the
  protector; `AddSecretProtection` inserts the warmup first and `DataProtectionKeyRingGate` second, both ahead of Data
  Protection's hosted service
  ([ADR-1247](../../docs/decisions/1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md)).
- 👁 **A start step that must precede the warmup runs in `IHostedLifecycleService.StartingAsync`**
  (`DeveloperPlatformMasterKeyPruning`), because every `StartingAsync` runs before any `StartAsync`
  ([ADR-1247](../../docs/decisions/1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md)).
- 👁 **A `SecretProtectionOptions` validator resolves no key wrapper.** Each KMS adapter's options validator reads
  `IOptions<SecretProtectionOptions>`, so a `SecretProtectionOptions` validator that takes `IEnumerable<IKeyWrapper>`
  closes a dependency cycle that stops every host that registers an adapter. The validators check the options' own
  shape; a check against the registered wrappers runs in `DefaultKeyWrapperCatalog` as it is composed, which the server
  does as it starts. An adapter's registration test composes it with `ValidateOnBuild` to keep the graph acyclic
  ([ADR-1414](../../docs/decisions/1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)).
- 👁 **A single-use or freshness check records into `IExpiringKeySet`, never a get-then-set** — template
  [Concurrency and Shared State](csharp.instructions.md#concurrency-and-shared-state). A replay guard (a `jti`) or a
  server-issued nonce is an atomic add-if-absent on the shared-state port (`NCode.Identity.SharedState.Abstractions`),
  in process for one replica and Valkey for several. Build the key with `ExpiringKeys.Create`, which puts the tenant in
  braces, the key's hash tag, so one script touches one tenant's keys on a clustered Valkey, and every Valkey adapter
  call goes through `ValkeySharedStateMetrics.MeasureAsync`
  ([ADR-1164](../../docs/decisions/1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)).
- 👁 **A record every replica must read and update is an `IExpiringValueSet` value, and a lookup by an attribute is an
  `IExpiringIndexSet`.** Take a single-use record with `TryTakeAsync`, never `GetAsync` then `RemoveAsync`. Update a
  record with `TryReplaceAsync`, naming the version you read; on `false` reload the record rather than overwrite it.
  The value is opaque bytes the caller serializes, and encrypts when it holds secrets. Keep an index's members and
  their records on the same expiry, and remove both. A store failure throws, and the caller fails closed. See
  [ADR-1164](../../docs/decisions/1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md).
- 👁 **A credential with a stored grant is spent through `IPersistedGrantService.ConsumeOnceOrDefault`, after every
  check passes and before its effect.** A refused request leaves it usable. `IExpiringKeySet` is for single-use state
  with no durable row. See
  [ADR-1192](../../docs/decisions/1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md).
- 👁 **Every grant that issues tokens for a subject validates it before it spends its credential.** A token grant sends
  `ValidateSubjectAuthenticationCommand` through `TokenGrantSubjectValidation.ValidateAsync` from its
  `ValidateTokenGrantCommand<TGrant>` handler, an out-of-band approval sends it with no request, and a stored credential
  issued to a client (an authorization code, a device code, an `auth_req_id`) is redeemed only by that client. The end
  user's standing (a blocked principal, a disabled or deleted local account) is `ValidateSubjectStandingCommand`, which
  every `ValidateSubjectAuthenticationCommand` includes, the interactive sign-in sends before it creates a session, and
  the authorization request answers with `access_denied` instead of a login challenge; a capability with its own reason
  to refuse an end user adds a `ValidateSubjectStandingCommand` handler with `TryAddEnumerable`. See
  [ADR-1225](../../docs/decisions/1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md),
  [ADR-1226](../../docs/decisions/1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md)
  and
  [ADR-1229](../../docs/decisions/1229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md).
- 👁 **A refresh-token rotation spends the presented token and stores its successor in one unit of work**
  (`ITokenService.RotateRefreshTokenAsync` over `IPersistedGrantService.ReplaceOnceOrDefault`), after every check and
  before any token is minted. A spend that loses to a spend or a revocation is answered as a reuse, and one that loses
  to a write that left the grant active reads the grant again. `RefreshTokenRotationRequirement` decides whether a token
  rotates. A successor stored in a later unit of work can escape the loser's lineage revocation. See
  [ADR-1216](../../docs/decisions/1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md) and
  [ADR-1217](../../docs/decisions/1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md).
- 👁 **A store reports a lost optimistic-concurrency race as `StoreConcurrencyException`, never an Entity Framework
  type.** `IStoreManager.SaveChangesAsync` and a store's own concurrency-token check throw it, so no caller references
  Entity Framework to handle a lost race, and a single-use spend that loses is answered as a second spend. A
  management endpoint never catches it: the management route group's `StoreConcurrencyEndpointFilter` answers it
  `412`. See
  [ADR-1012](../../docs/decisions/1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) and
  [ADR-1483](../../docs/decisions/1483-every-management-error-is-a-problem-document-that-its-route-declares.md).
- 👁 **A management route answers from what it read and declares every problem it answers.** A route answers `404` and
  `409` from the rows it loaded before the write — a secret's identifier with
  `BaseApiEndpointHandler.SecretIdConflictOrDefault`, an imported key's `kid` with `KeyIdConflictOrDefault`, a scope
  value or a system scope from the loaded resource server's scopes — and lets the store's `InvalidOperationException`
  propagate, so a store failure, such as an owner deleted after the read, is never reported as a conflict or a missing
  resource, and a lost race is answered `412` by `StoreConcurrencyEndpointFilter`. Every error is a problem document,
  a missing resource `ManagementProblems.ResourceNotFound()`, never a bare `TypedResults.NotFound()`. Each mapping
  declares its handler's problems with `ProducesProblem` (`ProducesValidationProblem` for a validation problem), and
  `412` when it saves or checks `If-Match`; `ManagementRootGroup` declares the `409` and `503` of its filters on every
  route. `ManagementProblemResponseMiddleware`, which the management library adds through an `IStartupFilter`, answers
  every error under the management prefix that carries no body as a problem, a failed request binding and the router's
  `404`, `405` and `415` included. A route that binds a JSON body declares `400` and `415`, and one with a parameter
  that can fail to parse declares `400`. A handler method's `<response>` lines describe the route's own answers, one per
  code; the route group describes `401`, `403`, `412`, `415`, its `409` and `503`, and the binding `400`. A handler's
  `Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers`, `ManagementRouteProblemDeclarationTests`,
  `ManagementRequestBindingTests` and `ManagementOpenApiDocumentTests` pin them. See
  [ADR-1186](../../docs/decisions/1186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md),
  [ADR-1483](../../docs/decisions/1483-every-management-error-is-a-problem-document-that-its-route-declares.md),
  [ADR-1481](../../docs/decisions/1481-the-composition-layer-serves-the-api-reference-and-each-management-problem-says-when-it-occurs.md),
  [ADR-1067](../../docs/decisions/1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md) and
  [ADR-1483](../../docs/decisions/1483-every-management-error-is-a-problem-document-that-its-route-declares.md).
- 👁 **A management route reads its caller only from what `ManagementAuthenticationEndpointFilter` published.** The
  filter sets `HttpContext.User` to the principal a valid management access token authenticates, or to an anonymous
  principal whatever an authentication scheme set, and sets `IManagementCallerFeature` only for that authenticated
  caller. A route that authorizes reads `HttpContext.User`; a route that only authenticates its caller, such as
  `GET /api/capabilities` and `GET /api/me`, reads `IManagementCallerFeature`, answers
  `ManagementProblems.AuthenticationRequired()` without it and declares `401` and no `403`. See
  [ADR-1044](../../docs/decisions/1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md).
- 👁 **A stored settings document that cannot be honored is answered `409` once, by the management route group.** A
  management request resolves a document a store holds with
  `IEffectiveSettingsResolver.ResolveStored(parent, scopeName, ownerId, document)`, and the platform's committed one
  with `ResolvePlatformAsync`; both throw `UnreadableSettingsException` naming the scope and the owner, and
  `UnreadableSettingsEndpointFilter` answers it with a `409` problem (`settingsScope`, `ownerId`, `documentError`), so
  no handler catches it. Resolve only after authorization (a read resolves in the `mapperAsync` of `ProcessGetAsync`),
  and keep `Resolve` for a proposed document and for a write that replaces the document it cannot read
  ([ADR-1245](../../docs/decisions/1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md)).
- 👁 **A management problem that asks the caller to wait answers through `RetryAfterProblem.Create`**, which sets
  `Retry-After` and `retryAfterSeconds`; extension members are named by `ManagementProblemMembers`
  ([ADR-1483](../../docs/decisions/1483-every-management-error-is-a-problem-document-that-its-route-declares.md)).
- 👁 **A revocation wins a lost race.** Revoke grants through `IPersistedGrantService` (`SetRevokedOnceAsync`,
  `RevokeWhereAsync`, `RevokeByRefreshTokenLineageAsync`), which read again in a new unit of work and retry, never
  through `IGrantStore.RevokeWhereAsync` and a save of your own; a revocation does not depend on what its caller read,
  so the server retries it instead of answering `412`. See
  [ADR-1222](../../docs/decisions/1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md).
- 👁 **Change a stored grant's payload with the read-modify-write
  `IPersistedGrantService.UpdatePayloadAsync(…, update, …)`, never by writing a payload computed from an earlier
  read.** The service applies `update` to the grant as stored under that read's concurrency token and again after a
  lost race, so `update` decides from its argument and returns `null` when the stored grant does not need the change.
  See [ADR-1222](../../docs/decisions/1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md).
- 👁 **When a request and a background pass can both act on one record, the caller whose write claims it acts.**
  `RevokeSessionOnceAsync` returns the session grant as this call revoked it, or `null` when another caller did, in the
  unit of work that also revokes the grants issued under it, so a failure after the claim leaves no token active, and
  only that caller terminates an ended session. See
  [ADR-1223](../../docs/decisions/1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md). Every path that
  terminates a named session goes through `ISessionService`, which claims the session this way before it dispatches
  `OnSessionTerminatedCommand`; never dispatch the command for a named session yourself, and revoke a subject's grants
  before a subject-wide dispatch. See
  [ADR-1112](../../docs/decisions/1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md). A termination notifies the
  clients that the claimed grant lists, never the clients an earlier read listed, and a subject-wide termination claims
  every unrevoked session, an ended one included
  ([ADR-1112](../../docs/decisions/1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)).
- 👁 **A security effect that follows a committed change — ending the sessions after a password change or reset, an
  account disable or a principal block, or clearing the failed password attempts after an administrator sets a
  password — runs through `IPostCommitSecurityEffects`.** Each effect runs on its own token bounded by
  `PostCommitWork.Timeout`, so a client disconnect can't leave the change committed without its effect and a hung
  store can't hold the response; a failure is logged, never thrown, and the audit record follows. A step that precedes
  its commit keeps the request token. Likewise, once a
  request wins a single-use lease (the BFF refresh lease), the guarded operation and its write-back run with
  `CancellationToken.None`, bounded by the client's own timeout, and a logout removes the session before any outbound
  call it makes. See
  [ADR-1226](../../docs/decisions/1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md),
  [ADR-1291](../../docs/decisions/1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md),
  [ADR-1293](../../docs/decisions/1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)
  and
  [ADR-1351](../../docs/decisions/1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md).
- 👁 **A cookie authentication handler authenticates only the endpoints its guard checked.** The BFF's handler
  authenticates the cookie on a guarded endpoint (an API, `/bff/user`, a page) and returns no result everywhere else, so
  a cookie the browser attaches to an unguarded endpoint is never a credential there. See
  [ADR-1292](../../docs/decisions/1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md).
- 👁 **The interactive surface serves a cookie only while the session it names is active.** An interactive endpoint
  authenticates its cookie through the handler's `AuthenticateSessionAsync`, never `AuthenticateAsync` directly, and a
  refusal that resumes a parked authorization request answers it through `IContinueRefusalService`, which delivers the
  error to the client, never a redirect elsewhere. See
  [ADR-1229](../../docs/decisions/1229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md).
- 👁 **A page the bundle draws over carries the state the bundle starts from.** The host writes what the bundle needs
  into the page, such as the tenant's branding as a `<script type="application/json" id="branding">` block serialized
  with `JsonSerializerOptions.Web`, whose encoder escapes `<`, `>` and `&`. The bundle reads it, and the server form's
  state, when it mounts. Never add an endpoint the bundle must call before it can render. See
  [ADR-1253](../../docs/decisions/1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md).
- 👁 **A content-hashed static file is served by a route group that resolves no tenant.**
  `InteractiveBundleAssetEndpointGroup` takes the tenant-route prefix and adds no `OpenIdEnvironmentEndpointFilter`. It
  reads through `NCode.Identity.AspNetCore`'s `SinglePageBundle<InteractiveBundleShell>`, whose file provider is rooted at
  `assets`, so nothing outside `assets`, the bundle's `index.html` included, is servable, and `ServeAsset` sends a
  served file `Cache-Control: public, max-age=31536000, immutable`. Every surface that serves a built bundle registers
  its own `SinglePageBundle<TIndex>`. See
  [ADR-1254](../../docs/decisions/1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md)
  and
  [ADR-1457](../../docs/decisions/1457-a-single-page-bundle-is-read-and-served-through-one-general-helper.md).
- 👁 **Every HTML page the server writes sets its headers through `SetHtmlPageSecurity`.** Build the page's
  `HtmlPageSecurityPolicy` from the strict default (`default-src 'none'`, `base-uri 'none'`, `form-action 'none'`,
  `frame-ancestors 'none'` with `X-Frame-Options: DENY`) and list only the sources the page needs — an inline script by
  its `ContentSecurityPolicySources.Sha256` hash, the bundle by the response's nonce, never `'unsafe-inline'` — and
  write no `style` attribute (use `hidden`). Set `AllowsAnyFormAction` only on a page whose form submission can reach a
  client (the interactive surface, the `form_post` response), because Chrome and Safari check `form-action` against
  every redirect that follows it, and `AllowsFraming` only on a page a relying party frames by design (the
  `check_session_iframe`). List `FrameAncestors` to let only named origins frame a page (the `form_post` response, by
  its redirect URI's origin). Antiforgery adds `X-Frame-Options` only when it stores an antiforgery cookie, so never
  rely on it. See
  [ADR-1256](../../docs/decisions/1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md).
- 👁 **Background work that runs protocol machinery in a tenant creates its context with
  `IOpenIdContextFactory.CreateForBackgroundAsync` in its own DI scope.** The context has no endpoint, connection or
  user, and it is disposed before the scope. See
  [ADR-1223](../../docs/decisions/1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md).
- 👁 **Read a stored grant's payload through `IPersistedGrantService` or `IGrantPayloadProtector`, never from
  `ProtectedPayload` directly.** A grant's `ProtectedPayload` is an `nce1.` envelope over the schema-versioned document
  (`$ver`, `$payload`), so reading it as the payload yields no members and throws for a `required` one; a session
  listing that did so failed for every stored session. See
  [ADR-1220](../../docs/decisions/1220-an-end-user-session-is-a-grant-keyed-by-its-sid-bounded-by-idle-and-absolute-lifetimes.md) and
  [ADR-1265](../../docs/decisions/1265-grant-payloads-are-protected-under-the-tenant-master-key.md).
- 👁 **A fetch of a URL a tenant or certificate supplies uses a named client whose primary handler has the
  `IOutboundHttpForgeryGuard` connect callback, and the platform `X509Chain` retrieves nothing.** Use
  `RevocationMode.NoCheck` and `DisableCertificateDownloads`; revocation goes through
  `IClientCertificateRevocationChecker`. See
  [ADR-1173](../../docs/decisions/1173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md).
- 👁 **A retrieval that concurrent requests share runs once, on its own terms** — template
  [Outbound Calls](csharp.instructions.md#outbound-calls); it also takes its own copy of any disposable input a caller
  owns, such as a chain's certificates (`DefaultClientCertificateRevocationChecker`,
  [ADR-1252](../../docs/decisions/1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md)).
- 👁 **A count every replica must agree on increments `IExpiringCounterSet`, never a get-then-set.** A counter is
  incremented with the port's atomic `IncrementAsync`, which starts a counter for one fixed window, or, for the limits
  of a rate-limit stage, with `IncrementUntilExceededAsync`; a read followed by a write loses concurrent requests. Read
  a counter this request does not add to (the previous window) with `GetAsync`, or as an increment's `ReferenceKey`
  within the batch. Count what is requested, not what later succeeds: counting only an outcome needs a read before the
  work and an increment after it. A rate limit or quota is a `RateLimitDescriptor` registered through
  `ICollectionDataSource<RateLimitDescriptor>` and judged by `IRequestRateLimiter.AcquireAsync` at its stage, which
  counts the stage's limits in one `IncrementUntilExceededAsync` call; never call the counter set for a limit from an
  endpoint. See
  [ADR-1246](../../docs/decisions/1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md),
  [ADR-1198](../../docs/decisions/1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md) and
  [ADR-1246](../../docs/decisions/1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md).
- 👁 **A password is judged by `IPasswordPolicy` wherever it is set.** The management API's create and reset, an
  `IInteractivePasswordChanger`, and a configuration seeder pass the tenant's effective settings, the account's user
  name and email, and the password to `IPasswordPolicy.ValidateAsync` before the store hashes it, and answer a refusal
  with its description (management `400`) or its error code (the change page). Another password the policy must refuse
  is a registered `IPasswordBlocklist`, never a check in one caller, and a store's own password validators are not the
  policy
  ([ADR-1236](../../docs/decisions/1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)).
- 👁 **Every password verification goes through `IPasswordAttemptLimiter`, inside the store's one lookup.** Pass
  the `IsRefusedAsync` of a `PasswordAttemptGate`, which asks `IsLimitReachedAsync`, as the `isRefusedAsync` of
  `ILocalAccountStore.VerifyCredentialAsync`, and answer the result of its `Enforce`, which fails a store that never
  asked and refuses a result verified after a refusal: the store looks the account up once, asks it about the user
  name the account holds (or the submitted name when none has it), and verifies a refused password against its dummy
  hash, so a refusal costs and answers what a wrong password does. Call `RecordFailureAsync` with
  `CancellationToken.None` and the result's `UserName` for every result that is not verified, a refusal included; after
  a verified password, call `ResetAsync`; after a password an administrator sets commits, clear the count through
  `IPostCommitSecurityEffects.ResetPasswordAttemptsAsync`. Never look the account up a second time to name the count. A
  count of a run of events that ends is cleared with `IExpiringCounterSet.RemoveAsync`
  ([ADR-1237](../../docs/decisions/1237-failed-password-attempts-are-limited-per-account.md),
  [ADR-1244](../../docs/decisions/1244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md),
  [ADR-1351](../../docs/decisions/1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)).
- 👁 **A password is judged, hashed and verified in its Unicode Normalization Form C (NFC), and a failed verification
  takes as long whether or not the account exists.** `DefaultPasswordPolicy` judges the NFC form and every
  `IPasswordHasher` hashes and verifies it, verifying the submitted form as a fallback that reports
  `SuccessRehashNeeded`; the ASP.NET Core Identity adapter hashes and verifies only through
  `NormalizingPasswordHasher<TUser>`, which `AddAspNetIdentityLocalAccountStore<TUser>()` wraps around the host's
  `IPasswordHasher<TUser>` registration whatever its order, so the host's own pages hash and verify the same form. When
  a store holds no hash it may verify (no account has the user name, the account has no password or is locked out, the
  stored hash cannot be read, or the password is refused at the attempt limit), it verifies the submitted password
  against a dummy hash of the current parameters (`IPasswordHasher.DummyPasswordHash`) through the same code path and
  refuses it whatever the result. A store never sets a password through an API that runs the host's password validators
  (`UserManager<TUser>.CreateAsync(user, password)`, `ResetPasswordAsync`); it hashes and stores the hash itself
  ([ADR-1238](../../docs/decisions/1238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md),
  [ADR-1239](../../docs/decisions/1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md),
  [ADR-1243](../../docs/decisions/1243-every-aspnet-core-identity-path-hashes-and-verifies-the-nfc-form-of-a-password.md)).
- 👁 **A write that a background loop retries is idempotent** — template
  [Concurrency and Shared State](csharp.instructions.md#concurrency-and-shared-state): the usage ledger's per-replica
  batch checkpoint ([ADR-1199](../../docs/decisions/1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md)).
- 👁 **Deleting an owner removes what it owns, in the same unit of work, through the change tracker.** A client's
  removal takes its secrets, client grants, consents and grants; a client's or a resource server's removal also takes
  the role assignments at its node, ownership included; a tenant's takes every `ISupportTenantEntity` row of
  the model, so an entity a package contributes is erased with its tenant. Foreign keys stay `Restrict`. See
  [ADR-1212](../../docs/decisions/1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md).
- 👁 **An Entity Framework store issues no `ExecuteUpdate`/`ExecuteDelete`.** EF InMemory, the hermetic host's provider,
  does not support them, and they bypass the concurrency-token interceptor; a bulk removal stages a bounded batch and
  its caller saves each batch as one unit of work (`ITenantUsageStore.RemoveBeforeAsync`,
  [ADR-1199](../../docs/decisions/1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md);
  `IGrantStore.RemoveEndedBeforeAsync`,
  [ADR-1218](../../docs/decisions/1218-ended-grants-are-removed-after-a-retention-period-in-bounded-batches.md)).
- 👁 **Public metadata is unmetered, and a limit an endpoint enforces refuses through `RateLimitErrors.Refuse`.** An
  endpoint any anonymous caller or cache fetches (discovery, the JSON Web Key Set) is marked `WithOpenIdMetered(false)`,
  so it spends only the per-minute limits, never the tenant's daily quota or billable usage. The limiter marks a
  request it refuses and adds the usage meters of each admitting stage to the request, and `RateLimitEndpointFilter`
  records them after the endpoint runs unless a stage refused it, so a meter is a descriptor's `UsageMeter` or
  `OverageUsageMeter`, never an `ITenantUsageRecorder.Record` call beside a limit; answer a refusal with
  `RateLimitErrors.Refuse`. See
  [ADR-1198](../../docs/decisions/1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md),
  [ADR-1199](../../docs/decisions/1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md) and
  [ADR-1246](../../docs/decisions/1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md).
- 👁 **A response that reveals a secret is marked `.WithNoStore()`.** Map every endpoint whose response can carry
  secret material (a generated secret or key, an initial access token, a `client_secret` or
  `registration_access_token`, or a one-time credential: a `request_uri`, a `device_code` and `user_code`, or an
  `auth_req_id`) with `.WithNoStore()` (`NCode.Identity.AspNetCore`), which writes
  `Cache-Control: no-store, no-cache, max-age=0` and `Pragma: no-cache` before the handler runs, so the mapping shows
  which routes reveal a secret and no handler path can forget it. `HttpResponse.SetNoStore()` writes the same headers
  where a response rather than a route carries the secret, as the authorization response writer does. Reference the
  `NCode.Identity.AspNetCore` foundation package. See
  [ADR-1345](../../docs/decisions/1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md),
  [ADR-1500](../../docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md) and
  [ADR-1388](../../docs/decisions/1388-the-html-page-security-model-and-the-front-channel-marker-are-general-aspnet-core-helpers.md).
- 👁 **Name an `IServiceCollection` `serviceCollection`** (parameter, local, or extension receiver) and the builder
  property `ServiceCollection` — never `services` or `service`. The canonical `NCode.Identity.Registration` builder sets this
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
- ⏳ **Code-style analyzers run on build** — the template's build settings
  ([project-template ADR-0100](../../docs/decisions/project-template/0100-warnings-are-errors-in-every-project.md)); a
  rule [`.editorconfig`](../../.editorconfig) raises to `warning` fails the build.
- ✅ **No unused `using` directives (`IDE0005`)** — template [Build](csharp.instructions.md#build);
  [`.editorconfig`](../../.editorconfig) raises it solution-wide, and CSharpier runs again after
  `dotnet format style --diagnostics IDE0005`.
- ✅ **Every C# file starts with the two-line license header** `// Copyright © NCode Group` and
  `// SPDX-License-Identifier: Apache-2.0`, then a blank line — no `#region`, no year.
  [`.editorconfig`](../../.editorconfig) sets it as `file_header_template` and raises `IDE0073` to `warning`, so a
  missing or different header fails the build, tests included; Visual Studio and Rider insert it in new files, and the
  `IDE0073` fix adds it. Generated code (EF migrations, `// <auto-generated>` files) is exempt
  ([ADR-1309](../../docs/decisions/1309-c-files-carry-a-two-line-spdx-header-that-the-build-checks.md)).
- ✅ **Banned APIs** (`Microsoft.CodeAnalysis.BannedApiAnalyzers`, `RS0030`) from
  [`build/BannedSymbols.txt`](../../build/BannedSymbols.txt), from the project template, in every project, tests
  included — no `Newtonsoft.Json` (use `System.Text.Json`), no sync-over-async, no `JsonDocument.Parse` — and from
  [`BannedSymbols.txt`](../../BannedSymbols.txt), this repository's own list for shipping code: no legacy
  `JwtSecurityTokenHandler` / `JwtSecurityToken` (use `JsonWebTokenHandler`) and no throwing Unix-time factories. Add
  house rules to that file as they emerge. `BannedSymbols.Encoding.txt` bans lenient text
  encodings in every project, tests included
  ([ADR-1266](../../docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).
- ⏳ **Warnings-as-errors in every project, tests and hosts included, and NuGet vulnerability advisories (`NU190x`) as
  errors in CI** — template [Build](csharp.instructions.md#build) and
  [ADR-1127](../../docs/decisions/1127-every-project-builds-with-zero-warnings-and-the-familys-own-banned-apis-bind-shipping-code.md).
- ✅ **An EF Core model change re-scaffolds its context's `InitialCreate`.** Until the first stable release
  ([#535](https://github.com/NCodeGroup/identity/issues/535)), each host context has one migration, `InitialCreate`,
  which creates its whole model and changes no data: `OpenIdDbContext` in `Migrations/OpenId` and
  `DeveloperDataProtectionDbContext` in `Migrations/DeveloperDataProtection` of `NCode.Identity.OpenId.Host.Database`.
  Any edit to an entity, an `IEntityTypeConfiguration`, or a model contributor that changes a host context model
  deletes that context's migration, designer file and model snapshot from its folder and scaffolds them again with
  `dotnet ef migrations add InitialCreate --project hosts/NCode.Identity.OpenId.Host.Database --startup-project hosts/NCode.Identity.OpenId.Host.Database --context OpenIdDbContext --output-dir Migrations/OpenId`
  (`--context DeveloperDataProtectionDbContext --output-dir Migrations/DeveloperDataProtection` for the other
  context); the database project carries the design-time factories, so it is both the project and the startup
  project. No migration carries raw SQL or seed-data changes: `InitialCreateTests` fails a context that holds any
  migration but `InitialCreate`, and a migration that changes data. The Definition of Done runs
  `dotnet ef migrations has-pending-model-changes` for both contexts and fails on un-migrated model drift, so it is
  caught pre-push and in CI, not at runtime. The dev database is Postgres applied by the migration worker, so
  migrations are authoritative. A re-scaffold gives `InitialCreate` a fresh identifier, which the migration worker
  cannot apply over a schema another identifier created; `build/dev-db-reset.ps1` resets the local database.
  `dotnet ef migrations add` writes its files with a UTF-8 BOM; the repository's files are BOM-less
  (`charset = utf-8`), so strip the BOM from every generated file. See
  [ADR-1073](../../docs/decisions/1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md) and
  [ADR-1180](../../docs/decisions/1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md).
- ✅ **Every migration passes the expand-and-contract gate** — template
  [Entity Framework Core](csharp.instructions.md#entity-framework-core). `ExpandOnlyRule.Check` judges every host
  migration in `ExpandOnlyRuleTests`, which fails a contracting or unclassified migration in the Definition of Done.
  Until the first stable release the gate judges the `InitialCreate` migrations, which pass it unmarked because every
  constraint sits on a table they create. From the first stable release, when each context's `InitialCreate` becomes
  the baseline, a model change adds a migration that keeps the previous release working: a foreign key that restricts
  deleting rows of an existing table tightens it too; raw SQL and seed-data changes carry `[ExpandMigration("why")]`
  (the previous release still reads every row they write, with the same meaning or a restriction this release applies
  too) or `[ContractMigration("why")]` (it fails on, or loses, a value it reads); and a deliberate contract step ships a
  release after its expansion. Hand-written code in `NCode.Identity.OpenId.Host.Database` stays outside
  `Migrations/`, which `.editorconfig` treats as generated code. See
  [ADR-1180](../../docs/decisions/1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md).
- ✅ **Raw SQL in the EF model quotes identifiers** — template
  [Entity Framework Core](csharp.instructions.md#entity-framework-core):
  `HasFilter("\"NormalizedDomainName\" IS NOT NULL")`. The schema uses PascalCase, and only applying the migration to
  Postgres catches an unquoted fragment; the drift gate cannot.
- ✅ **The whole gate is [`build/dod.ps1`](../../build/dod.ps1)** — template [Build](csharp.instructions.md#build).
- 👁 **`dod.ps1` prints its stage markers and the final `Definition of Done: PASSED` verdict via `Write-Host`,** which
  bypasses the PowerShell pipeline. `./build/dod.ps1 2>&1 | Out-File run.log` therefore captures the nested tool output
  but **silently drops the verdict line**, making a passing run look inconclusive. To capture everything, tee the
  console stream (`./build/dod.ps1 *>&1 | Tee-Object run.log`) or just read the verdict off the console.
- 👁 **A `build/*.ps1` script keeps a blank line between its `#requires` line and its `<#` help block.** PowerShell
  reads line comments that touch the block as part of it, so the shebang and `#requires` lines hide the help and
  `Get-Help ./build/<script>.ps1` shows only the generated syntax. The DoD's `build script help` stage
  ([`build/tests/script-help.tests.ps1`](../../build/tests/script-help.tests.ps1)) fails a script whose help does not
  parse.
- ⏳ **Local inner loop: `./build/dod.ps1 -Fast`, narrowed with `-Filter`** — template
  [Build](csharp.instructions.md#build). `./build/dod.ps1 -Fast -Affected` (only the test projects the change reaches
  since its merge base with `origin/main`) is the **local pre-push gate**, and the plain `./build/dod.ps1` is the full
  gate, which runs in the merge queue, on `main` and on release lines
  ([ADR-1263](../../docs/decisions/1263-branches-and-pull-requests-run-an-affected-gate-and-the-merge-queue-runs-the-full-gate.md)).
- 👁 **Never run two `dotnet build` / `dotnet test` invocations concurrently in one clone** — template
  [Build](csharp.instructions.md#build); here they fail with `MSB4018` on Nerdbank.GitVersioning's
  `obj/<config>/<tfm>/<Project>.Version.cs.new`.
- 👁 **Run the full `dod.ps1` before calling a structural change done** — template
  [Build](csharp.instructions.md#build); a `git mv` of a file open in the editor can be undone by the editor
  re-saving its stale buffer, leaving a duplicate type that only a clean Release build surfaces.

## 6. Clean Code / Refactoring (review-only)

Drawn from _Clean Code_ (R. C. Martin) and _Refactoring_ (Fowler / Beck). All 👁:

The template's [Clean Code](csharp.instructions.md#clean-code) rules: small functions that do one thing, few
parameters, command-query separation, no `null` where an empty form exists, intention-revealing names, typed exceptions
and no empty `catch` or `async void`, comments that say why and no commented-out code, a type for a concept with
invariants, the Law of Demeter, and no call nested in another call's arguments. The family adds:

- **Few parameters**: a clump that mirrors a well-known BCL shape
  (`ClaimsIdentity(authenticationType, nameType, roleType)`) is acceptable, and a private single-call-site helper isn't
  worth a parameter object.
- **Push store filtering into the DBMS** — template [Clean Code](csharp.instructions.md#clean-code) and
  [ADR-1080](../../docs/decisions/1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md): an intent-named store method
  (`FindEnabledByScopesAsync`, `GetEnabledIdentifiersAsync`), with `GetPageAsync` kept for genuine unfiltered
  pagination.
- **No repeated magic string literals** — template [Clean Code](csharp.instructions.md#clean-code); a sentinel in
  `HttpContext.Items` or `AuthenticationProperties.Items` carries a package prefix.
- **Dispose what you own** — template [Clean Code](csharp.instructions.md#clean-code); never leave an
  `HttpResponseMessage`, a `Stream` or an `IHttpClientFactory`-created client undisposed. **Parse JSON into a detached
  `JsonElement`** — template [Serialized and Stored Data](csharp.instructions.md#serialized-and-stored-data);
  `NCode.Identity.Json` also parses a `ReadOnlySequence<byte>`, so a token segment decoded into a rented buffer parses straight
  from the span, and a parsed `JsonElement` is named for what it holds, or `jsonElement`, never `document`.
- **Read `JsonElement` members with the `NCode.Identity.Json.JsonElements` extension methods, not hand-rolled accessors.** Use
  `TryGetPropertyValue<T>` / `TryGetValue<T>` — which convert by `ValueKind` and never throw — in place of a
  `TryGetProperty(name, out var e) + e.ValueKind == JsonValueKind.X + e.GetString()/GetInt64()/…` chain (the bare
  `GetInt64()`/`GetString()` form also throws on a mismatched kind); `IsNullOrUndefined()` in place of a bare
  `ValueKind == JsonValueKind.Null` absence check; and `EmptyObject` / `OrEmptyObject()` in place of re-parsing `"{}"`.
  The property readers (`TryGetPropertyValue<T>`, `TryGetPropertyNumericDate`) call `JsonElement.TryGetProperty`, which
  throws `InvalidOperationException` on anything but an object, so JSON parsed from wire input — a token's header or
  payload, a request body — is rejected unless `ValueKind == JsonValueKind.Object` before its members are read.
  Reference the **`NCode.Identity.Json`** foundation package — the shared home for these helpers
  ([ADR-1500](../../docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
- **Parse a date/time string with `NCode.Identity.Json.DateCoercion`, never a hand-rolled `DateTime(Offset).TryParse`.**
  `TryParseDateTimeOffset` / `TryParseDateTime` are the family's single lenient, invariant-culture date policy (zone-less
  → UTC, explicit offset honored, an out-of-range instant fails instead of clamping), so every package accepts the same
  formats and produces the same values. Apply it only where the value is **declared** to be a date (an explicit claim
  value type, a date-typed target) — **never** to sniff whether an arbitrary string "looks like" a date: lenient parsing
  turns `"1.2"` or `"10:30"` into dates and would rewrite them. A decoded JSON string claim stays a verbatim
  `ClaimValueTypes.String`. See [ADR-1121](../../docs/decisions/1121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md).
- ✅ **Read an RFC 7519 `NumericDate` (`exp`, `nbf`, `iat`, `auth_time`, a Unix-seconds parameter) strictly and
  range-guarded** — `JsonElement.TryGetNumericDate` / `TryGetPropertyNumericDate` for JSON (a number only, integer or
  fractional; never a string), `DateCoercion.TryParseNumericDate` for a claim value or parameter text, and
  `DateCoercion.TryFromNumericDate` for a number already in hand. They return `false` instead of throwing, so wire input
  can never fault the caller; `DateTimeOffset.FromUnixTimeSeconds`/`FromUnixTimeMilliseconds` are **banned** (`RS0030`)
  because they throw on out-of-range input. A time claim that is **present but invalid fails closed** — never read it
  as absent. Saturate (don't overflow) when adding a margin to an instant near `DateTimeOffset.MaxValue`, or compare
  the elapsed time instead (`now - instant - skew > limit`), which cannot overflow. See
  [ADR-1123](../../docs/decisions/1123-numeric-date-is-read-strictly-and-range-guarded.md).
- **Read a count of seconds a remote party sends (`expires_in`, `interval`) with `JsonElement.TryGetWholeInt32` /
  `TryGetPropertyWholeInt32`, or `[JsonConverter(typeof(WholeInt32JsonConverter))]` on a serialized member** — never
  `JsonElement.TryGetInt32` or the serializer's default `int` handling. JSON has one number type, so a peer may write
  60 as `60.0` or `6e1`, which the BCL readers reject; the `NCode.Identity.Json` readers accept any whole value in `int` range
  and still refuse a non-zero fraction, a string, and an out-of-range number. See
  [ADR-1277](../../docs/decisions/1277-a-whole-number-json-value-is-accepted-with-a-zero-fraction.md).
- **Stamp an instant that a JWT or a stored grant carries with `NCode.Identity.Time`'s whole-second helpers.** Use
  `TimeProvider.GetUtcNowWithPrecisionInSeconds()` (or `GetTimestampWithPrecisionInSeconds()` for ticks) and
  `WithPrecisionInSeconds()`, so a stored time equals the `NumericDate` it round-trips through. Reference the
  the **`NCode.Identity.Time`** foundation package, the home for general time helpers. A helper with a method body never lives
  in an `*.Abstractions` package. See [ADR-1500](../../docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md).
- **Bound a received JWT's lifetime through the lifetime requirement, never with ad-hoc `exp` arithmetic.** Cap a
  client-supplied JWT (assertion, request object) with `ValidateTokenLifeTime(o => o.MaxLifetime = …)`, fed from a
  `Min`-merged `*_max_lifetime` setting; it measures from the earliest asserted `iat`/`nbf` (or now plus skew), so the
  client cannot game it. A replay record for such a JWT is retained until `exp` plus the **effective** `clock_skew`, not
  a fixed margin. See [ADR-1187](../../docs/decisions/1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md).
- 👁 **Validate a received JWT only through `IJwtValidator`, stating its whole policy in the parameters.** Set
  `ValidSignatureAlgorithms` (and, for an encrypted token, `ValidKeyManagementAlgorithms`,
  `ValidContentEncryptionAlgorithms`, and `ValidCompressionAlgorithms = []`) from the governing setting, read at a trust
  boundary with `GetPermittedAlgorithms` so an **unset list permits nothing** — never `TryGetValue`-unset-means-all
  there. Name the decryption keys with `UseDecryptionKeys` (the recipient's keys, never the signer's); the validator
  never infers them. Never re-check `alg` by hand after validation in place of the allow-list — a check after decoding
  runs after the cryptography it must precede; an early check that only picks a precise error code is fine alongside it.
  The validator accepts only signed compact JWTs (a JWE only as a Nested JWT); there is no opt-in for `alg:none`. A
  validation handler fails with `JwtValidationContext.Fail(failureCode, reason)`, naming a `JwtValidationFailureCodes`
  value or a code of its own, and code that acts on why a token failed reads `JwtValidationResult.FailureCode` or a
  result's `FailureCode`, never the message text
  ([ADR-1278](../../docs/decisions/1278-token-validation-failures-carry-a-machine-readable-code.md)). See
  [ADR-1154](../../docs/decisions/1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md).
- 👁 **Protect a JWT addressed to a client through `IClientJwtCredentialSelector`.** It signs with the algorithm the
  client registered (`*_signed_response_alg`) and encrypts to a public key the client published, within the client's
  effective `*_values_supported` ceilings, and fails with `server_error` rather than substitute an algorithm. Never pick
  "the first listed algorithm" for a client-addressed token, never encrypt one to a tenant or shared key, and never
  compress a JWT the server produces (RFC 8725 §3.6). Add a new kind of client-addressed JWT as a new
  `ClientJwtProtectionSettings` profile. See
  [ADR-1155](../../docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md).
- 👁 **A key in a JOSE header carries only its public members.** An `epk` (RFC 7518 §4.6.1.1) or a DPoP `jwk`
  (RFC 9449 §4.2) is written without any member its `JsonWebKeyTypeDescriptor.PrivateMembers` lists, and a received
  one that carries such a member is refused: a header is readable by anyone holding the token, so an ephemeral private
  key there discloses the content encryption key. A test that reads a private member back out of a produced token is
  asserting the defect, not the behavior.
- 👁 **Bring a client's key in through `IClientKeyImporter`, and read a client's keys through `IClientKeyResolver`.**
  A client's public keys come from one source — the keys stored for it or the keys it publishes at its `jwks_uri`,
  never both — and every entry point (dynamic registration, the `jwks_uri` fetch, the management API) imports a key
  under one policy: a public key of a registered key type, no private or symmetric member, a `use` / `key_ops` / `alg`
  that agree with each other and the key, and a `kid` unique among the client's keys. Never call `ISecretImporter`
  directly for a client's key, never read `OpenIdClient.SecretKeys` for a client's public keys (it ignores the
  `jwks_uri`), and never use a key's `kid` as its secret identifier: a `kid` is the owner's name for the key
  (`PersistedSecret.KeyId`), while a secret identifier is globally unique. Match a key type by its JSON Web Key `kty`
  through `ISecretKeyTypeCatalog.TryGetByJwkKeyType`, never a `switch` over `kty` values. See
  [ADR-1161](../../docs/decisions/1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md).
- 👁 **Read a client's certificate only through `IClientCertificateResolver`.** It is the one place that knows where the
  deployment's certificate comes from: the connection under direct TLS, and behind a proxy only the trusted forwarded
  header, decoded by the `IClientCertificateHeaderDecoder` registered for the configured format. Never read
  `HttpContext.Connection.ClientCertificate` or a forwarded header directly — behind a proxy the connection's certificate
  is the proxy's. A forwarded value that cannot be decoded is `invalid_request`, never an absent certificate: a
  present-but-malformed credential fails visibly instead of looking like one that was never sent. See
  [ADR-1172](../../docs/decisions/1172-forwarded-client-certificates-are-decoded-by-a-registered-format.md).
- **Give a typed `Claim` its value in the declared type's XML Schema lexical form** — numbers via
  `value.ToString(CultureInfo.InvariantCulture)`, never culture-formatted (`"1,000"`). The JWT claim ⇄ JSON mapping
  (`ClaimValueMapper`, the single place a `Claim.ValueType` is interpreted) parses each value strictly in its declared
  type and emits a value outside it as a verbatim string. See
  [ADR-1122](../../docs/decisions/1122-jwt-claim-values-map-to-json-by-declared-type.md).

## 7. Testability & unit-test-friendliness

Production code **must** be unit-test-friendly. These are 👁 review-only except the `InternalsVisibleTo` wiring.

- 👁 **Prefer an instance service behind an interface, registered as a singleton, to a `static` utility class, and
  minimize `static`** — template [Testability](csharp.instructions.md#testability).
- 👁 **Never capture request or ambient context in stored state** — template
  [Testability](csharp.instructions.md#testability); a read-over-ambient type is a **live view** over
  `IHttpContextAccessor`.
- 👁 **Constructor-inject every dependency** — template [Testability](csharp.instructions.md#testability); the
  sanctioned singleton case resolves a scoped or open-generic collaborator from `HttpContext.RequestServices` or
  `IServiceProvider` at the point of use and stays testable through a real `ServiceProvider`.
- 👁 **Facade un-mockable dependencies** — template [Testability](csharp.instructions.md#testability): the
  `internal interface` and its `internal sealed` forwarding implementation live under a `Facades/` folder, and the
  passthrough implementation is `[ExcludeFromCodeCoverage]`.
- 👁 **`internal virtual` is the sanctioned local override seam** — template
  [Testability](csharp.instructions.md#testability), for a helper or a factory method that allocates an object handed
  to a dependency; reach for an extracted interface when the seam is reused or substantial.
- 👁 **Internal implementations stay unsealed so tests can mock them; public types are mocked through their
  interface** — template [Testability](csharp.instructions.md#testability). `CA1852` doesn't ask to seal them: it skips
  every assembly with `InternalsVisibleTo`, which each package grants its tests, and flags only internal types in an
  assembly that grants none. Never reach for `public virtual` to make a public class mockable; `sealed` stays correct
  for data, options and event types (§2) and facade implementations.
- ✅/👁 **`internal` for test access** — the test template's
  [InternalsVisibleTo](csharp-tests.instructions.md#internalsvisibleto): each package grants its `.Tests` project and
  `DynamicProxyGenAssembly2`, and a package with a benchmark project also grants its `.Benchmarks` project
  ([ADR-1386](../../docs/decisions/1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md)).
- 👁 **Test the unit through its public surface; privates are covered transitively** — the test template's
  [What a Test Proves](csharp-tests.instructions.md#what-a-test-proves).
- 👁 **Tested-or-excluded.** Every type/member that can carry `[ExcludeFromCodeCoverage]` is **either** exercised by
  tests **or** annotated with it (with a one-line reason when non-obvious). Exclude thin passthrough infrastructure
  (facades), pure DI/registration wiring, and framework-event adapters; **test** anything with behavior.

### Tests

Test-project conventions (xUnit, Moq `.Verifiable()`, `MockBehavior.Strict`, `Method_State_Expected` naming, `#region`
organization) live in [`csharp-testing.instructions.md`](csharp-testing.instructions.md).

## 8. Provenance & versioning

- ✅ Versioning is stamped by **Nerdbank.GitVersioning** (single root [`version.json`](../../version.json)), **lockstep**
  across the family. `AssemblyVersion` is pinned to `major` precision so patches/minors are drop-in within a major.
  Every workflow that builds or tests a .NET project checks out the full history (`fetch-depth: 0`): the version is
  computed from commit height, and the build fails on the shallow clone `actions/checkout` makes by default.
  See [ADR-1003](../../docs/decisions/1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md).
- ✅ Packages are **source-linked to GitHub** and ship a portable symbol package (`.snupkg`) so consumers can step into
  the exact released source. See [ADR-1003](../../docs/decisions/1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md).
- ✅ **Package versions live centrally** — template [Build](csharp.instructions.md#build).

## 9. Change intake (open source)

A change starts as a GitHub issue, and a change consumers can observe is decided and recorded as an ADR before or with
its pull request, as
[project-template ADR-0010](../../docs/decisions/project-template/0010-a-change-consumers-can-observe-is-decided-before-it-is-built.md)
says. Keep the `## [Unreleased]` catalog of [`CHANGELOG.md`](../../CHANGELOG.md) matching the code, as
[project-template ADR-0015](../../docs/decisions/project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)
says for a pre-release project.

## 10. XML documentation

- 👁 **New or changed API carries an XML doc comment with a substantive `<summary>`** — template
  [XML Documentation](csharp.instructions.md#xml-documentation).
- 👁 **Documentation describes the timeless present, never the change** — template
  [XML Documentation](csharp.instructions.md#xml-documentation); the same discipline applies to ADR prose.
- 👁 **A `<summary>` tag is multi-line, and a property summary opens with "Gets or sets", "Gets" or "Sets" by its
  accessors** — template [XML Documentation](csharp.instructions.md#xml-documentation).
- 👁 **Prose spells protocol terms the spec way, not the .NET-type way.** In `///` and `//` text write **"JSON Web
  Token (JWT)"** (and likewise JSON/JOSE/OAuth/OpenID in their canonical casing); do not carry the `JsonWebToken` type's
  Pascal casing into documentation. This applies only to prose — a `<see cref="JsonWebToken"/>` or an identifier keeps
  its real casing.
- ✅ **No internal citations in `///` doc comments** — template
  [XML Documentation](csharp.instructions.md#xml-documentation). Put the citation in a `//` line directly after the doc
  comment, above the declaration's attributes (`// See ADR-1150.`). The `doc comment citations` stage of
  [`build/dod.ps1`](../../build/dod.ps1) runs
  [`build/doc-comment-citations.ps1`](../../build/doc-comment-citations.ps1), which fails on any `///` line under
  `src/`, `hosts/`, `analyzers/` or `tests/` that matches `ADR-\d{4}` or `docs/decisions/`.
- 👁 **No internal implementation names in `///` doc comments** — template
  [XML Documentation](csharp.instructions.md#xml-documentation).
- 👁 **A minimal-API handler's `///` reaches its OpenAPI operation only under specific conditions** — template
  [XML Documentation](csharp.instructions.md#xml-documentation).
- 👁 **OpenAPI document contributions belong in the composition layer, not the host.** The document describes the
  server's own endpoints, so register document transformers (security schemes, tag groups, request/response examples)
  in the composition root's `AddOpenApi(...)`, and expose the document and the API-reference UI through the server's
  turnkey map extension — a host never re-declares them. See
  [ADR-1481](../../docs/decisions/1481-the-composition-layer-serves-the-api-reference-and-each-management-problem-says-when-it-occurs.md).
- 👁 **Never embed secret material in a served OpenAPI document or renderer configuration.** A renderer may pre-fill
  credentials for a one-click login, but pre-filling a **secret** bakes it into the served config where anyone who can
  load the reference page can read it — pre-fill a secret **only in Development**. The login flow itself (for example
  OAuth2 `client_credentials`) must be wired in **every** environment so the production cold-start/bootstrap path works;
  only the secret pre-fill is environment-gated. See
  [ADR-1481](../../docs/decisions/1481-the-composition-layer-serves-the-api-reference-and-each-management-problem-says-when-it-occurs.md) and
  [ADR-1011](../../docs/decisions/1011-secret-management-api.md).
- 👁 **A renderer builds a `x-www-form-urlencoded` body from the schema, not from examples.** Optional schema
  properties render unchecked and are omitted from the request, and a named `example` is not applied to form fields. To
  make a field pre-checked and pre-filled, a document transformer marks it `required` and sets its schema `Default`;
  the renderer's credential location must match the method the client is registered for (the bootstrap administrator
  registers `client_secret_basic`, so its credentials go in the `Authorization` **header**). See
  [ADR-1481](../../docs/decisions/1481-the-composition-layer-serves-the-api-reference-and-each-management-problem-says-when-it-occurs.md) and
  [ADR-1145](../../docs/decisions/1145-a-client-authenticates-only-with-its-registered-method.md).
- 👁 **Handle credential and secret bytes in a pinned, zeroed-on-return buffer.** Encode a password or secret into a
  `SecureMemoryPool<byte>` rental via `SecureEncoding.UTF8` (`NCode.Buffers`) — never a raw `ArrayPool<byte>` +
  manual `CryptographicOperations.ZeroMemory`, and never a plain `Encoding.UTF8.GetBytes` that leaves the plaintext
  lingering on the GC heap. Encode/decode **once** at the earliest boundary and thread the sensitive payload as
  `ReadOnlyMemory<byte>` through the (async) call chain rather than re-encoding a `string` in a leaf method. A secret
  that enters from configuration (already an unavoidable managed `string`) is supplied **pre-encoded** (Base64Url) and
  decoded straight into the secure buffer, so a raw process-memory snapshot does not expose the plaintext directly. See
  [ADR-1011](../../docs/decisions/1011-secret-management-api.md).
- ✅ **Encode and decode text only through a strict encoding.** Use `SecureEncoding.UTF8` / `SecureEncoding.ASCII`
  (`NCode.Buffers`), which throw on invalid input, never `Encoding.UTF8`, `Encoding.ASCII` or `Encoding.Default`, which
  substitute `?` or U+FFFD silently. An overload that takes no encoding (`File.ReadAllText(path)`,
  `new StreamReader(stream)`, `new StringContent(text)`) is replaced by the one that takes `SecureEncoding.UTF8`;
  `TypedResults.Text`/`Content` always pass `contentEncoding: SecureEncoding.UTF8`, except a media type without a
  charset parameter (`application/jwt`), which is written through `Utf8TextResults.Create`. See
  [ADR-1266](../../docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md).
- 👁 **Prefer `Span<byte>` / `ReadOnlyMemory<byte>` / `IBufferWriter<byte>` over `byte[]` for binary and key material.**
  A producer writes into a caller-supplied writer — `void Export…<TWriter>(ref TWriter destination) where TWriter :
IBufferWriter<byte>, allows ref struct` (as in `SymmetricSecretKey.ExportPrivateKey` /
  `AsymmetricSecretKey.ExportSubjectPublicKeyInfo`) — instead of returning a freshly-allocated `byte[]`; inputs are
  `ReadOnlySpan<byte>` / `ReadOnlyMemory<byte>`. Rent a pooled writer with
  `BufferFactory.CreatePooledBufferWriter(isSensitive:)` (`NCode.Buffers` over `Nerdbank.Streams`), not a bare
  `ArrayBufferWriter<byte>`, `ArrayPool<T>.Shared` or `MemoryPool<T>.Shared` — `isSensitive: true` for secrets (pinned,
  zeroed `SecureMemoryPool`), `false` for non-secret bytes. Read a multi-segment sequence segment by segment or with
  `SequenceReader<byte>`, and take one contiguous span with `GetSpanLease(isSensitive:)` only when a consumer needs
  it — never manual concatenation or `.ToArray()`. This is the general convention for all byte-array/memory/buffer
  usage; existing `byte[]` surfaces migrate incrementally. See
  [project-template ADR-0112](../../docs/decisions/project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md)
  and [ADR-1266](../../docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md).
- 👁 **Allocate a `byte[]` only when an external API demands an array, and then rent or pin it.** A rented buffer
  (`BufferFactory.Rent(…, isSensitive:, out …)`, `CreatePooledBufferWriter(isSensitive:)`) or a small, fixed
  `stackalloc` is the default for every transient; a secret held by an object or across an `await` is a sensitive
  rental held as an `IDisposable` lease beside the `Memory<byte>` it returned
  (`BufferFactory.Rent(…, isSensitive: true, out Memory<byte>)`, a pinned secure-pool array zeroed when returned),
  because `BufferFactory.CreatePinnedArray` returns a `ref struct` for synchronous, method-local use only; never a bare
  array or `GC.AllocateArray`. An API that needs an array gets one from a rental via `MemoryMarshal.TryGetArray`.
  `new byte[]`, `.ToArray()`, `ArrayBufferWriter<T>` and `MemoryStream` appear only at an API that requires them (an SDK
  request stream), over a rented or pinned array. An interface method's output is a `Span<T>`/`Memory<T>` with a
  returned count, or a generic `ref TWriter … allows ref struct` writer with a sizing method beside it
  (`IAesKeyWrap.WrapKey` with `GetEncryptedContentKeySizeBytes`); Moq proxies such methods. See
  [ADR-1266](../../docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md). `BufferFactory.Rent`'s `out` span or
  memory is exactly the requested length — never slice it back to that length — and the `out Span<T>` overload is the
  default; take `out Memory<T>` only for a buffer stored in a field or used across an `await`. A writable `Span<T>` or
  `Memory<T>` parameter is a destination whose size the caller knows before the call — a fixed size, a sizing member
  beside it, or a length the caller chooses — and a method whose output can be shorter returns the count it wrote;
  output whose size the caller cannot know goes to a `ref TWriter` writer. A helper that rents a buffer, fills it and
  hands it to its caller returns the lease and a read-only view, `out ReadOnlySpan<T>`, or `out ReadOnlyMemory<T>`
  across an `await`, as `CommonJoseEncoder.EncodeUtf8` does; only `BufferFactory.Rent` hands back a writable one.
- 👁 **Parse a fixed-segment token (compact JWS or JWE) without allocating its parts.** Split it with
  `MemoryExtensions.Split` into a `stackalloc Range[segmentCount + 1]` — never `string.Split` — and reject any count
  but `segmentCount`: the spare slot makes extra segments count past it instead of folding into the last range. The JWS
  signing input is the original text up to the end of the payload (`token.AsSpan(..ranges[1].End)`), never a
  re-concatenated string. Decode each base64url segment into a `BufferFactory.Rent` buffer sized by
  `Base64Url.GetByteCountForDecode`: `NCode.Base64Url` throws `FormatException` on an invalid length, an illegal
  character, or `=` padding, so a successful decode fills that buffer exactly and needs no slice by `bytesWritten`. A
  sync helper does the span work when the caller is `async` (`ClientAssertionClientAuthenticationHandler`). See
  [ADR-1266](../../docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md).
- 👁 **Sensitive text is never a `string`.** Read a key or secret file as bytes into a secure buffer
  (`File.OpenHandle` + `RandomAccess.Read`) and decode it from UTF-8; write sensitive XML with an `XmlWriter` over a
  sensitive pooled writer and read it back with an `XmlReader` over that buffer; transform identifiers on hot paths
  into rented `char` spans (`MemoryExtensions.ToLowerInvariant(source, destination)`) instead of allocating strings.
  A `string` is produced only for a non-secret value at a storage or wire boundary — an envelope stored in a text
  column, a persisted identifier — exactly once, written in place by `string.Create` — never by renting a `char`
  buffer and copying it out with `new string(span)`. Pass span inputs through an `internal readonly ref struct`
  state (`string.Create`'s `TState` allows ref structs) and `ReadOnlyMemory<T>` inputs through a value tuple; when an
  interface implementation writes the characters, check `writer.WrittenCount == chars.Length`, so no `'\0'` remainder
  is ever persisted. See [ADR-1266](../../docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md) and
  [ADR-1235](../../docs/decisions/1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md).
- 👁 **Size a collection from a count you already know, and keep per-call paths free of avoidable allocations** —
  template [Memory and Performance](csharp.instructions.md#memory-and-performance); test `Count == 0` before a
  `foreach` over an interface, so its boxed enumerator is not created for nothing
  ([ADR-1266](../../docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).
- 👁 **Cryptographic and numeric hot-path primitives are optimized aggressively; readability is secondary there.**
  Inner loops of the math/crypto primitives (field, scalar, and group arithmetic; encoders; constant-time helpers)
  should use every applicable trick — `[MethodImpl(MethodImplOptions.AggressiveInlining |
MethodImplOptions.AggressiveOptimization)]`, manual unrolling of tiny fixed-count (e.g. 4/5-limb) loops,
  `stackalloc`, `MemoryMarshal`/`Unsafe` `ref`-based **zero-copy** reinterpretation (`MemoryMarshal.Cast<byte, T>`,
  `MemoryMarshal.AsBytes`, with a JIT-constant `BitConverter.IsLittleEndian` guard for portability), and bounds-check
  elision — even when the result is dense and harder to read, to minimize allocations and branches. **Two constraints override speed and are never traded away:** (1) constant-time
  code must not gain a data-dependent branch or memory access from any optimization; (2) correctness is pinned by
  test vectors / differential tests that must stay green. This is a deliberate, scoped exception to the clean-code
  rules above, limited to performance-critical primitives. A new optimization of a primitive is justified by a benchmark
  in `benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks`, whose before and after numbers the pull request quotes
  ([project-template ADR-0112](../../docs/decisions/project-template/0112-binary-data-uses-spans-pooled-buffers-and-cleared-secrets.md)).
  See [ADR-1266](../../docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md) and
  [ADR-1386](../../docs/decisions/1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md).

## 11. Logging & metrics

- 👁 **A library never registers or removes a logging provider** — template [Logging](csharp.instructions.md#logging);
  the reference hosts log with `builder.Logging.ClearProviders().AddConsole().AddEventSourceLogger()`
  ([project-template ADR-0115](../../docs/decisions/project-template/0115-a-host-logs-to-standard-output.md)).
- 👁 **Log through source-generated `[LoggerMessage]` methods** in `Logging/Log.cs` — template
  [Logging](csharp.instructions.md#logging); mark the `Log` class `[ExcludeFromCodeCoverage]` (generated plumbing).
- ✅ **A shipping package never switches off its consumers' `[LoggerMessage]` generator** — `Directory.Build.targets`
  fails the build of a packable project that depends on `Microsoft.Extensions.Telemetry.Abstractions` (which
  `Microsoft.Extensions.Http.Resilience` brings) or sets `DisableMicrosoftExtensionsLoggingSourceGenerator`; bound
  outbound calls with `Polly.Core` instead. See
  [ADR-1385](../../docs/decisions/1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md).
- 👁 **A message template is a constant with named placeholders** — template
  [Logging](csharp.instructions.md#logging); a value that also feeds an error or a response is defined once (§6).
- 👁 **Event IDs are named `const`s in a per-package `Logging/EventIds.cs`** — template
  [Logging](csharp.instructions.md#logging): an `internal static class EventIds` with a `private const int Base` set to
  the package's band (below) and one `public const int` per event, `Base + n`, named exactly as its `[LoggerMessage]`
  method.
- 👁 **Every runtime package is pre-assigned a disjoint 1000-wide Event-ID band** in the registry below; a package
  starts using its band when it adds its first log. An `EventId` only has to be unique within an `ILogger<T>` category,
  but a family-wide band makes it **globally unique across packages**, so an operator's log filter or alert keys on a
  stable number regardless of which assembly emitted it. IDs are **append-only** — never renumber or reuse a shipped
  value (same discipline as enum members and the public API §2), and a band, once assigned, is never renumbered. Until
  the first stable release no ID has shipped: the ID of a removed event is free, and no comment reserves it
  ([ADR-1025](../../docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)).
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
    | `9000`–`9999`   | `NCode.Identity.Jwt`                                |
    | `10000`–`10999` | `NCode.Identity.Settings`                           |
    | `11000`–`11999` | `NCode.Identity.Registration`                       |
    | `12000`–`12999` | `NCode.Identity.Server`                             |
    | `13000`–`13999` | `NCode.Identity.OpenId.Host`                        |
    | `14000`–`14999` | `NCode.Identity.Events`                             |
    | `15000`–`15999` | `NCode.Identity.Dispatch`                           |
    | `16000`–`16999` | `NCode.Identity.SharedState`                        |
    | `17000`–`17999` | `NCode.Identity.SharedState.Valkey`                 |
    | `18000`–`18999` | `NCode.Identity.OpenId.Client`                      |
    | `19000`–`19999` | `NCode.Identity.Bff`                                |
    | `20000`–`20999` | `NCode.Identity.OpenId.Host.MigrationService`       |
    | `21000`–`21999` | `NCode.Identity.Jose.SdJwt`                         |
    | `22000`–`22999` | `NCode.Identity.Cose`                               |
    | `23000`–`23999` | `NCode.Identity.Cwt`                                |
    | `24000`–`24999` | `NCode.Identity.Bff.Blazor.Client`                  |
    | `25000`–`25999` | `NCode.Identity.Bff.Blazor`                         |
    | `26000`–`26999` | `NCode.Identity.Claims`                             |
    | `27000`–`27999` | `NCode.Identity.Cryptography`                       |

### Metrics

- 👁 **Measure a signal once, through its owner** — template
  [Metrics and Traces](csharp.instructions.md#metrics-and-traces) and
  [ADR-1197](../../docs/decisions/1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md):
  enrich `http.server.request.duration` through `IHttpMetricsTagsFeature`, count domain outcomes through the event
  seam's counter, and add a family instrument only for a mechanism such as rate limiting or shared state.
- 👁 **Create meters through `IMeterFactory`** — template [Metrics and Traces](csharp.instructions.md#metrics-and-traces);
  a package's instruments live in one internal `…Metrics` singleton, and a meter name starts with `NCode.Identity.`, so
  the host's `AddOpenTelemetryMetrics` subscribes to it without a change.
- 👁 **An event adds metric dimensions by implementing `ISupportMetricAttributes`**, never through a counter at its
  call site; an endpoint tags the request through `OpenIdMetricAttributes.Add`.
- 👁 **Name instruments and attributes `ncode.identity.{area}.…`** — template
  [Metrics and Traces](csharp.instructions.md#metrics-and-traces); counts use a `{noun}` unit.
- 👁 **A metric attribute's value comes from a vocabulary the server controls** — template
  [Metrics and Traces](csharp.instructions.md#metrics-and-traces); a tenant, client, subject or address is never one.
- 👁 **Test every instrument with `MetricCollector<T>`** — template
  [Metrics and Traces](csharp.instructions.md#metrics-and-traces).
- 👁 **A new or renamed instrument or attribute updates [`docs/metrics.md`](../../docs/metrics.md)** in the same
  change — template [Metrics and Traces](csharp.instructions.md#metrics-and-traces).

### Traces

- 👁 **Trace only your own mechanisms, through one `ActivitySource` named like your meter** — template
  [Metrics and Traces](csharp.instructions.md#metrics-and-traces) and
  [ADR-1197](../../docs/decisions/1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md);
  a span never carries a handle, subject or client id either.

## 12. Configuration & options

- 👁 **Configuration is a strongly-typed options class in an `Options/` folder** — template
  [Configuration and Options](csharp.instructions.md#configuration-and-options), bound through `BindStrictly` (§4).
- 👁 **`IOptions<T>` for start-up-fixed values and `IOptionsMonitor<T>` for values that change at runtime; an options
  class is a data type (§2)** — template [Configuration and Options](csharp.instructions.md#configuration-and-options).
- 👁 **A rule that validates configured data also validates the same data written at runtime** — template
  [Nullability and Invariants](csharp.instructions.md#nullability-and-invariants) and
  [Configuration and Options](csharp.instructions.md#configuration-and-options); the tenant-selection seam judges a
  tenant's address for configuration and the management API alike
  ([ADR-1160](../../docs/decisions/1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)).
- 👁 **Each strategy validates its own options** — template
  [Configuration and Options](csharp.instructions.md#configuration-and-options).
- 👁 **A default for runtime-written data is derived through the seam that judges it, then judged and stored like an
  explicit value** — template [Configuration and Options](csharp.instructions.md#configuration-and-options): every write
  path asks the owning seam (`ITenantResolver.GetDefaultIssuerAsync`) before it validates
  ([ADR-1165](../../docs/decisions/1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)).
- 👁 **A configuration seeder names exactly the target it seeds** — template
  [Configuration and Options](csharp.instructions.md#configuration-and-options): `BootstrapAdmin`,
  `DeveloperInteractiveSeed` and `ConformanceSeed` require their `TenantId`
  ([ADR-1166](../../docs/decisions/1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md)).
- 👁 **A period that options hand to a timer is validated when the host starts with `TimerPeriod.IsValid`**
  (`NCode.Identity.Time`) — template [Configuration and Options](csharp.instructions.md#configuration-and-options) and
  [ADR-1137](../../docs/decisions/1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md).
