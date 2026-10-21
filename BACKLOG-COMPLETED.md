# Backlog — completed & retired items (archive)

This file is the **read-only archive of backlog items that have been completed or retired**. It is the companion to
[`BACKLOG.md`](BACKLOG.md): that file is the single, primary view of **pending** work; this file preserves the full
original text of every item that has **left** that view, so the richer problem/design context survives beyond the terse
[`CHANGELOG.md`](CHANGELOG.md) line that closed it.

**Why this exists.** Backlog IDs outlive their entries — ADRs, the [`CHANGELOG.md`](CHANGELOG.md), and code comments
reference a number long after the item is done. Previously a completed entry was simply **deleted** from
[`BACKLOG.md`](BACKLOG.md), so its original wording survived only in git history. This archive recovers that text and
keeps it discoverable without `git` archaeology, per
[ADR-0008](docs/adr/0008-conventions-and-lessons-live-in-the-repository.md) (conventions, reasoning, and recurring work
live in the repository). It realizes backlog item `X2`.

**How entries get here.** When a pending item in [`BACKLOG.md`](BACKLOG.md) is **fully** completed (or retired by being
absorbed into another item), move its full text here **in the same change** — do **not** delete it. Keep it under its
group heading in ID order, preserve the original wording verbatim, and add a trailing `_Closed:_` line pointing at the
[`CHANGELOG.md`](CHANGELOG.md) entry and/or ADR that closed it. The ID stays recorded in the
[ID ledger](BACKLOG.md#backlog-id-ledger--never-reuse-a-number) — a number is **never** reused or backfilled.

> **Never reuse a number.** Every ID at or below a group's high-water mark is **allocated** — whether still pending in
> [`BACKLOG.md`](BACKLOG.md), archived here, or retired without a surviving entry. To find the next free ID for a group,
> take the maximum of the ledger high-water mark, the highest pending ID in [`BACKLOG.md`](BACKLOG.md), and the highest
> archived ID here, then allocate strictly **above** it.

---

## Deferred — evaluated, intentionally not-yet (group `D`)

- **`D2` — Front-Channel Logout and Session Management (`check_session_iframe`).** The OpenID Provider
  implements RP-Initiated Logout and Back-Channel Logout (both cookie-independent), but **not** Front-Channel
  Logout 1.0 (per-RP hidden iframes) or Session Management 1.0 (`check_session_iframe` / `session_state`). Both
  depend on third-party cookies inside an iframe, which the browser platform (Safari ITP, Firefox partitioning,
  Chrome deprecation) is removing, so they are unreliable by construction. The session model records each
  participating client and the logout pipeline is a mediator seam, so a deployment that still needs front-channel
  behavior can add it without a redesign. Rationale: [ADR-0074](docs/adr/0074-session-management-and-logout.md).
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/`,
  `src/NCode.Identity.OpenId.Authentication/Sessions/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0074](docs/adr/0074-session-management-and-logout.md)._
- **`D3` — Index tuning for resource-server runtime query filters.** The resource-server reads now push their
  predicates to the DBMS ([ADR-0080](docs/adr/0080-store-query-methods-push-filtering-to-dbms.md)), but no supporting
  indexes were added — resource servers and scopes are a small, slowly-changing per-tenant set, so a sequential scan is
  cheap today. If a deployment carries large per-tenant resource-server/scope counts, add an index on
  `ScopeEntity.NormalizedValue` (for the owns-any-scope join) and consider a filtered index on
  `ResourceServerEntity.IsDisabled`; measure before adding, since the extra write cost may not pay off for small sets.
  Touch points: `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/ScopeEntity.cs`,
  `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/ResourceServerEntity.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0080](docs/adr/0080-store-query-methods-push-filtering-to-dbms.md)._
- **`D4` — CIBA push delivery durability and request-object encryption advertisement.** CIBA **push** delivery
  ([ADR-0085](docs/adr/0085-ciba-grant.md)) issues and `POST`s the tokens to the client's `client_notification_endpoint`
  on a best-effort basis — a failed delivery is logged, not retried or queued, and the client cannot fall back to
  polling. A deployment that needs at-least-once push should replace `ICibaNotificationService` with a durable
  (store-and-retry) transport; a built-in retry/outbox is deferred until a concrete need. Separately, signed backchannel
  request objects are validated and advertised via `backchannel_authentication_request_signing_alg_values_supported`,
  but the matching **encryption** alg/enc advertisement (`backchannel_authentication_request_encryption_*`) is not yet
  surfaced — encrypted request objects still decrypt through the shared JWT path, but the capability is not advertised.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/BackchannelAuthentication/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0090](docs/adr/0090-durable-outbound-delivery-dispatch-adapter.md),
  [ADR-0085](docs/adr/0085-ciba-grant.md)._
- **`D6` — `PageRequest` pagination DTO across the store `GetPageAsync` family.** The evolving _filter_ sets are now
  DTOs ([ADR-0099](docs/adr/0099-forward-compatible-evolving-parameters.md), `GrantFilter`), but the stable
  `(cursor, limit)` pagination pair is still positional on every `*Store.GetPageAsync` (`IGrantStore`, `IClientStore`,
  `IClientGrantStore`, `IResourceServerStore`, `IServerStore`, `ITenantStore`, `ILocalAccountStore`) and the generic
  `BaseStore`. Wrapping it in a shared `PageRequest { Cursor, Limit }` (in `NCode.Persistence.Abstractions`, beside
  `PagedResult<T>`) would future-proof pagination evolution (e.g. a sort direction) non-breakingly and unify the
  signatures. Deferred: the pair has not churned, so the value is low today and the change touches every store +
  implementation + call site + test. Do it as one mechanical sweep before GA if pagination needs to grow.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0099](docs/adr/0099-forward-compatible-evolving-parameters.md)._
- **`D7` — Sweep the remaining `…Logic` grab-bag namespaces into purpose-based names.** The secret-key `…Secrets.Logic`
  and `…Jose.EdDsa.Logic` grab-bags were split into cohesive purpose namespaces (`…KeyTypes` / `…Collections` /
  `…KeyMaterial` / EdDsa `…KeyTypes`; see the [`CHANGELOG.md`](CHANGELOG.md) "Changed" entry). The same vague `Logic`
  bucket still exists elsewhere — `…OpenId.Authentication(.Abstractions).Logic`, `…OpenId.Core.Logic`,
  `…Secrets.Persistence.Logic`, `…NCode.Identity(.Abstractions).Logic`, and the per-endpoint `Endpoints/**/Logic/`
  folders. Each should be evaluated and, where it is a genuine grab-bag (not a legitimately-named slice), split the same
  way for family-wide consistency with the §3 "folder/namespace by purpose" convention. Scoped out of the Secrets sweep
  to keep that change reviewable; revisit as a dedicated cohesion pass.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased] ("Every remaining `Logic` grab-bag namespace is split…"). Every
  bucket was a genuine grab-bag and was split: `NCode.Identity.Cryptography`; the `NCode.Identity.Secrets.Persistence`
  package root; `NCode.Identity.OpenId.Principals`; and `…Authentication.Pkce` / `.OutboundHttp` / `.ResourceServers`
  / `.Persistence` / `.Tokens` (which also absorbed the `…Authentication.Models` and `…Endpoints.Token.Models`
  buckets); per-endpoint services moved to their endpoint's root namespace. The catch-all `AddLogicServices()`
  registration became one registration per purpose folder, and the rule is codified in
  [`csharp-production.instructions.md`](.github/instructions/csharp-production.instructions.md) §3._
- **`D8` — Redesign JWT validation as ASP.NET Core-style requirements/handlers and move the JWT contracts into
  `.Abstractions`.** The library was renamed `NCode.Identity.JsonWebTokens(.Abstractions)` →
  `NCode.Identity.Jwt(.Abstractions)` (package/assembly/namespace); the dual-purpose `IJsonWebTokenService` was
  **segregated** into `IJwtEncoder` + `IJwtValidator` (with `DefaultJwtEncoder` / `DefaultJwtValidator` impls);
  built-in validators became **pure-data requirement records** (`IJwtValidationRequirement`:
  `ClaimValueRequirement` — with `Issuer`/`Audience`/`TokenType`/`TokenLifetime`/`CertificateLifetime`/`Assertion`
  requirements) carried on `JwtValidationParameters.Requirements`, executed by `IJwtValidationHandler` /
  `JwtValidationHandler<TRequirement>` strategies resolved from DI and **externally contributable** via
  `AddJwtValidationHandler<T>()`, voting through `JwtValidationContext.Succeed(req)`/`Fail(reason)` (replacing the
  throw-based validators). The family was renamed uniformly to `Jwt*` (`JwtValidationParameters`/`Result`/`Context`,
  `JwtEncodingParameters`, `JwtLibrary`, `AddJwtLibrary`, `Jwt*Exception`), and all contracts + requirement records +
  fluent builders + exceptions + `JsonClaimValueTypes` moved into `.Abstractions` under purpose namespaces
  (`…Jwt.Encoding` / `…Jwt.Validation` / `…Jwt.Claims`), leaving the executor, handlers, key resolver, and claims
  factory in impl; `NCode.Identity.OpenId.Authentication` now references `NCode.Identity.Jwt.Abstractions` only.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0120](docs/adr/0120-jwt-validation-requirements-and-handlers.md)._
- **`D9` — `DictionaryExtensions.TryGetValue<T>` carries logic inside the `*.Abstractions` package.**
  `NCode.Identity.Jose.Abstractions/Extensions/DictionaryExtensions.cs` exposes a C# 14 `extension(IDictionary<string,
  object>)` member with a real method body — a typed `TryGetValue<T>` — which violates the house rule that
  `*.Abstractions` packages hold only dumb DTOs / POCOs / interfaces / contracts (no logic). It stays for now because it
  is consumed widely by **Abstractions-only** OpenId handlers via `using NCode.Identity.Jose.Extensions;`, so relocating
  it (e.g. into an impl/helper package, or replacing the call sites with a local helper) is a cross-cutting change best
  done deliberately. Revisit as a dedicated no-logic-in-abstractions cleanup. Touch points:
  `src/NCode.Identity.Jose.Abstractions/Extensions/DictionaryExtensions.cs` and every `NCode.Identity.Jose.Extensions`
  consumer.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. Relocated to `NCode.Json.ObjectGraphExtensions`; the real
  object-graph callers turned out to be two impl packages (`NCode.Identity.Jose`, `NCode.Identity.JsonWebTokens`) that
  already referenced `NCode.Json`, not the OpenId handlers (those import the namespace only for the time/date
  extensions — now tracked as `D11`)._
- **`D10` — Numeric claim reads couple to the exact boxed CLR type from loosely-typed deserialization.** The strict
  `obj is T` match in `DictionaryExtensions.TryGetValue<T>` (and similar reads over an `ObjectJsonConverter` object
  graph) assumes one specific boxed numeric width, but `ObjectJsonConverter.Default` maps integers **most-specifically**
  (`p2c` `4096` → `int`, a larger value → `long`/`ulong`), so the width is value-dependent. Consumers already disagree:
  `Pbes2KeyManagementAlgorithm` reads `TryGetValue<int>("p2c")` (matches the `int`-first mapping), while
  `DefaultJsonWebTokenService.ProcessTokenLifetime` reads `TryGetValue<long>("iat")` — a deserialized `1700000000`
  boxes as `int`, so `is long` misses and the code can re-set `iat`. The robust fix is a numeric-tolerant accessor that
  converts across `int`/`long`/`ulong`/`BigInteger` (as `JsonElements.TryGetPropertyValue<T>` already does for the
  `JsonElement` path) rather than relying on the exact boxed type; it pairs naturally with the `D9` relocation of that
  extension. Deferred out of the `ObjectJsonConverter` options rework to keep that change focused. Touch points:
  `src/NCode.Identity.Jose.Abstractions/Extensions/DictionaryExtensions.cs`,
  `src/NCode.Identity.Jose/Algorithms/KeyManagement/Pbes2KeyManagementAlgorithm.cs`,
  `src/NCode.Identity.JsonWebTokens/DefaultJsonWebTokenService.Encode.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. Fixed as part of the `D9` relocation: the new
  `NCode.Json.ObjectGraphExtensions.TryGetValue<T>` is numeric-width tolerant (`sbyte`…`ulong`/`BigInteger` + reals,
  range-checked) and reads a boxed integer as an RFC 7519 `NumericDate` (`DateTimeOffset`/`DateTime`). The epoch
  conversion is shared with the `JsonElement` path via the `NCode.Json.DateCoercion` helper so the two cannot drift._
- **`D11` — Time/date extension methods carry logic inside the `*.Abstractions` package.**
  `NCode.Identity.Jose.Abstractions/Extensions/` still holds `DateTimeExtensions`, `DateTimeOffsetExtensions`, and
  `TimeProviderExtensions` — each a C# 14 `extension(...)` member with a real method body (`WithPrecisionInSeconds`,
  `GetUtcNowWithPrecisionInSeconds`, `GetTimestampWithPrecisionInSeconds`) — which violates the house rule that
  `*.Abstractions` packages hold only dumb DTOs / POCOs / interfaces / contracts (no logic). This is the same violation
  the now-closed `D9` fixed for the dictionary accessor, but these are **general time/date helpers**, not JSON, so they
  cannot follow `D9` into `NCode.Json`; they need a different non-abstractions home (a general NCode utility library).
  They are consumed by ~28 OpenId handler files via `using NCode.Identity.Jose.Extensions;`
  (`GetUtcNowWithPrecisionInSeconds`), so the relocation is a larger cross-cutting change best done as its own focused
  pass. Touch points: `src/NCode.Identity.Jose.Abstractions/Extensions/DateTimeExtensions.cs`,
  `src/NCode.Identity.Jose.Abstractions/Extensions/DateTimeOffsetExtensions.cs`,
  `src/NCode.Identity.Jose.Abstractions/Extensions/TimeProviderExtensions.cs`, and every
  `NCode.Identity.Jose.Extensions` consumer.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0188](docs/adr/0188-general-time-helpers-live-in-a-dependency-free-package.md). The three helpers move unchanged
  to the new dependency-free `NCode.Time` package (namespace `NCode.Time`), the time counterpart of `NCode.Json`. Their
  only consumers are the implementation packages `NCode.Identity.Jwt` and `NCode.Identity.OpenId.Authentication`, which
  now reference it. The tests move to `NCode.Time.Tests` and use fixed instants instead of the wall clock._
- **`D12` — Consolidate claim-string date parsing in `ConvertClaimToNativeValue` onto `DateCoercion`.** The JWT
  encoder's `DefaultJwtEncoder.ConvertClaimToNativeValue` (which maps a `Claim`'s string value to a native CLR value by
  `Claim.ValueType` before serialization) parses a `ClaimValueTypes.DateTime` claim with its own
  `DateTimeOffset.TryParse(..., AssumeUniversal)` / `DateTime.TryParse(..., AssumeUniversal).ToUniversalTime()` logic.
  The `DateTimeOffset` branch already matches `NCode.Json.DateCoercion.TryParseDateTimeOffset` exactly, but the
  `DateTime` fallback branch diverges from `DateCoercion.TryParseDateTime` (which uses `RoundtripKind` and labels a
  zone-less value `Utc`), so an explicit-offset value is UTC-normalized here yet offset-preserved there. The branch is
  rarely reached — the `DateTimeOffset` branch wins for almost any date string — so this is latent rather than live,
  but it duplicates the date-parsing policy the `DateCoercion` consolidation deliberately centralized. Folding it in
  requires exposing `DateCoercion` (currently `internal` to `NCode.Json`) as public API, so it is a small cross-cutting
  change best done deliberately. Touch points: `src/NCode.Json/DateCoercion.cs` (make public) and
  `src/NCode.Identity.Jwt/Encoding/DefaultJwtEncoder.cs` (`ConvertClaimToNativeValue`).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0121](docs/adr/0121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md). `DateCoercion` is
  public (`TryParseDateTimeOffset` / `TryParseDateTime`); the encoder parses a declared `ClaimValueTypes.DateTime`
  claim through it and drops the `DateTime` fallback (reachable only for out-of-range instants, where it clamped to a
  wrong machine-local value). The claims-identity factory's second, divergent date policy — sniffing every JSON string
  and rewriting date-looking values (`"1.2"`, `"10:30"`, `birthdate`) — is retired: a JSON string decodes verbatim as
  `ClaimValueTypes.String`. Follow-ups surfaced during the work are tracked as `D13` and `D14`._
- **`D13` — Route wire-supplied `NumericDate` conversions through a range-guarded `DateCoercion` helper.** Several
  sites read an RFC 7519 `NumericDate` (or a Unix-seconds parameter) as a raw `long` and call
  `DateTimeOffset.FromUnixTimeSeconds` directly, which **throws** `ArgumentOutOfRangeException` for a value outside the
  representable range (below `-62135596800` or above `253402300799`) instead of failing validation. The DPoP proof `iat`
  is the sharpest case: the proof is signed by a client-chosen key, so any caller can submit `iat: 99999999999999` and
  turn an `invalid_dpop_proof` rejection into an unhandled exception; a Unix-seconds request parameter parsed by
  `DateTimeOffsetParser` is likewise caller-controlled. `NCode.Json.DateCoercion` already range-guards the identical
  conversion internally (`TryFromNumericDate<T>`, used by `JsonElements` / `ObjectGraphExtensions`) and is public for
  string parsing per [ADR-0121](docs/adr/0121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md),
  which keeps the `NumericDate` helper internal until a cross-assembly consumer exists — these sites are that consumer.
  Expose a public, non-generic range-guarded `NumericDate` conversion on `DateCoercion` (or read via
  `TryGetPropertyValue<DateTimeOffset>` where the source is a `JsonElement`) and route each site through it, mapping an
  out-of-range value to that site's invalid/absent outcome. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultDPoPProofValidator.cs` (`iat`),
  `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/ClientAssertionClientAuthenticationHandler.cs` (`exp`),
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/TokenExchange/DefaultTokenExchangeGrantHandler.cs`
  (`ReadAuthTime`), `src/NCode.Identity.OpenId.Authentication/Subject/DefaultValidateSubjectAuthenticationHandler.cs`
  (`auth_time`), `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointHandler.cs`
  (`ReadAuthTime`), `src/NCode.Identity.OpenId.Messages.Abstractions/Parsers/DateTimeOffsetParser.cs`, and
  `src/NCode.Identity.Jwt/Encoding/DefaultJwtEncoder.cs` (`ProcessTokenLifetime` `iat`).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0123](docs/adr/0123-numeric-date-is-read-strictly-and-range-guarded.md). `DateCoercion` exposes public,
  non-throwing `TryFromNumericDate(long)` / `TryFromNumericDate(double)` / `TryParseNumericDate(string?)`, and
  `JsonElements` adds the strict `TryGetNumericDate` / `TryGetPropertyNumericDate` (a JSON number only, integer or
  fractional per RFC 7519). Every listed site routes through them — rejecting an out-of-range value with the endpoint's
  own error — and the suggested `TryGetPropertyValue<DateTimeOffset>` route was **not** taken because it also parses date
  strings. The pass also covered the JWT lifetime handler, which read `nbf`/`exp` leniently and **failed open** on a
  present-but-unreadable value; it now fails closed and rejects date-string `exp`/`nbf`. The client-assertion replay
  retention saturates instead of overflowing for an `exp` at the top of the range, and
  `DateTimeOffset.FromUnixTimeSeconds` / `FromUnixTimeMilliseconds` are banned (`RS0030`). Capping far-future assertion
  lifetimes is tracked as `F39`._
- **`D14` — JWT claim ⇄ JSON numeric and extra-claim fidelity in `NCode.Identity.Jwt`.** The claim mapping is
  date-correct per [ADR-0121](docs/adr/0121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md), but
  three non-date defects remain in the same encode/decode pair. (1) `DefaultClaimsIdentityFactory.CreateNumberClaim`
  probes widths narrow → wide with each later probe **overwriting** the earlier one, so every JSON number ends up typed
  `ClaimValueTypes.Double` (any finite number satisfies `TryGetDouble`); re-encoding such a claim goes through `double`
  and silently loses precision for integers above 2^53. It should pick the **most specific** type (first match wins),
  matching `ObjectJsonConverter`'s most-specific integer mapping, and drop its empty `catch` (the `TryGet*` probes on a
  `Number` element cannot throw). (2) `DefaultJwtEncoder.ConvertClaimToNativeValue` has no `UInteger32` /
  `UInteger64` branch, so such claims are re-emitted as JSON **strings**. (3) `DefaultJwtEncoder.ProcessExtraPayloadClaims`
  returns early unless `ExtraPayloadClaims.Count > 1`, silently **dropping** a lone extra payload claim. This is live:
  an ID token whose only payload claim is `azp` (no `nonce`, `c_hash`/`at_hash`/`s_hash`, or `sid` — e.g. one issued by
  token exchange without a session) loses `azp`. The guard should be `Count > 0`. Add a decode →
  encode round-trip test over every JSON scalar kind so the pair cannot drift again. Touch points:
  `src/NCode.Identity.Jwt/Claims/DefaultClaimsIdentityFactory.cs`, `src/NCode.Identity.Jwt/Encoding/DefaultJwtEncoder.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0122](docs/adr/0122-jwt-claim-values-map-to-json-by-declared-type.md). Both directions of the claim-value
  mapping moved into one internal `ClaimValueMapper`: decode keeps each number's exact literal and types it most
  specific first (`#integer32` → `#integer64` → `#uinteger64` → unbounded `#integer` → `#double`); encode parses each
  declared type in its XML Schema lexical space (adding `#uinteger32` / `#uinteger64` and unbounded `#integer`, exact
  beyond 64 bits), and emits a value outside it — including `NaN`/`Infinity` and malformed or wrong-kind `JSON` — as its
  verbatim string instead of throwing. The same pass fixed two further round-trip defects: `JSON_NULL` now encodes as
  JSON `null` (not `""`), and lenient `NumberStyles.Any` parsing (`"1,000"` → `1000`) is gone. A lone extra payload
  claim is emitted. A decode → encode round-trip test over every JSON scalar kind guards the pair._
- **`D15` — Re-evaluate the existing public enums against the open-vocabulary rule.** Vocabularies are open unless
  provably closed ([ADR-0150](docs/adr/0150-vocabularies-are-open-unless-provably-closed.md)), and the public enums
  predate the three-part test. `SessionTerminationReason` is already converted to the `SessionTerminationReasons`
  names ([ADR-0098](docs/adr/0098-session-lifecycle-termination-seam.md)), and `SecretKeyImportFormat` to the
  `SecretKeyImportFormats` names each key type declares
  ([ADR-0104](docs/adr/0104-import-public-asymmetric-secret-material.md)). `HashAlgorithmType` is replaced by the
  framework's `HashAlgorithmName`, and `BinaryEncodingType` by `BinaryEncodings` names plus registered `IBinaryEncoder`
  strategies ([ADR-0190](docs/adr/0190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md)). One
  still looks open: `PersistedGrantStatus` (persisted; a lifecycle a grant type may extend). `AlgorithmType`,
  `RequestObjectSource`, `TenantPlane`, `SerializationFormat`, `DeveloperKeyMode`, the `ObjectJson*Handling` mirrors,
  and the operation outcomes (`PasswordVerificationResult`, `OutboundHttpReadResult`, `OwnerRemovalResult`) appear to
  pass. Apply the test to `PersistedGrantStatus` and, if it fails, convert it to names plus registered strategies with a
  data migration for the persisted value. Touch points: the enum file under `src/**/` and its `switch` consumers.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0150](docs/adr/0150-vocabularies-are-open-unless-provably-closed.md), whose consequences record the outcome for
  every public enum. Four failed the test and became names: `SessionTerminationReasons`
  ([ADR-0098](docs/adr/0098-session-lifecycle-termination-seam.md)), the `SecretKeyImportFormats` each key type declares
  ([ADR-0104](docs/adr/0104-import-public-asymmetric-secret-material.md)), the framework's `HashAlgorithmName`, and the
  `BinaryEncodings` names of registered `IBinaryEncoder` strategies
  ([ADR-0190](docs/adr/0190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md)).
  `PersistedGrantStatus` passes. The item assumed it is stored, but the grant service derives it on read from a
  grant's revocation and expiry instants, and a grant type's own lifecycle lives in its payload, so no data migration
  is needed. The enum now records that justification. The other listed enums, and `JoseSerializationFormat`, pass for
  the reasons ADR-0150 records._

## Feature gaps — implemented (group `F`)

- **`F1` — End-session / RP-initiated logout endpoint.** No OpenID Connect end-session endpoint, session
  termination, or back-channel logout notification exists. Until it does, there is no logout operation
  to audit. (ID-token `sid` claim for logout is also not yet emitted.)
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0074](docs/adr/0074-session-management-and-logout.md)._
- **`F2` — Consent follow-ups (granular per-scope, scope/client display, authorized-apps management).** Durable
  consent persistence and enforcement **are implemented** ([ADR-0076](docs/adr/0076-consent-persistence-and-enforcement.md)):
  a first-class `PersistedConsent` aggregate + `IConsentStore` + `ConsentEntity` table, the `IConsentService` seam, the
  client `require_consent` setting (honored alongside `prompt=consent`) enforced in `DefaultAuthorizeHandler`, the
  `ConsentChallengeCommand` routing seam, the interactive `GET /account/consent/details` + decision-recording
  `POST /account/consent`, and the `apps/auth` consent screen showing the real client and requested scopes. **Remaining:**
  per-scope (granular) consent toggling rather than an all-or-nothing allow; friendly scope descriptions and a client
  display name on the consent screen (today it shows raw scope names and the client id); and the end-user
  "authorized applications" self-service surface to **list and revoke** consents (the store already supports it via
  `IConsentStore.GetBySubjectAsync` / `RemoveAsync` and `IConsentService.RevokeConsentAsync`). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Consent/`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointHandler.cs`,
  `src/frontend/apps/auth/src/ConsentForm.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0076](docs/adr/0076-consent-persistence-and-enforcement.md)._
- **`F3` — CIBA grant (OpenID Connect Client-Initiated Backchannel Authentication).** The OAuth 2.0 Device
  Authorization Grant (`device_code`, RFC 8628) **is implemented** — device authorization and verification endpoints,
  the `device_code` token grant handler, and the pending-request persistence model (see
  [ADR-0059](docs/adr/0059-device-authorization-grant.md)). CIBA is **not** implemented: there is no
  `backchannel_authentication_endpoint`, no `auth_req_id` grant, and no poll/ping/push token-delivery modes. CIBA
  reuses the same pending-authorization-as-persisted-grant pattern the device flow established, but adds two design
  questions that need their own ADR before implementation: resolving `login_hint` / `login_hint_token` /
  `id_token_hint` to a subject, and the decoupled **authentication-device notification** seam (deployment-specific, so
  it must be an injectable abstraction rather than a built-in transport). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/` (the `// TODO: Ciba Grant` marker in
  `DefaultTokenEndpointProvider.cs`), plus a new `Endpoints/BackchannelAuthentication/` family.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0085](docs/adr/0085-ciba-grant.md)._
- **`F4` — `refresh_token_reuse_policy` (none / revoke_all).** Refresh-token replay is _detected and audited_
  (`token.refresh.replay`), but there is no configurable response that revokes the whole token family
  on reuse. See `// TODO: refresh_token_reuse_policy` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/RefreshToken/DefaultRefreshTokenGrantHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`F6` — Dynamic Client Registration (RFC 7591/7592).** There is no `registration_endpoint`; clients are
  provisioned only via the management API / seeding. Not required to certify the Basic/Config OP profiles
  (those allow manual client registration), but it is its own certification profile (Dynamic OP) and it
  removes manual client seeding _and_ unlocks parallel execution for the conformance harness (which
  serialises whenever a static client `alias` is used). Prerequisite for the Dynamic OP conformance
  profile — see [`docs/conformance-testing.md`](docs/conformance-testing.md). Touch points: a new
  `Endpoints/Registration/` family and discovery-metadata advertisement.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0063](docs/adr/0063-dynamic-client-registration.md)._
- **`F7` — DPoP / sender-constrained tokens (RFC 9449 + mTLS, `cnf` claim).** A single `IProofOfPossessionService`
  evaluates a DPoP proof and/or a mutual-TLS client certificate per token request, binds the issued access token's
  `token_type` (`DPoP`/`Bearer`) and `cnf` claim (`jkt` / `x5t#S256`) uniformly across every grant (passed first-class
  to `ITokenGrantHandler.HandleAsync` and carried on `CreateSecurityTokenRequest.Confirmation`), advertises
  `dpop_signing_alg_values_supported` and `tls_client_certificate_bound_access_tokens` in discovery, and enforces the
  `require_dpop` / `tls_client_certificate_bound_access_tokens` client policies. Certificate ingestion lives in the
  default `IClientCertificateResolver`, which reads the connection cert or a configured **trusted** forwarded header
  (`OpenId:MutualTls`) for any reverse-proxy topology; the DPoP replay/nonce stores run over `IDistributedCache`
  (in-memory by default, cross-node by registering a shared provider). The follow-ups that rode the same foundation are
  all implemented: (a) **resource-server-side** validation — `IProofOfPossessionService.ValidateResourceAccessAsync`
  verifies a presented DPoP proof and its `ath` (and the `x5t#S256` match) when a bound token is used at a protected
  resource, and the UserInfo / management-API endpoints accept both the `Bearer` and `DPoP` schemes; (b) **DPoP at the
  authorization endpoint** via the `dpop_jkt` parameter (RFC 9449 §10) that binds the authorization code (format
  validated at the authorization endpoint, matched at the token endpoint, and applied to the hybrid front-channel
  token); (c) **refresh-token binding continuity** — the `cnf` is persisted on `RefreshTokenGrant.Confirmation` so a
  rotated token stays bound to the same key and a bound refresh token presented without a matching proof is rejected;
  (d) `mtls_endpoint_aliases` (RFC 8705 §5) advertised via the `mtls_endpoint_alias_authority` setting.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0097](docs/adr/0097-sender-constrained-access-tokens-dpop-and-mtls.md)._
- **`F8` — Pushed Authorization Requests (PAR, RFC 9126).** There is no `pushed_authorization_request_endpoint`; the
  authorize endpoint only accepts inline request parameters. See
  `// TODO: add support for OAuth 2.0 Pushed Authorization Requests (PAR)` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultLoadAuthorizationRequestHandler.cs`.
  Touch points: a new `Endpoints/PushedAuthorization/` family plus discovery-metadata advertisement.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0089](docs/adr/0089-pushed-authorization-requests.md)._
- **`F9` — Pairwise subject identifiers (PPID) via the `subject_type` setting.** The subject identifier emitted into
  tokens is always the raw public subject; the `subject_type` (`public`/`pairwise`) setting is not honored, so there is
  no per-client/RP pseudonymous `sub`. See `// TODO: use the subject_type setting to support PPID` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0093](docs/adr/0093-pairwise-subject-identifiers.md).
  Implemented as a stateless, deterministic projection (`ISubjectIdentifierService`) applied at the outbound `sub`
  seams, keeping the stable principal identifier as the single internal subject; no persistence/migration. Two
  second-order follow-ups were split out as new items: `F20` (sector_identifier_uri remote fetch + validation) and
  `F21` (end-session reverse-map for a pairwise `id_token_hint`)._
- **`F10` — Extension grant + token-endpoint `401`/`WWW-Authenticate`.** The token endpoint has no extension-grant seam
  (`// TODO: Extension Grant` in `DefaultTokenEndpointProvider.cs`) and collapses client-authentication failures to
  `400` instead of emitting `401` with a `WWW-Authenticate` challenge (`// TODO: add support for 401 with
WWW-Authenticate header`). (Device Code is implemented; CIBA is `F3`.) Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/DefaultTokenEndpointProvider.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0092](docs/adr/0092-token-endpoint-401-www-authenticate-challenge.md).
  The extension-grant seam already existed via `ITokenGrantHandler` grant-type routing (now documented); the challenge
  work added the `ISupportResponseHeaders` seam and corrected the `client_secret_basic` standard-Base64 decoder._
- **`F11` — `acr` claim emission and conditional `auth_time` (blocked on / pending foundational subsystems).** The
  `sid` claim **is** now emitted into ID tokens from the session-management subsystem
  ([ADR-0074](docs/adr/0074-session-management-and-logout.md)). The `acr` claim still needs authentication-context-class
  tracking, which does not exist, so it is never emitted. `DefaultTokenService` additionally always emits `auth_time`
  unconditionally (`// TODO: … should we do this conditionally?`). Touch points (all
  `src/NCode.Identity.OpenId.Authentication/Tokens/`): `Handlers/DefaultGetIdTokenPayloadClaimsHandler.cs`,
  `DefaultTokenService.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0091](docs/adr/0091-acr-emission-and-conditional-auth-time.md).
  Essential-`acr` enforcement was carved out as the follow-up item `F19`._
- **`F12` — Token-request validation hardening.** The authorization-code grant validator does not yet validate the
  requested `resource`, does not validate the actual `scope` values against the grant, and hardcodes the supported PKCE
  `code_challenge_method` set with no extensibility seam. See the `// TODO: validate resource`,
  `// TODO: validate actual scope values`, and `// TODO: provide a way to allow custom code challenge methods` markers
  in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/AuthorizationCode/DefaultValidateAuthorizationCodeGrantHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`F13` — `max_age` re-authentication logs a WARN on the expected path.** When the end-user's `auth_time` exceeds the
  request `max_age` or the client's `subject_max_age`, `DefaultValidateSubjectAuthenticationHandler` sets
  `AccessDenied` and logs **`SubjectValidationFailed`** at warning level (event `2011`,
  "The end-user's authentication time is too old from the client's MaxAge."). On an interactive authorize that is the
  _normal_ freshness path — the flow re-challenges, the user re-authenticates, and the grant then succeeds
  (`authorization.granted` + `token.issued` follow). Surfaced while running the Aspire dev stack: a stale persistent
  dev cookie trips it on the first sign-in, so a successful re-auth looks like a failure in the logs. Decide whether to
  log the max-age-exceeded → re-prompt transition at a lower level (or as a distinct "re-authentication required" signal
  distinct from hard `AccessDenied` denials) so routine freshness re-prompts are not warnings. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Subject/DefaultValidateSubjectAuthenticationHandler.cs` (the two
  `ValidateMaxAge` branches + `Logger.SubjectValidationFailed`).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`F14` — EF `QuerySplittingBehavior` not configured (multi-collection includes).** The OpenID EF Core persistence
  emits `Microsoft.EntityFrameworkCore.Query[20504]` at warning level — a query loads more than one collection
  navigation (via `Include`/projection) with no `QuerySplittingBehavior` configured, so it defaults to `SingleQuery`
  (a potential cartesian-explosion / slow-query risk on relational providers). Decide the default: configure
  `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` at the provider (or `AsSplitQuery()` on the specific
  multi-collection query), or deliberately suppress the advisory where `SingleQuery` is intended. Touch points: the
  relational provider configuration (`src/NCode.Identity.OpenId.Host/Program.cs` `UseSqlServer`/`UseSqlite`) and
  the multi-collection load in `src/NCode.Identity.OpenId.Persistence.EntityFramework*`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`F15` — Test coverage for the logout endpoint + back-channel delivery.** `DefaultSessionService` has unit tests
  ([`tests/NCode.Identity.OpenId.Core.Tests/Sessions/DefaultSessionServiceTests.cs`](tests/NCode.Identity.OpenId.Core.Tests/Sessions/DefaultSessionServiceTests.cs)),
  but the RP-Initiated Logout `end_session_endpoint` handler and the `DefaultBackChannelLogoutNotifier`
  ([ADR-0074](docs/adr/0074-session-management-and-logout.md)) are not yet covered. Add handler unit tests (in
  `NCode.Identity.OpenId.Core.Tests`, which already sees this package's internals) and/or an end-to-end
  `NCode.Identity.OpenId.IntegrationTests` case (login → `sid` in the ID token → `end_session_endpoint` revokes the
  session/grants → a signed `logout_token` is POSTed to a stub `backchannel_logout_uri`). The **first-party**
  interactive logout shares this gap: the account page's `POST /account/logout` (which runs the same pipeline from the
  login cookie) and the `GET /account/me` session endpoint have only pure-helper unit tests (`BuildMeResponse`,
  `ResolveSafeReturnUrl`) plus the existing login-POST antiforgery integration test
  ([`tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/InteractiveLoginAntiforgeryTests.cs`](tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/InteractiveLoginAntiforgeryTests.cs));
  add an integration case (login → `GET /account/me` returns the session → `POST /account/logout` clears the cookie,
  revokes the session/grants, and fires back-channel). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/`,
  `src/NCode.Identity.OpenId.Authentication/Sessions/`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0074](docs/adr/0074-session-management-and-logout.md)._
- **`F17` — CIBA push delivery and signed backchannel request objects.** CIBA **poll** and **ping** delivery are
  implemented ([ADR-0085](docs/adr/0085-ciba-grant.md)): the `backchannel_authentication_endpoint`, the
  `urn:openid:params:grant-type:ciba` token grant, the pending-request persistence model, and the injectable
  subject-resolution (`AuthenticateBackchannelRequestCommand`) + notification (`ICibaNotificationService`) + approval
  (`ICibaRequestService`) seams all exist, and `backchannel_token_delivery_modes_supported` advertises `poll`/`ping`.
  **Push** delivery (`mode=push`, where the OP delivers the tokens inside the notification) is **not** implemented — it
  needs the notification channel to carry and secure a full token response; it layers onto the existing
  `ICibaNotificationService` seam. Signed/encrypted backchannel request objects
  (`backchannel_authentication_request_signing_alg_values_supported`, a FAPI-CIBA concern) are likewise a follow-up.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/BackchannelAuthentication/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0090](docs/adr/0090-durable-outbound-delivery-dispatch-adapter.md),
  [ADR-0085](docs/adr/0085-ciba-grant.md)._
- **`F18` — Migrate the device-authorization endpoint to the canonical endpoint shape.** The canonical OpenID endpoint
  shape — a thin `IEndpointProvider` over a strongly-typed `OpenIdMessage<T>` request and a mediator
  validate/process pipeline — is codified in [ADR-0086](docs/adr/0086-canonical-openid-endpoint-shape.md) and followed
  by the token, end-session, and CIBA endpoints. `DefaultDeviceAuthorizationEndpointHandler` predates the convention: it
  reads the form directly (`form.TryGetValue(...)`) and inlines its validation and persistence. Refactor it to a
  `DeviceAuthorizationRequest : OpenIdMessage<…>` + `ValidateDeviceAuthorizationRequestCommand` +
  `CreateDeviceAuthorizationRequestCommand` so the endpoint family is uniform. Touch point:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/DeviceAuthorization/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0086](docs/adr/0086-canonical-openid-endpoint-shape.md)._
- **`F19` — Essential `acr` enforcement.** `acr` is now emitted (recorded at authentication, resolved through the
  replaceable `IAuthenticationContextReferenceResolver`, and copied into tokens — [ADR-0091](docs/adr/0091-acr-emission-and-conditional-auth-time.md)),
  and the satisfied `acr` is available on the subject principal. What is **not** yet implemented is _enforcement_ of a
  requested essential `acr`: when the `claims` request parameter asks for `id_token.acr` as an essential claim with
  specific `values`, and the satisfied `acr` is not among them, the OP should deny (or trigger re-authentication) rather
  than silently issue a token whose `acr` does not match. The existing `idp:`-prefixed `acr_values` matching in
  `DefaultValidateSubjectAuthenticationHandler` is the natural home; the satisfied `acr` claim is now present to
  enforce against. Touch point:
  `src/NCode.Identity.OpenId.Authentication/Subject/DefaultValidateSubjectAuthenticationHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0091](docs/adr/0091-acr-emission-and-conditional-auth-time.md)._
- **`F20` — `sector_identifier_uri` remote fetch + validation at registration.** Pairwise subjects (`F9`,
  [ADR-0093](docs/adr/0093-pairwise-subject-identifiers.md)) honor the `sector_identifier_uri` client setting by using
  its **host** as the sector identifier, but the OpenID Connect Core 1.0 §8.1 registration-time step — fetching the
  JSON document at that URI and verifying it lists every one of the client's `redirect_uris` — is not performed. Add it
  as a Dynamic Client Registration (`F6`) validation, with the fetch guarded against SSRF (allow-list / timeout /
  size cap). Touch point: `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0095](docs/adr/0095-sector-identifier-uri-fetch-and-ssrf-guard.md).
  Added `subject_type` / `sector_identifier_uri` as first-class DCR metadata and the replaceable
  `ISectorIdentifierValidator` seam, whose default implementation performs the §8.1 fetch-and-validate behind an
  SSRF-guarded named `HttpClient` (always-on private-address block + no-redirect, plus an optional host allow-list, a
  fetch timeout, and a response size cap)._
- **`F21` — End-session subject resolution for a pairwise `id_token_hint`.** Because the pairwise projection is one-way
  and unstored (`F9`, [ADR-0093](docs/adr/0093-pairwise-subject-identifiers.md)), an `id_token_hint` presented at the
  end-session endpoint carries a pairwise `sub` the OP cannot map back to the stable principal. The original handler
  consumed that pseudonym as if it were the stable subject, silently no-op'ing subject-keyed revocation and
  double-projecting the back-channel `logout_token` `sub` for pairwise clients. Touch point:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/Handlers/DefaultResolveEndSessionSubjectHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0096](docs/adr/0096-end-session-subject-from-server-state.md).
  Resolved **without** a reverse map (rejected as a correlation liability): the handler now reads only `sid` and
  `client_id` from the hint, recovers the stable subject forward from the persisted session grant (falling back to the
  OP login cookie), and never reads the hint's `sub` — mirroring the one-way-hashed grant-key model where a presented
  value is matched forward, never reversed._

- **`F22` — Enforce the `Subject.sub == PrincipalId` invariant at every ticket-construction site.** The principal is now
  resolved uniformly and `SubjectAuthentication.PrincipalId` is the single authoritative subject
  ([ADR-0094](docs/adr/0094-resolve-principal-at-every-authentication-entry-point.md)), and the interactive login path
  re-stamps the cookie's `sub` claim to the resolved id. The ROPC and bearer/UserInfo paths, however, still leave the
  principal's raw `sub` claim (`LocalAccountId` / the token's `sub`) on `SubjectAuthentication.Subject` while
  `PrincipalId` holds the resolved id — harmless today because emission reads `PrincipalId`, not the claim, but a
  latent divergence. Normalize the principal's subject claim to `PrincipalId` at those construction sites too (a shared
  ticket factory), so `Subject.sub` and `PrincipalId` can never disagree and the two-sources-of-truth trap cannot
  return. Touch points: `src/NCode.Identity.OpenId.Accounts.Authentication/DefaultAuthenticatePasswordGrantHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Subject/DefaultAuthenticateSubjectHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0094](docs/adr/0094-resolve-principal-at-every-authentication-entry-point.md). Added the shared
  `SubjectAuthentication.Create` factory, which normalizes the raw principal's subject claim to the resolved
  `PrincipalId` through a new tenant-aware, host-replaceable `OpenIdContext.NormalizeSubjectId` seam (the inverse of
  `GetSubjectId`, configurable via `OpenIdOptions.NormalizeSubjectId`); every entry point — ROPC, bearer/UserInfo,
  interactive authorize, and the interactive login cookie re-stamp — routes through it, so the invariant holds by
  construction._

- **`F24` — Flow the resource-owner-password ticket's `AuthenticationProperties` from the request, not a fresh empty
  instance.** `DefaultAuthenticatePasswordGrantHandler` builds its `SubjectAuthentication` with a freshly-constructed
  empty `new AuthenticationProperties()`, so any properties the surrounding token-request flow could carry (issued/
  expiry timestamps, `amr`/`acr`-adjacent items, redirect state) are dropped on the ROPC path — unlike the interactive
  and bearer paths, which propagate the authentication's own `AuthenticationProperties`. Thread the properties in
  through the `AuthenticatePasswordGrantCommand` (or resolve them at the point the grant is authenticated) so the ROPC
  ticket carries the same authentication metadata as the other entry points. Touch point:
  `src/NCode.Identity.OpenId.Accounts.Authentication/DefaultAuthenticatePasswordGrantHandler.cs` (the `// TODO`
  on the `new AuthenticationProperties()` argument).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. `DefaultPasswordGrantHandler` now captures the authentication
  instant once at the start of the token-request flow, carries it as `AuthenticationProperties.IssuedUtc` through a new
  required `AuthenticatePasswordGrantCommand.AuthenticationProperties` parameter, and reuses the same timestamp for the
  issued token's `CreatedWhen`, so the ROPC ticket's `auth_time` and the token's `iat` are coherent and the leaf
  `DefaultAuthenticatePasswordGrantHandler` no longer materializes an empty `new AuthenticationProperties()`._

- **`F25` — Session claims & lifecycle enhancements (build on the existing session subsystem).** The end-user session
  subsystem already exists — `ISessionService`, `SessionGrant`, the `sid` claim in the ID token, RP-Initiated /
  Back-Channel / (opt-in) Front-Channel Logout, and session-scoped grant revocation
  ([ADR-0074](docs/adr/0074-session-management-and-logout.md),
  [ADR-0077](docs/adr/0077-session-scoped-grant-revocation-on-logout.md),
  [ADR-0088](docs/adr/0088-opt-in-front-channel-logout-and-session-management.md),
  [ADR-0096](docs/adr/0096-end-session-subject-from-server-state.md)). **Mostly delivered** — see
  [`CHANGELOG.md`](CHANGELOG.md) and [ADR-0098](docs/adr/0098-session-lifecycle-termination-seam.md): the setting-gated
  `sid` in the access token, the server-authoritative idle (inactivity) timeout, the concurrent-session limit with a
  swappable `ISessionEvictionPolicy`, the subject-scoped session query / bulk revocation (`ISessionService.GetSessionsAsync`
  / `RevokeAsync` / `RevokeAllAsync`), and session auditing/logging are all done. **Remaining:**
    - **A surface to view and terminate sessions.** The `ISessionService` query/revoke capability exists and is
      tested, but there is no dedicated surface yet: (a) a **user-facing** "active sessions" + "sign out everywhere"
      view on the OP-hosted account page (`apps/auth`), which runs in the ambient `OpenIdContext` and can call
      `GetSessionsAsync` / `RevokeAllAsync` directly; and (b) a tenant-addressed **admin** endpoint
      (`api/tenants/{tenantId}/sessions`). The admin endpoint is **blocked on a design decision**: management endpoints
      are addressed by a route `{tenantId}` while `ISessionService` is bound to the ambient `OpenIdContext.Tenant`, so
      it must first resolve a per-tenant context (or `ISessionService` must gain a tenant-parameterized overload). The
      existing grants management surface (`GET`/`DELETE api/tenants/{tenantId}/grants?subjectId=…`) already provides
      subject-filtered listing and bulk revoke in the interim (though it does not run the termination seam's
      back-channel/audit side effects).
      _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
      [ADR-0100](docs/adr/0100-explicit-tenant-context-materialization.md). Both remaining surfaces shipped: (a) the
      self-service **active sessions** page on `apps/auth` (per-session revoke, "sign out everywhere else", and "sign out
      everywhere"), backed by `GET /account/sessions/list` + `POST /account/sessions/revoke` + `.../revoke-all`, with
      sign-in `UserAgent`/`RemoteIpAddress` captured on `SessionGrant`, a new `SessionTerminationReason.UserInitiated`, and
      `ISessionService.RevokeAllExceptAsync`; and (b) the tenant-addressed admin endpoint
      `GET`/`DELETE api/tenants/{tenantId}/sessions`. The admin design block is resolved by materializing an `OpenIdContext`
      for the route-addressed tenant (`IOpenIdContextFactory.CreateForTenantAsync` /
      `IOpenIdTenantFactory.CreateTenantByIdAsync`) and delegating to the context-bound `ISessionService`, so admin
      revocation runs the full termination seam (back-channel logout + audit) in the addressed tenant's security domain —
      not a tenant-parameterized service overload._

- **`F23` — OAuth 2.0 Token Exchange (RFC 8693).** The token endpoint had no token-exchange grant, so a client could
  not exchange a presented `subject_token` (and optional `actor_token`) for a delegation/impersonation token.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0101](docs/adr/0101-oauth-token-exchange-rfc8693.md). A `DefaultTokenExchangeGrantHandler : ITokenGrantHandler`
  for `urn:ietf:params:oauth:grant-type:token-exchange` now mints `access_token` / `refresh_token` / `id_token` outputs
  (`issued_token_type` in the response), supporting both impersonation and delegation with spec-correct `act` / nested
  `act` chains and `may_act` actor authorization (RFC 8693 §4). Presented tokens are validated through a replaceable
  `ISubjectTokenValidator` seam whose default accepts only **self-issued** JWTs (tenant signing keys, issuer, lifetime);
  the requesting client must be confidential and be the client the token was issued to (or named in its `aud`); the
  subject is resolved forward through `IPrincipalResolver` (never by reversing a pairwise `sub`); scopes/audiences are
  downscoped via the RFC 8707 `resource` and RFC 8693 `audience` parameters; and refresh-token issuance is gated per
  client by `token_exchange_allow_refresh_token`. The grant is secure-by-default (not advertised in the baseline
  `grant_types_supported`); the conformance tenant opts in, flipping the `grant-token-exchange` coverage probe. External
  / enterprise-IdP federation is intentionally out of scope and tracked as `F26` (the `ISubjectTokenValidator` seam is
  its extension point)._

- **`F30` — Centralize persistence of a _given_ shared-secret string.**
  [ADR-0110](docs/adr/0110-shared-secret-vs-cryptographic-key-storage.md) added `ISecretGenerator.GenerateSharedSecret`
  for _generating_ a presented secret (stored as the UTF-8 bytes of its opaque string). The complementary operation —
  persisting a **caller-supplied** shared-secret string in that same shape — was duplicated inline in three places
  (`ConformanceSeedHandler.CreatePersistedSecret`, `BootstrapAdminSeedHandler.CreatePersistedSecret`, and the
  integration-test `HostApplicationFactory`), each re-implementing the data-protect-and-wrap logic.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0110](docs/adr/0110-shared-secret-vs-cryptographic-key-storage.md). Added
  `ISecretGenerator.ImportSharedSecret(ImportSharedSecretRequest, ReadOnlySpan<byte>)` — the complement to
  `GenerateSharedSecret` — which persists a caller-supplied `client_secret` (the UTF-8 octets of its opaque string) in
  the identical `symmetric_raw` shape, deriving the bit length from the value and sharing one internal construction with
  `GenerateSharedSecret` so the stored UTF-8 contract cannot drift. The value is accepted as `ReadOnlySpan<byte>` (not a
  managed `string`) so the seed handlers keep the secret in their pinned, zeroed-on-return buffers. All three sites now
  route through it._

- **`F28` — Complete outbound-fetch SSRF coverage and bound the `request_uri` fetch.** The shared
  `IOutboundHttpForgeryGuard` ([ADR-0106](docs/adr/0106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md))
  guarded the `sector_identifier_uri`, `jwks_uri`, and JAR `request_uri` fetches, but two gaps remained. **(a)** The JAR
  `request_uri` fetch (`DefaultLoadAuthorizationRequestHandler.FetchRequestUriAsync`) read the response body with **no
  size cap** (a DoS vector) and had no fetch-timeout/allowed-hosts settings. **(b)** The **server→client deliveries**
  that POST to client-registered URLs — back-channel logout (`backchannel_logout_uri`) and CIBA push notification
  (`client_notification_endpoint`) — used plain `IHttpClientFactory` clients with **no** SSRF guard.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0111](docs/adr/0111-complete-outbound-fetch-ssrf-coverage.md). The `request_uri` fetch now requires an absolute
  `https` URI, enforces an optional host allow-list, bounds the request with a wall-clock timeout, and caps the response
  body via a new non-discoverable `request_uri_\*` settings family (`request\*uri*allowed_hosts`,
`request_uri_fetch_timeout`default 10 s,`request_uri_max_size_bytes`default 100 KB). The back-channel-logout and
CIBA deliveries now POST through dedicated SSRF-guarded named clients (connection pinned away from
private/loopback/link-local addresses, auto-redirect disabled), so a registered internal URL cannot turn the OP into a
  confused deputy. The streamed response-size-cap read was extracted into one replaceable, DI-registered
  `IOutboundHttpContentReader`seam (buffer-writer in,`ReadOnlySequence<byte>` out) consumed by all three body-reading
  fetches.*

- **`F31` — User-facing HTML error page for non-redirectable authorization errors (content negotiation).** When the
  authorize endpoint cannot safely redirect an error back to the client — an unregistered/invalid `redirect_uri` or an
  unresolvable `client_id` (OIDC Core §3.1.2.4: the server MUST NOT redirect to an invalid URI and SHOULD **inform the
  resource owner**) — the OP currently returns a raw `application/json` error body
  (`DefaultOpenIdExceptionHandler`/`OpenIdExceptionEndpointFilter`), which a browser user sees as unstyled JSON. The
  correct behavior is a content-negotiated, human-readable HTML error page for an interactive (`Accept: text/html`)
  request to the authorize endpoint, while programmatic callers keep the JSON body. Deliverables: a non-redirectable
  interactive error renderer (reuse the `InteractiveAuthenticationPageRenderer` login/consent markup seam, with a stable
  element id/heading for automation) wired into the authorize error path via content negotiation; HTML for
  `text/html`, JSON otherwise. This also hardens the conformance harness: the
  `oidcc-ensure-registered-redirect-uri` browser override currently matches the `unauthorized_client` code in the JSON
  body (`build/conformance/oidcc-basic.json`); once a stable error-page element exists it can match that instead.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/OpenIdExceptionEndpointFilter.cs`,
  `src/NCode.Identity.OpenId.Core/Exceptions/DefaultOpenIdExceptionHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/DefaultAuthorizationEndpointHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0114](docs/adr/0114-non-redirectable-authorization-errors-are-content-negotiated.md). Delivered as a general,
  extensible **content-negotiation seam** rather than an error-specific renderer: `OpenIdResult<T>` is now a thin
  `IResult` that delegates to `IResultExecutor<IOpenIdResult>` (`DefaultOpenIdResultExecutor` in Core), which applies the
  response envelope and negotiates an `IOpenIdResponseFormatter` strategy collection — each formatter declares the media
  types it emits and a `CanFormat(IOpenIdResponse)` predicate, so the seam is open on both the response-type and
  media-type axes (not hardcoded to errors→HTML), and only an explicit `Accept` match (never a `*/*` wildcard) selects a
  formatter. The interactive package registers `InteractiveOpenIdErrorResponseFormatter`, which renders via the shared
  `InteractiveAuthenticationPageRenderer.RenderErrorPage` (stable `authorization-error` element + `data-error-code`) and
  writes the body with `SecureEncoding.UTF8`. A deployment without the interactive surface degrades gracefully to JSON.
  The planned `OpenIdExceptionEndpointFilter`/`DefaultOpenIdExceptionHandler`/`DefaultAuthorizationEndpointHandler` touch
  points were unnecessary — both non-redirectable error paths (the direct `error.AsHttpResult()` return and the thrown
  `OpenIdException`) already funnel through `OpenIdResult<T>`, so negotiating there covered both without touching the
  authorize handler or exception plumbing._

- **`F29` — EdDSA (Ed25519 / Ed448) signing algorithm support.** The JOSE library implemented RSA (`RS*`/`PS*`) and
  ECDSA (`ES*`) signatures but not **EdDSA** (the JWA `alg` value `EdDSA`, RFC 8037), the modern Edwards-curve signature
  scheme FAPI 2.0 permits. The algorithm code constant (`AlgorithmCodes.DigitalSignature.EdDsa`) and the FAPI
  signing-algorithm ceiling already listed `EdDSA`, so it is advertised/permitted the moment the implementation is
  registered (the ceiling intersects with the server's actually-supported set). Unlike the library's other
  self-implemented algorithms — AES-KW, Concat-KDF/ECDH, PBES2, AES-CBC-HMAC, which compose **BCL primitives** — EdDSA
  has no BCL primitive to compose: `System.Security.Cryptography` on .NET 10 ships the PQC set (`MLDsa`/`SlhDsa`/`MLKem`)
  but **no** Edwards-curve type, and native .NET EdDSA is a later-release proposal. Only the two hashes EdDSA needs
  (SHA-512 for Ed25519, SHAKE256 for Ed448) are in the BCL.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0117](docs/adr/0117-eddsa-clean-room-implementation-opt-in-package.md). Delivered as the opt-in
  `NCode.Identity.Jose.EdDsa` package: a **clean-room** implementation of Ed25519 and Ed448 from RFC 8032 / 8037 / 7748 /
  8410 — constant-time field, scalar, and group arithmetic composing only the BCL hash primitives, with **zero**
  third-party cryptographic dependency in shipping code. The package depends only on abstractions
  (`NCode.Identity.Jose.Abstractions`, `NCode.Identity.Secrets.Abstractions`). It adds a first-class `OKP` (Octet Key
  Pair) `AsymmetricSecretKey` (`OkpSecretKey`, JWK `kty` `OKP`, `crv` `Ed25519`/`Ed448`) with its
  `ISecretKeyTypeDescriptor` (generate, JWK/PEM import, JWK+SPKI-PEM export, PKCS#8/SPKI deserialize; private seed
  data-protected in memory), a single `EdDsaSignatureAlgorithm` that selects the curve from the key's `crv`, and a
  `serviceCollection.AddEdDsa()` turnkey registration. Discovery advertises `EdDSA` in the asymmetric signing metadata
  automatically once registered (the signing-alg partition keys on `AsymmetricSecretKey`). Correctness is pinned by
  differential tests that cross-check the output byte-for-byte against an independent implementation used solely as a
  test oracle (never referenced by shipping code). The curve catalog is deliberately `internal` (RFC 8032 defines a
  closed set of exactly two curves — unlike the open, public ECC curve catalog). Not wired into the reference host by
  default (it stays opt-in to avoid changing the conformance discovery baseline); enabling it in the host and
  seeding an OKP developer signing key is a follow-up. The planned touch points in `src/NCode.Identity.Jose/Algorithms/`
  and `src/NCode.Identity.Secrets/Logic/` were superseded by the standalone package so the core JOSE library gains no new
  dependency and no always-on algorithm._

- **`F32` — DPoP sender-constrained proof-of-possession (RFC 9449), JOSE building block.** The highest-value modern,
  non-algorithm JOSE addition and the OAuth 2.1 / FAPI 2.0 direction. Add a first-class capability to **create and
  verify `dpop+jwt` proofs**: the `typ: dpop+jwt` header with an embedded public `jwk`, the `htm`/`htu`/`jti`/`iat`
  claims, the `ath` access-token hash, the `nonce` challenge/replay flow, and the `jkt` (JWK SHA-256 thumbprint)
  confirmation that binds a token to the proof key. This is the JOSE primitive the OpenId authentication stack then
  consumes to issue and validate DPoP-bound access/refresh tokens; it complements the shipped `private_key_jwt`
  client-authentication work ([ADR-0102](docs/adr/0102-jwt-bearer-client-authentication.md)). The `F33` JWK thumbprint
  (`jkt`) it needs is delivered. Touch points: [`src/NCode.Identity.Jose`](src/NCode.Identity.Jose),
  [`src/NCode.Identity.Jose.Abstractions`](src/NCode.Identity.Jose.Abstractions),
  [`src/NCode.Identity.OpenId.Authentication`](src/NCode.Identity.OpenId.Authentication). Capture the design in an ADR.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0176](docs/adr/0176-dpop-proofs-are-a-jose-building-block.md) — `IDPoPProofService` creates and validates
  proofs through the registered key types and signature algorithms (so `EdDSA` proofs work with the opt-in package),
  and `DefaultDPoPProofValidator` keeps only the nonce challenge and the replay cache._

- **`F33` — First-class JWK / JWKS types + JWK Thumbprint (RFC 7517 / 7638 / 9278).** Keys lived only as `SecretKey`,
  the JWK form existed only as a raw `JsonObject` produced by the descriptor seam, the JWKS endpoint hand-assembled a
  result of `JsonObject` keys, and the DPoP validator computed the `jkt` from a hand-rolled canonical JSON string —
  three duplicative treatments of one concept.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0119](docs/adr/0119-first-class-jwk-jwks-and-thumbprint.md). Delivered as the new **`NCode.Identity.Jwk`**
  package holding rich, immutable `JsonWebKey` / `JsonWebKeySet` types backed by a detached `JsonElement` (net9+, no
  `JsonDocument`/clone), with typed standard-member accessors, `GetMember` for key-type-specific material,
  `ContainsPrivateKeyMaterial` / `ToPublicKey`, and `ComputeThumbprint` / `ComputeThumbprintUri` (RFC 7638 / 9278) as
  instance methods. Construction is cohesive: public `JsonWebKey(JsonElement)` / `JsonWebKey(JsonObject)` constructors
  and `Parse(string)` for text. Both `NCode.Identity.Secrets.Abstractions` and `NCode.Identity.Jose.Abstractions`
  reference it, so JWK is first-class in both stacks while every `*.Abstractions` package stays logic-free. The
  descriptor seam ([ADR-0105](docs/adr/0105-secret-key-type-descriptor-first-class-seam.md)) still owns per-key-type
  member production; `ExportedPublicKey.Jwk` is now a `JsonWebKey`, the JWKS endpoint returns a `JsonWebKeySet` (the old
  `JsonWebKeySetResult` is retired), the management public-key path exposes `JsonWebKey.AsElement()`, and the DPoP proof
  validator parses the embedded key into a `JsonWebKey` and calls `ComputeThumbprint()` — one representation end-to-end,
  no hand-rolled canonical form._

- **`F35` — Explicit `typ` / media-type validation hardening (RFC 8725 BCP + RFC 9068 `at+jwt`).** A
  validation-policy (not algorithm) feature: enforce an explicit, expected `typ` on decode to defeat token-substitution
  / cross-JWT-confusion, and recognize the registered application types (`at+jwt` access tokens, `dpop+jwt`,
  `logout+jwt`, `secevent+jwt`). Extends the decode/verify options already on `IJoseSerializer`. Touch points:
  [`src/NCode.Identity.Jose/JoseSerializer.Jws.Verify.cs`](src/NCode.Identity.Jose/JoseSerializer.Jws.Verify.cs),
  [`src/NCode.Identity.Jose.Abstractions/JoseTokenTypes.cs`](src/NCode.Identity.Jose.Abstractions/JoseTokenTypes.cs).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. Delivered at the JWT-validation layer (the cohesive seam where
  `ValidateIssuer` / `ValidateAudience` / `ValidateTokenLifeTime` already live) rather than as raw-JOSE decode options:
  the `typ` header lives in the JWS protected header the JOSE serializer already deserializes, but the policy decision
  is a JWT concern, so a new `ValidateJwtParameters.ValidateTokenType(params string[])` validator performs the check.
  Matching follows the media-type rules of RFC 7515 §4.1.9 (optional `application/` prefix, case-insensitive), exposed
  as reusable `JoseTokenTypes.Normalize` / `JoseTokenTypes.AreSame` helpers; the generic `ValidateClaim` was unsuitable
  because it compares with `StringComparer.Ordinal`. `JoseTokenTypes` gained the registered application `typ` constants
  `DPoPJwt`, `LogoutJwt`, `SecurityEventJwt`, and `AuthorizationRequestJwt` alongside `Jwt` / `AccessTokenJwt`. To make
  the capability real, the resource-server access-token path (`DefaultAuthenticateAccessTokenHandler`) now requires
  `at+jwt` per RFC 9068 §4 (single-valued — the generic `JWT` type is rejected to preserve the substitution defense),
  and the `access_token_type` setting default moved from `JWT` to `at+jwt` per RFC 9068 §2.1 so issued JWT access
  tokens are explicitly typed by default. The point-in-time backlog note that this would "extend the decode/verify
  options already on `IJoseSerializer`" was inaccurate — no such options object existed, and the JWT-validation layer is
  the correct home._

- **`F36` — JWS/JWE JSON serialization (General + Flattened) with multiple signatures / recipients.** The serializer is
  compact-only; RFC 7515/7516 also define the General and Flattened **JSON** serializations, which carry **multiple
  signatures** (JWS) / **multiple recipients** (JWE) and **unprotected** header members — the shape many
  verifiable-credential and multi-audience scenarios require, and a prerequisite for some `F34`/`F38` use. Additive to
  the existing encode/decode paths. Touch points: [`src/NCode.Identity.Jose`](src/NCode.Identity.Jose),
  [`src/NCode.Identity.Jose.Abstractions`](src/NCode.Identity.Jose.Abstractions).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. Rather than bolt a second, parallel JSON-only options family
  onto the existing compact-only one, the encode options were **unified**: `JoseSigningOptions` / `JoseEncryptionOptions`
  now model **any** serialization via a `JoseSerializationFormat` (`Compact` / `Flattened` / `General`) property, with
  signatures/recipients always expressed as collections (`JoseSignatureOptions` / `JoseRecipientOptions`) — compact is
  simply the degenerate one-element, no-unprotected-header case. The old per-operation `JoseEncodingOptions` base and the
  `ExtraHeaders` / single-credential shapes were removed (greenfield clean cutover); the shared content-encryption
  (`enc`) + compression (`zip`) algorithms moved to the operation level while per-recipient key-management stays on
  `JoseKeyManagementCredentials`. `KeyManagementAlgorithm.SupportsKeyWrapping` gates multi-recipient encodes (direct
  `dir` / `ECDH-ES` agreement is single-recipient only). Decode auto-detects the JSON serialization (leading `{`) on the
  existing `Decode` / `Deserialize` surface and verifies/decrypts against each signature/recipient until one matches the
  supplied key. Buffers use `NCode.Buffers` pooled writers throughout._

- **`F39` — Cap the accepted lifetime of client assertions (RFC 7523 §3).** A `private_key_jwt` / `client_secret_jwt`
  assertion is accepted for any `exp` that has not yet passed — including one at the top of the `NumericDate` range
  (year 9999), whose `jti` is then held in the replay cache effectively forever. RFC 7523 §3 lets the server reject an
  assertion whose `exp` is "unreasonably far in the future", and mainstream providers bound assertion lifetime. Add a
  per-client/tenant maximum assertion lifetime setting (bounded by the merge lattice, tightened by the FAPI 2.0
  security profile) enforced against `exp - iat` (or `exp - now` when `iat` is absent), rejecting with
  `invalid_client`; this also bounds replay-cache retention. The overflow on such an `exp` is already fixed
  ([ADR-0123](docs/adr/0123-numeric-date-is-read-strictly-and-range-guarded.md)); the integration test
  `PostToken_ClientSecretJwt_WhenExpAtMaxNumericDate_ReturnsAccessToken` flips to a rejection when this lands. Touch
  points: `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/ClientAssertionClientAuthenticationHandler.cs`,
  `OpenIdSettingKeys`, the FAPI security profile.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0124](docs/adr/0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md). Delivered as a general JWT-validation
  capability rather than a handler-local check: `TokenLifetimeRequirement.MaxLifetime` (via
  `ValidateTokenLifeTimeOptions.MaxLifetime`) bounds `exp` minus the earlier of `iat` and `nbf` — or the validation time
  plus the clock skew when neither is asserted — and a set maximum requires `exp`. The sketched `exp - iat` rule was
  gameable while `iat` went unvalidated, so the lifetime requirement now also validates `iat` for every token (a valid
  `NumericDate`, not beyond the clock skew in the future — the `iat` half of FAPI 2.0 §5.3.2.1). The new
  `client_assertion_max_lifetime` setting defaults to five minutes (Auth0's maximum) as a `Min` ceiling; the FAPI 2.0
  profile pins it at five minutes rather than tightening further, because FAPI defines no assertion lifetime. The
  replay record is now retained for `exp` plus the client's effective `clock_skew` instead of a fixed five minutes,
  closing a replay window under a larger configured skew. The integration test is now
  `PostToken_ClientSecretJwt_WhenExpAtMaxNumericDate_Returns400`. Capping signed request objects with the same
  primitive is tracked as `F40`._

- **`T2` — Browser-automatable login + consent UI with a seeded conformance identity.** _Login/consent surface
  implemented._ The OpenID Provider has an opt-in interactive login/consent surface
  ([ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)): a cookie scheme the authorize
  challenge redirects into, plus a React + MUI `apps/auth` bundle (OP-hosted under `/account/*`) and credential
  endpoints reusing the local-account path, exposing the stable locators (`username`, `password`, `login`,
  `consent-allow`, `consent-deny`). Decision record:
  [ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md). Detailed plan:
  [`docs/conformance/login-consent-ui.md`](docs/conformance/login-consent-ui.md). **Remaining for conformance:** the
  reachable HTTPS host to drive it headlessly end-to-end is **done** (`T3`); the seeded conformance identity is **done**
  (`T4`); independent of `F6`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md),
  [ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md)._
- **`T3` — Reachable HTTPS host for the external harness.** _Done._ The only launchable host used to be
  the in-process `TestServer`; the external OpenID Foundation suite needs a real HTTPS origin whose discovery
  `issuer` byte-matches that origin. Delivered as the dedicated **`Conformance` launch profile**
  ([`launchSettings.json`](src/NCode.Identity.OpenId.Host/Properties/launchSettings.json)): it binds all
  interfaces on the stable port (`applicationUrl = https://0.0.0.0:5001;http://0.0.0.0:5000`) so the suite's Docker
  containers reach it via `host.docker.internal:5001` (a `localhost`-only bind is not reachable through
  `host.docker.internal`, which maps to the host network, not loopback), and pins the discovery `issuer` to the reached
  origin via `OpenId__Server__Settings__tenant_issuer = https://host.docker.internal:5001`. The pin rides the
  parent-owned (`Keep`) `tenant_issuer` setting ([ADR-0038](docs/adr/0038-setting-merge-lattice-duals.md)), so the
  server-level value flows down to the single static tenant deterministically, independent of request host / proxy
  headers. It lives in the launch profile rather than `appsettings.Conformance.json` because the shared `Conformance`
  environment overlay is also loaded by the Aspire-orchestrated `ConformanceAspire` dashboard instance on `:5003`, which
  must keep its own request-derived issuer. The profile reuses the conformance tenant/seed (`T4` — `AddConformanceSeed`,
  via `ASPNETCORE_ENVIRONMENT=Conformance` → `appsettings.Conformance.json`) and the interactive login/consent UI (`T2`
  — the `apps/auth` bundle served under `/account/*`). Run recipe:
  `dotnet run --project src/NCode.Identity.OpenId.Host --launch-profile Conformance`. The live
  container-reachability + discovery byte-match re-confirmation and the launch→compose-up→run-test-plan→teardown
  orchestration script are `T1` Phase C/D, not this item. Detailed plan:
  [`docs/conformance/reachable-host.md`](docs/conformance/reachable-host.md).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`T4` — Deterministic conformance tenant, clients, and test user seeding.** Seeding exists only in test
  infrastructure; a launched host seeds only the bootstrap-admin client. Add an idempotent, config-driven
  startup seeder (bootstrap-admin-style) for the conformance tenant, the three Basic-OP clients, and a
  fixed test user. Detailed plan:
  [`docs/conformance/conformance-seeding.md`](docs/conformance/conformance-seeding.md). Static client half
  is retired by `F6` (DCR); tenant/user seeding is not.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased] (`AddConformanceSeed`)._
- **`T5` — Discovery-metadata alignment audit.** Discovery is data-driven from discoverable settings;
  `appsettings.json` over-advertises `grant_types_supported` (`implicit`) and the response-type set
  includes unverified flows, which fails Config OP. Audit every advertised field down to verified support.
  Detailed plan: [`docs/conformance/discovery-alignment.md`](docs/conformance/discovery-alignment.md).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)._

- **`T7` — Real Node-crypto signer for the probe runner's asymmetric legs.** _Done._ The Phase-3 probe runner's
  `private_key_jwt` and DPoP probes accepted an **injected** `ClientAssertionSigner` / `DpopProofFactory` (unit-tested
  with a fake), but the CLI did not wire a real signer, so those legs **skipped** with "no client key configured" even
  when the OP advertised them. Delivered a Node-`crypto`-backed signer
  ([`src/frontend/apps/conformance-runner/src/signer.ts`](src/frontend/apps/conformance-runner/src/signer.ts)) that
  loads a PEM- or JWK-encoded private key, resolves the JWA signature algorithm (inferred from the key — the curve for
  EC `ES256`/`ES384`/`ES512`, `EdDSA` for Ed25519/Ed448, `PS256` for RSA — overridable to any `ES*`/`PS*`/`RS*`/`EdDSA`
  via `--private-key-alg` / `--dpop-key-alg`), and builds the two compact JWS artifacts: the `private_key_jwt` client
  assertion (OIDC Core §9, carrying the JWK `kid` when present) and the DPoP proof (RFC 9449 §4, embedding the public
  JWK in the `dpop+jwt` header with a fresh `jti`/`iat`). ECDSA signatures use the JOSE raw `ieee-p1363` encoding and
  RSA-PSS uses digest-length salt. New `--private-key` / `--dpop-key` options load the keys at the process boundary and
  feed the signers into the runner. Unit-tested with freshly generated EC/RSA/Ed25519 keys (algorithm inference,
  override validation, JWK loading, public-only JWK projection, and round-trip signature verification). Lands most
  usefully once the backend advertises `private_key_jwt` / DPoP (`S3`, `F7`, `F16`). Touch points:
  [`src/frontend/apps/conformance-runner`](src/frontend/apps/conformance-runner).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._

- **`T8` — Triage the Basic OP (DCR) live-run dispositions.** _Done._ The module verdicts produced by the **external
  OpenID Foundation Conformance Suite** (the `T1` oracle), run **locally** via
  [`build/conformance/run-conformance.ps1 -IncludeBrowserPlans`](build/conformance/run-conformance.ps1) (`T6`). The
  first full browser-driven Basic OP run under Dynamic Client Registration completed `18 PASSED, 11 WARNING, 4 SKIPPED,
4 FAILED` (38 modules); after triage and fixes the plan runs **clean** — `32 PASSED, 0 WARNING, 3 REVIEW, 4 SKIPPED,
0 FAILED` (39 modules incl. Config OP) with a green gate (`Conformance run PASSED`), and `expected-failures.json` has
  no entries. The full disposition log — every fixed backend bug, the codified intentional skips, and the harness
  refinements — lives in [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md) (§ "Live-run
  dispositions (`T8`)"), with the individual fixes also in [`CHANGELOG.md`](CHANGELOG.md) and their ADRs. **Both
  harness follow-ups are resolved:**
    - **Unreachable-suite hang.** `run-conformance.ps1` now runs each `run-test-plan.py` invocation under a hard
      wall-clock watchdog (`-PlanTimeoutSeconds`, default `1800`) that tree-kills a plan on overrun, and pins the suite
      script's own retry bounds explicitly, so a suite that drops out mid-plan fails fast instead of hanging overnight.
    - **Parallel browser-session cookie collision (`oidcc-server`, `oidcc-idtoken-signature`).** The plan **keeps
      `parallel=2`** for the speed boost rather than falling back to `parallel=1`: cross-module contamination is
      structurally impossible because the suite isolates browser state **per test module** (each module gets its own
      `BrowserControl` with a per-instance `CookieManager`), the plan runs under `client_registration=dynamic_client` so
      each module registers its **own** client and `redirect_uri`, and the OP's interactive login is **request-bound**
      (`PostLoginAsync` round-trips the `ReturnUrl` carrying the parked request's `state` grant key, with no ambient
      server-side pending-request state). The one earlier observation did not reproduce and two consecutive full harness
      runs are green at `parallel=2`.

    Design, phases, and the baseline schema are in [`docs/conformance-testing.md`](docs/conformance-testing.md)
    (Appendix F) and [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md). Touch points:
    [`build/conformance`](build/conformance), the authorization/token endpoint handlers and the interactive consent
    surface under [`src/NCode.Identity.OpenId.Authentication`](src/NCode.Identity.OpenId.Authentication) /
    [`src/NCode.Identity.OpenId.Authentication.Interactive`](src/NCode.Identity.OpenId.Authentication.Interactive), and
    the SPA consent form under [`src/frontend/apps/auth`](src/frontend/apps/auth).
    _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md)._
- **`F40` — Harden signed request objects: lifetime, audience, and explicit typing (JAR, CIBA, FAPI Message
  Signing).** Signed request objects —
  the authorization endpoint's inline `request` / `request_uri` (RFC 9101) and CIBA signed authentication requests —
  have their `nbf` / `iat` / `exp` validated (and `exp` required) but no lifetime cap, so a pre-generated request object
  stays usable until an arbitrarily distant `exp`. FAPI 1.0 Advanced and FAPI 2.0 Message Signing
  require a request object to carry `nbf` and `exp`, with `exp` no more than 60 minutes after `nbf` and `nbf` no more
  than 60 minutes in the past; CIBA Core §7.1.1 requires `iat`, `nbf`, `exp`, and `jti` on a signed authentication
  request. The general `TokenLifetimeRequirement.MaxLifetime` cap built for `F39`
  ([ADR-0124](docs/adr/0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md)) provides the lifetime half: add a
  `request_object_max_lifetime` setting (`Min` ceiling, pinned by the FAPI profile), require the claims each profile
  demands (`nbf`, and CIBA's `iat` / `jti`) in both loaders, and decide the default. The authorization endpoint also
  checks a request object's `aud` only when `request_object_expected_audience` is set, although RFC 9101 §4 expects the
  issuer and FAPI 2.0 Message Signing requires it; validate `aud` against the issuer by default, and decide whether to
  require the explicit `typ` `oauth-authz-req+jwt` (RFC 9101 §10.8) so a request object cannot be confused with another
  JWT the client signs. The CIBA loader also still validates with the JWT library's default 5-minute clock skew rather
  than the client's `clock_skew` setting; align it with the JAR loader in the same pass. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultLoadAuthorizationRequestHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/BackchannelAuthentication/Handlers/DefaultLoadBackchannelAuthenticationRequestHandler.cs`,
  `OpenIdSettingKeys`, `Fapi2SecurityProfile`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0187](docs/adr/0187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md). The JWT library's
  lifetime requirement gains `RequireNotBefore` and `RequireIssuedAt`. The new client-scoped
  `request_object_max_lifetime` (`Min` ceiling, default and FAPI 2.0 pin 60 minutes) caps both loaders, and
  `request_object_nbf_required` (`Or` merge, default `false`, FAPI 2.0 pin `true`) makes `nbf` mandatory at the
  authorization endpoint. The authorization endpoint always validates `aud`, against the tenant's issuer unless
  `request_object_expected_audience` is set. The CIBA loader requires `iat`, `nbf`, `exp`, and `jti` and uses the
  client's `clock_skew`. The explicit `typ` `oauth-authz-req+jwt` is not required, because OpenID Connect Core clients
  and the conformance suite do not send it and `iss`, `aud`, and the client's signature already bind the object to
  its purpose._
- **`F41` — Make the tenant's issuer the single source of the routing host, retiring `DomainName`.** The tenant now
  carries a first-class `Issuer` (its request-independent identity, and the origin of every absolute URL the server
  publishes). `DomainName` overlaps it: the issuer URL already embeds a host, and `DomainName` exists only as the
  routing key for `DynamicByHostTenantStrategy`. Nothing keeps the two in step, so a tenant can be reachable at
  `op.example` while advertising `https://other.example` — and OIDC Discovery §4.3 requires the returned `issuer` to
  match the prefix the document was fetched from, so a drifted tenant is rejected by conforming clients. Derive the
  routing host from the issuer instead and drop `DomainName`. **Two constraints discovered while scoping it:** the
  derived host index must be **non-unique**, because `DynamicByPath` tenants legitimately share a host
  (`https://op.example/tenant-a`, `…/tenant-b`); and the uniqueness that *should* be enforced — and currently is not
  anywhere — is on the **full normalized issuer**, since two tenants sharing an issuer is a far worse collision than a
  duplicate domain name. Host uniqueness becomes a validator rule applied only when the host strategy is configured,
  because it is not a universal invariant. Touch points: `PersistedTenant`, `TenantEntity`, `CoreModelContributor`,
  `TenantStore`, `DynamicByHostTenantStrategy`, `DefaultTenantValidator`, the management tenant contracts, and a
  migration.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0129](docs/adr/0129-the-tenant-issuer-is-the-single-authoritative-origin.md). The derived host index is
  non-unique as scoped, and the
  new filtered UNIQUE index on the normalized issuer enforces an invariant nothing previously enforced._

- **`F42` — The host's tenant configuration section binds to nothing.** `AddConfiguration` binds tenant resolution from
  `{section}:TenantResolution` (so `OpenId:TenantResolution`), but the host's
  [`appsettings.json`](src/NCode.Identity.OpenId.Host/appsettings.json) configures `OpenId:Tenant` — which binds
  to no options type at all. The host therefore silently ignores its own `Mode` and `TenantPath`, and only works
  because the defaults coincide (`StrategyCode` already defaults to `StaticSingle`, and an empty tenant path is
  equivalent to the configured `/`). Any operator copying that shape would find their tenant configuration ignored with
  no error. Decide which key is canonical, make the other fail loudly (or bind), and add a test that a configured
  non-default tenant path is actually honored.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0137](docs/adr/0137-configuration-binds-strictly-and-is-validated-at-startup.md),
  [ADR-0138](docs/adr/0138-protocol-endpoints-are-served-under-the-tenant-route.md). `OpenId:TenantResolution` is
  canonical; `OpenId:Tenant` is the materialization section (`OpenIdTenantOptions`), so the misplaced keys did bind —
  into a type with no such members. Every server-bound section is now strict and validated on start, which also exposed
  the `Conformance` launch profile's dead `tenant_issuer` pin. Writing the honored-path test exposed that the tenant
  route had never been served: the protocol endpoint group used an empty prefix, so a configured path published an
  issuer nothing answered under and the path strategy could not resolve a tenant. The group is now mounted under the
  tenant route, and the base-address and link-generation defects on that path are fixed. Follow-ups: `F50`, `F51`,
  `F52`._

- **`F43` — Validate `tls_client_auth` certificate chains in the server against configured trust anchors.**
  `tls_client_auth` ([ADR-0107](docs/adr/0107-mutual-tls-client-authentication.md)) assumes the TLS terminator already
  PKI-validated the certificate; the handler only matches subject DN / SAN. That forces the proxy to
  `RequireAndVerifyClientCert` against a CA bundle, which (a) rejects `self_signed_tls_client_auth` certificates on the
  same host, (b) cannot select a per-tenant CA when tenants share a hostname (TLS options bind per SNI, before the tenant
  is known), and (c) becomes an **authentication bypass** if the proxy only requests the certificate
  (`RequireAnyClientCert`) — a self-signed certificate carrying the registered subject DN would match. Add
  tenant/client-scoped trust-anchor settings (CA bundle, optional intermediates from the forwarded chain, revocation
  policy) and build the chain with `X509Chain` (custom root trust, `clientAuth` EKU, validity) before the identity match,
  so the proxy only needs to request and forward the certificate. Found during the
  [hosting analysis](docs/hosting-analysis.md#8-traefik-mtls-and-tenant-pki). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/TlsClientAuthClientAuthenticationHandler.cs`,
  `MutualTlsClientAuthenticationHandler`, `OpenIdSettingKeys`. `IClientCertificateResolver` already surfaces the chain
  the client presented (`PresentedClientCertificate.Intermediates`,
  [ADR-0172](docs/adr/0172-forwarded-client-certificates-are-decoded-by-a-registered-format.md)). Capture the design in
  an ADR.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0173](docs/adr/0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md). Trust anchors are the
  tenant/client-scoped `tls_client_auth_trust_anchors` (a self-issued entry is an anchor, any other an intermediate),
  and revocation is `tls_client_auth_revocation_mode` (`online` by default). `IClientCertificateChainValidator` builds
  the chain with custom root trust, the forwarded intermediates as hints, and the `clientAuth` EKU, and fails closed
  without anchors. The mutual-TLS base handler gained an `IsCertificateTrustedAsync` step that runs before the match
  and audits `client_certificate_untrusted`._

- **`F44` — Accept Traefik's `X-Forwarded-Tls-Client-Cert` encoding.** Traefik's `passTLSClientCert` (`pem: true`)
  forwards each certificate as base64 DER (PEM delimiters and newlines stripped) and comma-separates the chain when the
  client presents one. `MutualTlsClientCertificateFormat.Base64Der` decodes a single certificate only, so a presented
  chain fails to parse and `DefaultClientCertificateResolver` silently falls back to "no certificate". Make the base64-DER
  format list-aware (leaf first; expose the rest as intermediates for `F43`) or add a dedicated format. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultClientCertificateResolver.cs`,
  `src/NCode.Identity.OpenId.Authentication.Abstractions/Tokens/MutualTlsClientCertificateFormat.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0172](docs/adr/0172-forwarded-client-certificates-are-decoded-by-a-registered-format.md). `Base64Der` became
  list-aware rather than gaining a Traefik-specific sibling, since a comma never occurs in base64. The format enum
  failed ADR-0150's test and became registered `IClientCertificateHeaderDecoder` strategies named by `Format`, which
  also closes `MutualTlsClientCertificateFormat` under `D15`. The PEM and XFCC formats read chains too, and a forwarded
  value that cannot be decoded is `invalid_request` rather than no certificate; behind a proxy the connection's
  certificate is never read._

- **`F45` — Per-tenant rate limits.** Customers run their dev, test, and prod environments as separate tenants on the
  one production platform ([hosting analysis](docs/hosting-analysis.md#environment-model)), so a customer's load test
  against a `-dev` tenant shares hardware with every other tenant's production. Nothing limits a tenant's request rate
  today. The design is
  [ADR-0196](docs/adr/0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md), and its shared counter
  port exists: `IExpiringCounterSet`, its in-process default (registered by `AddIdentityServer`), and the Valkey
  adapter `AddValkey` registers. Remaining, per the ADR:
  - **Settings.** The tenant-wide and per-client limits (requests per window) as integer settings at the server and
    tenant scopes, the per-client limit also at the client scope, merged with `Min` and unset meaning unlimited; with
    their descriptors, domain validation, and scope declarations.
  - **The limiter.** The two-window sliding estimate over `IExpiringCounterSet` (count first, so rejected requests
    count), keyed `rate-limit:{tenantId}:{partition}:{windowStart}` with counters kept for two windows; failing open
    with a warning when the store throws.
  - **Where it runs.** An endpoint filter on the OpenID endpoint group after `OpenIdEnvironmentEndpointFilter` for the
    tenant-wide and per-address limits; the per-client limit where client authentication succeeds, so a request cannot
    spend another client's allowance by naming its `client_id`.
  - **The response.** `429 Too Many Requests` (RFC 6585 §4) with `Retry-After` (whole seconds to the window's end,
    rounded up) and `Cache-Control: no-store`, plus a rejection metric and log.
  - **Tests.** Unit tests for the estimate, the partitions, and fail-open; an integration test that drives a tenant past
    its limit and reads `429` and `Retry-After`.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/OpenIdEndpointGroup.cs`, the setting descriptor
  data sources, `src/NCode.Identity.SharedState.Abstractions/IExpiringCounterSet.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0196](docs/adr/0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md), amended with what was
  built. The limits are per minute: `rate_limit_tenant_per_minute` and `rate_limit_address_per_minute` at the tenant
  scope, and `rate_limit_client_per_minute` at the tenant and client scopes. The per-address limit became its own
  setting, so the many clients behind one NAT or backend address are not throttled as one client.
  `RateLimitEndpointFilter` runs between the environment and exception filters, so a refused request is not audited.
  The address limit is judged before the tenant-wide one and a request it refuses does not count against the tenant;
  an IPv6 address counts by its `/64`. The client authentication service judges the per-client limit once per request,
  for confidential clients only, since a public client's `client_id` proves nothing. `Retry-After` is solved from the
  estimate rather than the window's end. A refusal is `temporarily_unavailable` with `429`,
  `Retry-After`, and `Cache-Control: no-store`. Rejections are logged with stable event IDs rather than counted in a
  metric, since the family has no protocol meter yet. Unit tests cover the estimate, each partition, fail-open, and
  cancellation; an integration test drives discovery past a server-wide limit. Quotas remain as `F79`._

- **`F80` — Metrics for the protocol endpoints.** Implement
  [ADR-0197](docs/adr/0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md): one owner per
  signal, only bounded attributes, and nothing measured twice. Done so far: the `NCode.Identity.OpenId.Authentication`
  meter (`OpenIdAuthenticationMetrics.MeterName`, instruments in the internal `AuthenticationMetrics` singleton from
  `IMeterFactory`), `ncode.identity.rate_limiting.requests` in `DefaultRequestRateLimiter`, and the
  `ncode.identity.openid.endpoint` tag from `OpenIdMetricsEndpointFilter`. Remaining:
  - **Event meter.** Move `DefaultEventMetricsHandler` to `IMeterFactory` with a public meter-name constant, and rename
    its attributes to `ncode.identity.event.type`, `ncode.identity.audit.action`, and `ncode.identity.audit.outcome`.
  - **Endpoint enrichment.** On the token endpoint, tag `ncode.identity.openid.grant_type` (`_OTHER` for an
    unregistered value). Tag `ncode.identity.openid.error` where the error response is written, so the endpoints, the
    exception filter, and the rate-limit filter all tag it.
  - **Event attributes.** An interface through which an event type declares bounded metric attributes (the grant and
    token type of `token.issued`, the method of a client-authentication failure), read by the event metrics handler.
  - **Shared-state instrument.** `ncode.identity.shared_state.operation.duration` (operation, adapter, `error.type`) in
    the in-process and Valkey adapters, under an `NCode.Identity.SharedState` meter.
  - **Reference host.** The OpenTelemetry SDK and OTLP exporter, the family's meters plus ASP.NET Core, Kestrel,
    `System.Net.Http`, and runtime instrumentation, and a view that drops `server.address` and `server.port` from
    `http.client.request.duration`. Pin current upstream versions and check them against the advisory database.
  - **Tests and catalog.** Adopt `MetricCollector<T>` (`Microsoft.Extensions.Diagnostics.Testing`) for the instrument
    tests, which read a factory-scoped `MeterListener` today, and add the instrument catalog beside the event catalog.
  Touch points: `src/NCode.Identity.Events/DefaultEventMetricsHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Metrics/`, the shared-state adapters, and the host's `Program.cs`.
  _Closed: 2026-10-21. [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0197](docs/adr/0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md), amended with
  what was built, and the instrument catalog [`docs/metrics.md`](docs/metrics.md). The error attribute is tagged once,
  in `DefaultOpenIdResultExecutor`, which writes every OpenID response, and the grant-type attribute in the grant
  handler selector, which knows the registered grant types; the attribute names are public on `OpenIdMetricAttributes`.
  Events declare their attributes through `ISupportMetricAttributes`: `token.issued` adds its token type, and a
  client-authentication failure adds the method the client attempted, which `IAuditEventRecorder` now takes. The grant
  type stays on the HTTP instrument rather than being repeated on the event. The shared-state histogram is recorded by
  the Valkey adapter only, in its own `NCode.Identity.SharedState.Valkey` meter; the in-process adapter has no latency
  worth a series. The host wiring is `AddOpenTelemetryMetrics` in `NCode.Identity.Server`, which subscribes to the
  family by the `NCode.Identity.*` wildcard, needs no instrumentation packages because every meter it adds is built
  into .NET, and adds the OTLP exporter only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Every instrument test reads
  through `MetricCollector<T>`._

- **`F46` — Kubernetes deployment artifacts for the host.** `NCode.Identity.OpenId.Host` is the deployable
  ([ADR-0130](docs/adr/0130-distinct-names-for-host-local-dev-orchestrator-and-library.md)); it already trusts forwarded
  headers only from configured proxies (`ReverseProxy:KnownNetworks` — set it to the cluster's pod CIDR), refuses
  development-only settings and an unprotected Data Protection key ring in Production (`AddProductionSafety`), and
  separates liveness (`/health/live`, no checks) from readiness (`/health/ready`: the database, and Valkey when
  configured). The production image is done: chiseled, non-root on port 8080, built for amd64 and arm64, carrying the
  `apps/auth` bundle ([ADR-0178](docs/adr/0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md)).
  The Helm chart [`charts/ncode-identity-host`](charts/ncode-identity-host) is done: restricted pods, probes, `preStop`
  drain, PodDisruptionBudget, secrets by reference, and Valkey required for more than one replica
  ([ADR-0179](docs/adr/0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)).
  Still missing, in one session-sized slice: `Host.MigrationService` (which has no image) should run as the chart's
  pre-deploy Job (a Helm `pre-install`/`pre-upgrade` hook) with expand/contract schema changes, so a rolling update
  never breaks the running version. Publishing the image and the chart is `F48`. A served tenant
  without an issuer already fails closed
  ([ADR-0165](docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)). Plus the
  OpenTofu modules, bootstrap stack, and GitHub
  Actions workflows in the public `NCodeGroup/identity-platform` repository
  ([ADR-0132](docs/adr/0132-platform-infrastructure-as-code-is-public-on-github.md); see the
  [hosting analysis](docs/hosting-analysis.md#11-iac-and-delivery)). Touch points:
  `src/NCode.Identity.OpenId.Host/Program.cs`, `src/NCode.Identity.OpenId.Host/Dockerfile`,
  `charts/ncode-identity-host`, `src/NCode.Identity.OpenId.Host.MigrationService`, `src/NCode.Identity.Server/ProductionSafety`,
  `src/frontend/apps/auth`.
  _Closed: the migration worker ships in the host image and the chart runs it as a `pre-install`/`pre-upgrade` Job, and
  every migration keeps the previous release working
  ([ADR-0180](docs/adr/0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md); see the
  [`CHANGELOG.md`](CHANGELOG.md)). The `NCodeGroup/identity-platform` modules, stacks, and workflows continue under
  `F48`._

- **`F47` — Route a tenant's mTLS endpoint-alias host.** `mtls_endpoint_alias_authority` publishes the RFC 8705 §5
  `mtls_endpoint_aliases` on a separate authority (for example `https://acme.mtls.example`), but `DynamicByHost` routes
  only on the host component of the tenant's issuer
  ([ADR-0129](docs/adr/0129-the-tenant-issuer-is-the-single-authoritative-origin.md)), so a request to the alias host
  resolves no tenant. Multi-tenant mTLS on per-tenant hostnames therefore cannot work today. Found during the
  [hosting analysis](docs/hosting-analysis.md#domain-and-dns--nidentityio). The design is settled; deliver it in one
  change:

  - **The alias authority becomes a first-class tenant property.** Add `PersistedTenant.MtlsEndpointAliasAuthority`
    beside `Issuer`: nullable, and an `https` origin (scheme and authority only). Retire the
    `mtls_endpoint_alias_authority` setting outright, with no shim. A value the router looks up must be typed and
    indexed. Deriving a host from one key of the opaque settings document would make the store know an OpenID setting.
    It would also give a settings write a routing side effect that tenant validation never judges, because settings
    have their own endpoint and concurrency token.
  - **Derive the alias host into the routing lookup the same way as the issuer host.** `TenantEntity` gains
    `NormalizedMtlsAliasHost`, computed from the property on every write and never independently writable, with a
    filtered index like `NormalizedIssuerHost`. `ITenantStore.GetOrDefaultByIssuerHostAsync` becomes
    `GetOrDefaultByHostAsync`. It matches either column and resolves no tenant when more than one row matches, so a
    lookup fails closed even if validation is bypassed.
  - **Each strategy supplies the default, persisted on write**
    ([ADR-0165](docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)).
    `TenantStrategy` gains a default-alias method beside `GetDefaultIssuerAsync`. `DynamicByHostTenantOptions` gains
    `MtlsEndpointAliasTemplate`, for example `https://{tenantId}.mtls.example`. `DynamicByHostTenantOptionsValidator`
    checks it like `IssuerTemplate`, and it must also be an origin whose host `RegexPattern` maps to itself. The
    expansion is stored with the tenant, so changing the template never moves an existing tenant. The other strategies
    have no template; there an alias is set explicitly.
  - **Validation judges both hosts**
    ([ADR-0160](docs/adr/0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md)).
    `ITenantStrategy.ValidateTenantAsync` and `ITenantResolver.ValidateTenantAsync` take the alias authority beside the
    issuer. Update every caller: `DefaultTenantProvisioner`, both calls in `DefaultTenantValidator`, and
    `TenantApiEndpointHandler`. The base strategy requires an `https` origin. `DynamicByHost` requires the pattern to
    map the alias host to itself. It also rejects, as a conflict, any of the tenant's hosts that another tenant holds
    as either its issuer host or its alias host.
  - **Discovery reads the tenant, not a setting.** `OpenIdTenant` exposes the alias authority, and
    `DefaultDiscoverMetadataHandler` builds `mtls_endpoint_aliases` from it. That publication has no test today; add
    one.
  - **Management contract.** `CreateTenantRequest`, `UpdateTenantRequest`, `TenantResource`, and
    `TenantAddressResource` carry the alias authority, and `TenantAddressResource` gains an
    `IsMtlsEndpointAliasAuthorityDerived` flag beside `IsIssuerDerived`. `TenantApiEndpointHandler` handles it the way
    it handles the issuer. Editing it in the console is `U25` (done).
  - **Migration (expand-only).** Add the column and its index. Then move each tenant's stored
    `mtls_endpoint_alias_authority` out of `SettingsJson` into the column, removing the key and rewriting the settings
    concurrency token. This is the same retirement `RetireAllowUnsafeTokenResponse` performs
    ([ADR-0139](docs/adr/0139-setting-names-are-a-closed-vocabulary.md)).
  - **Records.** Write an ADR that amends ADR-0129's single-origin rule with a derived secondary routing origin, and
    amend the alias bullet of [ADR-0097](docs/adr/0097-sender-constrained-access-tokens-dpop-and-mtls.md). Update
    `CHANGELOG.md` and the F47 note in [`docs/hosting-analysis.md`](docs/hosting-analysis.md).
  - **Tests.**
    - `DynamicByHostTenantStrategyTests`: route by the alias host, a conflict in each direction, a pattern mismatch,
      and the template default.
    - The options validator, the tenant store, and the discovery handler.
    - An integration test that resolves a tenant through its alias host and reads its `mtls_endpoint_aliases`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0195](docs/adr/0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md). Delivered: the tenant property,
  the derived `NormalizedMtlsEndpointAliasHost` column and index, `GetOrDefaultByHostAsync`, validation of both hosts,
  discovery from the tenant, the create/update/read contract, the migration, and the tests. The template default and its
  options-validator rules, the `TenantAddressResource` alias and derived flag, and their tests moved to `F78`; console
  editing moved to `U25` (done)._

- **`F49` — Shared `IDistributedCache` for multi-replica deployments.** The DPoP nonce service, the DPoP proof replay
  cache, and the client-assertion replay cache (RFC 7523 §3) run over `IDistributedCache`, and the server registers
  `AddDistributedMemoryCache()` by default, so every replica keeps its own: a nonce issued by one pod is rejected by
  another, and a proof or assertion replayed against a different pod is accepted. Nothing in the server composition or
  the host registers a shared provider, so a deployment must run a single replica until one exists. Add a
  configuration-bound Valkey (Redis-protocol) registration to the server composition, wire it in the host, and report
  it in readiness (`F46`). Give the replay caches an atomic set-if-absent on that store to close the check-then-set race
  their `IDistributedCache` implementations document. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Tokens/DefaultRegistration.cs`,
  `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultDPoPNonceService.cs`,
  `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultDPoPProofReplayCache.cs`,
  `src/NCode.Identity.OpenId.Authentication/Clients/DefaultClientAssertionReplayCache.cs`,
  `src/NCode.Identity.OpenId.Host/Program.cs`.
  _Closed: [ADR-0164](docs/adr/0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) and the
  [`CHANGELOG.md`](CHANGELOG.md) `[Unreleased]` entries "Several replicas can now share DPoP nonces and single-use
  records through Valkey", "DPoP nonces and the DPoP-proof and client-assertion replay records are stored in the
  shared-state port, scoped by tenant", and "A DPoP proof or client assertion could be accepted twice". The three
  stores record into one atomic port, `IExpiringKeySet` (`NCode.Identity.SharedState.Abstractions`), rather than
  `IDistributedCache`, which has no add-if-absent. The in-process adapter (`NCode.Identity.SharedState`) is the
  composition root's default, and the opt-in `NCode.Identity.SharedState.Valkey` adapter (`SET NX PX`, a `ready`-tagged
  `valkey` health check) is registered from `ConnectionStrings:valkey` by the reference host, with Valkey run by the
  AppHost. Records are keyed by tenant and a digest of the request-supplied values. The Valkey adapter is an opt-in
  package wired through `IIdentityServerBuilder.SharedStateLibraryBuilder`, not part of the server composition itself,
  so single-replica self-hosters carry no Redis client. Splitting liveness from readiness, and requiring Valkey when the
  Helm chart runs more than one replica, stay with `F46`._

- **`F51` — Validate a tenant's issuer against the tenant route in the management API.** The static tenant's configured
  issuer is checked when the host starts: an absolute `http`/`https` URL ending in the tenant path
  ([ADR-0137](docs/adr/0137-configuration-binds-strictly-and-is-validated-at-startup.md)). A tenant created or updated
  through the management API gets no such check, so it can be given an issuer that no endpoint is served under — a
  `DynamicByPath` tenant `acme` whose issuer does not end in `/acme`, or any tenant with a query or fragment in its
  issuer — and conforming clients reject its discovery document. Apply the same rules in the tenant validator, binding
  the tenant route with the tenant's own identifier for the path strategy. Touch points:
  `src/NCode.Identity.OpenId.Management/Endpoints/Tenants/DefaultTenantValidator.cs`,
  `src/NCode.Identity.OpenId.Core/Tenants/TenantResolutionOptionsValidator.cs` (share the issuer rule).

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0159](docs/adr/0159-a-tenant-is-named-by-its-creator.md),
  [ADR-0160](docs/adr/0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md). Scoping it exposed that a
  server-minted tenant identifier made a path tenant's issuer unknowable before the tenant existed, so tenants are named
  by their creator (Auth0's model): an immutable lowercase host label. The address rules moved onto the tenant-selection
  seam (`ITenantResolver.ValidateTenantAsync`), shared with startup validation, which each strategy now performs for its
  own options. Beyond the scoped rules, the host strategy now requires an issuer, rejects a host pattern that does not
  map the issuer host to itself, and enforces the host uniqueness ADR-0129 assigned to it; `http` issuers are limited to
  loopback hosts; and the management console's tenant form, which still sent the removed `domainName`, sends the
  issuer. Follow-ups: `F70` (never reissue a retired name or issuer), `F71` (derive an issuer from a template), `U22`
  (the console's tenant scope in its URL)._

- **`F52` — Fail loudly on the configuration that is still accepted without being honored.** Every options section the
  server binds is strict ([ADR-0137](docs/adr/0137-configuration-binds-strictly-and-is-validated-at-startup.md)); three
  inputs remain lenient. (a) Setting **names** under `OpenId:Server:Settings` (and in persisted settings) are an open
  vocabulary: an unregistered name loads as an ad-hoc string setting, which is how the removed `tenant_issuer` pin kept
  "working" unnoticed. Decide whether an unregistered name is rejected, which requires an extension's settings to
  register descriptors. (b) `ReverseProxy:KnownProxies` / `KnownNetworks` skip an entry that does not parse, so a
  mistyped proxy address silently trusts nothing. (c) Single-key reads — `InteractiveAuthentication:BundleRootPath`,
  `DeveloperKeys:Mode`, `Cors:AllowedOrigins` — are not bound, so a misspelled sibling key goes unnoticed. Touch points:
  `src/NCode.Identity.OpenId.Core/Settings/RootSettingsCollectionDataSource.cs`,
  `src/NCode.Identity.Server/ReverseProxy/ReverseProxyRegistration.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/DefaultRegistration.cs`,
  `src/NCode.Identity.Server/DevelopmentEnvironment`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0139](docs/adr/0139-setting-names-are-a-closed-vocabulary.md). (a) Setting names are a closed vocabulary: an
  unregistered name or a value that does not convert to its descriptor's type stops the host when configured, returns
  `400` from the management API, and fails closed when a stored document is loaded; a configuration reload that cannot
  be honored is rejected with an error log. Closing the vocabulary exposed the untyped writes: dynamic client
  registration's RFC 7591 metadata became registered client settings, and its dead `scopes_supported` write was dropped
  (the registration response now reports the scopes the client may request), as was the conformance seed's.
  (b) `ReverseProxy` is validated on start, including the IPv4 shorthand the address parser accepts and the host bits
  the network parser masks. (c) `InteractiveAuthentication`, `DeveloperKeys`, and `Cors` are bound strictly; an unknown
  key mode no longer falls back to `Ephemeral`, and allowed origins must be origin-form. Making `Cors` honest exposed
  that the reference host could not start in Production (`UseDeveloperCors` ran without CORS services); it is now a
  no-op there and any `Cors` section is a production-safety violation. Follow-up: `F53` (value-domain rules)._

- **`F53` — Validate setting values against their domain, not only their type.** Setting names are a closed vocabulary
  and every value must convert to its descriptor's type
  ([ADR-0139](docs/adr/0139-setting-names-are-a-closed-vocabulary.md)), but a value of the right type is still accepted
  without being honored when it is outside the setting's domain. The sharpest case is `security_profile`: a value that
  names no registered `ISecurityProfile` (a typo of `fapi2`) is stored and loaded, and `DefaultSecurityProfileCatalog`
  then applies no profile at all — the client silently runs without the FAPI 2.0 ceiling and requirements it was
  configured for. Enumerated string settings (`subject_type`, `token_endpoint_auth_method`, `access_token_type`,
  `refresh_token_expiration_policy`, `refresh_token_reuse_policy`, `backchannel_token_delivery_mode`, …) accept any
  string, and numeric/duration settings accept out-of-range values (a negative lifetime). Give `SettingDescriptor<T>` a
  value rule (an allowed set, a range, or a validator that can consult registered strategies such as the security
  profiles) and enforce it at the same three entry points — configuration on start, management writes, and stored
  documents failing closed on read. Touch points: `src/NCode.Identity.Settings.Abstractions/SettingDescriptor.cs`,
  `src/NCode.Identity/Settings/DefaultSettingSerializer.cs`,
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingConfigurationReader.cs`,
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSecurityProfileCatalog.cs`,
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs`. Capture the design in an ADR.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0140](docs/adr/0140-setting-values-are-validated-against-their-domain.md). `SettingDescriptor<TValue>.Rule` (a
  `SettingRule<TValue>` built with `SettingRules`) declares every library setting's domain, and the domain is enforced
  with the name and type at every entry point: configuration on start and reload, management writes, stored documents
  (fail closed), and — because they also write settings documents — dynamic client registration and the conformance and
  developer seeders. Domains defined by registered strategies consult them: `security_profile` names a registered
  profile (and the catalog throws rather than applying none), the client-authentication method settings name
  registered handlers (so their descriptors moved to the authentication library), and algorithm lists name registered
  algorithms of their kind, never `none` or `dir`. Declaring the domains surfaced three corrections: the signing lists
  for tokens that cross a trust boundary are asymmetric-only (amending ADR-0083), the never-expiring refresh-token
  policy is removed, and `client_notification_endpoint` gained an empty default. A composed-server test pins every
  default to its own rule._

- **`F54` — Make every registered security profile un-loosenable down the cascade, not only `fapi2`.** The
  `security_profile` domain is the set of registered `ISecurityProfile` names
  ([ADR-0140](docs/adr/0140-setting-values-are-validated-against-their-domain.md)), so a deployment can add a profile by
  registering one. Its merge is not generic: `MergeSecurityProfile` keeps a parent's selection only when it is `fapi2`,
  so a tenant that mandates any other registered profile can have it cleared (or replaced by a weaker one) by a client.
  Make the merge honor every registered profile — a child may not clear or replace a parent's profile, or may only
  select one the parent's profile declares as at least as strict — and let the profile itself declare that ordering
  rather than hard-coding a name. Touch points:
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs` (`MergeSecurityProfile`),
  `src/NCode.Identity.OpenId.Abstractions/Settings/ISecurityProfile.cs`. Capture the design in an ADR (amending
  [ADR-0108](docs/adr/0108-fapi-security-profile-merge-lattice-ceiling-injection.md)).

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0148](docs/adr/0148-security-profiles-accumulate-down-the-settings-cascade.md). Rather than letting a profile
  declare which profiles it outranks — which would let a replacement drop the parent profile's requirements on the
  strength of an author's claim — the selection became a set: `security_profiles` merges with `Union`, so profiles
  accumulate down the cascade, every selected profile's ceilings and requirements apply, and a layered profile selects
  the profile it builds on. Making the guarantee hold for every registered profile surfaced three further corrections:
  the server scope (configuration and the persisted server document) was never expanded, so a server-selected profile
  ran its requirements without its ceilings; the management API's effective-settings previews skipped expansion; and a
  profile could place a ceiling on a projection-merged setting that a child overrides, so ceilings now go through
  `ISecurityProfileCeilingBuilder`, which accepts only lattice-merged settings (`SettingDescriptor.HasLatticeMerge`). The
  `TranslateSecurityProfileToSecurityProfiles` migration was verified against Postgres in both directions. The review
  surfaced the follow-ups `F60` (scope-aware setting vocabulary) and `F61` (client and resource-server policy for
  access-token encryption)._

- **`F55` — Accept combined `response_types` values in dynamic client registration.** RFC 7591 §2 registers
  `response_types` as whole `response_type` values (`code id_token`), but the server models
  `response_types_supported` as single values whose combinations the authorization endpoint accepts, so a registration
  that lists a combination is refused as outside the setting's domain
  ([ADR-0140](docs/adr/0140-setting-values-are-validated-against-their-domain.md)). Implicit and hybrid flows are off by
  default, so only a deployment that re-enables them is affected. Decide the model — store the registered combinations
  and enforce them exactly, or map a combination to its single values (which also admits each value alone) — and make
  registration, the registration response, and the authorization endpoint agree. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultValidateAuthorizationRequestHandler.cs`,
  `src/NCode.Identity.OpenId.Core/Settings/OpenIdSettingRules.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0141](docs/adr/0141-response-types-are-whole-values.md). Response types are whole values everywhere: the
  registered combinations are stored in canonical form (names in ordinal order) and enforced exactly.
  `response_types_supported` lists the seven whole values, discovery advertises the list as configured (the
  combinatorial formatter is gone), and the authorization endpoint accepts exactly a listed value whose grant types are
  allowed (`unsupported_response_type` for the tenant, `unauthorized_client` for the client). Making registration and
  the authorization endpoint agree required three further corrections: the non-standard `hybrid` grant type is removed
  in favor of OpenID Connect Registration §2 (`authorization_code` + `implicit`), registration defaults and checks
  `grant_types` / `response_types` consistently and against what the tenant supports (reporting empty lists rather than
  omitting them), and a response type that returns tokens defaults to the fragment on every authorization-endpoint path
  — the first pass had defaulted to the query string._

- **`F56` — Require PKCE for the authorization code flow by default.** `require_pkce` defaults to `false`, so an
  authorization request without a `code_challenge` is accepted unless the client runs the FAPI 2.0 profile or is the
  developer seed client. The OAuth 2.1 direction and RFC 9700 §2.1.1 expect PKCE (`S256`) for every client that uses the
  authorization code flow; `code_challenge_methods_supported` already defaults to `S256` only. Default `require_pkce` to
  `true` (it merges as a floor, so a client cannot loosen it), decide whether a confidential
  OpenID Connect client that sends a `nonce` may be exempt (RFC 9700 permits the nonce as the alternative mitigation for
  such clients — a genuine trade-off to record in the ADR), and verify the conformance plans and seeders send a
  challenge. Touch points: `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs`
  (`require_pkce`), `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultValidateAuthorizationRequestHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/AuthorizationCode/DefaultValidateAuthorizationCodeGrantHandler.cs`.
  Capture the design in an ADR (amending [ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)).

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0144](docs/adr/0144-authorization-codes-are-bound-to-pkce-by-default.md). `require_pkce` defaults to `true`, and
  one policy (`CodeChallengeRequirement`) decides the binding at both endpoints. The nonce exemption follows OAuth 2.1
  §7.5.1.1 exactly: the deployment relaxes `require_pkce`, an administrator attests the specific client with the new
  `pkce_nonce_exemption` setting (never set by dynamic client registration), the client is confidential — by
  registration at the authorization endpoint, by actual authentication at the token endpoint — and the request requests
  `openid` with a `nonce` (at the token endpoint, in the effective scopes). A missing challenge is `invalid_request`,
  only for code-issuing response types. The work also removed PKCE `plain` (OAuth 2.1 §7.5.2) with a verified data
  migration, made `code_challenge_methods_supported` a registered-strategy domain with an `S256` default, required
  `code_challenge_method`, checked the RFC 7636 syntax of both PKCE values, and fixed the pushed authorization request
  endpoint returning `200` for refusals. The conformance host relaxes and attests; the one Basic OP module that sends
  neither PKCE nor a nonce is a documented expected failure. A design review surfaced the follow-up `F58`._

- **`F57` — Retire `allow_unsafe_token_response`, which duplicates listing `token` in `response_types_supported`.**
  Response types are whole values enforced exactly
  ([ADR-0141](docs/adr/0141-response-types-are-whole-values.md)), so an operator who wants an access token returned by
  the authorization endpoint lists `token`, `id_token token`, `code token`, or `code id_token token`, and a tenant or
  client narrows that list to forbid it. The boolean `allow_unsafe_token_response` (default `true`, merged `And`) is a
  second switch for the same capability — exactly the "second switch to forget" ADR-0083 avoids. Remove the setting and
  its check, and migrate any stored document that sets it (narrowing `response_types_supported` instead). Touch points:
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs` (`allow_unsafe_token_response`),
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultValidateAuthorizationRequestHandler.cs`
  (`ValidateConfiguredConstraints`), `src/NCode.Identity.OpenId.Abstractions/Settings/OpenIdSettingNames.cs`,
  `src/NCode.Identity.OpenId.Abstractions/Settings/OpenIdSettingKeys.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0142](docs/adr/0142-listed-response-types-are-the-only-front-channel-token-control.md). The setting, its name,
  key, descriptor, and check are removed; `response_types_supported` is the only control over the tokens the
  authorization endpoint returns. The `RetireAllowUnsafeTokenResponse` EF migration is the first data migration of
  stored settings documents under ADR-0139: it rewrites every `Servers` / `Tenants` / `Clients` / `ResourceServers`
  document that sets the setting, translating `false` into an intersection-equivalent `response_types_supported`
  (rather than dropping a restriction), removing `true` / `null`, leaving an already-invalid value untouched, and
  bumping `SettingsConcurrencyToken`. It was verified against Postgres over seeded documents (including rollback and
  idempotent re-apply), and the way to write and verify such a migration is now a convention in
  `csharp-production.instructions.md`._

- **`F58` — Enforce the client's registered `token_endpoint_auth_method` at every endpoint that authenticates it.** The
  client-authentication service checks the method a client authenticated with only against its effective
  `token_endpoint_auth_methods_supported`, whose default is every registered handler, `none` included — not against the
  client's registered `token_endpoint_auth_method` (RFC 7591 §2). A client registered as confidential (every dynamically
  registered `client_secret_*` client) can therefore present only its `client_id` and be treated as public: at the token
  endpoint for the `refresh_token` grant and other grants that rely on client authentication, and at the pushed
  authorization request endpoint. Authorization codes are safe — a code that is not bound to PKCE is redeemable only by a
  client that actually authenticates ([ADR-0144](docs/adr/0144-authorization-codes-are-bound-to-pkce-by-default.md)) —
  but the client type should be one fact. Require the method a client authenticates with to be its registered method
  (normalizing an unset registration, and treating `token_endpoint_auth_methods_supported` as the server/profile
  ceiling it is), and decide how a management-API client that registers no method is typed (the conformance seeder
  already registers one method and permits only it). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Clients/DefaultClientAuthenticationService.cs`,
  `src/NCode.Identity.OpenId.Authentication/Clients/OpenIdClientRegistrationExtensions.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/RefreshToken/DefaultValidateRefreshTokenGrantHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs`.
  Capture the design in an ADR.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0145](docs/adr/0145-a-client-authenticates-only-with-its-registered-method.md). The registered method is the one
  fact: the client-authentication service accepts a client only with it and within the
  `token_endpoint_auth_methods_supported` ceiling (an empty ceiling now permits nothing), every built-in handler
  authenticates only clients registered for its method (so a `client_secret_basic` refusal keeps its `401` challenge),
  and `none` is one handler that owns a credential-less form post from a client registered as public. An unset
  registration is the RFC 7591 default `client_secret_basic` (configurable per server or tenant), which types a
  management-API client that registers none; dynamic registration checks the method against the tenant's ceiling. The
  refresh-token grant needed no change of its own: enforcement sits in the one service every authenticating endpoint
  calls. The authorization endpoint now identifies the client by `client_id` instead of through client authentication,
  which also fixed FAPI 2.0 clients being refused there. Seeds register their method explicitly (the developer SPAs
  `none`, the bootstrap administrator `client_secret_basic`, with the API reference sending Basic credentials), and the
  `RegisterClientAuthenticationMethod` migration (verified against Postgres, including rollback and idempotent re-apply)
  turns a single-method ceiling into a registration and removes empty registrations._

- **`F59` — Let configuration replace the developer seed's default SPA clients.**
  `DeveloperInteractiveSeedOptions.SpaClients` starts with the `mgmt-spa` and `playground-spa` entries, and the
  configuration binder appends configured entries to a list that already holds some, so configuring
  `DeveloperInteractiveSeed:SpaClients` adds clients but can never remove or replace the defaults (the convention:
  a bound collection starts empty and its default applies only when nothing is configured). Start the list empty and
  apply the two defaults when none are configured. Touch points:
  `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperInteractiveSeedOptions.cs`,
  `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperInteractiveSeedHandler.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0146](docs/adr/0146-a-development-seeder-registers-only-the-clients-its-configuration-declares.md). The list
  starts empty, but the two clients were not kept as a library default applied when none are configured. The seed now
  has no built-in clients and registers exactly what configuration declares, so it is inert in a host that does not
  ask for it (the Conformance environment had been persisting both loopback clients). The reference host declares
  `mgmt-spa` and `playground-spa` in `appsettings.Development.json`, beside the `Frontend` section that publishes the
  same client ids. An integration test (`DeveloperSpaClientSeedingTests`) fails if a published client id is not
  seeded, or if a seeded client does not resolve at the authorization endpoint with each of its redirect URIs. The
  handler needed no change._

- **`F60` — Make the setting vocabulary scope-aware: reject a setting at a scope that never honors it.** Setting names
  and values are validated against one catalog for every document type
  ([ADR-0139](docs/adr/0139-setting-names-are-a-closed-vocabulary.md),
  [ADR-0140](docs/adr/0140-setting-values-are-validated-against-their-domain.md)), so a server, tenant, client, or
  resource-server document accepts any registered setting even where the runtime never reads it. The sharpest case is
  `security_profiles` on a resource server: it is accepted and stored, but resource-server settings are merged
  unexpanded and requirements are evaluated against client settings, so the profile neither tightens nor enforces
  anything there ([ADR-0148](docs/adr/0148-security-profiles-accumulate-down-the-settings-cascade.md)). Client-only
  settings on a resource server, and resource-server-only settings on a client, are the same class. Give each descriptor
  the scopes it applies to, reject a setting outside them at every entry point (configuration, management writes,
  dynamic client registration and seeders, and stored documents failing closed), and ship the migration that removes —
  or translates — what stored documents carry at a scope that never honored it. Decide separately whether a resource
  server may select a security profile with its own enforcement (for example refusing to issue an access token that
  is not sender-constrained for an audience that requires it). Touch points:
  `src/NCode.Identity.Settings.Abstractions/SettingDescriptor.cs`, `src/NCode.Identity/Settings/DefaultSettingSerializer.cs`,
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs`,
  `src/NCode.Identity.OpenId.Management/Endpoints/*/*ApiEndpointHandler.cs`. Capture the design in an ADR.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0149](docs/adr/0149-settings-declare-the-scopes-they-apply-to.md). Every descriptor declares the required
  `AppliesTo` — the names of the scopes whose effective settings the runtime reads it from — and a document accepts a
  setting only at a scope that applies it or cascades into one that does (`ISettingScopeCollection.AppliesWithin`).
  Scopes are registered `SettingScopeDescriptor` data with the cascade derived from them, so an extension can add a scope
  for an entity of its own; a first cut modeled them as a closed flags enum, which led to the open-vocabulary rule of
  [ADR-0150](docs/adr/0150-vocabularies-are-open-unless-provably-closed.md). The classification came
  from tracing every reader: 39 settings apply to tenants only (discovery metadata and the tenant-wide services), 25 to
  tenants and clients, 78 to clients, and the four access-token encryption settings to clients and resource servers.
  The serializer, the configuration reader, and the catalog's JSON `Expand` take the document's scope, so every entry
  point enforces it; the management previews show only the settings that apply within their scope. The separate
  decision: a resource server selects no security profile — a constraint it needs on its tokens is one of its own
  settings, recorded as `F62`. Tracing the readers surfaced one setting meaningful at a scope that never read it:
  `token_endpoint_auth_signing_alg_values_supported` is now enforced against the client's effective settings (so a
  client-selected profile's ceiling binds), with an empty list permitting nothing. The `RemoveSettingsOutsideTheirScopes`
  migration was verified against Postgres, including rollback and idempotent re-apply. The tracing also surfaced `F63`
  (request-object and UserInfo algorithm settings that only discovery reads)._

- **`F61` — Combine the client's and the targeted resource server's settings for access-token encryption.** When an
  access token targets a single resource server, `DefaultTokenService` takes the encryption settings from that resource
  server's effective settings (server ∩ tenant ∩ resource server) and ignores the client's
  ([ADR-0075](docs/adr/0075-runtime-consumption-of-resource-server-secrets.md)). A bound the client carries — set on the
  client or by a security profile it selects, such as `access_token_encryption_required` or a narrowed
  `access_token_encryption_*_values_supported` — therefore does not apply once a resource server is targeted, although
  a profile's ceiling is meant to bind every scope below the one that selects it
  ([ADR-0148](docs/adr/0148-security-profiles-accumulate-down-the-settings-cascade.md)). FAPI 2.0 constrains none of these
  settings today. Combine the two policies with their lattice merges (`Or` for the requirement, `Intersect` for the
  algorithm lists) before choosing credentials, and decide which side's preference order wins. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Tokens/DefaultTokenService.cs`. Amend ADR-0075.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0158](docs/adr/0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md), together
  with `F62`. A new lattice-only `IReadOnlySettingCollection.Combine` joins sibling scopes' settings (a projection
  throws, so the result cannot depend on order), and the settings that govern an access token are the client's
  effective settings combined with the resource-server-scoped settings of every targeted resource server. The preference
  order is the server's: every effective list keeps the root's order, so a resource server states a preference by
  narrowing. Combining exposed a silent misdirection — the client's narrowing could exclude the resource server's keys
  and the tenant-key fallback would mint a token the resource server cannot decrypt (RFC 9068 §4) — and a
  provenance-dependent multi-audience guard. Both were replaced by one recipient rule: a resource server that holds a key
  usable under its own settings decrypts its own tokens, so a token for it alone is encrypted to that key within the
  combined settings or fails with `server_error`, and a token for it with another resource server is refused; any other
  encrypted token goes to the tenant's key. The policy lives in `IAccessTokenPolicyService` — a public seam with an
  internal default, so a replacement `ITokenService` resolves it rather than skip it (the replaceable resolver only
  supplies each targeted resource server's settings and keys)._

- **`F62` — Let a resource server constrain the access tokens issued for it.** A resource server's settings govern only
  the encryption of the access tokens issued for it ([ADR-0075](docs/adr/0075-runtime-consumption-of-resource-server-secrets.md)),
  and it selects no security profile ([ADR-0149](docs/adr/0149-settings-declare-the-scopes-they-apply-to.md)), so an
  API cannot require that its tokens be sender-constrained, short-lived, or signed with particular algorithms. Auth0
  exposes all three per API, and RFC 9728 lets a protected resource advertise `dpop_bound_access_tokens_required`. Make
  them resource-server settings — a sender-constraint requirement (`Or`), an access-token lifetime ceiling (`Min`), and
  the signing algorithms (`Intersect`) — read from the effective settings of every targeted resource server and combined
  with the client's by their lattice merges at issuance, refusing a token request that cannot satisfy them (every
  audience of a multi-audience token binds). Widen `AppliesTo` of the settings it reuses to resource servers, and share
  the combination with `F61`. Touch points: `src/NCode.Identity.OpenId.Authentication/Tokens/DefaultTokenService.cs`,
  `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultProofOfPossessionService.cs`,
  `src/NCode.Identity.OpenId.Authentication/ResourceServers/DefaultResourceServerCredentialResolver.cs`,
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs`. Capture the design in an ADR.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0158](docs/adr/0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md), together
  with `F61`. `access_token_lifetime` (`Min`) and `access_token_signing_alg_values_supported` (`Intersect`) now apply to
  resource servers, and two settings are new: `access_token_sender_constraint_required` (`Or`, clients and resource
  servers) and `access_token_confirmation_methods_supported` (`Intersect`, resource servers — the IANA `cnf` methods
  `jkt` and `x5t#S256` a resource server verifies, Auth0's per-API `mechanism` generalized to a list). A requirement
  without methods was rejected: a token bound by a method a resource server cannot verify would pass as compliant. The
  client's requirement is enforced at the token endpoint before the grant is spent; the combined requirement and methods
  at issuance. FAPI 2.0's sender-constraint rule became the ceiling `access_token_sender_constraint_required = true`, and
  `SenderConstrainedAccessTokenRequirement` was removed. A composed-server test pins every setting that applies to
  resource servers to a lattice merge. No data migration was needed._

- **`F63` — Honor the request-object and UserInfo algorithm settings, or stop advertising them.** Discovery advertises
  `request_object_signing_alg_values_supported`, `request_object_encryption_{alg,enc}_values_supported`,
  `userinfo_signing_alg_values_supported`, and `userinfo_encryption_{alg,enc}_values_supported`, but no runtime path
  reads them, so they apply to tenants only ([ADR-0149](docs/adr/0149-settings-declare-the-scopes-they-apply-to.md)) and
  their non-standard `_zip_` companions are read by nothing. A request object's `alg` is checked only against `none`, so
  the FAPI 2.0 profile's ceiling on request-object signing algorithms does not bind; and the UserInfo endpoint returns
  only plain JSON, so the UserInfo metadata advertises a capability the server lacks. Enforce the request-object
  algorithms against the requesting client's effective settings (widening their scopes to clients), and either
  implement signed and encrypted UserInfo responses (`userinfo_signed_response_alg` / `userinfo_encrypted_response_*`
  client metadata) or stop advertising and retire the UserInfo algorithm settings with a data migration. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultLoadAuthorizationRequestHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/`,
  `src/NCode.Identity.OpenId.Core/Settings/RootSettingsCollectionDataSource.cs`,
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0154](docs/adr/0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) and
  [ADR-0155](docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md). Signed and encrypted
  UserInfo responses are implemented rather than retired: the six OpenID Connect registration settings
  (`id_token_signed_response_alg`, `id_token_encrypted_response_{alg,enc}`, `userinfo_signed_response_alg`,
  `userinfo_encrypted_response_{alg,enc}`) are client settings with the registration defaults, accepted by dynamic
  client registration, and one `IClientJwtCredentialSelector` protects ID tokens, logout tokens, and UserInfo responses
  as the client registered — signed with the tenant's key, encrypted to the client's published key, never compressed.
  Request objects are validated under the client's effective request-object lists (now applying to tenants and
  clients), CIBA requests and client assertions under theirs, with an unset list permitting nothing at these trust
  boundaries. The work surfaced and fixed a critical gap: the JWT validator accepted an unsigned (`alg:none`) token
  against any key, so a forged access token authenticated at UserInfo and the management API. The validator now
  accepts only signed compact JWTs (and Nested JWTs), under caller-stated algorithm allow-lists and explicit decryption
  keys. It also fixed the ID token being encrypted to the tenant's key instead of the client's, UserInfo accepting the
  session cookie in place of an access token, and request objects with numeric claims failing to deserialize. The
  `_zip_` lists and the non-standard CIBA request-encryption settings are retired, encryption to a party is asymmetric
  only, and `RSA1_5` is offered by no setting; the `RetireUnsafeJoseSettingValues` migration was verified against
  Postgres. Follow-ups recorded: `F40` (request-object audience and typing), `F65`–`F68`._

- **`F64` — Release the `cloudflare-zone` module that turns Universal SSL off, and apply it to the zone.** The
  [hosting analysis](docs/hosting-analysis.md#domain-and-dns--nidentityio) intends that no CA but Let's Encrypt may
  issue for `nidentity.io`, but Cloudflare Universal SSL, on by default for every zone, publishes hidden CAA records for
  its own CAs (`pki.goog`, `digicert.com`, `ssl.com`, `comodoca.com`) that never appear in OpenTofu state, so the domain
  authorizes five CAs (observed 2026-10-19). The `cloudflare-zone` module now always manages
  `cloudflare_universal_ssl_setting` with `enabled = false`, its test asserts it, and its cloudflare provider floor is
  `>= 5.13`, the first release with that resource (`NCodeGroup/identity-platform` branch
  `feat/cloudflare-zone-universal-ssl-off`); the bootstrap token carries *Zone → SSL and Certificates → Edit* (runbook
  section [3. Cloudflare](docs/platform-bootstrap.md#3-cloudflare-dns-and-inbound-email)). Remaining: merge that
  branch and release `v0.3.0`; consume it in `live/bootstrap` (`cloudflare.tf` zone `ref`, the README's module table,
  and *Universal SSL off* in its *Creates* table); apply `live/bootstrap` with a bootstrap token that carries the new
  permission. Verify: `Resolve-DnsName nidentity.io -Type CAA -Server 1.1.1.1` lists only `letsencrypt.org` and the
  `iodef` record.
  _Closed: `NCodeGroup/identity-platform` #6 (released as `v0.3.0`) and #7 (`live/bootstrap` consumes it), applied
  2026-10-19 with `tofu plan '-target=module.zone'`; an unexpired bootstrap token gained the new permission by editing
  it. `nidentity.io` now publishes only `issue` / `issuewild` `letsencrypt.org` and the `iodef` record, DNSSEC-validated
  at Cloudflare's and Google's resolvers. The verify command above does not work on Windows, whose `Resolve-DnsName` has
  no CAA type; check over DNS-over-HTTPS instead:
  `(Invoke-RestMethod 'https://cloudflare-dns.com/dns-query?name=nidentity.io&type=CAA' -Headers @{ accept = 'application/dns-json' }).Answer.data`.
  The provider cannot destroy `cloudflare_universal_ssl_setting`: removing the resource leaves Universal SSL off, so
  turning it back on takes `enabled = true`, not a deletion._

- **`F65` — Validate client key material at every entry point.** A client's `jwks` and the keys its `jwks_uri`
  publishes are the keys the server verifies its signatures with and encrypts its ID tokens and UserInfo responses to
  ([ADR-0155](docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)), but only dynamic client
  registration checks them, and only that `jwks` and `jwks_uri` are not both present. RFC 7591 §2 requires a registered
  key set to hold public keys: reject a JWK that carries private or symmetric key material (`d`, `p`, `q`, `k`, …) at
  registration, in the management API, in the seeders, and when a `jwks_uri` document is fetched; require a `kid` that is
  unique within the set; reject a key whose `use` / `alg` / `key_ops` are inconsistent; and decide whether
  `IClientKeyResolver` merges a stored `jwks` with a `jwks_uri` or uses only one (OpenID Connect Dynamic Client
  Registration 1.0 §2 forbids registering both). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Clients/DefaultClientKeyResolver.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs`, the
  management client endpoints, and the development seeders.
  _Closed: [ADR-0161](docs/adr/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md) and the
  [`CHANGELOG.md`](CHANGELOG.md) `[Unreleased]` entries "A client's public keys have one source and are imported under
  one policy", "A client's key set could carry private or symmetric key material…", "A client's stored public keys
  stayed in force…", and "Two clients could not register a key with the same `kid`…". A client's keys come from one
  source (stored keys or its `jwks_uri`, never merged); the `jwks` setting is retired in favor of the stored keys, which
  every registration response projects back; one public `IClientKeyImporter` applies the key policy at registration,
  the `jwks_uri` fetch, and the management import, and the management update checks a key's metadata against it; a
  key's `kid` is stored apart from its secret identifier. The seeders needed no change — they register no client keys,
  and settings validation rejects the retired setting. The `AddSecretKeyId` and `RetireClientJwksSetting` migrations
  were verified up and down against Postgres 18.3. Follow-ups recorded: `F72` (`x5c` certificates for
  `self_signed_tls_client_auth`) and `F73` (the key policy and `kid` for the other secret owners)._

- **`F66` — Accept the remaining algorithm and CIBA metadata at dynamic client registration.** Registration accepts the
  ID token, UserInfo, and request-object algorithm metadata
  ([ADR-0155](docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)) but not
  `token_endpoint_auth_signing_alg` (OpenID Connect Dynamic Client Registration 1.0 §2) or the CIBA registration
  metadata (`backchannel_token_delivery_mode`, `backchannel_client_notification_endpoint`,
  `backchannel_authentication_request_signing_alg`, `backchannel_user_code_parameter`, CIBA Core §4), so a dynamically
  registered client cannot narrow its assertion algorithm or use CIBA at all and must be provisioned through the
  management API. Map each algorithm to a single-element client list as `request_object_signing_alg` is, validate it
  against the tenant's ceiling, and report it in the client information response. Pairs with the `F16` follow-up that
  accepts the non-secret `token_endpoint_auth_method` values. Touch points:
  `src/NCode.Identity.OpenId.Authentication.Abstractions/Endpoints/Registration/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0155](docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md) (decision and consequences
  amended). Each algorithm narrows the client's own list under the tenant's ceiling; the notification endpoint is stored
  as `client_notification_endpoint` and is required for `ping` and `push`; the user-code flag is stored at the client's
  scope of `backchannel_user_code_parameter_supported`, which the tenant's value bounds. The non-secret
  `token_endpoint_auth_method` values remain the `F16` follow-up._

- **`F67` — Validate the access tokens the server encrypted itself.** When an access token's settings require encryption,
  it is a Nested JWT encrypted to the key of the single targeted resource server that decrypts its own tokens or,
  otherwise, to the tenant's
  ([ADR-0158](docs/adr/0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md)).
  The server's own consumers of access tokens — UserInfo, introspection, token exchange, and the management API —
  name no decryption keys and no algorithm lists, so they reject every encrypted access token
  ([ADR-0154](docs/adr/0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)): an
  encrypted token for the OpenID system resource server cannot call UserInfo, and a resource server cannot introspect
  a token encrypted to it. Name the tenant's keys (and, for introspection, the calling resource server's keys) as
  decryption keys, and state the access-token signing and encryption lists as allow-lists. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Tokens/Handlers/DefaultAuthenticateAccessTokenHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Introspection/Handlers/DefaultIntrospectTokenHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/TokenExchange/DefaultSubjectTokenValidator.cs`.
  _Closed: [ADR-0167](docs/adr/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md) and
  [ADR-0168](docs/adr/0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md); see the
  [`CHANGELOG.md`](CHANGELOG.md) `[Unreleased]` entries "A resource server can introspect through a client that acts for
  it…", "Introspection reports a token only to the party that consumes it", "The server validates every token it issued
  through one seam…", "Any authenticated client could introspect any token", "The server's readers of its own tokens
  checked neither the token type nor an algorithm allow-list", and "The server rejected the access tokens it
  encrypted". Every reader of a token the tenant issued — UserInfo and the management API through resource-request
  authentication, introspection, token exchange, and the end-session `id_token_hint` — validates through one public seam,
  `IIssuedTokenValidator`. The seam names the tenant's keys and access-token algorithm lists, and checks `typ`, the
  issuer, the lifetime, and session revocation. Token exchange picks the policy by `subject_token_type`. Investigation
  widened the item: ADR-0029 let any client introspect any token, which RFC 7662 §4 and RFC 9701 §3/§5 forbid, and
  decrypting at introspection would have let a token's own client read it. Folded in:
  - a resource server introspects through a client bound to it, accepted only at introspection through fail-closed,
    opt-in endpoint metadata;
  - an access token is disclosed only to a resource server in its audience and narrowed to it, and a refresh token
    only to its own client;
  - RFC 9701 JWT introspection responses, with their discovery metadata and a FAPI 2.0 ceiling;
  - the introspection and revocation endpoints' client-authentication discovery metadata;
  - the management API binding, filter, and console **Clients** tab;
  - system resource servers hold no keys.

  The `AddClientResourceServer` migration adds the binding's foreign key. Follow-ups recorded: `F76` (an
  `id_token_hint` re-encrypted to the server) and `F77` (registering a resource-server client through dynamic client
  registration)._

- **`F68` — Check a client's registered response algorithms when the settings above it are written.** A client's
  registered response algorithms must be in its effective `*_values_supported` lists, but only dynamic client
  registration checked them; a management write, a tenant or server settings write that narrowed a list, or a client
  registering no algorithm under a profile that excludes the default all surfaced only when issuance failed with
  `server_error`. A security profile now supplies a compliant default
  ([ADR-0148](docs/adr/0148-security-profiles-accumulate-down-the-settings-cascade.md) decision 7). The management
  API's client create and settings update reject a registered algorithm their effective lists exclude
  (`ClientResponseAlgorithmRule`), and a tenant or server settings update checks every client it governs through
  `IClientResponseAlgorithmValidator`, reporting only the mismatches the change introduces; a server update merges
  onto the new `OpenIdServer.ConfiguredSettingsProvider`. Seeded clients register no response algorithm, so they
  inherit a mismatch only from the settings these writes check.
  _Closed: [ADR-0155](docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md) decision 2; see the
  [`CHANGELOG.md`](CHANGELOG.md) `[Unreleased]` entries "A security profile supplies compliant defaults" and "The
  management API rejects a client algorithm its effective settings do not permit"._
- **`F69` — Expose the client JWT credential selector beside `ITokenService`.** A replacement `ITokenService` must
  protect ID tokens exactly as the client registered
  ([ADR-0155](docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)), but
  `IClientJwtCredentialSelector`, `ClientJwtCredentials`, and `ClientJwtProtectionSettings` are internal to
  `NCode.Identity.OpenId.Authentication`, so a replacement can only reimplement that policy or skip it — the gap
  `IAccessTokenPolicyService` closes for access tokens
  ([ADR-0158](docs/adr/0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md)). Move the
  interface and its models to `NCode.Identity.OpenId.Authentication.Abstractions` as a public seam with the internal
  default, so every issuer of a client-addressed JWT (ID token, logout token, UserInfo response, JWT introspection
  response) resolves the same selector. Touch points: `src/NCode.Identity.OpenId.Authentication/Clients/IClientJwtCredentialSelector.cs`,
  `ClientJwtCredentials.cs`, `ClientJwtProtectionSettings.cs` (which now carries an `Introspection` entry,
  [ADR-0168](docs/adr/0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)), and the
  Authentication.Abstractions public API.
  _Closed: the three types are public in `NCode.Identity.OpenId.Authentication.Abstractions` and the default stays
  internal and replaceable ([ADR-0181](docs/adr/0181-every-issuer-of-a-jwt-addressed-to-a-client-resolves-one-public-credential-selector.md); see the
  [`CHANGELOG.md`](CHANGELOG.md))._

- **`F70` — Never reissue a retired tenant name or issuer.** A tenant is named by its creator
  ([ADR-0159](docs/adr/0159-a-tenant-is-named-by-its-creator.md)), and its issuer is its identity in every token it
  mints ([ADR-0129](docs/adr/0129-the-tenant-issuer-is-the-single-authoritative-origin.md)). Deleting a tenant frees
  both, so a new tenant can take the name `acme` — and with it an issuer that relying parties configured to trust the old
  tenant: they fetch the new tenant's JWKS and accept its tokens. Auth0 never reuses a deleted tenant's name. Keep a
  tombstone of every retired tenant identifier and normalized issuer, reject both when a tenant is created or its issuer
  changes (`409`), and decide whether an operator can release a retired issuer explicitly, with an audit record. A
  retired issuer is rejected for a derived issuer too: the template default is judged by the same
  `ITenantResolver.ValidateTenantAsync`, and the address preview reports it.
  Touch points: `ITenantStore` and `TenantStore` (a tombstone entity and migration),
  `src/NCode.Identity.OpenId.Core/Tenants/DefaultTenantResolver.cs` (`ValidateTenantAsync`),
  `src/NCode.Identity.OpenId.Management/Endpoints/Tenants/DefaultTenantValidator.cs`, `TenantApiEndpointHandler`
  (`DeleteTenantAsync`, `GetAddressAsync`).
  _Closed: [CHANGELOG.md](CHANGELOG.md) [Unreleased]; [ADR-0189](docs/adr/0189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md).
  The tenant store retires a deleted tenant's identifier and issuer, and a changed issuer, in the `RetiredTenants` and
  `RetiredIssuers` tables; the resolver reports either as a conflict (`409`), except an issuer reclaimed by the tenant
  that gave it up. No operator API releases a retired issuer: Auth0 never reuses a name either._

- **`F71` — Derive a tenant's issuer from a configured template.** A tenant served at a request-independent address
  needs an issuer, and a production deployment must not rely on the request-derived fallback
  ([ADR-0129](docs/adr/0129-the-tenant-issuer-is-the-single-authoritative-origin.md); the production-safety part of
  `F46`). Auth0 derives a tenant's canonical domain from its name, and tenant names are readable host labels
  ([ADR-0159](docs/adr/0159-a-tenant-is-named-by-its-creator.md)), so a strategy can do the same: a configured issuer
  template (for example `https://{tenantId}.auth.nidentity.io` for `DynamicByHost`, `https://op.example/{tenantId}` for
  `DynamicByPath`) fills in, and persists, the issuer of a tenant created without one, judged by the same rules as an
  explicit issuer ([ADR-0160](docs/adr/0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md)). A custom
  domain stays an explicit issuer. Touch points: `DynamicByHostTenantOptions`, `DynamicByPathTenantOptions` and their
  options validators, `ITenantStrategy`, `TenantApiEndpointHandler.CreateTenantAsync`, `DefaultTenantProvisioner`.
  _Closed: [ADR-0165](docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md) and
  [ADR-0166](docs/adr/0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md); see the
  [`CHANGELOG.md`](CHANGELOG.md) entries "A tenant can be created without an issuer…", "Every tenant a strategy serves
  has an issuer…", and "The root tenant is provisioned under every tenant strategy…". The host and path strategies take
  an `IssuerTemplate`, the root tenant a `Root:Issuer`, and `ITenantStrategy.GetDefaultIssuerAsync` (through
  `ITenantResolver`) supplies the default that management create and update persist and judge. The request-derived
  issuer fallback is removed rather than gated: `StaticSingle:Issuer` is required, the dynamic strategies require a root
  address, and a served tenant without an issuer fails closed. Folded in: the root tenant is provisioned on demand under
  every strategy, a management-created tenant is seeded before its create returns, every tenant is re-seeded when next
  materialized, and the configuration seeders (`BootstrapAdmin`, `DeveloperInteractiveSeed`, `ConformanceSeed`) target
  exactly one named tenant; `GET /api/tenants/{tenantId}/address` and the console's tenant form preview the address.
  The work also fixed management tenant creation, which wrote the creator's ownership before the tenant was committed.
  Follow-ups recorded: `F74` (the frontend bootstrap document's request-derived issuer) and `F75` (the management API's
  authenticating tenant under the dynamic strategies)._

- **`F72` — Match `self_signed_tls_client_auth` against the certificates in a client's JSON Web Key Set.** RFC 8705
  §2.2 lets a client register its self-signed certificates through `jwks` or `jwks_uri`, as the `x5c` of its keys. The
  handler matches the presented certificate against the client's keys resolved through `IClientKeyResolver`
  ([ADR-0161](docs/adr/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)), but a key
  imported from a JSON Web Key is a bare public key — its `x5c` chain is dropped — so a client that publishes its keys at
  a `jwks_uri`, or registers them as a `jwks`, cannot authenticate with `self_signed_tls_client_auth`; only a
  certificate imported through the management API matches. Import the leaf certificate of a key's `x5c` in the
  key-type descriptors' JSON Web Key import, after checking that it carries the key's public key, so a registered or
  published certificate is matched by thumbprint. Touch points:
  `src/NCode.Identity.Secrets/KeyTypes/AsymmetricSecretKeyTypeDescriptor.cs` (JSON Web Key import),
  `src/NCode.Identity.OpenId.Authentication/Clients/DefaultClientKeyImporter.cs`,
  `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/SelfSignedTlsClientAuthClientAuthenticationHandler.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0161](docs/adr/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md) (consequences
  amended). The JSON Web Key import keeps the leaf `x5c` certificate once its public key equals the key's, and refuses
  an empty, malformed, or mismatched `x5c`; the self-signed handler matches it by thumbprint unchanged._

- **`F73` — Apply the key policy to every secret owner in the management API, and report a key's `kid`.** A client's
  keys are imported and updated under one policy
  ([ADR-0161](docs/adr/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)), but the
  tenant, server, and resource-server secret endpoints keep their earlier checks: an imported key's `use` and `alg` are
  not checked against each other or the key, an update can give a key an `alg` its type does not accept, and a JSON Web
  Key's `kid` is discarded in favor of the secret identifier. Check those imports and updates with the same
  key-to-algorithm rule (`IAlgorithmCollection.ValidateKeyAlgorithm`), keep a JSON Web Key's `kid` (unique among the
  owner's keys), and report every key's `kid` (its `KeyId`, or its secret identifier) in the management
  `SecretResource` and the admin console's secret tables, which show only the secret identifier. Touch points:
  `src/NCode.Identity.OpenId.Management/Endpoints/{Tenants,Servers,ResourceServers}/*ApiEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Management.Abstractions/Contracts/Secrets/SecretResource.cs`, and the `@ncode/api`
  contracts and secret tables in `src/frontend`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0186](docs/adr/0186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md) (amends
  [ADR-0161](docs/adr/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)). Every owner's
  import goes through `IClientKeyImporter.TryImportKey` with a per-owner `kid` uniqueness check (`409`), every owner's
  update goes through `ISecretValidator.ValidateUpdate` and keeps the key's `KeyId`, and `SecretResource` reports a
  required `KeyId` that the console's secret table shows._

- **`F74` — Derive the frontend bootstrap document's issuer and API base from the tenant, not the request.** Every
  served tenant's issuer is now its stored, validated origin and nothing else derives one from the request
  ([ADR-0165](docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)), but
  `/config/bootstrap.json` still defaults `issuer` and `apiBaseUrl` to the serving request's `scheme://host`
  (`FrontendBootstrapOptions`, [ADR-0064](docs/adr/0064-frontend-presentation-platform.md)). That
  is a second, unvalidated source of the same value: under a forged or proxied `Host` (or a shared cache in front of
  the host) the bundle discovers the wrong authority, and under `DynamicByHost` the console served at
  `manage.<apex>` needs the root tenant's issuer (`https://auth.<apex>`), which the request origin is not. Default the
  issuer to the issuer of the tenant that serves the surface (the root tenant's for the console), and the API base to
  the configured management origin; keep explicit options as overrides. Touch points:
  `src/NCode.Identity.Server/Frontend/FrontendBootstrapEndpointProvider.cs`, `FrontendBootstrapOptions.cs`,
  `FrontendBootstrapDocument.cs`, `ITenantResolver.GetDefaultIssuerAsync`.

  _Closed: the issuer is the serving tenant's stored issuer — the tenant the request names (`StaticSingle`) or the
  root tenant — then its strategy's default, and the endpoint answers `500` rather than fall back to the request. The
  API base deliberately stays the serving origin: the management API is mapped at `/api` on every admitted host, so
  same-origin needs no CORS and no second setting; the response is `Cache-Control: no-store`. See
  [ADR-0182](docs/adr/0182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md) and the
  [`CHANGELOG.md`](CHANGELOG.md) entry "The frontend bootstrap document's issuer is the serving tenant's…"._

- **`F76` — Accept an `id_token_hint` the client re-encrypted to the server.** OpenID Connect RP-Initiated Logout 1.0
  §2 lets a relying party that received an encrypted ID token re-encrypt it to the OP's public key before passing it as
  `id_token_hint`. The server validates a hint under the ID-token policy, which names no decryption keys because an ID
  token is encrypted only to its client
  ([ADR-0167](docs/adr/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md)), so a re-encrypted hint is
  rejected and logout proceeds without one. Accept a hint encrypted to the tenant's encryption keys under the tenant's
  advertised `id_token_encryption_*` lists, keep the enclosed ID token's checks unchanged, and decide whether the OP
  advertises the keys a relying party re-encrypts to. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Tokens/DefaultIssuedTokenValidator.cs` (`ValidateIdTokenHintAsync`),
  `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/Handlers/DefaultResolveEndSessionSubjectHandler.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0167](docs/adr/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md) (decision and options
  amended). `ValidateIdTokenHintAsync` names the tenant's keys as decryption keys under the tenant's
  `id_token_encryption_{alg,enc}_values_supported`, with no compression; `ValidateIdTokenAsync` still rejects an
  encrypted ID token. The tenant's JSON Web Key Set already publishes the public keys a relying party re-encrypts to,
  as it does for request objects, so nothing new is advertised._

- **`F75` — Authenticate the management API against the control plane, not the tenant the request resolves.** The
  management `/api` surface materializes its OpenID context through `CreateForRequestAsync`, so the tenant that
  validates the caller's access token is whichever tenant the protocol strategy resolves from the request. That holds
  under `StaticSingle`, but under `DynamicByPath` a collection route such as `POST /api/tenants` has no `tenantId`
  route value and fails, and `/api/tenants/{tenantId}/…` authenticates the caller against the target tenant rather
  than the tenant that issued the token; under `DynamicByHost` the management host `manage.<apex>`
  ([ADR-0131](docs/adr/0131-hosted-platform-environment-model-and-hostname-scheme.md)) is no tenant's host, so every
  call is `404`. The
  management plane should authenticate against a configured authenticating tenant — the root (control-plane) tenant
  by default ([ADR-0024](docs/adr/0024-control-plane-and-per-tenant-planes.md),
  [ADR-0042](docs/adr/0042-management-api-route-driven-tenant-scope.md)) — independent of the protocol strategy, and
  the route's `tenantId` should only scope the data. Capture the design in an ADR and cover every strategy with
  integration tests. The access token itself is validated through `IIssuedTokenValidator` against the `OpenIdContext`
  it is given ([ADR-0167](docs/adr/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md)), so passing the
  authenticating tenant's context is enough; the seam needs no change. Touch points:
  `src/NCode.Identity.OpenId.Management/Endpoints/ManagementEnvironmentEndpointFilter.cs`, `IOpenIdContextFactory`,
  `TenantScopeEndpointFilter`, `OwnershipHandler`,
  `src/NCode.Identity.OpenId.Authentication/Tokens/Handlers/DefaultAuthenticateAccessTokenHandler.cs`.

  _Closed: [ADR-0191](docs/adr/0191-the-management-api-authenticates-against-the-control-plane-tenant.md);
  [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. `ITenantStrategy.ResolveControlPlaneTenantAsync` names the
  control-plane tenant (the root tenant, created on demand; a static strategy's own tenant),
  `IOpenIdTenantFactory.CreateControlPlaneTenantAsync` materializes it through the shared cache and seed, and
  `ManagementEnvironmentEndpointFilter` publishes it through `IOpenIdContextFactory.CreateForControlPlaneAsync`.
  `ManagementControlPlaneTests` drives `DynamicByHost` (the `manage.` host and a workload tenant's host) and
  `DynamicByPath` (a collection route and a tenant route); every other management integration test drives
  `StaticSingle`._

- **`F77` — Register a resource-server client through dynamic client registration.** A resource server introspects
  through a client bound to it, and the binding is set only through the management API, by a caller allowed to update
  that resource server ([ADR-0168](docs/adr/0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)).
  RFC 9701 §3 allows the authorization server to register resource servers dynamically, which a deployment that
  provisions APIs automatically would need. Define how a registration request names its resource server and how the
  registrant proves it may act for it — an initial access token scoped to that resource server is the likely shape —
  register the `introspection_*_response_alg` metadata, and report the binding in the client information response.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication.Abstractions/Endpoints/Registration/`.

  _Closed: [ADR-0192](docs/adr/0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md);
  [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. `CreateInitialAccessTokenRequest.resourceServerId` scopes a token
  under the management API's binding checks, `IInitialAccessTokenValidator` returns the token's grant, and a client
  registered with a scoped token is bound to its resource server, must be confidential, and registers the
  `introspection_*_response_*` metadata (refused for an ordinary client). The client information response reports
  `resource_server_id`. `TokenIntrospectionEndpointTests` registers a bound client and introspects with it, plain and
  encrypted; the admin console's mint dialog follows as `U24`._

- **`F50` — Serve the interactive login and consent surface under the tenant route.** The protocol endpoints are served
  under the tenant route ([ADR-0138](docs/adr/0138-protocol-endpoints-are-served-under-the-tenant-route.md)), but the
  interactive surface is still mounted at absolute routes (`/account/*`) with a fixed cookie `LoginPath`, a single
  cookie, and its bundle's static assets at the root. A static or host-selected tenant does not notice, but under
  `DynamicByPath` the surface's environment filter has no `{tenantId}` to resolve, so interactive flows fail — and
  path tenants sharing an origin would share one session cookie, which must not happen. Mount the surface's group under
  the tenant route, make the login and logout redirects tenant-relative, scope the cookie (name or path) per tenant,
  serve the bundle beneath the tenant route, and have the `apps/auth` bundle derive its API base from its own location.
  Capture the design in an ADR.

  _Closed: [ADR-0193](docs/adr/0193-the-interactive-surface-is-served-under-the-tenant-route.md);
  [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. The interactive endpoint group takes the tenant route as its prefix;
  `InteractiveSurfacePaths` makes every redirect, the consent challenge, and the cookie login challenge
  tenant-relative; the server-rendered forms post page-relative; and `InteractiveSessionCookieManager` scopes the
  session cookie to the tenant's path. The group serves the bundle's files from `GET|HEAD /account/{**asset}`
  (`UseInteractiveAuthentication` is removed), every page carries `<base href="{tenant path}/account/">`, the bundle
  builds with a relative base, and `apps/auth` resolves its paths against `document.baseURI`.
  `InteractiveTenantRouteTests` proves the routing, redirects, cookie path, logout, asset serving, and document base
  under `DynamicByPath`._

- **`F78` — Derive a tenant's default mTLS endpoint alias from a template, and preview it.** `F47` made the alias a
  routed tenant property ([ADR-0195](docs/adr/0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md)), but
  it is only ever set explicitly. Finish the shape the issuer already has
  ([ADR-0165](docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)):
  - **Each strategy supplies the default, persisted on write.** `TenantStrategy` gains a default-alias method beside
    `GetDefaultIssuerAsync`. `DynamicByHostTenantOptions` gains `MtlsEndpointAliasTemplate`, for example
    `https://{tenantId}.mtls.example`. `DynamicByHostTenantOptionsValidator` checks it like `IssuerTemplate`, and it
    must also be an origin whose host `RegexPattern` maps to itself. Provisioning and the management create path store
    the expansion when no alias is given, so changing the template never moves an existing tenant. The other strategies
    have no template.
  - **Preview it.** `TenantAddressResource` carries the alias authority and an `IsMtlsEndpointAliasAuthorityDerived`
    flag beside `IsIssuerDerived`, and the address preview endpoint takes the proposed alias.
  - **Tests.** The template default in `DynamicByHostTenantStrategyTests`, the options validator, and the preview.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0195](docs/adr/0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md) decision 5. Delivered:
  `DynamicByHost:MtlsEndpointAliasTemplate` and its startup rules, `Root:MtlsEndpointAliasAuthority` and
  `StaticSingle:MtlsEndpointAliasAuthority` (mirroring the configured issuers),
  `ITenantStrategy`/`ITenantResolver.GetDefaultMtlsEndpointAliasAuthorityAsync`, the derived alias stored by management
  create and update and by the provisioner, the preview's `mtlsEndpointAliasAuthority` parameter and the
  `TenantAddressResource` alias and derived flag, and unit and integration tests. Console editing is `U25` (done)._

## Frontend presentation platform (group `U`)

- **`U1` — Management auth bootstrap: silent renewal + end-to-end sign-in.** _Done._
  `@ncode/auth` gained `createTokenRenewer` (an expiry-driven renewal loop that re-arms from each new token and falls
  back to interactive sign-in on failure), driven by the console's `AuthGate` while signed in. The interactive login
  surface (`T2`, [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)) and the seeded public
  SPA client + administrator user (`AddDeveloperInteractiveSeed`) let the auth-code-with-PKCE flow complete end to end
  against the host. **Verified live:** authorize → interactive login → code → token issues a
  `urn:ncode:management` access token whose `GlobalAdmin` role (projected by `DeveloperInteractiveSubjectClaimsHandler`,
  ADR-0055) authorizes management reads/writes (`GET /api/servers`, `/api/tenants`, `/api/tenants/{id}/clients` all
  return `200`). Three latent bugs surfaced and were fixed to get there: the auth-code grant was persisted in the wrong
  shape (subject dropped), access-token validation failures were silently swallowed, and multi-audience `aud` validation
  wrongly required every audience to be expected (see CHANGELOG `Fixed`). Touch points: `src/frontend/packages/auth`,
  `packages/store` (`authSlice`), `apps/mgmt` (`AuthGate`, `auth.ts`),
  `src/NCode.Identity.Server/DevelopmentEnvironment` (`DeveloperInteractiveSeedHandler`).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)._
- **`U2` — Management console secrets/settings for the remaining owners + initial-access-token mint.** _Backend
  complete; frontend remaining for resource servers._ The console extends the client **Secrets**/**Settings** pattern
  (one-time reveal per [ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md); read-only
  effective-settings view + top-level add/remove/replace JSON Patch diff via the `@ncode/api` `diffPatch` helper) to
  **tenants** (a tenant-scope **Tenant** tab) and the **server** (a global-scope **Servers** tab), and surfaces the
  initial-access-token mint (RFC 7591) in the **Grants** tab. The shared `SecretsManager`/`SettingsManager` containers
  back every owner. **Resource-server (API) settings/secrets are now supported by the backend**
  ([ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md)): `ResourceServerApiEndpointHandler` exposes
  `GET`/`PATCH .../settings`, `GET .../effective-settings`, and the full `.../secrets` CRUD. **Remaining:** wire the
  resource-server **Settings**/**Secrets** surface in the console (drill-in from the resource-servers list, same
  pattern as clients) — add the `@ncode/api` contracts + `managementApi` endpoints + a `ResourceServerDetailPage`. A
  client-secret rotate convenience is tracked separately under `S1`. Touch points: `src/frontend/packages/api`,
  `packages/store` (`managementApi`), `packages/ui`, `apps/mgmt/src` (`containers`).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md)._
- **`U3` — Build out the Playground and conformance surfaces.** _Done._ The Auth/Consent/Login surface is a full app on
  the shared platform (the `apps/auth` consent screen shows the real client and requested scopes and records the
  decision — [ADR-0076](docs/adr/0076-consent-persistence-and-enforcement.md)), and the Playground (`apps/playground`)
  and conformance (`apps/conformance`) surfaces are now **real apps** on the same `@ncode/*` platform
  ([ADR-0064](docs/adr/0064-frontend-presentation-platform.md), [ADR-0082](docs/adr/0082-playground-and-conformance-surfaces.md)).
  The Playground is an interactive OAuth 2.1 / OIDC **authorization-code + PKCE (S256) flow explorer** (surfacing the
  PKCE challenge, `state`, `nonce`, authorize URL, token response, decoded claims, UserInfo, refresh, RP-Initiated
  Logout; implicit/hybrid/password flows are intentionally unavailable). The conformance app is a **security-posture &
  conformance-readiness dashboard** that audits the OP's published discovery metadata against modern guidance (OAuth
  2.1, RFC 9700, OIDC Core/Discovery, RFC 7636/8414/7009/7662/9126) and reports Basic/Config OP readiness plus the
  external-suite config hints — it does **not** duplicate the `apps/auth` login/consent surface the suite drives (`T2`).
  Both are standalone Vite SPAs served via the Aspire AppHost (`playground` → `5174`, `conformance` → `5175`), each
  with a surface-aware bootstrap (`?surface=`), and the Playground has its own seeded public `playground-spa` client
  and dev CORS origin. Touch points: `src/frontend/apps/playground/`, `src/frontend/apps/conformance/`,
  `src/NCode.Identity.Server/Frontend/`, `src/NCode.Identity.Server/DevelopmentEnvironment/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0082](docs/adr/0082-playground-and-conformance-surfaces.md)._
- **`U4` — Runtime bootstrap endpoint on the OP host.** The tier-agnostic bundle fetches `/config/bootstrap.json` from
  the serving host; only a dev copy exists (`apps/mgmt/public/config/bootstrap.json`). Add a host endpoint that emits
  the bootstrap document (API base, issuer, feature flags, branding) per deployment so one bundle serves every tier.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`U5` — Frontend CI job.** The frontend toolchain is intentionally outside `dod.ps1`. Add a separate CI job
  (`corepack` + `pnpm install` + `turbo run lint typecheck test build`) so the platform is gated independently.
  _Closed: [`.github/workflows/frontend.yml`](.github/workflows/frontend.yml) — a path-filtered workflow running
  Corepack-pinned pnpm on Node 22, then `format:check`, `lint`, `typecheck`, `test`, and `build` as separate steps so a
  failure names its stage. Landing it also uncovered and fixed a `.prettierignore` gap: Prettier walked the gitignored
  `.turbo/cache` build output, so `format:check` reported 122 false "code style issues" and could never pass._
- **`U7` — Runtime consumption + auditing of resource-server secrets/settings.** [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md)
  delivers resource-server settings/secrets **management** only. Two follow-ups remain: (1) **runtime consumption** —
  resource servers have no runtime domain object and are not resolved at token-issuance time, so the stored per-audience
  secrets (HS256 signing, JWE decryption) are not yet used to actually sign/encrypt access tokens; and (2) **auditing** —
  `ResourceServerApiEndpointHandler` emits no `IManagementAuditRecorder` events for **any** operation (create, update,
  delete, scopes, settings, secrets), unlike the client/tenant/server handlers. Retrofit audit uniformly across all
  resource-server operations rather than only the new leaves. Touch points:
  `src/NCode.Identity.OpenId.Management/Endpoints/ResourceServers/ResourceServerApiEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Management.Abstractions/Auditing`, and the token-issuance pipeline for consumption.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md),
  [ADR-0075](docs/adr/0075-runtime-consumption-of-resource-server-secrets.md)._
- **`U8` — Strip internal `$`-prefixed ticket parameters from the authorization response.** The interactive flow
  surfaced that an internal ticket parameter (`$created_when`, `OpenIdConstants.Parameters.CreatedWhen`) leaks into the
  authorization response callback query string sent back to the client. `$`-prefixed parameters are internal and should
  never appear on the wire; the authorization-response/ticket rendering should omit them. Low priority (cosmetic — the
  client ignores unknown params), but it is protocol hygiene. Touch point:
  `src/NCode.Identity.OpenId.Core/Endpoints/Authorization/Results/AuthorizationResultExecutor.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`U9` — Server create/edit management (UI + backend).** The Servers surface is read-only today: `ServersPage`
  lists servers and drills into `ServerDetailPage` for a server's **settings and secrets** (which already exist),
  but there is **no way to add a new server or edit a server's top-level record** (e.g. display name). Unlike
  tenants and resource-servers — which have full create/update/delete contracts, API endpoints, and frontend
  dialogs — servers have only `listServers` plus server-secrets/settings endpoints; there is no
  `createServer`/`updateServer`/`deleteServer`. The current design treats the server inventory as provisioned
  out of band (documented in `ServersPage.tsx`/`ServerTable.tsx`), so closing this gap is a cross-stack feature:
  new management contracts, API endpoints (with `IManagementAuditRecorder` audit parity), persistence, and
  `PublicAPI` changes on the backend, plus store mutations, a `ServerFormDialog`, and row-level actions on the
  frontend. Decide first whether a server is intended to be a user-creatable, multi-instance entity or a
  provisioned singleton; that product/security question gates the scope. Touch points:
  `src/NCode.Identity.OpenId.Management.Abstractions`, `src/NCode.Identity.OpenId.Management/Endpoints`,
  `src/frontend/packages/store/src/managementApi.ts`, `src/frontend/apps/mgmt/src/containers/ServersPage.tsx`,
  `src/frontend/packages/ui/src/components/ServerTable.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`U10` — Re-home the interactive-auth capability out of the composition root, and expose its seams.** A design
  review of how the interactive login/consent UI is served ([ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md))
  found **one real inconsistency** and **one latent gap**; most other concerns raised were already resolved by existing
  ADRs (recorded here so the review does not have to be re-derived).

    **Actionable #1 — extract the capability to a leaf package (recommended).** The interactive-auth code currently lives
    under [`src/NCode.Identity.Server/InteractiveAuthentication/`](src/NCode.Identity.Server/InteractiveAuthentication),
    but `NCode.Identity.Server` is the **composition root** (`AddIdentityServer()`, `DefaultRegistration`, the builder) of
    the capability packages — not a home for a capability itself. The folder _is_ a capability: a cookie scheme, four
    endpoints, credential verification, antiforgery wiring, and a static-file server. Every other capability (Accounts,
    Management, Authentication) is a leaf package; this one got inlined. ADR-0065 even calls it "a reusable opt-in module …
    surfaced from `NCode.Identity.Server`" — the module intent exists, the package boundary was just never drawn.
    **Proposal:** move it to a leaf package (candidate name `NCode.Identity.OpenId.Authentication.Interactive`, with the
    `Defaults`/`Options` + stable-locator contract in a thin `.Abstractions` pair); `NCode.Identity.Server` then only
    _references_ it like any other capability. Entails `PublicAPI.Unshipped.txt` churn + a new ADR (both expected under the
    pre-release posture, [ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)). **Open fork:** confirm
    the package name (`NCode.Identity.OpenId.Authentication.Interactive` vs. a `Server`-family name) before moving.
    Files to move: all of
    [`src/NCode.Identity.Server/InteractiveAuthentication/`](src/NCode.Identity.Server/InteractiveAuthentication)
    (`InteractiveAuthenticationRegistration.cs`, `…ApplicationBuilderExtensions.cs`, `…EndpointRouteBuilderExtensions.cs`,
    `…EndpointHandler.cs`, `…Options.cs`, `…Defaults.cs`); wiring to repoint in
    [`src/NCode.Identity.OpenId.Host/Program.cs`](src/NCode.Identity.OpenId.Host/Program.cs) (lines ~30, 78,
    135, 156) and `launchSettings.json` (`InteractiveAuthentication__BundleRootPath`).

    **Actionable #2 — invert the credential verifier behind an interface (latent gap).** The real UI extensibility seam is
    _already three seams_: (a) the **bundle** is swappable via `InteractiveAuthenticationOptions.BundleRootPath`; (b) the
    **render surface** is swappable by pointing the cookie `LoginPath`/scheme elsewhere and serving anything at
    `/account/*`; but (c) the **credential verifier** is **not** pluggable —
    [`InteractiveAuthenticationEndpointHandler`](src/NCode.Identity.Server/InteractiveAuthentication/InteractiveAuthenticationEndpointHandler.cs)
    hard-wires `ILocalAccountStore.VerifyCredentialAsync` and the `ClaimsPrincipal` construction. LDAP / external-IdP /
    passwordless backends have no interface to replace. **Open fork:** invert (c) behind an interface now, or leave it
    local-account-only and just document the (a)/(b) seams until a real second backend exists. (Decide with #1, since the
    extraction is the natural moment to introduce the abstraction.)

    **Already-resolved concerns (do not re-litigate; captured for the next session):**
    - _Same-origin is the hard constraint for `apps/auth`._ The cookie `LoginPath` redirect, the antiforgery POST, and the
      `ReturnUrl` round-trip all require same-origin with the OP issuing the cookie (ADR-0065). "Same-origin" ≠ "served by
      Kestrel" — a CDN/reverse-proxy may front `/account/*` at the _same origin_, but the OP-hosted static root is the
      correct default.
    - _Serve assets by purpose._ `apps/auth` must be OP same-origin (cookie flow); `apps/mgmt` is a pure API client with
      **no** same-origin requirement — it is the tier-agnostic "build once, discover at runtime" bundle
      ([ADR-0064](docs/adr/0064-frontend-presentation-platform.md) #5) and is the textbook CDN/static-host workload, not a
      .NET-runtime concern. So production serving is **both, by purpose**: CDN-first for the console; OP-hosted (or
      same-origin CDN path) for auth.
    - _Dev orchestrator already exists._ The VS Code `API + UI (F5)` compound launch runs the host plus
      [`build/dev-ui.ps1`](build/dev-ui.ps1) → `pnpm dev` → `turbo run dev` across every app; each app's `dev` script
      self-describes its serving model and new surfaces join automatically. Accepted cost: `apps/auth` gets build-watch
      (`vite build --watch`), **not** HMR, _because_ the OP serves it same-origin (ADR-0065). If the auth inner loop
      becomes painful, a **separate** decision is whether to adopt **.NET Aspire** as the orchestrator (model "API + node
      dev servers" as one app graph) instead of the current PowerShell + compound-launch combo — a real fork, not a free
      win; it would be its own ADR/backlog item.
    - _Bundling is already decided._ ADR-0064 **rejected** the single unified SPA and chose **per-surface bundles sharing
      `@ncode/*` packages** (auth vs. mgmt sit on different security boundaries / blast radius). Dev vs. prod use the
      **same** bundle — "one bundle, every tier," nothing baked at build time; `@ncode/config` discovers API base / issuer
      / flags / branding at boot.

    _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
    [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md). The capability now lives in the
    `NCode.Identity.OpenId.Authentication.Interactive(.Abstractions)` leaf packages._

- **`U11` — Adopt .NET Aspire as the local dev orchestrator (needs its own ADR).** **Decided (2026-10-11):** adopt
  Aspire to realize the host's intended purpose — a single one-stop entry to build/run/debug the entire stack
  locally. Aspire is **not** added _inside_ the host (the host is the reference OP host — one process); it
  is a new **sibling AppHost project** (`NCode.Identity.OpenId.LocalDev`) that orchestrates the host +
  the frontend apps (+ optional backing services) as one resource graph. This closes the orchestration fork left open
  by the (now-complete) interactive-auth extraction and
  [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md) and supersedes the
  dev-orchestration half of that ADR (the `build/dev-ui.ps1` + VS Code `API + UI (F5)` compound launch, which has **no
  dependency ordering** — the root symptom being `apps/auth`/`apps/mgmt` booting before the API is listening and failing
  in the browser). **Why Aspire:** `WaitFor(api)` + ASP.NET Core health-check integration gates each UI resource on the
  API being **healthy** (not merely "process started"), deterministically eliminating the race; `WithReference(api)`
  provides service discovery that feeds `@ncode/config`'s runtime-discovery model
  ([ADR-0064](docs/adr/0064-frontend-presentation-platform.md)); the dashboard gives one place for ordered startup,
  logs, and traces. **Topology:**
  `builder.AddProject<Projects.NCode_Identity_OpenId_Host>("api")`, then
  `builder.AddViteApp("auth", …).WithReference(api).WaitFor(api)` and the same for `"mgmt"`; backing SQL Server/Redis
  modeled as containers is **opt-in** (keep SQLite/in-memory to avoid a hard container-runtime prerequisite). **Scope:**
  new `*.AppHost` project (`IsPackable=false`, excluded from `dod.ps1` packing) + Aspire SDK/hosting packages pinned in
  [`Directory.Packages.props`](Directory.Packages.props); node apps modeled as Aspire resources
  (`AddViteApp`/`AddNpmApp`); repoint the F5/dev experience onto the AppHost and retire/relegate
  [`build/dev-ui.ps1`](build/dev-ui.ps1). **Sequencing:** its own ADR'd change — a toolchain-wide
  orchestration change on a different axis than the completed interactive-auth package extraction; keep the two
  separate. Touch points: new
  `src/NCode.Identity.OpenId.LocalDev/`, [`Directory.Packages.props`](Directory.Packages.props),
  [`build/dev-ui.ps1`](build/dev-ui.ps1), `.vscode/launch.json` (the `API + UI (F5)` compound), `NCode.Identity.slnx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0071](docs/adr/0071-aspire-local-dev-orchestrator.md)._
- **`U12` — Aspire migration-worker resource (optional runtime polish).** [ADR-0072](docs/adr/0072-self-healing-dev-database-and-model-drift-gate.md)
  keeps database bootstrap inside the host `api` process. The Aspire-idiomatic alternative is a dedicated
  migration/initializer resource in the AppHost graph that other resources `WaitFor`, so a migration failure surfaces
  as a failed dashboard resource rather than an `api` crash. This only relocates the failure (the self-healing +
  Definition-of-Done drift gate already prevent drift), and costs a new project, so it is deferred.
  Touch points: `src/NCode.Identity.OpenId.LocalDev/Program.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0072](docs/adr/0072-self-healing-dev-database-and-model-drift-gate.md)._
- **`U14` — Frontend dev-dependency security advisories.** `pnpm audit` reports 10 advisories (3 critical, 1 high,
  6 moderate) against [`src/frontend/pnpm-lock.yaml`](src/frontend/pnpm-lock.yaml); GitHub Dependabot counts the same
  advisories per workspace path and reports them as 47 alerts on the default branch. Every one is a **`devDependency`
  of the test/dev toolchain** — `vitest` 2.1.9 and `@vitest/mocker`, `vite`, `esbuild`, `tinypool`, `launch-editor`,
  `uuid` — reachable only when the Vitest UI server or the Vite dev server is running locally. **None ship**: the
  `apps/auth` bundle served by the OP host is static build output, and no NuGet package is affected
  (`dotnet list package --vulnerable --include-transitive` is clean). Clearing them means a `vitest` 2 → 4 major
  upgrade across every app plus the matching `vite`/`esbuild` bumps — and `@vitest/coverage-v8`, which must always match
  the installed `vitest` version (it drives the CI coverage report, ADR-0143); do it with `U5` so the new CI job proves
  the upgrade rather than discovering it later.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0147](docs/adr/0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md)._
- **`U16` — Move the frontend toolchain runtime to the next Node LTS.** The frontend runs on Node 22
  ([`.node-version`](src/frontend/.node-version), the CI `node-version`), which is in Maintenance LTS until
  2027-04-30; Node 24 is Active LTS until 2026-10-20 and Node 26 enters Active LTS on 2026-10-28. `@types/node` tracks
  the runtime major and Dependabot ignores its majors
  ([ADR-0147](docs/adr/0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md)), so the move is
  one deliberate change: `.node-version`, `node-version` in
  [`.github/workflows/frontend.yml`](.github/workflows/frontend.yml), `engines.node` and the `@types/node` range in
  [`src/frontend/package.json`](src/frontend/package.json) (plus `apps/conformance-runner`), the Aspire AppHost's NVS
  lookup ([ADR-0071](docs/adr/0071-aspire-local-dev-orchestrator.md)), and the Node 22 mentions in
  [`src/frontend/README.md`](src/frontend/README.md) and [`CONTRIBUTING.md`](CONTRIBUTING.md).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0152](docs/adr/0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md) — the frontend
  runs on Node 26, `.node-version` is the single pin that CI, the AppHost, and the bootstrap script read, and pnpm 10
  is installed from its `packageManager` pin because Node 26 no longer ships Corepack. Moving to pnpm 12 is `U21`._
- **`U17` — Frontend tests leave state updates outside `act(...)`.** A green `pnpm test` still logs eight React
  "An update to … inside a test was not wrapped in act(...)" warnings, from `ExportPublicKeyDialog`,
  `RevealSecretDialog`, `RevealInitialAccessTokenDialog`, MUI's `FormControl`, and `FlowExplorer`. The same set appears
  under React 18 and React 19, so it predates the upgrade. Each one is a test that asserts before an async update
  (a resolved promise, a transition) has settled. That can pass for the wrong reason, and it buries any new warning in
  noise. Await the settled state with `findBy…` / `waitFor` (or wrap the trigger in `act`) until the suites run with
  no `act` warnings. Touch points: the `*.test.tsx` of those components in `packages/ui` and
  `apps/playground/src/containers/FlowExplorer.test.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0151](docs/adr/0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md) — every test that
  logs a console warning or error now fails, so the fix is also enforced._
- **`U18` — One `BootstrapGate` for every standalone surface.** `apps/mgmt`, `apps/playground`, and
  `apps/conformance` each carry a copy of `src/BootstrapGate.tsx` that differs only in its doc comment — each discovers
  the runtime configuration through its own `bootstrap.ts` (the surface binding), dispatches it to the store, and themes
  the loading and error states — so every fix lands three times and each copy needs the same tests. Extract one gate
  that takes the loader (`(signal) => Promise<RuntimeConfig>`) as a prop into a shared workspace that may depend on both
  `@ncode/store` and `@ncode/ui`, keep each app's `bootstrap.ts` as its surface binding, and move the tests with the
  gate. Touch points: the three `src/BootstrapGate.tsx` / `BootstrapGate.test.tsx` pairs and each app's `main.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0156](docs/adr/0156-shared-app-shell-containers-live-in-ncode-shell.md) — one `BootstrapGate` in the new
  `@ncode/shell` workspace serves every standalone surface. Its loading and error views are the stateless
  `BootstrapScreen` in `@ncode/ui`. The per-app `bootstrap.ts` modules are gone, because each surface names itself in
  `main.tsx` through `createRuntimeConfigLoader({ surface })`, and `SurfaceKind` is open. The same change moved every
  workspace's Vite and Vitest configuration into the shared `@ncode/vite-config` preset and made turbo's hashes cover
  internal dependencies
  ([ADR-0157](docs/adr/0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md))._
- **`U19` — Key the Management API cache tags by scope.** [ADR-0064](docs/adr/0064-frontend-presentation-platform.md)
  makes the global-vs-tenant boundary first-class with distinct cache tags, and the doc comment on `managementApi`
  promises that a tenant mutation never invalidates the global list and vice versa. The code does not deliver it: the
  `Client` and `ResourceServer` `LIST` tags are shared by the global scope and every tenant, the `Grant` `LIST` tag by
  every tenant, and the per-id `Client`/`ResourceServer`/`Secret`/`Settings` tags use the bare resource id, so a
  mutation in one scope refetches the matching caches of every other scope. RTK Query caches by request arguments, so
  no data crosses a boundary — the cost is redundant refetches and an invariant that only exists in a comment. Derive
  each tag id from the scope (for example `tenant:{tenantId}:LIST` / `global:LIST`, and the scope prefix on per-id and
  `client:`/`tenant:` tags) through one helper beside `scopeApiPrefix`, and extend `managementApi.test.ts`'s
  invalidation matrix (it already asserts which of 27 cached queries refetch per mutation) to pin cross-scope
  isolation. Touch points: [`packages/store/src/managementApi.ts`](src/frontend/packages/store/src/managementApi.ts),
  `packages/api/src/scope.ts`, `packages/store/src/managementApi.test.ts`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased] — `scopeTagId` in `packages/api/src/scope.ts` derives every tag
  id from the scope that owns the resource (`global:…`, `tenant:{tenantId}:…`, with the tenant id URI-encoded), and the
  invalidation matrix subscribes the same resource ids in the global scope and a second tenant to pin the isolation._
- **`U20` — Split the standalone SPA bundles below Vite's chunk-size warning.** `pnpm build` warns that `apps/mgmt`,
  `apps/playground`, and `apps/conformance` each emit a chunk larger than 500 kB after minification — React, MUI,
  emotion, and Redux all land in one entry chunk. Raising `build.chunkSizeWarningLimit` would only hide it. Split
  instead: lazy-load the route-level containers (`React.lazy` + `Suspense`) and separate the vendor libraries into their
  own chunks in the Vite build options, then make a chunk-size warning fail the build so it stays fixed — the
  build-time counterpart of the test gates in
  [ADR-0151](docs/adr/0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md). Every app's build
  options come from the shared preset
  ([ADR-0157](docs/adr/0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md)), so the chunking and the
  failing warning land there once. Touch points:
  [`packages/vite-config/src/preset.ts`](src/frontend/packages/vite-config/src/preset.ts) and each app's `src/App.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0175](docs/adr/0175-bundles-split-into-vendor-and-surface-chunks-and-an-oversized-chunk-fails-the-build.md) —
  the preset's `VENDOR_CHUNKS` code-splitting groups and `failOnOversizedChunks` plugin, and `React.lazy` surface
  containers in `apps/mgmt`, `apps/playground`, and `apps/conformance`._
- **`U22` — Put the management console's tenant scope in its URL.** The console keeps its active scope (global or one
  tenant) and surface in React state, so a tenant view cannot be bookmarked, linked, or restored on reload, and the
  browser's back button leaves the console. Auth0's dashboard addresses a tenant in its route
  (`manage.auth0.com/dashboard/{region}/{tenant}/…`), and tenant identifiers are readable names
  ([ADR-0159](docs/adr/0159-a-tenant-is-named-by-its-creator.md)). Route the console by scope and surface (for example
  `/tenants/{tenantId}/clients`), derive the `ManagementScope` from the route, and keep the global-versus-tenant security
  boundary of [ADR-0064](docs/adr/0064-frontend-presentation-platform.md) explicit in the route structure. Touch points:
  [`apps/mgmt/src/App.tsx`](src/frontend/apps/mgmt/src/App.tsx), `packages/api/src/scope.ts`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0174](docs/adr/0174-the-management-consoles-url-is-its-navigation-state.md) — `apps/mgmt/src/routes.ts` parses
  and formats the routes, `navigation.ts` binds them to the browser history, and `AuthGate` carries a deep link through
  the sign-in round trip._

- **`U23` — Rotate a client secret from the Management console.** The management API rotates a client secret in one
  call ([ADR-0183](docs/adr/0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md)), but the console's
  client secret table offers only create and delete. Add a `rotateClientSecret` mutation to `@ncode/store`, a
  `RotateSecretRequest` contract to `@ncode/api`, and a rotate action on a shared secret's row that reveals the
  replacement's material once, as create does. Touch points:
  `src/frontend/packages/store/src/managementApi.ts`, `src/frontend/packages/api`, the client secret table in the
  Management console.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0183](docs/adr/0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md) (consequences amended).
  `@ncode/api` mirrors `RotateSecretRequest`, `@ncode/store` adds `rotateClientSecret`, `SecretTable` shows a rotate
  action for the `rotatableSecretTypes` it is given, and `SecretsManager` derives them from the capabilities document
  (the types that are not asymmetric), confirms, rotates with the create form's one-year expiry, and reveals the
  replacement once. The client detail page wires the client's rotate endpoint._
- **`U24` — Scope an initial access token to a resource server in the admin console.** The management API mints an
  initial access token scoped to a resource server, so the client it registers introspects for that resource server
  ([ADR-0192](docs/adr/0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md)), and the
  `@ncode/api` contract carries `resourceServerId` on the request and the created resource. The console's mint dialog
  offers only a description and a lifetime. Add an optional resource-server picker that lists the tenant's non-system
  resource servers, send its `resourceServerId`, show the scope in the one-time reveal, and cover it in the
  `GrantsPage` and `managementApi` tests. Touch points:
  `src/frontend/packages/ui/src/components/InitialAccessTokenFormDialog.tsx`,
  `src/frontend/apps/mgmt/src/containers/GrantsPage.tsx`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0192](docs/adr/0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md)
  (consequences amended). `InitialAccessTokenFormDialog` takes the `resourceServers` it may scope to and offers a
  picker only when there is one, `RevealInitialAccessTokenDialog` names the scoped resource server, and `GrantsPage`
  loads the tenant's non-system resource servers only while the mint dialog is open and sends `resourceServerId`._
- **`U13` — Buffer-writer (pipe) render model for the server-rendered interactive surface.** The server-side HTML
  renderer `InteractiveAuthenticationPageRenderer` (login, consent, and the `F31` non-redirectable error page) builds
  each page as a `string` via `StringBuilder` and injects it into the SPA bundle's `#root` with `string.Replace`, then
  the caller writes it with `HttpResponse.WriteAsync(html, SecureEncoding.UTF8, …)`. This matches the form-post writer
  (`DefaultAuthorizationResultExecutor.GetFormPostHtml`) but allocates a full form string **and** a full
  combined-document string per request. The family already has the lower-allocation idiom — `IBufferWriter<char>` /
  `Sequence<char>` + `SecureEncoding.UTF8` written straight to `HttpResponse.BodyWriter` (`NCode.Buffers`, as
  `CommonJoseEncoder` / `JoseSerializer` use it). Deliverables: render each page into an `IBufferWriter<char>` (pooled
  `Sequence<char>`) instead of returning a `string`; split the cached bundle `index.html` (`InteractiveBundleReader`)
  **once** per version at the `<div id="root"></div>` marker into head/tail `ReadOnlyMemory<char>` slices and write
  `head → form → tail` segments to the response pipe (no combined document string); apply the same seam to the login,
  consent, and error renderers **and** the form-post executor so the whole interactive render surface is consistent. Not
  hot-path critical for the `F31` error page (a cold error path), but login/consent are warmer and the
  cohesion/allocation benefit applies surface-wide. Touch points:
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveBundleReader.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveOpenIdErrorResponseFormatter.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Results/DefaultAuthorizationResultExecutor.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. `InteractiveBundleReader` caches the index per version and
  splits it once into an `InteractiveBundleShell` (head and tail slices around the mount point);
  `InteractiveAuthenticationPageRenderer` writes `head → form → tail` into an `IBufferWriter<char>`, HTML-encoding each
  value span by span; `InteractiveHtmlResult` transcodes the pooled `Sequence<char>` into `HttpResponse.BodyWriter`.
  `DefaultAuthorizationResultExecutor` renders the `form_post` page the same way, which also fixed its values being
  url-encoded before the browser's own form encoding._

- **`U6` — Per-tenant branding.** `@ncode/ui` themes from a runtime `BrandingConfig` (the seam exists); wire
  end-to-end per-tenant brand/theme resolution through the discovery channel for customer-facing surfaces.

  _Closed: [ADR-0194](docs/adr/0194-a-tenant-brands-its-interactive-surface-through-settings.md);
  [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. The interactive package registers the tenant settings
  `branding_display_name`, `branding_logo_uri`, `branding_primary_color`, `branding_secondary_color`, and
  `branding_color_mode`, validated against the shapes the theme draws, and serves them at
  `GET {tenant}/account/branding`. `apps/auth` fetches them relative to its document base, keeps the well-formed
  tokens through `@ncode/config`'s shared `parseBrandingConfig`, themes through `AppThemeProvider`, draws the logo, and
  titles the login and account pages with the display name. `InteractiveTenantRouteTests` proves two path tenants
  receive their own branding._

- **`U25` — Edit a tenant's mTLS endpoint-alias authority in the console.** `F47` made the alias authority a tenant
  property that the management API carries as `mtlsEndpointAliasAuthority`
  ([ADR-0195](docs/adr/0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md)). Add it to the `@ncode/api`
  mirrors (`TenantResource`, `CreateTenantRequest`, `UpdateTenantFields` in `packages/api/src/contracts.ts`), then show
  and edit it beside the issuer in the management console's tenant pages (`apps/mgmt` `TenantsPage.tsx` and
  `TenantDetailPage.tsx`, and `TenantFormDialog`). Preview its derived default through `TenantAddressResource`, the way
  the create and edit dialog already previews the issuer, and re-judge it only when it changes. The preview's
  `mtlsEndpointAliasAuthority` query parameter and the resource's `mtlsEndpointAliasAuthority` and
  `isMtlsEndpointAliasAuthorityDerived` fields exist (`F78`, done); add them to the `@ncode/api` `TenantAddressResource`
  mirror as well.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0195](docs/adr/0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md) (consequences amended). The
  `@ncode/api` mirrors carry `mtlsEndpointAliasAuthority` on `TenantResource`, `CreateTenantRequest`,
  `UpdateTenantFields`, and `TenantAddressResource` (with `isMtlsEndpointAliasAuthorityDerived`), and the store's
  `getTenantAddress` sends a proposed alias. `TenantFormDialog` edits the alias beside the issuer and shows the derived
  default as its hint and placeholder, `TenantTable` shows it in its own column, and `TenantsPage` previews it,
  re-judges an existing tenant's address only when its issuer or alias changes, and sends it on create and update (empty
  restores the derived one). `TenantDetailPage` shows no address (it is the tenant's secrets and settings tabs), so the
  alias is shown and edited where the issuer is._

## Secrets management (group `S`)

- **`S1` — Client-secret rotate endpoint.** Rotation is create-new + delete-old (two calls) per
  [ADR-0011](docs/adr/0011-secret-management-api.md); ADR-0050 notes a convenience `rotate` would inherit
  the same one-time reveal. A `POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate`
  returning `CreatedSecretResource` is the natural next increment.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs`.

  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0183](docs/adr/0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md).
  `POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate` generates a replacement shared secret of
  the rotated secret's type, size, `use`, and `alg`, adds it and removes the rotated secret in one save, and returns
  the replacement's `CreatedSecretResource`, the only place its material appears. It is authorized as the create and
  delete it replaces and honors `If-Match`; a key answers `400`. The rotated secret stops at once, because expiry is
  not enforced (`S8` adds a grace period). Follow-up recorded: `U23` (the console's rotate action)._

- **`S2` — Server-secret change auditing.** Tenant- and client-secret create/delete emit audit events
  (`RecordTenantSecretChangedAsync` / `RecordClientSecretChangedAsync`), but there is no
  `RecordServerSecretChangedAsync` — server-secret operations are not audited. Decide whether this is
  intentional (servers are the `GlobalAdmin`-only control plane) or a parity gap to close.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Servers/ServerApiEndpointHandler.cs`,
  `IManagementAuditRecorder`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased] (`ServerSecretChangedAuditEvent`,
  `RecordServerSecretChangedAsync`)._
- **`S3` — Key import / BYOK.** Secret creation was server-generation-only; there was no way to onboard a
  `private_key_jwt` client (client generates its keypair and registers the **public** half) or to import an
  `x509` certificate. Resolved by importing a **public** asymmetric key as JWK / PEM / certificate across the
  management API (`POST .../secrets/import` on every secret owner) and Dynamic Client Registration (inline `jwks`),
  persisted as a first-class secret via the new `SecretEncoding` axis (`symmetric_raw` / `pkcs8` / `spki` / `x509`)
  and consumed for client authentication / JAR verification. Import is **public-only by construction** (private /
  symmetric material is rejected), so the reveal-once model remains the sole path a symmetric value leaves the
  server. The key-type **descriptor** (`ISecretKeyTypeDescriptor`) became the first-class seam for the full key
  lifecycle (validate / generate / import / export / deserialize), collapsing the parallel `IJsonWebKeyConverter`
  registry. The FAPI-preferred runtime `jwks_uri` resolver remains tracked under `F16` Phase 0.
  Touch points: `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretImporter.cs`,
  `src/NCode.Identity.OpenId.Management/Endpoints/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/DynamicClientRegistration/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0104](docs/adr/0104-import-public-asymmetric-secret-material.md),
  [ADR-0105](docs/adr/0105-secret-key-type-descriptor-first-class-seam.md)._
- **`S4` — Asymmetric public-material exposure.** No read path returns public key material (JWK/PEM) for a
  client's registered public key or for server/tenant signing keys. ADR-0011/ADR-0050 reserve this as a
  deliberate additive option (public material only; private keys stay unreachable).
  Touch points: `src/NCode.Identity.OpenId.Management.Abstractions/Contracts/Secrets/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0011](docs/adr/0011-secret-management-api.md), [ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)._
- **`S6` — Grow the management capabilities document.** `GET /api/capabilities`
  ([ADR-0067](docs/adr/0067-secret-type-capability-catalog-and-capabilities-discovery.md)) is composed from
  registered providers and currently publishes `secretTypes` and `eccCurves`
  ([ADR-0068](docs/adr/0068-unified-extensible-elliptic-curve-catalog.md)). Natural next sections: the supported
  signature/key-management **algorithms** (from `IAlgorithmCollectionProvider`) and per-type **key-size** options, so
  the console create-secret form drives its algorithm and key-size inputs from discovery instead of its remaining
  hard-coded defaults. Each is a new `ICommandHandler<DiscoverManagementCapabilitiesCommand>` provider.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Capabilities/`,
  `src/frontend/packages/ui/src/components/SecretFormDialog.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0067](docs/adr/0067-secret-type-capability-catalog-and-capabilities-discovery.md),
  [ADR-0068](docs/adr/0068-unified-extensible-elliptic-curve-catalog.md)._
- **`S11` — Provision tenant signing keys in production.** A tenant's signing keys come only from its persisted secrets
  (`DefaultOpenIdTenantFactory.GetTenantSecretsAsync`), and the only seeder that creates one is the development-only
  `DeveloperSigningKeySeedHandler`. `AddProductionSafety` requires `DeveloperKeys:Mode=None` in Production, so on a
  fresh production database no tenant has a signing key: the JWKS is empty, no token can be issued, and the
  [ADR-0044](docs/adr/0044-management-api-authentication-and-globaladmin-bootstrap.md) bootstrap administrator cannot
  obtain the management token it needs to create or import one through `POST api/tenants/{tenantId}/secrets` — a
  cold-start deadlock that blocks the lab. Add production key provisioning (the open "first-class production
  key-management registration" follow-up of [ADR-0002](docs/adr/0002-ephemeral-development-keys.md)): on tenant seed,
  generate the tenant's asymmetric signing keys through the normal persistence path when none is usable — covering the
  mandatory-to-implement `RS256` and the preferred `PS256` / `ES256`. Every tenant passes through the one seed pipeline
  — the root tenant when it is first served, a management-created tenant before its create returns, and every tenant
  again when it is next materialized
  ([ADR-0166](docs/adr/0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md)) — so a
  `SeedTenantCommand` handler reaches each one. Unlike the developer seeder, never prune
  undecryptable secrets; fail loudly, since that signals a lost or changed Data Protection key ring. Rotation and
  retirement follow in `S8`, external keystores in `S7`. Touch points:
  `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperSigningKeySeedHandler.cs`,
  `src/NCode.Identity.Server/ProductionSafety/ProductionSafetyRegistration.cs`,
  `src/NCode.Identity.OpenId.Core/Tenants/DefaultOpenIdTenantFactory.cs`. Capture the design in an ADR.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0169](docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md). Follow-up: `S12` tailors the
  provisioned algorithm set per tenant._

- **`S10` — Encrypt the Data Protection key ring at rest.** The host persists the ASP.NET Core Data Protection key ring
  to the same Postgres database as the secrets it protects (`PersistKeysToDbContext<DeveloperDataProtectionDbContext>`,
  unconditionally and with no `ProtectKeysWith…`), so a database backup or dump contains both the protected secret
  material and the keys that unprotect it. Production must wrap the ring with a key held outside the database — a cloud
  KMS / HSM key (Azure Key Vault, OCI Vault) or a certificate from the secret store — the server composition should
  make that the secure default rather than an opt-in, and `AddProductionSafety` should refuse an unprotected ring.
  Complements `S9` (envelope encryption of stored secrets) and `S7` (external-keystore providers). Touch points:
  `src/NCode.Identity.OpenId.Host/Program.cs`, `src/NCode.Identity.Server/IdentityServerBuilder.cs`,
  `src/NCode.Identity.Server/ProductionSafety`, `src/NCode.Identity.Secrets/DefaultRegistration.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0177](docs/adr/0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md). `AddConfiguration`
  wraps the ring with a `DataProtection:KeyProtection` certificate, and in Production `AddProductionSafety` refuses a
  ring with no repository or no `IXmlEncryptor` as the host starts._

- **`S12` — Tailor each tenant's provisioned keys to the algorithms it can sign with.** Every tenant is provisioned with a
  persisted key for each algorithm `OpenId:Tenant:KeyProvisioning:Keys` configures — `RS256`, `PS256`, and `ES256` by
  default ([ADR-0169](docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md)). The set is server-wide, so a
  tenant whose effective signing lists exclude an algorithm still holds and publishes a key for it: a `fapi2` tenant
  publishes an RS256 key it never signs with. Conversely, a client that registers an algorithm outside the set, such as
  `ES384`, fails at issuance until a key is imported. Provision per tenant from what its effective
  `*_signing_alg_values_supported` lists and security profile permit, intersected with the configured set. That needs the
  tenant's merged settings while it is seeded, which today materialize only after the seed. Decide whether keys a tenant
  no longer permits are retired, which is `S8`'s lifecycle. Touch points:
  `src/NCode.Identity.OpenId.Core/Tenants/Keys/` (`TenantKeySeedHandler`, `DefaultTenantKeyPolicy`),
  `DefaultOpenIdTenantFactory` (the settings it materializes), `ISecurityProfile`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0169](docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md) decision 9. The seed merges the tenant's
  document onto the server's settings and provisions a configured algorithm only when one of the lists
  `OpenId:Tenant:KeyProvisioning:AlgorithmSettings` names includes it; a key the settings no longer use is kept, and
  retiring it is `S8`'s lifecycle._

## Tooling & agent customization (group `X`)

- **`X1` — Chat commands duplicated as prompt + skill.** Every chat command exists **twice**: a VS Code prompt under
  `.github/prompts/` and a Copilot CLI skill under `.github/skills/`, because the Agent Host / Copilot CLI does not load
  prompt files and reads `SKILL.md` instead. There are two sets. The hand-written `audit-conventions`
  [prompt](.github/prompts/audit-conventions.prompt.md) and [skill](.github/skills/audit-conventions/SKILL.md) must be
  mirrored by hand — the bodies are identical apart from a `+1 ../` link-depth adjustment. The 16 plan-and-execute
  commands are generated in pairs from one template each by the workflow installer
  ([ADR-0170](docs/adr/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md)),
  so they cannot drift. VS Code lists both copies of each command in its slash menu. No skill carries
  `disable-model-invocation: true`: the flag makes a skill **unreachable** in Copilot CLI, even on explicit invocation
  (upstream bug github/copilot-cli#4438). **Trade-off accepted:** every skill is also model-auto-invocable, and its
  `description` is the only guard on when the model reaches for it — including `execute-step`, which is meant to run
  only on the executor model. A prompt's `agent:` and `model:` frontmatter have no skill equivalent; the generated
  planning prompts select Opus through `model:`. **Runtime-validated (2026-10-10):** the `audit-conventions` skill was
  discoverable and loadable in a live Agent Host / Copilot CLI session, with its relative links and LF line endings
  intact. **Decision still deferred:** delete the prompts (Agent-Host-only) or keep both (VS Code chat + Agent Host).
  Keep both for now, and restore `disable-model-invocation: true` once github/copilot-cli#4438 is fixed — by hand for
  `audit-conventions`, and in the installer for the generated set.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0170](docs/adr/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md#dual-runtime-customization-contract).
  The historical deferred decision and flag-restoration follow-up above are superseded: both runtimes and automatic
  skill availability are intentionally supported, and the Definition-of-Done customization gate enforces parity for
  all 17 pairs, including the hand-written audit workflow. No upstream fix or further X1 work is required._
- **`X2` — Archive completed backlog items (full text) for reference.** Completed entries were **deleted** from
  [`BACKLOG.md`](BACKLOG.md) when their work landed, so their original wording survived only in git history and in the
  terse [`CHANGELOG.md`](CHANGELOG.md) line that closed them. Mine the history — `git log --follow -p -- BACKLOG.md` —
  to recover every removed entry, and collect the full text into a dedicated, read-only archive (this
  `BACKLOG-COMPLETED.md`), keyed by backlog ID, each cross-referenced to the [`CHANGELOG.md`](CHANGELOG.md) entry and/or
  ADR that closed it. Going forward, when an entry is completed and removed, copy its text into the archive in the same
  change (and the ID stays recorded in the [ID ledger](BACKLOG.md#backlog-id-ledger--never-reuse-a-number)). This
  preserves the richer problem/design context the changelog omits, and makes the retired IDs' rationale discoverable
  without archaeology. Touch points: [`BACKLOG.md`](BACKLOG.md), [`CHANGELOG.md`](CHANGELOG.md),
  [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md).
  _Closed: this archive; the going-forward process is documented in [`BACKLOG.md`](BACKLOG.md)._
- **`X3` — Unused `using` directives are not enforced in production code.** `IDE0005` is explicitly off for tests in
  [`.editorconfig`](.editorconfig), but nothing turns it on for `src/`: under `AnalysisLevel` `latest-recommended` it
  stays at its default hidden severity, so warnings-as-errors never sees it. Raising it to `warning` for `src/` (found
  during the `D7` sweep) flags unused usings in roughly 80 production files (e.g. `NCode.Identity.Jose`,
  `NCode.Identity.OpenId.Authentication.Interactive`, the Management endpoint groups). Clean them with
  `dotnet format style --diagnostics IDE0005`, then set `dotnet_diagnostic.IDE0005.severity = warning` under `[*.cs]` so
  the build keeps them out. Touch points: [`.editorconfig`](.editorconfig) and the flagged `src/` files.
  _Closed by [ADR-0127](docs/adr/0127-warnings-are-errors-in-every-project.md), which unified the warning gate across
  the whole repository. `IDE0005` was raised solution-wide under `[*.cs]` — not just `src/` — and the tests-only
  `IDE0005 = none` override was removed, so the rule now covers both trees; `dotnet format style --diagnostics IDE0005`
  cleaned 182 files. Two Roslyn quirks were discovered and recorded in the ADR: the rule is hidden by default under
  `latest-recommended`, and it requires `GenerateDocumentationFile=true`, hard-erroring with
  `EnableGenerateDocumentationFile` otherwise (dotnet/roslyn#41640) — which is why test projects now generate (and
  discard) an XML documentation file and suppress `CS1591`._

- **`X4` — Decide the .NET coverage scope.** The merged .NET coverage report
  ([ADR-0143](docs/adr/0143-ci-presents-test-results-and-coverage-in-github.md)) measures every assembly the tests load,
  so its overall line figure (≈36% on the first isolated run) is dragged down by code that is not unit-testable library
  surface: EF Core migrations in `NCode.Identity.OpenId.Host.Database` (0%), the reference host
  `NCode.Identity.OpenId.Host` (<1%), and source-generator output under `obj/` (OpenAPI XML-comment and `Regex`
  generators — ReportGenerator also warns that these files "do not exist" because they are build intermediates). Decide
  what the headline number should measure — exclude generated code (`[GeneratedCode]`, `**/obj/**`, `**/Migrations/**`)
  and the host/composition assemblies via coverlet `ExcludeByFile`/`Exclude` or ReportGenerator filters, applied
  identically to the job summary and to Codecov (`codecov.yml` `ignore`) so the two never disagree. They already
  disagree: on the first CI run (`d02ca805`) the job summary reported 35.8% .NET line coverage over 75,871 coverable
  lines, while Codecov's `dotnet` flag reported 52.8% over ≈49,600 — Codecov silently drops every report file that is
  not in the repository (the generated `obj/` sources) and scores partially-covered lines differently. Touch points:
  [`build/coverage-report.ps1`](build/coverage-report.ps1), [`tests/Directory.Build.props`](tests/Directory.Build.props),
  [`codecov.yml`](codecov.yml). Coverage thresholds are a separate, later policy decision.

  _Closed by [ADR-0185](docs/adr/0185-coverage-measures-hand-written-code-and-counts-a-line-that-runs-as-covered.md).
  The headline measures hand-written code: `build/coverage.runsettings`, which `dod.ps1` passes whenever it collects
  coverage, makes coverlet skip `**/obj/**`, `**/Migrations/**`, and `[GeneratedCode]` members, so the job summary and
  Codecov read the same reports and no `codecov.yml` `ignore` list is needed. The reference host stays in scope because
  it ships. `codecov.yml` sets `parsers.cobertura.partials_as_hits` so Codecov counts a partially covered line as
  ReportGenerator does. The first full run under this scope reports 83.6% line coverage over 38,091 coverable lines,
  with the reference host at 79.1%. Coverage thresholds remain a separate policy decision._

- **`X5` — Flag a migration that would break the previous release.** Every EF Core migration must keep the previous
  release working, because the chart's Job migrates while the previous release serves
  ([ADR-0180](docs/adr/0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). Today that is
  a review rule only. Add a Definition of Done check that fails when a new migration drops or renames a table or
  column, or makes a column required without a default, unless the migration is marked as a deliberate contract step.
  Touch points: `build/dod.ps1`, `src/NCode.Identity.OpenId.Host.Database`.

  _Closed by [ADR-0184](docs/adr/0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md). The
  check is a unit test, not a `dod.ps1` text scan: `ExpandOnlyRuleTests` in the new
  `NCode.Identity.OpenId.Host.Database.Tests` project reads every host migration's `Up` operations through EF Core's
  `MigrationOperation` objects and runs in the Definition of Done's test stage. `ExpandOnlyRule` reports dropped or
  renamed schemas, tables, columns, and sequences; required columns added or altered without a default; changed column
  types; and shortened columns. `[ContractMigration("why")]` marks a deliberate contract step, and the test fails a
  marker on a migration that only expands. `ReplaceTenantDomainNameWithIssuerHost`, which shipped before any release,
  is the one marked migration. Raw SQL stays a review item._
