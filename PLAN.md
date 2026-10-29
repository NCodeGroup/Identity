# Plan: feat(bff): add the NCode.Identity.Bff core (BFF phase 2)
Issue: #394

## Goal
Phase 2 of the BFF design (`docs/backend-for-frontend.md` §5.2–§5.9, §9; umbrella #123) ships two new packages,
`NCode.Identity.Bff.Abstractions` and `NCode.Identity.Bff`. They turn an ASP.NET Core app into the confidential OAuth
client of its browser frontend. The browser holds only an opaque `__Host-Http-bff` cookie. Tokens stay in an encrypted,
server-side session that every replica sees. The phase covers login and callback with initiator binding, `/bff/user`,
local APIs behind a static anti-forgery header and Fetch Metadata, the `user` token requirement with cluster-wide
single-flight refresh and DPoP, RP-initiated logout, and back-channel logout.

## Context for the executor
- Build: `dotnet build tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj` (it builds both BFF packages).
- Test: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~<TestClass>" -- RunConfiguration.TreatNoTestsAsError=true`.
  Keep the trailing setting: without it a filter that matches nothing exits 0. Never run two `dotnet build`/`dotnet test`
  at once in this clone (they share `obj/`).
- Format: run `dotnet tool restore` once, then `dotnet csharpier format <each file you changed>` before you finish a step.
- Conventions (read `.github/instructions/csharp-production.instructions.md` and `csharp-testing.instructions.md` once):
  - Every `.cs` file starts with the Apache copyright preamble; copy it from `src/NCode.Identity.OpenId.Client/DefaultRegistration.cs`.
    LF line endings, no BOM, file-scoped namespaces, one type per file, no nested types.
  - Async methods return `ValueTask`/`ValueTask<T>`, end in `Async`, and take the `CancellationToken` last. A
    minimal-API handler or middleware `InvokeAsync` returns `Task`. No `ConfigureAwait`, no `.Result`/`.Wait()`.
  - The null-forgiving operator `!` is banned in `src/`. Use `?? throw`, `is { } x` or `[NotNullWhen]` instead.
  - Visibility starts at `internal`. A type is `public` only where a step says so. Internal implementations stay
    unsealed. Public data types are `sealed` with `[PublicAPI]`. Interfaces are public only where listed.
  - Every public symbol needs a line in the project's `PublicAPI.Unshipped.txt`, or the build fails (RS0016). A C# 14
    `extension(...)` member needs two lines: the `extension(Receiver!)` container line and the member line. You can
    regenerate the file with `dotnet format analyzers <csproj> --diagnostics RS0016`, then restore LF endings.
  - Every new or changed member gets XML docs. A `<summary>` spans several lines. A property summary starts with "Gets"
    (init or get-only) or "Gets or sets". `///` text never cites an ADR; put ADR links in `//` comments.
  - Text goes through `SecureEncoding.UTF8` (`NCode.Buffers`), never `Encoding.UTF8`. Prefer collection expressions,
    `const` locals and `u8` literals. Never nest one call inside another call's arguments; hoist it to a local first.
  - Log through `[LoggerMessage]` methods in `Logging/Log.cs`, with ids in `Logging/EventIds.cs` (band `19000`). Never log
    a token, a handle, a cookie value, a state, a code or a secret.
  - Tests use `BaseTests` (strict `MockRepository`, verified on dispose). Every `Setup` ends with `.Verifiable()` and
    uses `x` as its parameter. Names follow `Method_State_Expected`, tests are grouped in `#region <Member> Tests`, and
    there are no `// Arrange` comments. Use `CreateLooseMock<ILogger<T>>()` for loggers. Build `HttpContext`s with
    `DefaultHttpContext` and set `RequestServices` when the code under test resolves services.
- Gotchas:
  - `Directory.Build.props` turns on the Request Delegate Generator. Every BFF handler takes only `HttpContext` and reads
    its query or form itself (`Request.Query`, `await Request.ReadFormAsync(...)`). Never bind `[FromForm]`.
  - `NCode.Identity.Bff` may reference only `*.Abstractions` packages of the family, plus `NCode.Json`, `NCode.Time` and
    `NCode.Registration` (ADR-0016). It must never reference `NCode.Identity.OpenId.Client`, `.SharedState`, `.Events`
    or any OP package. Their implementations are registered by the composition root (the host or a test).
  - `NCode.Base64Url` has `Base64Url.Encode(ReadOnlySpan<byte>)`, `Base64Url.GetByteCountForDecode(int)` and
    `Base64Url.TryDecode(ReadOnlySpan<char>, Span<byte>, out int)`, which throws `FormatException` on illegal input.
    Catch it and treat it as invalid.
  - A stored instant is whole seconds: `timeProvider.GetUtcNowWithPrecisionInSeconds()` (`NCode.Time`).
  - `IExpiringValueSet.TryReplaceAsync` bumps the version by one. `TryTakeAsync` removes and returns atomically.
  - A test that builds a real container with `AddSecretsLibrary().AddJoseLibrary()` implements `IAsyncLifetime` and
    disposes the provider with `await DisposeAsync()`.

## Decisions
- **Scope.** The whole of #394 is in one plan, grouped by slice: (a) abstractions, session record, store and cookie;
  (b) scheme handler, login, callback and `/bff/user`; (c) the `user` token requirement, single-flight refresh and DPoP;
  (d) local-API cookie acceptance and the CSRF layers; (e) logout and back-channel logout. End-to-end tests and the
  ADRs and docs follow.
- **§11.5, package naming:** `NCode.Identity.Bff` and `NCode.Identity.Bff.Abstractions`. `OpenId.*` names a protocol
  concern (the OP and the protocol client). The BFF is an application pattern that spans login, cookie sessions, CSRF
  and API forwarding. ADR-0289 records this.
- **§11.1, a `bff` client profile at the OP: none.** In this family a security profile is a lattice ceiling over
  security settings (ADR-0108/0148), not a provisioning template. Everything the proposed profile would set is either
  the client's own registration metadata (`token_endpoint_auth_method`, `jwks_uri`, `redirect_uris`,
  `post_logout_redirect_uris`, `backchannel_logout_uri`, `backchannel_logout_session_required`,
  `require_pushed_authorization_requests`, `dpop_bound_access_tokens`, refresh-token lifetimes) or already enforced by
  the registered `fapi2` profile. A `bff` profile would duplicate `fapi2` and tie the BFF to the NCode OP (goal G3).
  ADR-0293 records this and lists the metadata to register.
- **§11.2, refresh-token lifetime discovery:** the BFF reads the de-facto `refresh_token_expires_in` member of a token
  response (`TokenResponse.ExtensionData`, read with `TryGetWholeInt32`) whenever an OP sends it, and caps the session's
  absolute expiry and cookie `Max-Age` at it. The cap can only shorten a session, never extend it. Without it the session
  still ends at `AbsoluteLifetime` or on `invalid_grant`. Making the NCode OP emit the member is an OP change, filed as a
  follow-up. ADR-0290 records this.
- **Abstractions reference ASP.NET Core.** `NCode.Identity.Bff.Abstractions` has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`,
  because its contracts take `HttpContext` (token requirements, login gates, session validators) and `BffApiAttribute`
  derives from `AuthorizeAttribute`. It also references `NCode.Identity.OpenId.Client.Abstractions`,
  `NCode.Identity.Events.Abstractions` and `NCode.Registration`.
- **Composition.** `AddBffLibrary(IServiceBuilder<EventsLibrary> events)` extends `IServiceBuilder<OpenIdClientLibrary>`
  and mints `BffLibrary`. It requires the events builder, as the account bolt-ons require their store builder.
  `AddBffConfiguration(IConfiguration)` on `IServiceBuilder<BffLibrary>` binds the `Bff` section with `BindStrictly`.
  The host registers the shared-state adapters
  (`AddSharedStateServices().AddInProcessExpiringKeySet().AddInProcessExpiringValueSet().AddInProcessExpiringIndexSet()`,
  or Valkey), because an implementation package cannot reference another one. A one-call turnkey root is a follow-up.
- **One frontend.** Phase 2 has a single frontend, configured at the root of `BffOptions`. Every session records
  `Frontend = "default"` (`BffDefaults.FrontendName`), so phase 4 (several frontends) needs no data migration.
- **Session record (§5.3).** The handle is 32 random bytes in base64url (43 characters).
  - The store key is base64url(HKDF-SHA-256(handle, info `ncode.bff.session.id`, 32 bytes)).
  - The payload is AES-256-GCM under HKDF(handle, `ncode.bff.session.payload`). It is stored as a 12-byte nonce, then
    the ciphertext, then a 16-byte tag.
  - The associated data is UTF-8(store key), then the version as 8 bytes big-endian, then the metadata JSON. A plaintext
    metadata field changed in the store (an expiry pushed out, for example) therefore fails decryption.
  - The stored value is format byte `1`, then the metadata length as 4 bytes big-endian, then the metadata JSON
    (snake_case, with an extension-data bag), then the protected payload.
- **Login transaction (§5.2).** It is stored under base64url(HKDF(state, `ncode.bff.login.id`)) and encrypted under
  HKDF(initiator secret, `ncode.bff.login.payload`), with that key as associated data. It is taken with
  `TryTakeAsync`, so it is single-use on every replica. The login cookie `__Host-Http-bff-login` is `Lax`, with a
  ten-minute `Max-Age`.
- **The store seam.** `IBffSessionStore` is public and replaceable. It holds opaque records, the
  `sid`/`sub` indexes, login transactions and refresh leases. A replacement only ever sees ciphertext, so it cannot skip
  the encryption. The default store prefixes its keys (`bff:session:`, `bff:txn:`, `bff:lease:`). Index keys are
  `bff:sid:`/`bff:sub:` plus base64url(SHA-256) of the length-prefixed issuer and value, so a value an attacker supplies
  cannot make a key ambiguous or unbounded. Any adapter exception becomes `BffStoreUnavailableException`, which
  `UseBff()` answers with `503` and never with `401`.
- **Lifetimes.** `AbsoluteLifetime` is 8 h and `IdleTimeout` 1 h. The stored value expires at
  min(idle, absolute), and the index members expire at the absolute expiry. The idle window slides only on requests to
  `[BffApi]` endpoints, never on `/bff/user`, and only when at least a tenth of `IdleTimeout` has passed since the last
  write. Every session write is read-modify-write with `TryReplaceAsync`, retried up to 5 times on a version conflict.
- **The session principal** is the ID token's claims as the JWT stack maps them (ADR-0122). The protocol client gains
  `IdTokenValidationResult.Identity`, so the BFF never maps JSON to claims itself. The BFF drops `nonce`, `at_hash`,
  `c_hash`, `s_hash`, `exp`, `iat`, `nbf`, `aud` and `azp`. It stores each remaining claim as (type, value, value type),
  and builds `ClaimsIdentity(claims, "Bff", "name", "role")`.
- **Audit events** use one public record, `BffAuditEvent : AuditEvent, ISupportAuditDetails`, with
  `Name`/`Category`/`Reason`, event source `bff` and the names in `BffAuditEventNames`. It adds no metric attributes, so
  `docs/metrics.md` does not change. Its request context is built the way `AuditRequestFactory.Create` builds it.
- **Problems.** Every API refusal is `application/problem+json` with `type` from `BffProblemTypes`
  (`urn:ncode:identity:bff:<code>`), `title` and `status`, and with `Cache-Control: no-store`.
- **The pipeline fails closed for each request.** The design asks for a startup check (§5.9); the plan does this
  instead:
  - `UseBff()` throws `InvalidOperationException` when it runs after `UseAuthentication()` (an
    `IAuthenticationFeature` is already set).
  - The session resolver throws when `UseBff()` did not run, or when it did not see the BFF-protected endpoint being
    served (routing ran after it).
  - So a missing or misordered `UseBff()` can never serve a BFF API without the CSRF checks. A host cannot see the
    built pipeline at startup.
- **CSRF (§5.9).** Every request to a `[BffApi]` endpoint or `/bff/user`, of any method, must carry the configured header
  (`X-CSRF: 1`) exactly once.
  - Fetch Metadata: `Sec-Fetch-Site` absent, `same-origin` or `none` passes. `same-site` passes only when `Origin`
    equals an entry of `Bff:TrustedOrigins`, because the BFF cannot read the app's CORS decision. Every other value is
    refused with `403`.
  - The header name may not be one that skips a CORS preflight (`Accept`, `Accept-Language`, `Content-Language`,
    `Content-Type`, `Range`), nor `Cookie`, `Authorization` or `DPoP`.
- **Challenge.** A `[BffApi]` challenge is a `401` problem and a forbid is a `403` problem. Any other challenge on the
  BFF scheme redirects to `/bff/login?returnUrl=<path>` when that path is allow-listed, and to `/` otherwise. The BFF
  scheme becomes the default scheme only when none is set (`PostConfigure`, `DefaultScheme ??=`).
- **Single-flight refresh (§5.5).**
  - Inside a replica, refreshes are serialized by 64 striped `SemaphoreSlim`s indexed by the store key.
  - Across replicas the lease is keyed by `{storeKey}:{SHA-256 fingerprint of the refresh token}`, so it guards the
    invariant itself: a refresh token is never presented twice. A version-keyed lease would let an idle-slide write
    open a second refresh.
  - The lease is an `IExpiringValueSet` entry (through `IBffSessionStore`), not an `IExpiringKeySet` key, because it
    must be released for an OP that does not rotate. It is released only when the refresh finished more than 5 s before
    the lease expires, so it never removes a lease that expired and that another replica took.
  - The lease lasts `Tokens.RefreshLeaseDuration` (40 s, at least twice the client's 15 s request timeout).
  - Losers poll the session with backoff (50 ms doubling to 800 ms) until a fresh token appears, the refresh token
    changes, or the lease is gone. They give up with `503` at the lease duration, and never retry with the old token.
  - A failed write after a successful refresh is merged onto the newer version and never dropped.
- **Refresh failures.** `invalid_grant` ends the session (`refresh_failed`) and answers `401`. Any other OAuth error
  answers `502` (`provider_error`). An `OpenIdProviderException`, an `HttpRequestException` or a timeout answers `503`
  (`provider_unavailable`). A missing refresh token ends the session and answers `401`. `offline_access` is always
  requested.
- **Token cache.** It is keyed by (requirement, resource, scope), with ordinal compares and `null` for absent. The
  code-exchange token is cached under (`user`, `null`, `null`). A token without `expires_in` counts as already
  expiring, so it is refreshed on first use. The cache holds at most 32 entries; the entry that expires soonest goes
  first.
- **DPoP.** `Bff:DPoP:Mode` is `when_supported` (the default) or `required`, and `KeyRing` names the ring. The modes
  map to `ClientRegistration.DPoPKeyRing` and `DPoPBoundAccessTokens`. The session pins the `jkt` that the code
  exchange reported, and passes it as `TokenRequest.DPoPJwkThumbprint` on every refresh. `BffAccessTokenHandler` sends
  `Authorization: DPoP` with an `IDPoPProofFactory` resource proof, records every `DPoP-Nonce`, and retries a
  `use_dpop_nonce` challenge once, with the content buffered first.
- **Token access.** `IBffTokenRequirement` is an open strategy collection; only `user` is registered in phase 2.
  `IBffTokenAccessor` resolves a requirement by name and throws `InvalidOperationException` for an unknown name. A
  failure inside `BffAccessTokenHandler` throws `BffTokenUnavailableException(status, reason)`, which `UseBff()` answers
  as a problem.
- **Seams in phase 2.** `IBffSessionStore` is a replaceable service. `IBffLoginGate` (reject only, after the ID token
  validates) and `IBffSessionValidator` (can only end a session, `ended_by_validator`) are strategy collections. Claim
  contributors, `/bff/user` contributors and UserInfo are follow-ups.
- **Startup checks.** `BffOptionsValidator` runs with `ValidateOnStart`. It never resolves strategies. The client
  authentication method is checked against the registered `IClientAuthenticator.MethodName`s by a hosted
  `BffStartupCheck`, because options validation must not construct strategies that have dependencies.
- **Logout (§5.8).**
  - `sid` must equal the session's logout guard, compared with `CryptographicOperations.FixedTimeEquals`; a mismatch
    answers `400`. Without a session, logout redirects to the return URL.
  - Revocation is best effort: it is logged and never blocks logout.
  - The session is removed with `CancellationToken.None`, and then the browser is redirected to
    `end_session_endpoint` with `id_token_hint`, `client_id`, `post_logout_redirect_uri` and a random `state`.
  - Back-channel logout ends sessions by `sid` (or by `sub` when `BackchannelLogoutSessionRequired` is false, which
    defaults to true). It records the `jti` only after the sessions end (ADR-0284). It answers `200`, or `400` when the
    token is invalid or a session cannot be ended.
- **New ADRs are numbered from 0289** (0287 is taken by #404 on `main`, 0288 by open PR #405): 0289 sessions and login binding, 0290 refresh,
  0291 cookie acceptance and CSRF, 0292 logout, 0293 no `bff` profile.

## Out of scope
- Phase 3 and later: the remote-API forwarder, `MapBffRemoteApi`, the `client`, `user_or_client`, `exchange` and `none`
  requirements, `NCode.Identity.Bff.Yarp`, several frontends, `UseBffSecurityHeaders()`, `/bff/jwks`, and moving the
  console onto the BFF (#158).
- Any change to the OP packages (`NCode.Identity.OpenId.Authentication*`, `.Core`, `.Server`, `.Host`), and to the
  shared-state and Valkey packages.
- mTLS client authentication and every line that open PR #405 changes: `IClientAuthenticator`, `ClientRequestSender`,
  ADR-0270's status line, the ADR README's 0288 row, and the phase-1 row of `docs/backend-for-frontend.md` §9.
- A dedicated BFF meter or `ActivitySource`, claim and `/bff/user` contributors, UserInfo, and a browser (Playwright)
  suite: these are follow-ups.
- Template-owned files (`CLAUDE.md`, `.claude/commands/`, `.github/agents|prompts|skills/`, `.template/`).

## Steps

### Slice (a): abstractions, session record, store and cookie (§5.3–§5.4)

### [x] 1. Create the NCode.Identity.Bff.Abstractions project
- Files: `src/NCode.Identity.Bff.Abstractions/NCode.Identity.Bff.Abstractions.csproj` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Shipped.txt` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (new), `src/NCode.Identity.Bff.Abstractions/BffLibrary.cs` (new), `NCode.Identity.slnx` (edit)
- Change: Copy the shape of `src/NCode.Identity.OpenId.Client.Abstractions/NCode.Identity.OpenId.Client.Abstractions.csproj`,
  with `RootNamespace` `NCode.Identity.Bff`. Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and project
  references to `NCode.Identity.OpenId.Client.Abstractions`, `NCode.Identity.Events.Abstractions` and
  `NCode.Registration`. Both PublicAPI files hold `#nullable enable`. Add
  `public readonly struct BffLibrary : IRegistrationMarker<BffLibrary>;` with `[PublicAPI]` and a summary ("minted by
  `AddBffLibrary`"), plus its PublicAPI lines (`NCode.Identity.Bff.BffLibrary` and `….BffLibrary.BffLibrary() -> void`).
  Add the project to the `/src/` folder of `NCode.Identity.slnx`, in alphabetical order.
- Pattern: `src/NCode.Identity.OpenId.Client.Abstractions/OpenIdClientLibrary.cs`.
- Done when: `if (Select-String -Path NCode.Identity.slnx -Pattern 'NCode.Identity.Bff.Abstractions.csproj' -Quiet) { dotnet build src/NCode.Identity.Bff.Abstractions/NCode.Identity.Bff.Abstractions.csproj; exit $LASTEXITCODE } else { exit 1 }`

### [x] 2. Create the NCode.Identity.Bff project and claim its event-ID band
- Files: `src/NCode.Identity.Bff/NCode.Identity.Bff.csproj` (new), `src/NCode.Identity.Bff/AssemblyInfo.cs` (new), `src/NCode.Identity.Bff/PublicAPI.Shipped.txt` (new), `src/NCode.Identity.Bff/PublicAPI.Unshipped.txt` (new), `NCode.Identity.slnx` (edit), `.github/instructions/csharp-production.instructions.md` (edit)
- Change: Copy the shape of `src/NCode.Identity.OpenId.Client/NCode.Identity.OpenId.Client.csproj`, with `RootNamespace`
  `NCode.Identity.Bff`.
  - Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.
  - Project references: `NCode.Identity.Bff.Abstractions`, `NCode.Identity.OpenId.Client.Abstractions`,
    `NCode.Identity.SharedState.Abstractions`, `NCode.Identity.Events.Abstractions`, `NCode.Json`, `NCode.Time`,
    `NCode.Registration`.
  - Package references: `JetBrains.Annotations` (`PrivateAssets="All"`), `NCode.Base64Url`, `NCode.Buffers`.
  - `AssemblyInfo.cs` grants `InternalsVisibleTo` to `NCode.Identity.Bff.Tests` and `DynamicProxyGenAssembly2`.
  - Both PublicAPI files hold `#nullable enable`. Add the project to the slnx `/src/` folder.
  - In the Event-ID band table of §11 of the instructions file, add the row `| \`19000\`–\`19999\` | \`NCode.Identity.Bff\` |`.
- Pattern: `src/NCode.Identity.OpenId.Client/AssemblyInfo.cs`.
- Done when: `if (Select-String -Path .github/instructions/csharp-production.instructions.md -Pattern '19000.*NCode.Identity.Bff' -Quiet) { dotnet build src/NCode.Identity.Bff/NCode.Identity.Bff.csproj; exit $LASTEXITCODE } else { exit 1 }`

### [x] 3. Create the NCode.Identity.Bff.Tests project with dependency tests
- Files: `tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj` (new), `tests/NCode.Identity.Bff.Tests/BaseTests.cs` (new), `tests/NCode.Identity.Bff.Tests/MutableTimeProvider.cs` (new), `tests/NCode.Identity.Bff.Tests/BffPackageDependencyTests.cs` (new), `NCode.Identity.slnx` (edit)
- Change: Copy `tests/NCode.Identity.OpenId.Client.Tests/NCode.Identity.OpenId.Client.Tests.csproj`, with `RootNamespace`
  `NCode.Identity.Bff.Tests`.
  - Project references: `NCode.Identity.Bff`, `NCode.Identity.OpenId.Client`, `NCode.Identity.SharedState`,
    `NCode.Identity.Events`, `NCode.Identity.Secrets`, `NCode.Identity.Jose`, `NCode.Identity.Jwt`.
  - Copy `BaseTests` and `MutableTimeProvider` from the client tests, changing the namespace.
  - `BffPackageDependencyTests` has two tests, following `ProtocolPackageDependencyTests`:
    - `BffAssemblies_ReferenceNoServerPackage`, over `typeof(BffLibrary).Assembly` and
      `Assembly.Load(new AssemblyName("NCode.Identity.Bff"))`: every referenced `NCode.Identity.OpenId.*` name is
      `NCode.Identity.OpenId.Client.Abstractions` or `NCode.Identity.OpenId.Protocol.Abstractions`.
    - `BffAssembly_ReferencesOnlyAbstractionPackages`: every referenced `NCode.Identity.*` name ends with `.Abstractions`.
  - Add the project to the slnx `/tests/` folder.
- Pattern: `tests/NCode.Identity.OpenId.Client.Tests/ProtocolPackageDependencyTests.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffPackageDependencyTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 4. Add the BFF name constants
- Files: `src/NCode.Identity.Bff.Abstractions/BffDefaults.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffProblemTypes.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffSessionEndReasons.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffLoginFailureReasons.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/BffDefaultsTests.cs` (new)
- Change: Add four public static classes of `public const string`s, each documented as the known values of an open
  vocabulary.
  - `BffDefaults`: `AuthenticationScheme="Bff"`, `EndpointPrefix="/bff"`, `SessionCookieName="__Host-Http-bff"`,
    `LoginCookieName="__Host-Http-bff-login"`, `AntiforgeryHeaderName="X-CSRF"`, `AntiforgeryHeaderValue="1"`,
    `FrontendName="default"`, `ConfigurationSectionName="Bff"`.
  - `BffProblemTypes`: `urn:ncode:identity:bff:` plus `session_required`, `antiforgery_header_missing`,
    `cross_site_request`, `forbidden`, `store_unavailable`, `token_unavailable` and `invalid_request`. The members are
    named `SessionRequired`, `AntiforgeryHeaderMissing`, `CrossSiteRequest`, `Forbidden`, `StoreUnavailable`,
    `TokenUnavailable` and `InvalidRequest`.
  - `BffSessionEndReasons`: `LoggedOut="logged_out"`, `IdleTimeout="idle_timeout"`, `AbsoluteExpiry="absolute_expiry"`,
    `RefreshFailed="refresh_failed"`, `BackchannelLogout="backchannel_logout"`, `Replaced="replaced"`,
    `EndedByValidator="ended_by_validator"`.
  - `BffLoginFailureReasons`: `UnknownState`, `InitiatorMismatch`, `IssuerMismatch`, `AuthorizationError`,
    `AuthorizationRequestFailed`, `InvalidResponse`, `TokenError`, `IdTokenInvalid`, `NonceMismatch`, `RejectedByGate`.
    Each value is the snake_case of its name (`unknown_state`, …).
  - Test `BffDefaults_CookieNames_UseTheHostHttpPrefix`: both cookie names start with `__Host-Http-`.
- Pattern: `src/NCode.Identity.OpenId.Client.Abstractions/TokenValidationFailureCodes.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffDefaultsTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 5. Add the BFF audit event
- Files: `src/NCode.Identity.Bff.Abstractions/BffAuditEventNames.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffAuditEvent.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/BffAuditEventTests.cs` (new)
- Change:
  - `BffAuditEventNames` (public static) has `Source="bff"` and these names: `LoginSucceeded="bff.login.succeeded"`,
    `LoginFailed="bff.login.failed"`, `SessionEnded="bff.session.ended"`,
    `TokenRefreshFailed="bff.token.refresh_failed"`, `BackchannelLogoutReceived="bff.backchannel_logout.received"`,
    `BackchannelLogoutRejected="bff.backchannel_logout.rejected"`, `AntiforgeryRejected="bff.antiforgery.rejected"`.
  - `public sealed record BffAuditEvent : AuditEvent, ISupportAuditDetails`, with `[PublicAPI]`:
    - `public required string Name { get; init; }` and `public required string Category { get; init; }`.
    - `public string? Reason { get; init; }`.
    - `EventName => Name`, `EventCategory => Category`.
    - `WriteAuditDetails` writes the string `reason` only when `Reason` is not null.
  - Tests: `EventName` and `EventCategory` echo `Name` and `Category`. `WriteAuditDetails` writes `{"reason":"x"}` for
    `Reason="x"`, and nothing when it is null (write inside `WriteStartObject`/`WriteEndObject` and compare the JSON).
- Pattern: `src/NCode.Identity.OpenId.Authentication.Abstractions/Auditing/ClientAuthenticationFailedAuditEvent.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffAuditEventTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 6. Add the session record contracts
- Files: `src/NCode.Identity.Bff.Abstractions/BffSessionMetadata.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffSessionRecord.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffStoreUnavailableException.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/BffSessionRecordTests.cs` (new)
- Change:
  - `BffSessionMetadata` is a public sealed class with `{ get; init; }` properties, none of them `required`:
    - `Frontend`, `Issuer` and `Subject` (string, default `string.Empty`), and `SessionId` (`string?`, the OP `sid`).
    - `CreatedAt`, `LastAccessedAt`, `AbsoluteExpiresAt` and `IdleExpiresAt` (`DateTimeOffset`).
    - `[JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }`.
  - `BffSessionRecord(string storeKey, long version, BffSessionMetadata metadata, ReadOnlyMemory<byte> protectedPayload)`
    is a public sealed class with get-only properties. It throws on an empty store key, a negative version or a null
    metadata.
  - `BffStoreUnavailableException : Exception` is public sealed, with constructors `(string message)` and
    `(string message, Exception innerException)`. Its doc says that every `IBffSessionStore` throws it when the store
    cannot be reached.
  - Tests cover the record's guards and that its properties echo the constructor.
- Pattern: `src/NCode.Identity.OpenId.Client.Abstractions/ClientRegistration.cs` (guards), `OpenIdProviderException.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffSessionRecordTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 7. Add IBffSessionStore
- Files: `src/NCode.Identity.Bff.Abstractions/IBffSessionStore.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit)
- Change: Add a public interface with a full doc for each member. Its summary says the store holds only opaque
  ciphertext, and throws `BffStoreUnavailableException` when the store fails. Members (all `ValueTask`, all with a
  trailing `CancellationToken cancellationToken`):
  - `ValueTask<bool> TryCreateAsync(BffSessionRecord record, …)` adds the record at version 1, plus its `sid`/`sub`
    index members. It returns false when the key exists.
  - `ValueTask<BffSessionRecord?> GetAsync(string storeKey, …)`.
  - `ValueTask<bool> TryReplaceAsync(BffSessionRecord record, long expectedVersion, …)`.
  - `ValueTask RemoveAsync(string storeKey, BffSessionMetadata metadata, …)` removes the record and its index members.
  - `ValueTask<IReadOnlyList<string>> FindBySessionIdAsync(string issuer, string sessionId, …)` and
    `FindBySubjectAsync(string issuer, string subject, …)` return store keys.
  - `ValueTask<bool> TryAddTransactionAsync(string transactionKey, ReadOnlyMemory<byte> protectedTransaction, DateTimeOffset expiresWhen, …)`.
  - `ValueTask<ReadOnlyMemory<byte>?> TryTakeTransactionAsync(string transactionKey, …)`.
  - `ValueTask<bool> TryAcquireRefreshLeaseAsync(string leaseKey, DateTimeOffset expiresWhen, …)`,
    `ValueTask<bool> IsRefreshLeaseHeldAsync(string leaseKey, …)` and `ValueTask ReleaseRefreshLeaseAsync(string leaseKey, …)`.
- Pattern: `src/NCode.Identity.SharedState.Abstractions/IExpiringValueSet.cs`.
- Done when: `dotnet build src/NCode.Identity.Bff.Abstractions/NCode.Identity.Bff.Abstractions.csproj`

### [x] 8. Add BffOptions with client and antiforgery options
- Files: `src/NCode.Identity.Bff/Options/BffOptions.cs` (new), `src/NCode.Identity.Bff/Options/BffClientAuthenticationOptions.cs` (new), `src/NCode.Identity.Bff/Options/BffAntiforgeryOptions.cs` (new), `src/NCode.Identity.Bff/PublicAPI.Unshipped.txt` (edit)
- Change: These are public sealed classes in namespace `NCode.Identity.Bff.Options`, with `{ get; set; }` properties (the
  binder sets them).
  - `BffOptions`:
    - `Authority` and `ClientId` (string, `""`).
    - `ClientAuthentication` (`new()`).
    - `Scopes`, `Resources`, `ReturnUrls`, `UserClaims`, `LoginParameters` and `TrustedOrigins` (`List<string>`, `[]`).
    - `ErrorPath` (`"/"`) and `PathPrefix` (`BffDefaults.EndpointPrefix`).
    - `BackchannelLogoutSessionRequired` (`true`).
    - `Antiforgery`, `Session`, `DPoP` and `Tokens` (each `new()`). The last three types come in step 9; add those three
      properties in step 9.
  - `BffClientAuthenticationOptions`: `Method` (`OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt`) and
    `KeyRing` (`string?`).
  - `BffAntiforgeryOptions`: `HeaderName` (`BffDefaults.AntiforgeryHeaderName`) and `HeaderValue`
    (`BffDefaults.AntiforgeryHeaderValue`).
  - Each property's doc names its configuration key (`Bff:ClientAuthentication:KeyRing`).
- Pattern: `src/NCode.Identity.OpenId.Client/Options/OpenIdClientOptions.cs`.
- Done when: `dotnet build src/NCode.Identity.Bff/NCode.Identity.Bff.csproj`

### [x] 9. Add the session, DPoP and token options
- Files: `src/NCode.Identity.Bff/Options/BffSessionOptions.cs` (new), `src/NCode.Identity.Bff/Options/BffDPoPOptions.cs` (new), `src/NCode.Identity.Bff/Options/BffDPoPModes.cs` (new), `src/NCode.Identity.Bff/Options/BffTokenOptions.cs` (new), `src/NCode.Identity.Bff/Options/BffOptions.cs` (edit), `src/NCode.Identity.Bff/PublicAPI.Unshipped.txt` (edit)
- Change:
  - `BffSessionOptions`: `AbsoluteLifetime` (8 h), `IdleTimeout` (1 h), `SameSite` (`SameSiteMode.Strict`) and
    `LoginLifetime` (10 min).
  - `BffDPoPModes` (public static): `WhenSupported="when_supported"` and `Required="required"`.
  - `BffDPoPOptions`: `Mode` (`BffDPoPModes.WhenSupported`) and `KeyRing` (`string?`).
  - `BffTokenOptions`: `RefreshBeforeExpiry` (60 s) and `RefreshLeaseDuration` (40 s).
  - Add `Session`, `DPoP` and `Tokens` (each `new()`) to `BffOptions`.
- Pattern: step 8.
- Done when: `dotnet build src/NCode.Identity.Bff/NCode.Identity.Bff.csproj`

### [x] 10. Add the return-URL rules
- Files: `src/NCode.Identity.Bff/Navigation/BffReturnUrls.cs` (new), `tests/NCode.Identity.Bff.Tests/Navigation/BffReturnUrlsTests.cs` (new)
- Change: Add `internal static class BffReturnUrls` in namespace `NCode.Identity.Bff.Navigation`.
  - `bool IsWellFormedPath(string value)` is true only when all of these hold:
    - The value starts with `/` and not with `//`.
    - It has no `\`, `#`, whitespace or control character.
    - It has no `%2f`, `%5c` or `%2e` (case-insensitive).
    - No path segment before `?` is `.` or `..`.
  - `bool IsAllowed(string returnUrl, IReadOnlyList<string> allowList)` is true when `returnUrl` is well formed and its
    path (the part before `?`) equals an entry, or starts with the entry followed by `/`. An entry that ends with `/`
    (such as `/` itself) matches by prefix alone.
  - `[Theory]` tests cover: `/`, `/orders`, `/orders/1?x=1` allowed under `["/orders"]`; `/ordersx`, `//evil.com`,
    `/\evil`, `/a/../b`, `/%2e%2e/x`, `https://x/`, `orders` and `/a#b` refused; everything well formed allowed under `["/"]`.
- Pattern: none; pure functions.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffReturnUrlsTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 11. Validate the client, path and origin options
- Files: `src/NCode.Identity.Bff/Options/BffOptionsValidator.cs` (new), `tests/NCode.Identity.Bff.Tests/Options/BffOptionsValidatorTests.cs` (new)
- Change: Add `internal class BffOptionsValidator(IHostEnvironment environment) : IValidateOptions<BffOptions>`. It collects
  failures and returns `ValidateOptionsResult.Fail(failures)`; each message names the configuration key. Rules:
  - `Authority` is an absolute URI that starts with its own `https:` scheme and has no whitespace. `http:` is accepted
    only when `environment.IsDevelopment()`.
  - `ClientId` and `ClientAuthentication.Method` are not blank.
  - `ReturnUrls` is not empty, and each entry passes `BffReturnUrls.IsWellFormedPath` and has no `?`. `ErrorPath`
    passes the same check.
  - `PathPrefix` starts with `/`, does not end with `/`, and passes `IsWellFormedPath`.
  - Each `TrustedOrigins` entry equals `new Uri(entry).GetLeftPart(UriPartial.Authority)` exactly (no path or trailing
    `/`), and is `https` outside Development.
  - Tests: a valid baseline passes, and each rule has a failing case. Mock `IHostEnvironment` strictly, setting up
    `EnvironmentName`.
- Pattern: `src/NCode.Identity.OpenId.Client/Options/OpenIdClientOptionsValidator.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffOptionsValidatorTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 12. Validate the session, DPoP, token, antiforgery, claim and login-parameter options
- Files: `src/NCode.Identity.Bff/Options/BffOptionsValidator.cs` (edit), `tests/NCode.Identity.Bff.Tests/Options/BffOptionsValidatorTests.cs` (edit)
- Change: Add these rules to `BffOptionsValidator`, with one failing test each:
  - `Session.SameSite` is defined (`Enum.IsDefined`) and is `Strict` or `Lax`.
  - `IdleTimeout > 0`, and `AbsoluteLifetime >= IdleTimeout`.
  - `LoginLifetime` is between 1 and 30 minutes.
  - `DPoP.Mode` is one of `BffDPoPModes`, ordinal. `required` needs a non-blank `KeyRing`.
  - `Tokens.RefreshBeforeExpiry` is between 0 and 1 hour. `RefreshLeaseDuration` is between 10 s and 5 min.
  - `Antiforgery.HeaderName` and `HeaderValue` are not blank. `HeaderName` is not, ignoring case, one of `Accept`,
    `Accept-Language`, `Content-Language`, `Content-Type`, `Range`, `Cookie`, `Authorization` or `DPoP`.
  - `UserClaims` holds none of `iss`, `sid`, `nonce`, `at_hash`, `c_hash`, `s_hash`, `aud`, `azp`, `exp`, `iat`, `nbf`.
  - `LoginParameters` holds only `prompt`, `max_age`, `acr_values`, `login_hint` and `ui_locales` (the
    `OpenIdProtocol.Parameters` constants).
- Pattern: step 11.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffOptionsValidatorTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 13. Add key derivation for handles, states and initiator secrets
- Files: `src/NCode.Identity.Bff/Sessions/BffKeyDerivation.cs` (new), `tests/NCode.Identity.Bff.Tests/Sessions/BffKeyDerivationTests.cs` (new)
- Change: Add `internal static class BffKeyDerivation` in namespace `NCode.Identity.Bff.Sessions`.
  - Consts: `SessionIdPurpose="ncode.bff.session.id"`, `SessionPayloadPurpose="ncode.bff.session.payload"`,
    `LoginIdPurpose="ncode.bff.login.id"`, `LoginPayloadPurpose="ncode.bff.login.payload"`, `SecretLength=43`.
  - `string CreateSecret()`: `RandomNumberGenerator.Fill` into a `stackalloc byte[32]`, then `Base64Url.Encode`.
  - `bool TryDecodeSecret(string? secret, Span<byte> destination32)`: the secret is exactly 43 characters and decodes to
    32 bytes. A `FormatException` returns false.
  - `bool TryDeriveKey(string? secret, string purpose, Span<byte> destination32)`:
    `HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, destination, salt: [], info: UTF-8 purpose)`. Zero the decoded
    secret in a `finally`.
  - `bool TryDeriveIdentifier(string? secret, string purpose, [NotNullWhen(true)] out string? identifier)` returns the
    base64url of the derived key.
  - `string Fingerprint(string value)`: base64url(SHA-256(UTF-8 value)).
  - Tests: a secret is 43 characters and decodes. A derivation is deterministic, and differs per purpose. A 42-character,
    44-character or illegal-character secret is refused.
- Pattern: `src/NCode.Identity.OpenId.Client/Authorization/DefaultAuthorizationRequestClient.cs` (PKCE hashing).
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffKeyDerivationTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 14. Add the AES-256-GCM payload protector
- Files: `src/NCode.Identity.Bff/Sessions/BffPayloadProtector.cs` (new), `tests/NCode.Identity.Bff.Tests/Sessions/BffPayloadProtectorTests.cs` (new)
- Change: Add `internal static class BffPayloadProtector`.
  - `byte[] Protect(string secret, string purpose, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData)`:
    - Derive the key into `stackalloc byte[32]` with `BffKeyDerivation.TryDeriveKey`. On false, throw `ArgumentException`.
    - The output is `new byte[12 + plaintext.Length + 16]`: a random 12-byte nonce, then the ciphertext, then the tag.
    - Encrypt with `using var aes = new AesGcm(key, 16)`. Zero the key in a `finally`.
  - `bool TryUnprotect(string secret, string purpose, ReadOnlySpan<byte> protectedData, ReadOnlySpan<byte> associatedData, IBufferWriter<byte> plaintext)`:
    - Return false for a bad secret, an input shorter than 28 bytes, or an `AuthenticationTagMismatchException`
      (catch `CryptographicException`). Otherwise decrypt into `plaintext.GetSpan(n)` and `Advance(n)`.
  - Tests: a round trip. A wrong secret, a wrong purpose, a changed associated data, a flipped ciphertext byte and a
    truncated input each return false. Use `ArrayBufferWriter<byte>` in the tests.
- Pattern: `src/NCode.Identity.Jose/Algorithms/AuthenticatedEncryption/AesGcmAuthenticatedEncryptionAlgorithm.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffPayloadProtectorTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 15. Add the encrypted session payload types and the JSON context
- Files: `src/NCode.Identity.Bff/Sessions/BffSessionPayload.cs` (new), `src/NCode.Identity.Bff/Sessions/BffStoredClaim.cs` (new), `src/NCode.Identity.Bff/Sessions/BffStoredAccessToken.cs` (new), `src/NCode.Identity.Bff/Serialization/BffJsonContext.cs` (new), `tests/NCode.Identity.Bff.Tests/Serialization/BffJsonContextTests.cs` (new)
- Change: These are internal sealed records with `{ get; init; }` properties and no `required` members.
  - `BffStoredClaim`: `Type`, `Value`, `ValueType` (strings).
  - `BffStoredAccessToken`: `Requirement`, `Resource?`, `Scope?`, `Value`, `TokenType`, `ExpiresAt`
    (`DateTimeOffset`), `DPoPJwkThumbprint?`.
  - `BffSessionPayload`:
    - `List<BffStoredClaim> Claims` (`[]`), `IdToken` (`""`), `RefreshToken?`, `List<BffStoredAccessToken> AccessTokens`
      (`[]`), `DPoPJwkThumbprint?` and `LogoutGuard` (`""`).
    - `[JsonExtensionData] Dictionary<string, JsonElement>? ExtensionData { get; set; }`.
  - `internal sealed partial class BffJsonContext : JsonSerializerContext`, with
    `[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]`
    and `[JsonSerializable]` for `BffSessionMetadata` and `BffSessionPayload`.
  - Tests: a payload round-trips through `BffJsonContext.Default.BffSessionPayload`, unknown members survive in
    `ExtensionData`, and the metadata JSON uses `absolute_expires_at`.
- Pattern: `src/NCode.Identity.OpenId.Client.Abstractions/TokenResponse.cs` (extension data).
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffJsonContextTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 16. Add the stored-value envelope serializer
- Files: `src/NCode.Identity.Bff/Sessions/BffSessionRecordSerializer.cs` (new), `tests/NCode.Identity.Bff.Tests/Sessions/BffSessionRecordSerializerTests.cs` (new)
- Change: Add `internal static class BffSessionRecordSerializer` with `private const byte FormatVersion = 1`.
  - `byte[] SerializeMetadata(BffSessionMetadata metadata)` uses
    `JsonSerializer.SerializeToUtf8Bytes(metadata, BffJsonContext.Default.BffSessionMetadata)`.
  - `byte[] Serialize(BffSessionMetadata metadata, ReadOnlySpan<byte> protectedPayload)` writes the format byte, the
    metadata length (4 bytes, `BinaryPrimitives.WriteInt32BigEndian`), the metadata JSON, then the payload.
  - `bool TryDeserialize(ReadOnlyMemory<byte> value, [NotNullWhen(true)] out BffSessionMetadata? metadata, out ReadOnlyMemory<byte> protectedPayload)`
    returns false for a wrong format byte, a length out of range, or a `JsonException`.
  - Tests: a round trip, a wrong format byte, a truncated length, and a length past the end.
- Pattern: none.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffSessionRecordSerializerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 17. Add the default session store's record operations
- Files: `src/NCode.Identity.Bff/Sessions/BffStoreKeys.cs` (new), `src/NCode.Identity.Bff/Sessions/DefaultBffSessionStore.cs` (new), `tests/NCode.Identity.Bff.Tests/Sessions/DefaultBffSessionStoreTests.cs` (new)
- Change:
  - `internal static class BffStoreKeys`:
    - `Session(k)` is `"bff:session:"+k`, `Transaction(k)` is `"bff:txn:"+k` and `Lease(k)` is `"bff:lease:"+k`.
    - `SessionIdIndex(issuer, sid)` is `"bff:sid:"+Digest(issuer, sid)` and `SubjectIndex(issuer, sub)` is
      `"bff:sub:"+Digest(issuer, sub)`.
    - `Digest` is base64url(SHA-256) of each value's UTF-8 bytes, each prefixed by its length as a 4-byte big-endian int.
  - `internal class DefaultBffSessionStore(IExpiringValueSet values, IExpiringIndexSet indexes) : IBffSessionStore`
    implements in this step:
    - `TryCreateAsync`: `values.TryAddAsync(Session(key), Serialize(...), Min(idle, absolute))`. On true, add the
      record's key to the `sid` index (when `SessionId` is set) and to the `sub` index, each expiring at
      `AbsoluteExpiresAt`.
    - `GetAsync`: return a record with the stored version, or null when absent or when `TryDeserialize` fails.
    - `TryReplaceAsync`: replace with expiry `Min(idle, absolute)`.
    - `RemoveAsync`: remove the value, then `RemoveMemberAsync` from both indexes.
    - Wrap every adapter exception except `OperationCanceledException` in `BffStoreUnavailableException`.
    - Leave the other members throwing `NotImplementedException` until step 18.
  - Strict-mock tests for each method, plus a test that an adapter exception becomes `BffStoreUnavailableException`.
- Pattern: `src/NCode.Identity.SharedState/InProcess/DefaultInProcessExpiringValueSet.cs` (port semantics).
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffSessionStoreTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 18. Add the default store's lookups, transactions and leases
- Files: `src/NCode.Identity.Bff/Sessions/DefaultBffSessionStore.cs` (edit), `tests/NCode.Identity.Bff.Tests/Sessions/DefaultBffSessionStoreTests.cs` (edit)
- Change: Implement the remaining members, all wrapped as in step 17:
  - `FindBySessionIdAsync` and `FindBySubjectAsync` return `indexes.GetMembersAsync(<index key>)`.
  - `TryAddTransactionAsync` is `values.TryAddAsync(Transaction(k), bytes, expiresWhen)`.
  - `TryTakeTransactionAsync` is `values.TryTakeAsync(Transaction(k))`; return its `Value` or null.
  - `TryAcquireRefreshLeaseAsync` is `values.TryAddAsync(Lease(k), "1"u8.ToArray(), expiresWhen)`.
  - `IsRefreshLeaseHeldAsync` is `values.GetAsync(Lease(k)) is not null`.
  - `ReleaseRefreshLeaseAsync` is `values.RemoveAsync(Lease(k))`.
  - Add a strict-mock test for each member.
- Pattern: step 17.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffSessionStoreTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 19. Add the session codec
- Files: `src/NCode.Identity.Bff/Sessions/BffSessionCodec.cs` (new), `tests/NCode.Identity.Bff.Tests/Sessions/BffSessionCodecTests.cs` (new)
- Change: Add `internal static class BffSessionCodec`.
  - `bool TryGetStoreKey(string? handle, [NotNullWhen(true)] out string? storeKey)` calls
    `BffKeyDerivation.TryDeriveIdentifier(handle, SessionIdPurpose, …)`.
  - `BffSessionRecord Encode(string handle, long version, BffSessionMetadata metadata, BffSessionPayload payload)`:
    - Throw `ArgumentException` for a bad handle.
    - Serialize the payload with `JsonSerializer.Serialize(Utf8JsonWriter, payload, BffJsonContext.Default.BffSessionPayload)`
      into `BufferFactory.CreatePooledBufferWriter(isSensitive: true)`, and dispose it.
    - The associated data is UTF-8(storeKey), then the version as 8 bytes big-endian, then `SerializeMetadata(metadata)`.
    - Protect with `SessionPayloadPurpose`.
  - `bool TryDecode(string handle, BffSessionRecord record, [NotNullWhen(true)] out BffSessionPayload? payload)`:
    - Recompute the associated data from `record.StoreKey`, `record.Version` and `record.Metadata`.
    - Unprotect into a sensitive pooled writer, and deserialize from a contiguous span (`GetSpanLease(isSensitive: true)`).
  - Tests: a round trip. Decoding fails with another handle, with the version changed, and with `IdleExpiresAt`
    changed in the metadata.
- Pattern: ADR-0116 buffers as used in `src/NCode.Identity.OpenId.Client/Tokens/ClientRequestSender.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffSessionCodecTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 20. Add the audit recorder
- Files: `src/NCode.Identity.Bff/Auditing/BffAuditRequestFactory.cs` (new), `src/NCode.Identity.Bff/Auditing/IBffAuditRecorder.cs` (new), `src/NCode.Identity.Bff/Auditing/DefaultBffAuditRecorder.cs` (new), `tests/NCode.Identity.Bff.Tests/Auditing/DefaultBffAuditRecorderTests.cs` (new)
- Change:
  - `BffAuditRequestFactory.Create(HttpContext)` copies the logic of `AuditRequestFactory.Create` in
    `src/NCode.Identity.OpenId.Authentication.Abstractions/Auditing/AuditRequestFactory.cs`.
  - `internal interface IBffAuditRecorder` has these methods, each returning `ValueTask` with a trailing `CancellationToken`:
    - `LoginSucceededAsync(HttpContext, string subject)`: category `authentication`, actor `EndUser(subject)`, `success`.
    - `LoginFailedAsync(HttpContext, string reason)`: `authentication`, `Anonymous`, `failure`, `Error = new AuditError(reason)`.
    - `SessionEndedAsync(HttpContext?, string subject, string reason)`: `session`, `success`, `Reason = reason`. The
      actor is `EndUser(subject)` when the reason is `logged_out`, and `System` otherwise.
    - `TokenRefreshFailedAsync(HttpContext, string subject, string reason)`: `token`, `EndUser`, `failure`, error = reason.
    - `BackchannelLogoutReceivedAsync(HttpContext)`: `session`, `System`, `success`.
    - `BackchannelLogoutRejectedAsync(HttpContext, string reason)`: `session`, `Anonymous`, `failure`, error = reason.
    - `AntiforgeryRejectedAsync(HttpContext, string reason)`: `protocol`, `Anonymous`, outcome `AuditOutcome.Denied`, error = reason.
  - `DefaultBffAuditRecorder(IEventPublisher publisher, IOptions<BffOptions> options, TimeProvider timeProvider)`
    publishes a `BffAuditEvent`:
    - `EventId = Guid.CreateVersion7(now)`, `EventTime = now`, `EventSource = BffAuditEventNames.Source`.
    - `ClientId = options.ClientId`, `Request = httpContext is null ? null : BffAuditRequestFactory.Create(httpContext)`.
    - Use the `AuditEventCategories` constants.
  - Tests: capture the published event with a strict `IEventPublisher` setup, and assert its name, category, outcome,
    actor and reason for two methods.
- Pattern: `src/NCode.Identity.OpenId.Authentication/Auditing/DefaultAuditEventRecorder.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffAuditRecorderTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 21. Add the session manager's create and load
- Files: `src/NCode.Identity.Bff/Sessions/BffSessionState.cs` (new), `src/NCode.Identity.Bff/Sessions/IBffSessionManager.cs` (new), `src/NCode.Identity.Bff/Sessions/DefaultBffSessionManager.cs` (new), `tests/NCode.Identity.Bff.Tests/Sessions/DefaultBffSessionManagerTests.cs` (new)
- Change:
  - `internal sealed record BffSessionState(string Handle, string StoreKey, long Version, BffSessionMetadata Metadata, BffSessionPayload Payload)`.
  - `internal interface IBffSessionManager`:
    - `ValueTask<BffSessionState> CreateAsync(BffSessionMetadata metadata, BffSessionPayload payload, CancellationToken)`.
    - `ValueTask<BffSessionState?> LoadAsync(string handle, CancellationToken)`.
    - `ValueTask<BffSessionState?> UpdateAsync(BffSessionState current, Func<BffSessionState, BffSessionState?> update, CancellationToken)`.
    - `ValueTask EndAsync(HttpContext? httpContext, string storeKey, BffSessionMetadata metadata, string reason, CancellationToken)`.
  - `DefaultBffSessionManager(IBffSessionStore store, IBffAuditRecorder recorder)` implements two members in this step.
    Leave `UpdateAsync` and `EndAsync` throwing `NotImplementedException` until step 22.
    - `CreateAsync`: `CreateSecret()` gives a handle; encode at version 1; `TryCreateAsync`. On false, retry once with a
      new handle, then throw `InvalidOperationException`.
    - `LoadAsync`: `TryGetStoreKey` (false gives null), then `GetAsync` (null gives null), then `TryDecode` (false gives
      null). Return the state.
  - Tests with a strict `IBffSessionStore`: create stores version 1; load returns null for an absent record, for an
    undecodable one and for a malformed handle; load round-trips a record from `BffSessionCodec.Encode`.
- Pattern: step 17 tests.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffSessionManagerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 22. Add the session manager's read-modify-write update and end
- Files: `src/NCode.Identity.Bff/Sessions/DefaultBffSessionManager.cs` (edit), `tests/NCode.Identity.Bff.Tests/Sessions/DefaultBffSessionManagerTests.cs` (edit)
- Change:
  - `UpdateAsync` runs at most 5 attempts:
    - `next = update(state)`. A null `next` returns `state` unchanged.
    - Encode `next` at `state.Version + 1`, then `TryReplaceAsync(record, state.Version)`.
    - On true, return `next with { Version = state.Version + 1 }`.
    - On false, `state = await LoadAsync(current.Handle)`. A null state returns null.
    - After the 5th lost attempt, throw `BffStoreUnavailableException("The session changed too often to be updated.")`.
  - `EndAsync` calls `store.RemoveAsync(storeKey, metadata)`, then `recorder.SessionEndedAsync(httpContext, metadata.Subject, reason)`.
  - Tests: a first-try success; a conflict followed by a reload and success (`SetupSequence`, a new object per read);
    a reload that finds nothing returns null; five conflicts throw; end removes and records.
- Pattern: `csharp-testing.instructions.md` "A strict-mock test of a read-and-retry loop returns a new object for each read".
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffSessionManagerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 23. Add the cookie writer
- Files: `src/NCode.Identity.Bff/Cookies/BffCookies.cs` (new), `tests/NCode.Identity.Bff.Tests/Cookies/BffCookiesTests.cs` (new)
- Change: Add `internal class BffCookies(IOptions<BffOptions> options, TimeProvider timeProvider)` with:
  - `string? ReadSessionHandle(HttpRequest)` and `string? ReadLoginSecret(HttpRequest)`. Each returns the cookie only
    when it is exactly 43 characters.
  - `void SetSession(HttpResponse, string handle, DateTimeOffset absoluteExpiresAt)`, with
    `MaxAge = absoluteExpiresAt - now` and `SameSite = options.Session.SameSite`.
  - `void DeleteSession(HttpResponse)`.
  - `void SetLogin(HttpResponse, string secret)` (`SameSite.Lax`, `MaxAge = Session.LoginLifetime`) and
    `void DeleteLogin(HttpResponse)`.
  - Every cookie is `Secure`, `HttpOnly`, `Path="/"`, with no `Domain`. A delete passes the same attributes.
  - Tests read `Set-Cookie` from a `DefaultHttpContext`. They check the name, `secure`, `httponly`, `samesite=strict`
    (`lax` for login), `path=/`, `max-age=` and no `domain=`. They also check that a 42-character value reads as null.
- Pattern: none.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffCookiesTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 24. Register the BFF library and its configuration
- Files: `src/NCode.Identity.Bff/DefaultRegistration.cs` (new), `src/NCode.Identity.Bff/BffLibraryRegistration.cs` (new), `src/NCode.Identity.Bff/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/DefaultRegistrationTests.cs` (new)
- Change:
  - `DefaultRegistration` (public static, `[PublicAPI]`) has `extension(IServiceBuilder<OpenIdClientLibrary> builder)`
    with `public IServiceBuilder<BffLibrary> AddBffLibrary(IServiceBuilder<EventsLibrary> events)`. It null-guards
    both, then calls `NewBuilder<BffLibrary>()` and registers:
    - `TryAddSingleton(TimeProvider.System)` and `AddHttpContextAccessor()`.
    - `AddOptions<BffOptions>().ValidateOnStart()` and the `IValidateOptions<BffOptions>` → `BffOptionsValidator`
      enumerable.
    - `TryAddSingleton` of `IBffSessionStore`→`DefaultBffSessionStore`, `IBffSessionManager`→`DefaultBffSessionManager`,
      `IBffAuditRecorder`→`DefaultBffAuditRecorder` and `BffCookies`.
    - Later steps add their services here.
  - `BffLibraryRegistration` has `extension(IServiceBuilder<BffLibrary> builder)` with
    `AddBffConfiguration(IConfiguration configuration)`, which calls
    `AddOptions<BffOptions>().Configure(o => configuration.GetSection(BffDefaults.ConfigurationSectionName).BindStrictly(o))`.
  - Tests build `new ServiceCollection()` with `AddLogging()`,
    `AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddOpenIdClientLibrary().AddBffLibrary(services.AddEventServices())`
    and the in-process value and index sets. They assert:
    - `IBffSessionStore` and `IBffSessionManager` resolve.
    - A second `AddBffLibrary` throws `InvalidOperationException`.
    - An unknown key under `Bff` makes `IOptions<BffOptions>.Value` throw.
- Pattern: `src/NCode.Identity.OpenId.Client/DefaultRegistration.cs`, `tests/NCode.Identity.OpenId.Client.Tests/DefaultRegistrationTests.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~NCode.Identity.Bff.Tests.DefaultRegistrationTests" -- RunConfiguration.TreatNoTestsAsError=true`

### Slice (b): scheme handler, login, callback and /bff/user (§5.2, §5.7)

### [x] 25. Add the client registration provider and navigation helpers
- Files: `src/NCode.Identity.Bff/Client/BffClientRegistrationProvider.cs` (new), `src/NCode.Identity.Bff/Navigation/BffNavigation.cs` (new), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `tests/NCode.Identity.Bff.Tests/Client/BffClientRegistrationProviderTests.cs` (new), `tests/NCode.Identity.Bff.Tests/Navigation/BffNavigationTests.cs` (new)
- Change:
  - `internal class BffClientRegistrationProvider(IOptions<BffOptions> options)` exposes `ClientRegistration Registration`.
    It is built once through `Lazy<T>` (the options are fixed at start):
    `new(Authority, ClientId, ClientAuthentication.Method) { KeyRing = ClientAuthentication.KeyRing, DPoPKeyRing = DPoP.KeyRing, DPoPBoundAccessTokens = DPoP.Mode == Required, BackchannelLogoutSessionRequired = options.BackchannelLogoutSessionRequired }`.
  - `internal static class BffNavigation`:
    - `string CallbackUri(HttpRequest, BffOptions)` is `$"{Scheme}://{Host}{PathBase}{PathPrefix}/callback"`.
    - `string LocalUrl(HttpRequest, string path)` is `PathBase + path`.
    - `string ErrorUrl(HttpRequest, BffOptions, string reason)` is `LocalUrl(ErrorPath) + "?error=" + Uri.EscapeDataString(reason)`.
    - `string AbsoluteUrl(HttpRequest, string path)` is `$"{Scheme}://{Host}{PathBase}{path}"`.
  - Register the provider.
  - Tests: the registration's properties for `required` and for `when_supported`; each URL for a request with
    `PathBase="/app"`.
- Pattern: none.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffClientRegistrationProviderTests|FullyQualifiedName~BffNavigationTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 26. Add the problem and no-store writers
- Files: `src/NCode.Identity.Bff/Responses/BffProblem.cs` (new), `src/NCode.Identity.Bff/Responses/BffResponses.cs` (new), `src/NCode.Identity.Bff/Serialization/BffJsonContext.cs` (edit), `tests/NCode.Identity.Bff.Tests/Responses/BffResponsesTests.cs` (new)
- Change:
  - `internal sealed record BffProblem(string Type, string Title, int Status)`, with `[JsonSerializable(typeof(BffProblem))]`
    added to `BffJsonContext`.
  - `internal static class BffResponses`:
    - `void SetNoStore(HttpResponse)` sets `Cache-Control: no-store`.
    - `Task WriteProblemAsync(HttpContext, int status, string type, string title)` calls `SetNoStore`, sets the status
      code, then
      `Response.WriteAsJsonAsync(problem, BffJsonContext.Default.BffProblem, "application/problem+json", RequestAborted)`.
  - Test: writing a `401` problem to a `DefaultHttpContext` whose `Response.Body` is a `MemoryStream` gives status 401,
    the content type, `no-store`, and JSON with `type`, `title` and `status`.
- Pattern: none.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffResponsesTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 27. Add the login transaction and its service
- Files: `src/NCode.Identity.Bff/Login/BffLoginTransaction.cs` (new), `src/NCode.Identity.Bff/Login/BffLoginTransactionService.cs` (new), `src/NCode.Identity.Bff/Serialization/BffJsonContext.cs` (edit), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `tests/NCode.Identity.Bff.Tests/Login/BffLoginTransactionServiceTests.cs` (new)
- Change:
  - `internal sealed record BffLoginTransaction` has init properties `Issuer`, `RedirectUri`, `CodeVerifier`, `Nonce`
    and `ReturnUrl` (strings), plus `DPoPJwkThumbprint?` and `int? MaxAge`. Add it to `BffJsonContext`.
  - `internal class BffLoginTransactionService(IBffSessionStore store)`:
    - `ValueTask StoreAsync(string state, string initiatorSecret, BffLoginTransaction transaction, DateTimeOffset expiresWhen, CancellationToken)`:
      the key is `TryDeriveIdentifier(state, LoginIdPurpose)`. Protect the JSON with
      `(initiatorSecret, LoginPayloadPurpose, json, UTF-8 key)`. If `TryAddTransactionAsync` returns false, throw
      `InvalidOperationException`.
    - `ValueTask<BffLoginTransactionResult> TakeAsync(string? state, string? initiatorSecret, CancellationToken)`, where
      `internal readonly record struct BffLoginTransactionResult(BffLoginTransaction? Transaction, string? FailureReason)`
      lives in the same file. Taking happens before decrypting.
      - A bad state gives `unknown_state` without calling the store. No stored value also gives `unknown_state`.
      - A missing initiator, a failed unprotect or bad JSON gives `initiator_mismatch`.
  - Register the service. Tests: a store then a take round-trips; a second take is `unknown_state`; another initiator is
    `initiator_mismatch`. Use the real `DefaultBffSessionStore` over in-process `IExpiringValueSet`/`IExpiringIndexSet`
    from a `ServiceCollection` (`AddSharedStateServices().AddInProcessExpiringValueSet().AddInProcessExpiringIndexSet()`).
- Pattern: steps 14 and 19.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffLoginTransactionServiceTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 28. Add the login handler's request validation and authorization request
- Files: `src/NCode.Identity.Bff/Login/BffLoginHandler.cs` (new), `tests/NCode.Identity.Bff.Tests/Login/BffLoginHandlerTests.cs` (new)
- Change: Add `internal class BffLoginHandler`. Its constructor takes `IOptions<BffOptions>`,
  `BffClientRegistrationProvider`, `IAuthorizationRequestClient`, `BffLoginTransactionService`, `BffCookies`,
  `IBffAuditRecorder`, `IHostEnvironment` and `TimeProvider`. `public async Task HandleAsync(HttpContext httpContext)`
  does this in order:
  - `SetNoStore`.
  - `returnUrl` is the query value, `"/"` when absent. More than one value, or a value that is not
    `BffReturnUrls.IsAllowed`, answers a `400` problem (`InvalidRequest`).
  - Each name in `LoginParameters` with exactly one query value goes into a `Dictionary<string,string>`. Two values
    answer `400`. A `max_age` that is not a non-negative integer (`int.TryParse`, invariant) answers `400`.
  - `redirectUri = BffNavigation.CallbackUri`. When its scheme is not `https` and the environment is not Development,
    throw `InvalidOperationException`.
  - `state`, `initiator`, `verifier` and `nonce` each come from `BffKeyDerivation.CreateSecret()`.
  - Call `CreateAuthorizationUriAsync(registration, new AuthorizationRequest(redirectUri, state, verifier) { … }, RequestAborted)`
    with `Nonce`, `Scope = "openid " + configured scopes (distinct, ordinal) + " offline_access"`,
    `ResponseMode = OpenIdProtocol.ResponseModes.Query`, `Resources = [.. Resources]` and `AdditionalParameters`
    (null when empty).
  - An error result, or an `OpenIdProviderException`, records `LoginFailedAsync(authorization_request_failed)` and
    redirects to `BffNavigation.ErrorUrl`.
  - Tests: an unlisted return URL answers 400 with no client call; a duplicate parameter answers 400; a client error
    redirects to the error URL. A captured request carries the scope, nonce and `prompt` when `prompt` is allow-listed.
    Return an error result from the client in that last test.
- Pattern: `tests/NCode.Identity.OpenId.Client.Tests/Authorization/DefaultAuthorizationRequestClientTests.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffLoginHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 29. Store the login transaction and redirect to the provider
- Files: `src/NCode.Identity.Bff/Login/BffLoginHandler.cs` (edit), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `tests/NCode.Identity.Bff.Tests/Login/BffLoginHandlerTests.cs` (edit)
- Change: After a successful result:
  - Build a `BffLoginTransaction`: `Issuer = registration.Authority`, `RedirectUri`, `CodeVerifier`, `Nonce`,
    `ReturnUrl`, `DPoPJwkThumbprint = result.DPoPJwkThumbprint`, and the parsed `MaxAge`.
  - `StoreAsync(state, initiator, txn, now + Session.LoginLifetime)`, then `cookies.SetLogin(Response, initiator)`.
  - Redirect 302 to `result.AuthorizationUri.AbsoluteUri`.
  - Register `BffLoginHandler`.
  - Test: a success answers 302 to the client's URI and sets the `__Host-Http-bff-login` cookie. A follow-up
    `TakeAsync` with the captured `state` and the cookie value returns the transaction with the return URL. Use the
    real transaction service as in step 27.
- Pattern: step 28.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffLoginHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 30. Expose the ID token's claims identity from the protocol client
- Files: `src/NCode.Identity.OpenId.Client.Abstractions/IdTokenValidationResult.cs` (edit), `src/NCode.Identity.OpenId.Client.Abstractions/PublicAPI.Unshipped.txt` (edit), `src/NCode.Identity.OpenId.Client/Tokens/DefaultIdTokenValidator.cs` (edit), `tests/NCode.Identity.OpenId.Client.Tests/Tokens/DefaultIdTokenValidatorTests.cs` (edit)
- Change:
  - Add `public ClaimsIdentity? Identity { get; init; }` to `IdTokenValidationResult`. Its doc: "Gets the claims of the
    valid ID token, mapped by the JWT stack; otherwise, null."
  - Add the two PublicAPI lines: `….IdTokenValidationResult.Identity.get -> System.Security.Claims.ClaimsIdentity?` and
    `….Identity.init -> void`.
  - In `DefaultIdTokenValidator.ValidateAsync`, the final return becomes
    `var identity = await result.GetClaimsIdentityAsync(cancellationToken); return new IdTokenValidationResult(payload) { Identity = identity };`.
  - Test `ValidateAsync_WhenValid_ReturnsTheIdentity`, built like `ValidateAsync_WhenValid_ReturnsPayload`: the
    `Identity` has a `sub` claim equal to the token's subject.
- Pattern: `ValidateAsync_WhenValid_ReturnsPayload` in the same test file.
- Done when: `dotnet test tests/NCode.Identity.OpenId.Client.Tests/NCode.Identity.OpenId.Client.Tests.csproj --filter "FullyQualifiedName~DefaultIdTokenValidatorTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 31. Add the login gate contract
- Files: `src/NCode.Identity.Bff.Abstractions/IBffLoginGate.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffLoginGateContext.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit)
- Change:
  - `BffLoginGateContext(HttpContext httpContext, string issuer, ClaimsPrincipal principal)` is a public sealed class
    with get-only properties and null guards.
  - `public interface IBffLoginGate { ValueTask<bool> AllowAsync(BffLoginGateContext context, CancellationToken cancellationToken); }`.
    Its doc: runs after the ID token validates and before a session exists; it can only reject (false); every
    registered gate must allow.
- Pattern: `src/NCode.Identity.OpenId.Client.Abstractions/ClientAuthenticationContext.cs`.
- Done when: `dotnet build src/NCode.Identity.Bff.Abstractions/NCode.Identity.Bff.Abstractions.csproj`

### [x] 32. Add the callback handler's binding checks
- Files: `src/NCode.Identity.Bff/Login/BffCallbackHandler.cs` (new), `src/NCode.Identity.Bff/Logging/EventIds.cs` (new), `src/NCode.Identity.Bff/Logging/Log.cs` (new), `tests/NCode.Identity.Bff.Tests/Login/BffCallbackHandlerTests.cs` (new)
- Change:
  - `EventIds` (`Base = 19000`) and `Log` (`[ExcludeFromCodeCoverage]`) get
    `LoginFailed(this ILogger, string reason)` at Debug: "The BFF login failed: {Reason}".
  - `internal class BffCallbackHandler`. Its constructor takes `IOptions<BffOptions>`, `BffClientRegistrationProvider`,
    `BffLoginTransactionService`, `BffCookies`, `IProviderMetadataResolver`, `ITokenClient`, `IIdTokenValidator`,
    `IEnumerable<IBffLoginGate>`, `IBffSessionManager`, `IBffAuditRecorder`, `TimeProvider` and
    `ILogger<BffCallbackHandler>`. `Task HandleAsync(HttpContext)` does this in order:
    - `SetNoStore`, then `DeleteLogin`.
    - `TakeAsync(single query "state", ReadLoginSecret)`. A failure fails with its reason.
    - An `error` query value fails with `authorization_error`.
    - Get the metadata for `txn.Issuer`. When `AuthorizationResponseIssParameterSupported`, `iss` is required. When
      `iss` is present it must equal `txn.Issuer` (ordinal). Otherwise the callback fails with `issuer_mismatch`.
    - A missing or repeated `code` fails with `invalid_response`.
  - A private `FailAsync(HttpContext, string reason)` logs, calls `LoginFailedAsync` and redirects 302 to `ErrorUrl`.
    Until step 33, the success path throws `NotImplementedException`.
  - Tests, each asserting the redirect's `error` and the recorded reason: no state; a state with no transaction; an
    initiator mismatch; an `error` response; an `iss` mismatch; missing `iss` when supported; no code.
- Pattern: `src/NCode.Identity.OpenId.Client/Logging/Log.cs`, `EventIds.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffCallbackHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 33. Exchange the code, validate the ID token and run the login gates
- Files: `src/NCode.Identity.Bff/Login/BffCallbackHandler.cs` (edit), `tests/NCode.Identity.Bff.Tests/Login/BffCallbackHandlerTests.cs` (edit)
- Change: After the binding checks:
  - Call `RequestTokenAsync(registration, new AuthorizationCodeTokenRequest(code, txn.RedirectUri, txn.CodeVerifier) { DPoPJwkThumbprint = txn.DPoPJwkThumbprint }, RequestAborted)`.
    An error result, an `OpenIdProviderException`, or a response without `IdToken` or `AccessToken` fails with `token_error`.
  - Call `IIdTokenValidator.ValidateAsync(registration, new IdTokenValidationRequest { IdToken, Nonce = txn.Nonce, MaxAge = txn.MaxAge seconds, AccessToken, AuthorizationCode = code, State = state }, …)`.
    - `FailureCode == TokenValidationFailureCodes.NonceMismatch` fails with `nonce_mismatch`. Any other failure, or a
      null `Identity`, fails with `id_token_invalid`.
  - Build the principal with an internal static helper `BffSessionPrincipal` in `src/NCode.Identity.Bff/Sessions/` (add
    the file in this step):
    - `IReadOnlyList<BffStoredClaim> StoreClaims(ClaimsIdentity identity)` drops `nonce`, `at_hash`, `c_hash`, `s_hash`,
      `exp`, `iat`, `nbf`, `aud` and `azp`.
    - `ClaimsPrincipal Create(IReadOnlyList<BffStoredClaim> claims, string issuer)` builds
      `ClaimsIdentity(…, BffDefaults.AuthenticationScheme, "name", "role")`.
  - Run every gate. A false result fails with `rejected_by_gate`.
  - Tests: a token error, a nonce mismatch, an invalid ID token, and a gate rejection, each with the reason asserted.
    `StoreClaims` drops `nonce` and keeps `email`.
- Pattern: step 32.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffCallbackHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 34. Create the session at the callback
- Files: `src/NCode.Identity.Bff/Login/BffCallbackHandler.cs` (edit), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `tests/NCode.Identity.Bff.Tests/Login/BffCallbackHandlerTests.cs` (edit)
- Change: After the gates (with `now` in whole seconds):
  - `absolute = now + AbsoluteLifetime`. When `response.ExtensionData` has `refresh_token_expires_in` and its
    `TryGetWholeInt32` is greater than 0, cap `absolute` at `now + seconds`. Then `idle = Min(now + IdleTimeout, absolute)`.
  - Metadata: `Frontend = BffDefaults.FrontendName`, `Issuer = txn.Issuer`, `Subject = sub`,
    `SessionId = sid claim (JoseClaimNames.Payload.Sid)`, `CreatedAt = LastAccessedAt = now`, and the two expiries.
  - Payload:
    - `Claims`, `IdToken`, `RefreshToken`, `DPoPJwkThumbprint = tokenResult.DPoPJwkThumbprint` and
      `LogoutGuard = CreateSecret()`.
    - `AccessTokens` holds one entry: `Requirement = BffTokenRequirementNames.User` (step 44 adds it; use the literal
      `"user"` until then through a private const), `Resource = null`, `Scope = null`, `Value`,
      `TokenType = response.TokenType ?? Bearer`, `ExpiresAt = now + ExpiresIn` (or `now` when absent), and the `jkt`.
  - When the request's old session cookie loads, end it with `replaced`.
  - Call `CreateAsync`, then `SetSession(handle, absolute)`, then `LoginSucceededAsync(sub)`, then redirect 302 to
    `LocalUrl(txn.ReturnUrl)`. Register the handler.
  - Tests: success creates a session (capture the metadata and payload), sets the cookie and redirects to the return
    URL; `refresh_token_expires_in` caps the absolute expiry; an old session is ended with `replaced`.
- Pattern: step 33.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffCallbackHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 35. Add the session feature and validator contracts
- Files: `src/NCode.Identity.Bff.Abstractions/IBffSessionFeature.cs` (new), `src/NCode.Identity.Bff.Abstractions/IBffSessionValidator.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffSessionValidationContext.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit)
- Change:
  - `public interface IBffSessionFeature { BffSessionMetadata Metadata { get; } ClaimsPrincipal Principal { get; } }`,
    documented as the request's resolved session (`HttpContext.Features`).
  - `BffSessionValidationContext(HttpContext httpContext, BffSessionMetadata metadata, ClaimsPrincipal principal)` is a
    sealed class.
  - `public interface IBffSessionValidator { ValueTask<bool> ValidateAsync(BffSessionValidationContext context, CancellationToken cancellationToken); }`.
    It can only end a session (false ends it as `ended_by_validator`).
- Pattern: step 31.
- Done when: `dotnet build src/NCode.Identity.Bff.Abstractions/NCode.Identity.Bff.Abstractions.csproj`

### [x] 36. Add the BFF API attribute and the request feature
- Files: `src/NCode.Identity.Bff.Abstractions/BffApiAttribute.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit), `src/NCode.Identity.Bff/Sessions/BffRequestFeature.cs` (new), `src/NCode.Identity.Bff/Endpoints/BffUserEndpointMetadata.cs` (new)
- Change:
  - `[AttributeUsage(Class | Method)] public sealed class BffApiAttribute : AuthorizeAttribute`. Its constructor sets
    `AuthenticationSchemes = BffDefaults.AuthenticationScheme`. Its doc: marks a local BFF API, so the anti-forgery
    header, Fetch Metadata and a BFF session are required, and a challenge answers `401`.
  - `internal sealed class BffRequestFeature` has these settable properties: `Endpoint? CheckedEndpoint`,
    `bool IsResolved`, `BffSessionState? Session` and `ClaimsPrincipal? Principal`.
  - `internal sealed class BffUserEndpointMetadata` has `public static BffUserEndpointMetadata Instance { get; } = new();`.
  - Add the PublicAPI lines for the attribute, including its constructor.
- Pattern: none.
- Done when: `dotnet build src/NCode.Identity.Bff/NCode.Identity.Bff.csproj`

### [x] 37. Resolve the request's session and end expired sessions
- Files: `src/NCode.Identity.Bff/Sessions/BffSessionResolver.cs` (new), `src/NCode.Identity.Bff/Sessions/BffSessionFeature.cs` (new), `tests/NCode.Identity.Bff.Tests/Sessions/BffSessionResolverTests.cs` (new)
- Change:
  - `internal sealed class BffSessionFeature(BffSessionMetadata metadata, ClaimsPrincipal principal) : IBffSessionFeature`.
  - `internal class BffSessionResolver`. Its constructor takes `IBffSessionManager`, `BffCookies`,
    `IEnumerable<IBffSessionValidator>`, `IOptions<BffOptions>` and `TimeProvider`.
    `ValueTask<BffSessionState?> ResolveAsync(HttpContext httpContext, CancellationToken)` does this:
    - Get `BffRequestFeature`. When it is null, throw `InvalidOperationException("UseBff() must be called before UseAuthentication() and the BFF endpoints.")`.
    - When the endpoint has `BffApiAttribute` or `BffUserEndpointMetadata` and `feature.CheckedEndpoint` is not that
      endpoint, throw `InvalidOperationException("UseBff() must run after routing.")`.
    - When `feature.IsResolved`, return `feature.Session`.
    - No handle resolves to null. A null `LoadAsync` calls `DeleteSession` and resolves to null.
    - `now >= AbsoluteExpiresAt` ends the session with `absolute_expiry`, and `now >= IdleExpiresAt` ends it with
      `idle_timeout`. Each calls `DeleteSession` and resolves to null.
    - Store `feature.Session` and `feature.Principal` (from `BffSessionPrincipal.Create`), and set
      `httpContext.Features.Set<IBffSessionFeature>(new BffSessionFeature(...))`. Mark `IsResolved`.
  - Tests: no feature throws; an unchecked protected endpoint throws; no cookie gives null; a missing record deletes the
    cookie; absolute and idle expiry end the session; a valid session is memoized (one load for two calls).
- Pattern: steps 21 and 23.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffSessionResolverTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 38. Run the session validators and slide the idle window
- Files: `src/NCode.Identity.Bff/Sessions/BffSessionResolver.cs` (edit), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `tests/NCode.Identity.Bff.Tests/Sessions/BffSessionResolverTests.cs` (edit)
- Change: Before the session is stored on the feature:
  - Each `IBffSessionValidator` that returns false ends the session with `ended_by_validator`, deletes the cookie and
    resolves to null.
  - When the endpoint has `BffApiAttribute` and `now - LastAccessedAt >= IdleTimeout / 10`, call `UpdateAsync`. The
    update sets `LastAccessedAt = now` and `IdleExpiresAt = Min(now + IdleTimeout, AbsoluteExpiresAt)`. A null result
    deletes the cookie and resolves to null.
  - `/bff/user` and other endpoints never slide.
  - Register `BffSessionResolver`.
  - Tests: a rejecting validator ends the session; an API request past the threshold slides; one under the threshold
    does not; a `BffUserEndpointMetadata` request never slides.
- Pattern: step 37.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffSessionResolverTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 39. Add the BFF authentication handler
- Files: `src/NCode.Identity.Bff/Authentication/BffAuthenticationHandler.cs` (new), `tests/NCode.Identity.Bff.Tests/Authentication/BffAuthenticationHandlerTests.cs` (new)
- Change: Add `internal class BffAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions, ILoggerFactory loggerFactory, UrlEncoder encoder, BffSessionResolver resolver, IOptions<BffOptions> options) : AuthenticationHandler<AuthenticationSchemeOptions>`.
  - `HandleAuthenticateAsync`: resolve the session. Null gives `AuthenticateResult.NoResult()`. Otherwise return
    `Success(new AuthenticationTicket(feature.Principal, Scheme.Name))`.
  - `HandleChallengeAsync`:
    - An endpoint with `BffApiAttribute` answers a `401` problem (`SessionRequired`, "A BFF session is required.").
    - Otherwise redirect 302 to `LocalUrl(PathPrefix + "/login") + "?returnUrl=" + escape(path)`. Here `path` is
      `Request.Path + Request.QueryString` when `BffReturnUrls.IsAllowed`, and `"/"` otherwise.
  - `HandleForbiddenAsync`: a `[BffApi]` endpoint answers a `403` problem (`Forbidden`); otherwise call the base.
  - Tests: run the handler with `InitializeAsync(new AuthenticationScheme("Bff", null, typeof(BffAuthenticationHandler)), httpContext)`
    over a `DefaultHttpContext` whose feature is pre-resolved (`IsResolved = true`). Cover: success; no result; an API
    challenge giving 401 problem JSON; a page challenge giving a redirect with `returnUrl`; an API forbid giving 403.
- Pattern: ASP.NET Core `AuthenticationHandler<T>`; no in-repo example.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffAuthenticationHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 40. Add the /bff/user handler
- Files: `src/NCode.Identity.Bff/Endpoints/BffUserHandler.cs` (new), `tests/NCode.Identity.Bff.Tests/Endpoints/BffUserHandlerTests.cs` (new)
- Change: Add `internal class BffUserHandler(BffSessionResolver resolver, IOptions<BffOptions> options)` with
  `Task HandleAsync(HttpContext)`:
  - `SetNoStore`. No session answers a `401` problem (`SessionRequired`).
  - Otherwise write `application/json; charset=utf-8` with `Utf8JsonWriter` over `Response.BodyWriter`. The object:
    - `sub`, and `name` when there is a `name` claim.
    - `claims`: for each `UserClaims` entry that the principal has, one value as a string, several as an array. An
      entry the principal lacks is omitted.
    - `session`: `{ expiresAt: AbsoluteExpiresAt.ToUnixTimeSeconds(), idleExpiresAt: IdleExpiresAt.ToUnixTimeSeconds() }`.
    - `logoutUrl`: `LocalUrl(PathPrefix + "/logout") + "?sid=" + Uri.EscapeDataString(LogoutGuard)`.
  - The handler never writes a token, `iss` or `sid`.
  - Tests: no session gives 401; a session gives JSON with the allow-listed `email` and a two-value `role` array, no
    unlisted claim, and a `logoutUrl` that carries the guard.
- Pattern: none.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffUserHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 41. Map the BFF endpoints
- Files: `src/NCode.Identity.Bff/Endpoints/BffEndpointRouteBuilderExtensions.cs` (new), `src/NCode.Identity.Bff/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/Endpoints/BffEndpointRouteBuilderExtensionsTests.cs` (new)
- Change: Add a public static class with `extension(IEndpointRouteBuilder endpoints)` and
  `public RouteGroupBuilder MapBffEndpoints()`.
  - The prefix comes from `endpoints.ServiceProvider.GetRequiredService<IOptions<BffOptions>>().Value.PathPrefix`.
  - The group is `.AllowAnonymous().WithTags("Bff")`.
  - Map `GET /login` → `BffLoginHandler` (name `Bff.Login`) and `GET /callback` → `BffCallbackHandler` (`Bff.Callback`),
    each as `static (HttpContext context) => context.RequestServices.GetRequiredService<T>().HandleAsync(context)`.
  - Map `GET /user` → `BffUserHandler` (`Bff.User`) with `.WithMetadata(BffUserEndpointMetadata.Instance)`.
  - Return the group so the app can add a rate limiter or `ExcludeFromDescription()`.
  - Test: build a `WebApplication.CreateSlimBuilder()` app with the BFF composition (as in step 24) and call
    `MapBffEndpoints()`. Assert that the `EndpointDataSource`s hold the three route patterns under `/bff`, and that
    `/bff/user` carries the metadata.
- Pattern: `src/NCode.Registration.AspNetCore/EndpointRouteBuilderExtensions.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffEndpointRouteBuilderExtensionsTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 42. Register the scheme, handlers and the startup check
- Files: `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `src/NCode.Identity.Bff/Hosting/BffStartupCheck.cs` (new), `tests/NCode.Identity.Bff.Tests/DefaultRegistrationTests.cs` (edit), `tests/NCode.Identity.Bff.Tests/Hosting/BffStartupCheckTests.cs` (new)
- Change:
  - In `AddBffLibrary`, register `BffUserHandler` and these:
    - `serviceCollection.AddAuthentication().AddScheme<AuthenticationSchemeOptions, BffAuthenticationHandler>(BffDefaults.AuthenticationScheme, null, null)`.
    - `serviceCollection.PostConfigure<AuthenticationOptions>(o => o.DefaultScheme ??= BffDefaults.AuthenticationScheme)`.
    - `serviceCollection.AddAuthorization()` and `AddHostedService<BffStartupCheck>()`.
  - `internal class BffStartupCheck(IEnumerable<IClientAuthenticator> authenticators, IOptions<BffOptions> options) : IHostedService`:
    - `StartAsync` throws `InvalidOperationException` when no authenticator's `MethodName` equals
      `ClientAuthentication.Method` (ordinal). The message names `Bff:ClientAuthentication:Method`.
    - `StopAsync` completes.
  - Tests: the scheme `Bff` is registered and is the default scheme; a host-set default is kept; the startup check
    throws for `client_secret_jwt` and passes for `private_key_jwt`.
- Pattern: step 24.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~NCode.Identity.Bff.Tests.DefaultRegistrationTests|FullyQualifiedName~BffStartupCheckTests" -- RunConfiguration.TreatNoTestsAsError=true`

### Slice (c): the user token requirement, single-flight refresh and DPoP (§5.5)

### [x] 43. Add the token request and result types
- Files: `src/NCode.Identity.Bff.Abstractions/BffTokenRequest.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffAccessToken.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffTokenResult.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffTokenFailureReasons.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/BffTokenResultTests.cs` (new)
- Change: These are all public sealed types.
  - `BffTokenRequest(string requirement)` (not empty) has init properties `Resource?` and `Scope?`.
  - `BffAccessToken(string value, string tokenType, DateTimeOffset expiresAt)` has an init property `DPoPJwkThumbprint?`.
  - `BffTokenResult` has a private constructor and `static Success(BffAccessToken? token)` and
    `static Failure(int statusCode, string reason)`. Its properties are `Token`, `StatusCode` (200 on success),
    `Reason`, and `IsSuccess` with `[MemberNotNullWhen(false, nameof(Reason))]`.
  - `BffTokenFailureReasons`: `SessionRequired="session_required"`, `RefreshFailed="refresh_failed"`,
    `RefreshWaitTimeout="refresh_wait_timeout"`, `ProviderError="provider_error"`,
    `ProviderUnavailable="provider_unavailable"`.
  - Tests: the guards, `Success(null)`, and `Failure`.
- Pattern: `src/NCode.Identity.OpenId.Client.Abstractions/TokenResult.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffTokenResultTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 44. Add the token requirement and accessor contracts
- Files: `src/NCode.Identity.Bff.Abstractions/IBffTokenRequirement.cs` (new), `src/NCode.Identity.Bff.Abstractions/IBffTokenAccessor.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffTokenRequirementNames.cs` (new), `src/NCode.Identity.Bff.Abstractions/BffTokenUnavailableException.cs` (new), `src/NCode.Identity.Bff.Abstractions/PublicAPI.Unshipped.txt` (edit), `src/NCode.Identity.Bff/Login/BffCallbackHandler.cs` (edit)
- Change:
  - `public interface IBffTokenRequirement { string Name { get; } ValueTask<BffTokenResult> GetAccessTokenAsync(HttpContext httpContext, BffTokenRequest request, CancellationToken cancellationToken); }`,
    documented as an open strategy collection selected by `Name`.
  - `public interface IBffTokenAccessor { ValueTask<BffTokenResult> GetAccessTokenAsync(HttpContext httpContext, BffTokenRequest request, CancellationToken cancellationToken); }`.
  - `BffTokenRequirementNames.User = "user"`, documented as the known values.
  - `BffTokenUnavailableException(int statusCode, string reason) : Exception` has `StatusCode` and `Reason`.
  - In `BffCallbackHandler`, replace the private `"user"` const with `BffTokenRequirementNames.User`.
- Pattern: step 31.
- Done when: `dotnet build src/NCode.Identity.Bff/NCode.Identity.Bff.csproj`

### [x] 45. Add the striped refresh locks and the token cache helpers
- Files: `src/NCode.Identity.Bff/Tokens/BffRefreshLocks.cs` (new), `src/NCode.Identity.Bff/Tokens/BffTokenCache.cs` (new), `tests/NCode.Identity.Bff.Tests/Tokens/BffTokenCacheTests.cs` (new)
- Change:
  - `internal sealed class BffRefreshLocks : IDisposable` holds 64 `SemaphoreSlim(1, 1)`. Its method is
    `SemaphoreSlim For(string storeKey)`; the index is `(uint)StringComparer.Ordinal.GetHashCode(storeKey) % 64`.
  - `internal static class BffTokenCache`:
    - `bool TryGetFresh(BffSessionPayload payload, string requirement, string? resource, string? scope, DateTimeOffset now, TimeSpan refreshBeforeExpiry, [NotNullWhen(true)] out BffStoredAccessToken? token)`
      matches the three keys ordinally (null equals null) and requires `ExpiresAt - refreshBeforeExpiry > now`.
    - `BffSessionPayload WithToken(BffSessionPayload payload, BffStoredAccessToken token)` replaces a matching entry or
      appends. When the list then exceeds 32, it drops the entry with the earliest `ExpiresAt`.
  - Tests: fresh, stale and missing lookups; a replace; an append; the cap.
- Pattern: none.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffTokenCacheTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 46. Add the user token service's cached path
- Files: `src/NCode.Identity.Bff/Tokens/IBffUserTokenService.cs` (new), `src/NCode.Identity.Bff/Tokens/DefaultBffUserTokenService.cs` (new), `tests/NCode.Identity.Bff.Tests/Tokens/DefaultBffUserTokenServiceTests.cs` (new)
- Change:
  - `internal interface IBffUserTokenService { ValueTask<BffTokenResult> GetAccessTokenAsync(HttpContext httpContext, string? resource, string? scope, CancellationToken cancellationToken); }`.
  - `DefaultBffUserTokenService`'s constructor takes `BffSessionResolver`, `IBffSessionManager`, `IBffSessionStore`,
    `ITokenClient`, `BffClientRegistrationProvider`, `BffRefreshLocks`, `BffCookies`, `IBffAuditRecorder`,
    `IOptions<BffOptions>`, `TimeProvider` and `ILogger<DefaultBffUserTokenService>`. In this step:
    - No session gives `Failure(401, session_required)`.
    - `BffTokenCache.TryGetFresh` gives `Success(new BffAccessToken(Value, TokenType, ExpiresAt) { DPoPJwkThumbprint })`.
    - A null `RefreshToken` ends the session (`refresh_failed`), deletes the cookie, and gives `Failure(401, refresh_failed)`.
    - Otherwise call a private `RefreshAsync`, which throws `NotImplementedException` until step 47.
  - Tests for the three paths.
- Pattern: step 37 tests (pre-resolved feature).
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffUserTokenServiceTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 47. Refresh under the lease and write the tokens back
- Files: `src/NCode.Identity.Bff/Tokens/DefaultBffUserTokenService.cs` (edit), `tests/NCode.Identity.Bff.Tests/Tokens/DefaultBffUserTokenServiceTests.cs` (edit)
- Change: Implement `RefreshAsync(HttpContext, BffSessionState state, string? resource, string? scope, CancellationToken)`:
  - Acquire `locks.For(storeKey).WaitAsync(RefreshLeaseDuration, ct)`. False gives `Failure(503, refresh_wait_timeout)`.
    Release it in a `finally`.
  - Reload with `manager.LoadAsync(handle)`. Null gives 401 `session_required`. A fresh cached token is returned.
  - `leaseKey = storeKey + ":" + BffKeyDerivation.Fingerprint(refreshToken)`. When
    `store.TryAcquireRefreshLeaseAsync(leaseKey, now + RefreshLeaseDuration)` returns false, call
    `WaitForRefreshAsync`, which step 49 adds; return `Failure(503, refresh_wait_timeout)` until then.
  - Call `RequestTokenAsync(registration, new RefreshTokenRequest(rt) { Resources = resource is null ? [] : [resource], Scope = scope, DPoPJwkThumbprint = payload.DPoPJwkThumbprint }, ct)`.
  - On success, build the new entry. `ExpiresAt` is `now + ExpiresIn`, or `now` when absent; `jkt` is
    `result.DPoPJwkThumbprint`. Call `manager.UpdateAsync(state, s => …)` with this update:
    - `WithToken`. Set `RefreshToken = response.RefreshToken ?? rt`, but only when `s.Payload.RefreshToken == rt`;
      otherwise keep the stored refresh token.
    - Cap `AbsoluteExpiresAt`, and `IdleExpiresAt` to it, by `refresh_token_expires_in` when it is present.
    - A null update result gives 401 `session_required`. Otherwise return `Success`.
  - In a `finally`, release the lease (`ReleaseRefreshLeaseAsync`) only when
    `now - acquiredAt < RefreshLeaseDuration - 5 s`.
  - Tests: a won lease refreshes and writes a rotated refresh token and a resource-scoped entry; a lease released in
    time; a refresh slower than the margin (advance `MutableTimeProvider` inside the token-client callback) keeps the lease.
- Pattern: step 46.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffUserTokenServiceTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 48. Map refresh failures
- Files: `src/NCode.Identity.Bff/Tokens/DefaultBffUserTokenService.cs` (edit), `src/NCode.Identity.Bff/Logging/EventIds.cs` (edit), `src/NCode.Identity.Bff/Logging/Log.cs` (edit), `tests/NCode.Identity.Bff.Tests/Tokens/DefaultBffUserTokenServiceTests.cs` (edit)
- Change: In `RefreshAsync`, with the lease release still in the `finally`:
  - An error result whose `Error.Error == invalid_grant` does this: `TokenRefreshFailedAsync(subject, invalid_grant)`;
    `manager.EndAsync(httpContext, storeKey, metadata, refresh_failed, CancellationToken.None)`; `DeleteSession`;
    then `Failure(401, refresh_failed)`.
  - Any other error result: `TokenRefreshFailedAsync(subject, error.Error)`, then `Failure(502, provider_error)`.
  - `OpenIdProviderException`, `HttpRequestException`, or an `OperationCanceledException` while `!ct.IsCancellationRequested`:
    `TokenRefreshFailedAsync(subject, provider_unavailable)`, then `Failure(503, provider_unavailable)`.
  - Add `Log.RefreshFailed(this ILogger, string reason, Exception? exception)` at Warning ("The BFF token refresh failed: {Reason}").
  - Tests: one per branch.
- Pattern: step 32 (logging).
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffUserTokenServiceTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 49. Wait for another replica's refresh
- Files: `src/NCode.Identity.Bff/Tokens/DefaultBffUserTokenService.cs` (edit), `tests/NCode.Identity.Bff.Tests/Tokens/DefaultBffUserTokenServiceTests.cs` (edit)
- Change: Replace the step-47 placeholder with a loop. `deadline = now + RefreshLeaseDuration`, and `delay` starts at
  50 ms and doubles up to 800 ms. Each pass:
  - `await Task.Delay(delay, timeProvider, ct)`, then `LoadAsync`. Null gives 401 `session_required`.
  - A fresh entry gives `Success`.
  - When the refresh token differs from the one this request saw, or `IsRefreshLeaseHeldAsync(leaseKey)` is false, go
    back to the acquire step of step 47 with the reloaded state (make that part a private method you can call again).
  - Past the deadline, give `Failure(503, refresh_wait_timeout)`. The old token is never sent.
  - Tests construct the service with `TimeProvider.System` and `Options.Create(new BffOptions { Tokens = { RefreshLeaseDuration = TimeSpan.FromMilliseconds(400) } })`
    (unit tests do not run the options validator, so the short duration is allowed):
    - A lost lease followed by a load that shows a fresh token: success, and the token client is never called.
    - A lost lease, then a load with a changed refresh token, then a won lease: one refresh with the new token.
    - A lease that stays held (`IsRefreshLeaseHeldAsync` always true, the refresh token unchanged): 503
      `refresh_wait_timeout`, and the token client is never called.
- Pattern: step 47.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffUserTokenServiceTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 50. Register the user requirement and the token accessor
- Files: `src/NCode.Identity.Bff/Tokens/BffUserTokenRequirement.cs` (new), `src/NCode.Identity.Bff/Tokens/DefaultBffTokenAccessor.cs` (new), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `tests/NCode.Identity.Bff.Tests/Tokens/DefaultBffTokenAccessorTests.cs` (new)
- Change:
  - `internal class BffUserTokenRequirement(IBffUserTokenService service) : IBffTokenRequirement` has
    `Name = BffTokenRequirementNames.User` and delegates with `request.Resource` and `request.Scope`.
  - `internal class DefaultBffTokenAccessor(IEnumerable<IBffTokenRequirement> requirements) : IBffTokenAccessor`:
    - Build a `FrozenDictionary` by `Name` (ordinal). A duplicate name throws `InvalidOperationException` when the
      accessor is built.
    - An unknown requirement throws `InvalidOperationException` naming it.
  - Register `BffRefreshLocks`, `IBffUserTokenService`, the requirement (`TryAddEnumerable`) and the accessor.
  - Tests: dispatch by name; an unknown name throws; a duplicate throws.
- Pattern: ADR-0001 strategy collection, `TryAddEnumerable` in `src/NCode.Identity.OpenId.Client/DefaultRegistration.cs`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~DefaultBffTokenAccessorTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 51. Attach the token and DPoP proof to outbound calls
- Files: `src/NCode.Identity.Bff/Tokens/BffAccessTokenHandler.cs` (new), `tests/NCode.Identity.Bff.Tests/Tokens/BffAccessTokenHandlerTests.cs` (new)
- Change: Add `internal class BffAccessTokenHandler(IHttpContextAccessor accessor, IBffTokenAccessor tokens, IDPoPProofFactory proofs, BffClientRegistrationProvider registrations, BffTokenRequest tokenRequest) : DelegatingHandler`.
  `SendAsync` does this:
  - A missing `HttpContext` throws `InvalidOperationException`.
  - A failed result throws `BffTokenUnavailableException(result.StatusCode, result.Reason)`.
  - A null token sends the request unchanged.
  - Remove any `Authorization` and `DPoP` headers.
  - For a `DPoP` token type (ignoring case), with a null `jkt` throwing `InvalidOperationException`: call
    `proofs.CreateResourceProofAsync(registration, jkt, request.Method, request.RequestUri, token.Value, ct)`, then set
    `Authorization: DPoP <token>` and `DPoP: <proof>`. Otherwise set `Authorization: Bearer <token>`.
  - After the response, when it has a `DPoP-Nonce` header, call `proofs.RecordNonce(request.RequestUri, nonce)`.
  - Tests use a stub inner handler that records the request: Bearer; DPoP header and proof; a failed result throws with
    the status; a null token leaves no `Authorization`.
- Pattern: `tests/NCode.Identity.OpenId.Client.Tests/StubHttpMessageHandler.cs` (copy it into the BFF tests as a test helper).
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffAccessTokenHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 52. Retry a resource server's DPoP nonce challenge once
- Files: `src/NCode.Identity.Bff/Tokens/BffAccessTokenHandler.cs` (edit), `tests/NCode.Identity.Bff.Tests/Tokens/BffAccessTokenHandlerTests.cs` (edit)
- Change: For DPoP only:
  - Before the first send, `await request.Content.LoadIntoBufferAsync(ct)` when the content is not null.
  - When the response is 401, has a `DPoP-Nonce`, and a `WWW-Authenticate` value with scheme `DPoP` whose parameter
    contains `error="use_dpop_nonce"`: record the nonce, dispose the response, create a new proof, replace the `DPoP`
    header, and send once more. Never retry a second time.
  - Tests: a challenge followed by 200 sends twice with different proofs and the same body; two challenges send twice
    and return the second 401; a Bearer 401 is not retried.
- Pattern: step 51.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffAccessTokenHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 53. Add AddBffAccessTokenHandler
- Files: `src/NCode.Identity.Bff/Tokens/BffHttpClientBuilderExtensions.cs` (new), `src/NCode.Identity.Bff/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/Tokens/BffHttpClientBuilderExtensionsTests.cs` (new)
- Change: Add a public static class with `extension(IHttpClientBuilder builder)` and
  `public IHttpClientBuilder AddBffAccessTokenHandler(BffTokenRequest request)`. It null-guards, then calls
  `builder.AddHttpMessageHandler(sp => ActivatorUtilities.CreateInstance<BffAccessTokenHandler>(sp, request))`. The doc
  says the handler attaches the requirement's token and DPoP proof and retries a nonce challenge once, and that a
  failure surfaces as `BffTokenUnavailableException`, which `UseBff()` answers.
  - Test: with the composition of step 24, `services.AddHttpClient("api").AddBffAccessTokenHandler(new("user"))` builds,
    and `IHttpMessageHandlerFactory.CreateHandler("api")` returns a chain that contains a `BffAccessTokenHandler`. Walk
    `DelegatingHandler.InnerHandler`.
- Pattern: step 24.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffHttpClientBuilderExtensionsTests" -- RunConfiguration.TreatNoTestsAsError=true`

### Slice (d): local-API cookie acceptance and the CSRF layers (§5.6, §5.9)

### [x] 54. Add AsBffApi
- Files: `src/NCode.Identity.Bff/Endpoints/BffEndpointConventionBuilderExtensions.cs` (new), `src/NCode.Identity.Bff/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/Endpoints/BffEndpointConventionBuilderExtensionsTests.cs` (new)
- Change: Add a public static class with `extension<TBuilder>(TBuilder builder) where TBuilder : IEndpointConventionBuilder`
  and `public TBuilder AsBffApi()`, which calls `builder.WithMetadata(new BffApiAttribute())` and returns the builder.
  Its doc lists what the marking enforces (step 36), and says controllers use `[BffApi]`.
  - Test: `MapGet("/api/x", …).AsBffApi()` on a slim app yields an endpoint with `BffApiAttribute`, which is also an
    `IAuthorizeData` with scheme `Bff`.
- Pattern: step 41 test.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffEndpointConventionBuilderExtensionsTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 55. Add the anti-forgery and Fetch Metadata checks
- Files: `src/NCode.Identity.Bff/Antiforgery/BffRequestGuard.cs` (new), `tests/NCode.Identity.Bff.Tests/Antiforgery/BffRequestGuardTests.cs` (new)
- Change: Add `internal static class BffRequestGuard`. `string? Check(HttpRequest request, BffOptions options)` returns
  null when the request passes, and otherwise a reason:
  - The `Antiforgery.HeaderName` header must have exactly one value, equal (ordinal) to `HeaderValue`. Otherwise
    `"antiforgery_header_missing"`.
  - `Sec-Fetch-Site`: absent, `same-origin` or `none` passes. `same-site` passes only when the single `Origin` value
    equals (ordinal) an entry of `TrustedOrigins`. Anything else is `"cross_site_request"`.
  - `[Theory]` tests for each case, including a header sent twice, a wrong value, `cross-site`, an untrusted
    `same-site`, a trusted `same-site`, and an unknown value.
- Pattern: none.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffRequestGuardTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 56. Add the UseBff middleware
- Files: `src/NCode.Identity.Bff/Antiforgery/BffMiddleware.cs` (new), `src/NCode.Identity.Bff/Antiforgery/BffApplicationBuilderExtensions.cs` (new), `src/NCode.Identity.Bff/PublicAPI.Unshipped.txt` (edit), `tests/NCode.Identity.Bff.Tests/Antiforgery/BffMiddlewareTests.cs` (new)
- Change:
  - `internal class BffMiddleware(RequestDelegate next, IOptions<BffOptions> options, IBffAuditRecorder recorder)` with
    `Task InvokeAsync(HttpContext context)`:
    - A non-null `IAuthenticationFeature` throws `InvalidOperationException("UseBff() must be called before UseAuthentication().")`.
    - Set a new `BffRequestFeature`.
    - When the endpoint has `BffApiAttribute` or `BffUserEndpointMetadata`:
      - Set `CheckedEndpoint = endpoint`.
      - A guard reason records `AntiforgeryRejectedAsync` and writes a `403` problem (`AntiforgeryHeaderMissing` or
        `CrossSiteRequest`), then returns.
    - Call `next`, catching only while `!context.Response.HasStarted`:
      - `BffStoreUnavailableException` gives a `503` problem (`StoreUnavailable`).
      - `BffTokenUnavailableException` gives a problem with its status. The type is `SessionRequired` for 401 and
        `TokenUnavailable` otherwise.
  - The public `extension(IApplicationBuilder app)` member `UseBff()` calls `app.UseMiddleware<BffMiddleware>()` and
    returns `app`. Its doc gives the order: CORS, `UseBff()`, `UseAuthentication()`, `UseAuthorization()`.
  - Tests: called after authentication throws; a guarded endpoint without the header gives 403 and `next` is not
    called; with the header, `next` is called and the feature holds the endpoint; each exception maps to its problem.
- Pattern: steps 26 and 55.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffMiddlewareTests" -- RunConfiguration.TreatNoTestsAsError=true`

### Slice (e): logout and back-channel logout (§5.8)

### [x] 57. Add the logout service and the scheme's sign-out
- Files: `src/NCode.Identity.Bff/Logout/BffLogoutService.cs` (new), `src/NCode.Identity.Bff/Authentication/BffAuthenticationHandler.cs` (edit), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `src/NCode.Identity.Bff/Logging/EventIds.cs` (edit), `src/NCode.Identity.Bff/Logging/Log.cs` (edit), `tests/NCode.Identity.Bff.Tests/Logout/BffLogoutServiceTests.cs` (new)
- Change:
  - `internal class BffLogoutService(IBffSessionManager manager, ITokenRevocationClient revocation, BffClientRegistrationProvider registrations, BffCookies cookies, ILogger<BffLogoutService> logger)`
    has `ValueTask EndSessionAsync(HttpContext httpContext, BffSessionState state, string reason, CancellationToken)`:
    - When there is a refresh token, call
      `RevokeTokenAsync(registration, new TokenRevocationRequest(rt) { TokenTypeHint = OpenIdProtocol.TokenTypeHints.RefreshToken }, ct)`.
      A returned error, or any exception except `OperationCanceledException`, logs `Log.RefreshTokenRevocationFailed`
      (Warning) and continues.
    - Then call `manager.EndAsync(httpContext, storeKey, metadata, reason, CancellationToken.None)` and `DeleteSession`.
    - Then mark the request feature resolved with no session.
  - `BffAuthenticationHandler` also implements `IAuthenticationSignOutHandler`. `SignOutAsync(properties)` resolves the
    session and, when there is one, calls `EndSessionAsync(..., logged_out, ...)`, getting `BffLogoutService` from
    `Context.RequestServices`. Register `BffLogoutService` in `DefaultRegistration`.
  - Tests: revoke, then end; a revocation exception still ends the session; no refresh token skips revocation.
- Pattern: step 48 (logging).
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffLogoutServiceTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 58. Add the logout handler's checks
- Files: `src/NCode.Identity.Bff/Logout/BffLogoutHandler.cs` (new), `tests/NCode.Identity.Bff.Tests/Logout/BffLogoutHandlerTests.cs` (new)
- Change: Add `internal class BffLogoutHandler`. Its constructor takes `IOptions<BffOptions>`, `BffSessionResolver`,
  `BffLogoutService`, `IProviderMetadataResolver` and `BffClientRegistrationProvider`. `Task HandleAsync(HttpContext)`:
  - `SetNoStore`.
  - `returnUrl` (default `"/"`) must be `IsAllowed`. Otherwise answer a `400` problem (`InvalidRequest`).
  - No session redirects 302 to `LocalUrl(returnUrl)`.
  - A single `sid` value is compared with `LogoutGuard`:
    `CryptographicOperations.FixedTimeEquals(SecureEncoding.UTF8.GetBytes(sid), SecureEncoding.UTF8.GetBytes(guard))`.
    A missing value or a mismatch answers `400`, and the session is kept.
  - On a match, call `EndSessionAsync(..., logged_out, ...)` and redirect 302 to `LocalUrl(returnUrl)` (step 59
    replaces this redirect with the end-session redirect).
  - Tests: a bad return URL gives 400; no session redirects; a wrong `sid` gives 400 and the strict service is never
    called; the right `sid` ends the session.
- Pattern: step 40.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffLogoutHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 59. Redirect to the provider's end-session endpoint and map /logout
- Files: `src/NCode.Identity.Bff/Logout/BffLogoutHandler.cs` (edit), `src/NCode.Identity.Bff/Endpoints/BffEndpointRouteBuilderExtensions.cs` (edit), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `tests/NCode.Identity.Bff.Tests/Logout/BffLogoutHandlerTests.cs` (edit)
- Change: Keep the session's `IdToken` before ending it. After ending it:
  - Get the metadata. An `OpenIdProviderException` falls back to the return URL.
  - A null `EndSessionEndpoint` redirects to `LocalUrl(returnUrl)`.
  - Otherwise redirect 302 to `QueryHelpers.AddQueryString(endpoint, …)` with `id_token_hint`, `client_id`,
    `post_logout_redirect_uri = BffNavigation.AbsoluteUrl(request, returnUrl)` and `state = CreateSecret()`.
  - Map `GET /logout` → `BffLogoutHandler` (name `Bff.Logout`) in `MapBffEndpoints`, and register the handler.
  - Tests: the redirect carries the four parameters; no end-session endpoint goes to the return URL.
- Pattern: step 58.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffLogoutHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 60. Add the back-channel logout receiver
- Files: `src/NCode.Identity.Bff/Logout/BffBackChannelLogoutHandler.cs` (new), `src/NCode.Identity.Bff/Endpoints/BffEndpointRouteBuilderExtensions.cs` (edit), `src/NCode.Identity.Bff/DefaultRegistration.cs` (edit), `tests/NCode.Identity.Bff.Tests/Logout/BffBackChannelLogoutHandlerTests.cs` (new)
- Change: Add `internal class BffBackChannelLogoutHandler`. Its constructor takes `BffClientRegistrationProvider`,
  `ILogoutTokenValidator`, `IBffSessionStore`, `IBffSessionManager` and `IBffAuditRecorder`.
  `Task HandleAsync(HttpContext)` does this:
  - `SetNoStore`.
  - When `!Request.HasFormContentType`, or the form's `logout_token` is not one non-empty value: record
    `BackchannelLogoutRejectedAsync("invalid_request")` and answer 400.
  - Validate the token. An invalid result records rejected with `FailureCode` and answers 400.
  - `keys` is `FindBySessionIdAsync(result.Issuer, sid)` when `SessionId` is set, and `FindBySubjectAsync(Issuer, Subject)`
    otherwise. For each key: `GetAsync`, skipping a null record and a record whose `Issuer` (or `SessionId`, on the sid
    path) differs. Then `EndAsync(context, key, metadata, backchannel_logout, CancellationToken.None)`.
  - A `BffStoreUnavailableException` records rejected (`session_end_failed`) and answers 400.
  - Then `TryRecordHandledAsync(registration, result, CancellationToken.None)`, `BackchannelLogoutReceivedAsync`, and 200.
  - Map `POST /backchannel-logout` (name `Bff.BackchannelLogout`) and register the handler.
  - Tests: no form gives 400; an invalid token gives 400; the sid path ends both matching sessions and skips a
    foreign-issuer record; the sub path; a store failure gives 400 with no `jti` recorded.
- Pattern: `docs/decisions/0284-a-logout-token-is-recorded-only-after-the-client-handles-it.md`.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj --filter "FullyQualifiedName~BffBackChannelLogoutHandlerTests" -- RunConfiguration.TreatNoTestsAsError=true`

### End-to-end tests against the NCode OP (§10)

### [x] 61. Add the BFF test host and run login, /bff/user and a local API end to end
- Files: `tests/NCode.Identity.OpenId.IntegrationTests/NCode.Identity.OpenId.IntegrationTests.csproj` (edit), `tests/NCode.Identity.OpenId.IntegrationTests/Bff/BffTestHost.cs` (new), `tests/NCode.Identity.OpenId.IntegrationTests/Bff/BffEndToEndTests.cs` (new)
- Change:
  - Add a project reference to `src/NCode.Identity.Bff`.
  - `BffTestHost : IAsyncDisposable` builds `WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" })`
    with `UseTestServer()`, and in-memory configuration:
    - `Bff:Authority` = `HostApplicationFactory.TestServerIssuer`, a `ClientId`, and `ClientAuthentication:Method`
      `client_secret_basic` with `KeyRing` `bff-e2e`.
    - `ReturnUrls:0` `/`, `LoginParameters:0` `prompt`, `UserClaims:0` `email`.
  - Compose it as in step 24, routing `OpenIdClientDefaults.HttpClientName` to `factory.Server.CreateHandler()`. Take
    the `IExpiringValueSet`, `IExpiringIndexSet` and `IExpiringKeySet` instances from the constructor (so two hosts can
    share them) and register the keyed key ring as `AuthorizationRequestClientEndToEndTests` does.
  - The pipeline is `UseBff()`, `UseAuthentication()`, `UseAuthorization()`, `MapBffEndpoints()`, and
    `MapGet("/api/token", …).AsBffApi()`, which returns the `IBffTokenAccessor` user token's type.
  - `Server.BaseAddress` is `https://bff.example/`. Give it a cookie-carrying helper that parses `Set-Cookie` itself.
  - Test `Login_ThenUserAndApi_Succeed`:
    - Seed the client with `SeedSharedSecretClientWithSettingsAsync`, redirect `https://bff.example/bff/callback`.
      Grant refresh tokens the way `TokenClientDPoPEndToEndTests` seeds its client.
    - Sign in at the OP, then `GET /bff/login?returnUrl=/&prompt=none`, follow the OP authorize redirect with the OP
      session cookie, and `GET` the callback with the login cookie.
    - Assert a 302 to `/`; `/bff/user` with `X-CSRF: 1` gives 200 with `sub`; without the header gives 403;
      `/api/token` gives 200; `/api/token` without the cookie gives 401.
- Pattern: `tests/NCode.Identity.OpenId.IntegrationTests/Client/AuthorizationRequestClientEndToEndTests.cs` (`CreateClientProvider`, `AssertCodeFlowCompletesAsync`).
- Done when: `dotnet test tests/NCode.Identity.OpenId.IntegrationTests/NCode.Identity.OpenId.IntegrationTests.csproj --filter "FullyQualifiedName~BffEndToEndTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [x] 62. Prove that concurrent refreshes across two replicas never trip reuse detection
- Files: `tests/NCode.Identity.OpenId.IntegrationTests/Bff/BffRefreshEndToEndTests.cs` (new), `tests/NCode.Identity.OpenId.IntegrationTests/Bff/BffTestHost.cs` (edit)
- Change:
  - Let `BffTestHost` take extra configuration. Set `Bff:Tokens:RefreshBeforeExpiry` to `01:00:00`, so every call
    refreshes, and leave DPoP off, so the OP rotates refresh tokens (ADR-0216).
  - Build two hosts over one shared set of value, index and key sets, and log in on host A.
  - Copy the session cookie to host B. Send 8 parallel `/api/token` calls, 4 to each host (`Task.WhenAll`).
  - Assert that all 8 answer 200, that a further call answers 200, and that no token-endpoint exchange recorded with
    `RecordingHttpMessageHandler` answered `invalid_grant`.
- Pattern: step 61; `RecordingHttpMessageHandler` in `Infrastructure/`.
- Done when: `dotnet test tests/NCode.Identity.OpenId.IntegrationTests/NCode.Identity.OpenId.IntegrationTests.csproj --filter "FullyQualifiedName~BffRefreshEndToEndTests" -- RunConfiguration.TreatNoTestsAsError=true`

### [ ] 63. Prove that back-channel logout ends the BFF session and misordered UseBff fails closed
- Files: `tests/NCode.Identity.OpenId.IntegrationTests/Bff/BffBackChannelLogoutEndToEndTests.cs` (new), `tests/NCode.Identity.OpenId.IntegrationTests/Bff/BffTestHost.cs` (edit)
- Change:
  - Test `BackChannelLogout_EndsTheSession`:
    - Use `new HostApplicationFactory { BackChannelLogoutExchanges = queue }`, and seed the client with
      `backchannel_logout_uri` `https://bff.example/bff/backchannel-logout` and session required.
    - Log in through the BFF, then end the OP session with `GET /oauth2/end_session` and the OP session cookie.
    - Poll the queue (Stopwatch, 100 ms steps, 15 s), and post the delivered body's `logout_token` to
      `/bff/backchannel-logout` as a form.
    - Assert 200, and that `/bff/user` with the header then answers 401.
  - Give `BffTestHost` a constructor parameter `bool authenticateBeforeBff` (default false) that calls
    `UseAuthentication()` before `UseBff()`. Test `UseBffAfterAuthentication_Fails`: with it set, `GET /bff/user` with
    the header answers 500 (the developer exception page) and never 200.
- Pattern: `tests/NCode.Identity.OpenId.IntegrationTests/Client/LogoutTokenValidatorEndToEndTests.cs`.
- Done when: `dotnet test tests/NCode.Identity.OpenId.IntegrationTests/NCode.Identity.OpenId.IntegrationTests.csproj --filter "FullyQualifiedName~BffBackChannelLogoutEndToEndTests" -- RunConfiguration.TreatNoTestsAsError=true`

### ADRs and documentation

### [ ] 64. Record ADR-0289: sessions and login binding
- Files: `docs/decisions/0289-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md` (new), `docs/decisions/README.md` (edit)
- Change: Use ADR-0286's structure (Status Accepted, Date 2026-10-28, Context, Decision, Options considered,
  Consequences, References), in timeless present tense.
  - Decision covers: the packages and their names (§11.5), with Abstractions on ASP.NET Core; the handle, store key,
    payload encryption and associated data including the metadata; the envelope format; the store seam and its key
    prefixes and digests; the login transaction, the initiator cookie and the take-then-decrypt order; the cookie
    attributes; the lifetimes and the idle slide only on `[BffApi]`; claim filtering through
    `IdTokenValidationResult.Identity`; the single `BffAuditEvent`; and one `default` frontend.
  - Options: an encrypted cookie (design D1), Data Protection (D2), the BFF mapping claims itself, and `OpenId.Bff` naming.
  - Add an index row after 0286's: `| [0289](./0289-….md) | The BFF keeps each session as an encrypted server-side record under a cookie handle | Accepted |`.
- Pattern: `docs/decisions/0286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md`.
- Done when: `if ((Select-String -Path docs/decisions/0289-*.md -Pattern '^## Decision' -Quiet) -and (Select-String -Path docs/decisions/README.md -Pattern '\[0289\]' -Quiet)) { exit 0 } else { exit 1 }`

### [ ] 65. Record ADR-0290: single-flight refresh
- Files: `docs/decisions/0290-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md` (new), `docs/decisions/README.md` (edit)
- Change: The Decision covers:
  - the striped local lock and the lease keyed by the refresh-token fingerprint, held in the store's value set and
    released under the 5 s margin;
  - the loser poll and backoff, and the merge write;
  - failure mapping (401, 502 and 503), the token cache key and cap, and `offline_access`;
  - DPoP `jkt` pinning, the resource proof and the single nonce retry;
  - `IBffTokenRequirement` with `user` only, `IBffTokenAccessor`, `AddBffAccessTokenHandler` and
    `BffTokenUnavailableException`;
  - §11.2: honoring `refresh_token_expires_in` only to shorten a session.
  - Options: a version-keyed lease, an `IExpiringKeySet` lease, an OP reuse grace window (D6), and a background refresh.
  - Add the index row.
- Pattern: step 64.
- Done when: `if ((Select-String -Path docs/decisions/0290-*.md -Pattern 'refresh_token_expires_in' -Quiet) -and (Select-String -Path docs/decisions/README.md -Pattern '\[0290\]' -Quiet)) { exit 0 } else { exit 1 }`

### [ ] 66. Record ADR-0291: cookie acceptance and the CSRF layers
- Files: `docs/decisions/0291-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md` (new), `docs/decisions/README.md` (edit)
- Change: The Decision covers:
  - the guarded endpoints (`[BffApi]`, `/bff/user`) and the header rule with its forbidden names;
  - the Fetch Metadata rule with `TrustedOrigins`;
  - the problem types and status codes, and the challenge and forbid behavior;
  - the per-request fail-closed pipeline checks that stand in for the design's startup check;
  - `503` for store failures.
  - Options: a double-submit token (D3), evaluating the app's CORS policy, and a startup pipeline check.
  - Add the index row.
- Pattern: step 64.
- Done when: `if ((Select-String -Path docs/decisions/0291-*.md -Pattern 'TrustedOrigins' -Quiet) -and (Select-String -Path docs/decisions/README.md -Pattern '\[0291\]' -Quiet)) { exit 0 } else { exit 1 }`

### [ ] 67. Record ADR-0292: logout and back-channel logout
- Files: `docs/decisions/0292-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md` (new), `docs/decisions/README.md` (edit)
- Change: The Decision covers:
  - the logout guard and its constant-time compare, best-effort revocation, removal with `CancellationToken.None`, the
    end-session redirect parameters, and idempotent logout without a session;
  - the back-channel `sid`/`sub` lookup through the indexes with the issuer check, `jti` recorded after the sessions end
    (ADR-0284), and 200 or 400;
  - `BackchannelLogoutSessionRequired` defaulting to true;
  - sign-out through the scheme.
  - Add the index row.
- Pattern: step 64.
- Done when: `if ((Select-String -Path docs/decisions/0292-*.md -Pattern 'FixedTimeEquals' -Quiet) -and (Select-String -Path docs/decisions/README.md -Pattern '\[0292\]' -Quiet)) { exit 0 } else { exit 1 }`

### [ ] 68. Record ADR-0293: no bff security profile at the OP
- Files: `docs/decisions/0293-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md` (new), `docs/decisions/README.md` (edit)
- Change: The Decision settles design §11.1.
  - There is no `bff` profile. A BFF client registers this metadata: confidential `private_key_jwt` with `jwks_uri`,
    `redirect_uris` (`…/bff/callback`), `post_logout_redirect_uris`, `backchannel_logout_uri` (`…/bff/backchannel-logout`),
    `backchannel_logout_session_required=true`, `require_pushed_authorization_requests`, `dpop_bound_access_tokens`
    (when the BFF runs DPoP `required`), the refresh-token grant, and a refresh-token absolute lifetime no shorter than
    the BFF's `AbsoluteLifetime`.
  - A deployment that wants a ceiling selects the `fapi2` profile.
  - Reasons: a profile is a ceiling (ADR-0108, ADR-0148), duplication with `fapi2`, and goal G3.
  - Add the index row.
- Pattern: step 64.
- Done when: `if ((Select-String -Path docs/decisions/0293-*.md -Pattern 'fapi2' -Quiet) -and (Select-String -Path docs/decisions/README.md -Pattern '\[0293\]' -Quiet)) { exit 0 } else { exit 1 }`

### [ ] 69. Update the BFF design document
- Files: `docs/backend-for-frontend.md` (edit)
- Change: Make four edits. Do not edit the §9 phase-1 row.
  - Status (lines 3–5): say that phase 2 is delivered, linking ADR-0289 to ADR-0292.
  - §4.1 table rows for `NCode.Identity.Bff.Abstractions` and `NCode.Identity.Bff`: the real dependencies (ASP.NET Core
    framework, `OpenId.Client.Abstractions`, `Events.Abstractions`, `SharedState.Abstractions`; abstractions only, per
    ADR-0016) and `AddBffLibrary`/`AddBffConfiguration`.
  - §9 phase-2 row: append "Delivered" with the four ADR links.
  - §11 items 1, 2 and 5: append "Decided: …" with a one-sentence answer linking ADR-0293, ADR-0290 and ADR-0289.
  - Also replace the §5.12 sketch's `AddIdentityBff()`/`AddConfiguration` lines with the real calls, and add
    `TrustedOrigins` to the §5.12 JSON example.
- Pattern: the phase-1 row's "delivered" wording.
- Done when: `if ((Select-String -Path docs/backend-for-frontend.md -Pattern '0293-a-bff-client' -Quiet) -and (Select-String -Path docs/backend-for-frontend.md -Pattern 'AddBffLibrary' -Quiet)) { exit 0 } else { exit 1 }`

### [ ] 70. Catalog the BFF audit events
- Files: `docs/audit-records.md` (edit)
- Change:
  - Add `bff` to the Event sources table ("The backend-for-frontend (`NCode.Identity.Bff`)").
  - Add a `### BFF plane` section after `### Protocol plane`. It has one table row per `BffAuditEventNames` name, with
    the category, outcomes, severity, actor, `error.code` and `details` from step 20.
  - Add notes: reasons come from `BffLoginFailureReasons` and `BffSessionEndReasons`, and no token, handle or cookie
    value is ever written.
- Pattern: the Protocol plane table in the same file.
- Done when: `if (Select-String -Path docs/audit-records.md -Pattern 'bff.backchannel_logout.rejected' -Quiet) { exit 0 } else { exit 1 }`

### [ ] 71. Update AGENTS.md and the changelog
- Files: `AGENTS.md` (edit), `CHANGELOG.md` (edit)
- Change:
  - In AGENTS.md's Layout list, add a bullet after the OpenID protocol client: "**Backend for frontend** —
    `NCode.Identity.Bff(.Abstractions)`: …", one sentence linking ADR-0289.
  - In `CHANGELOG.md` under `## [Unreleased]` → `### Added`, add a first bullet, "**The backend-for-frontend core
    (`NCode.Identity.Bff`).**", in the style of the DPoP entry. Name `AddBffLibrary`, `AddBffConfiguration`,
    `MapBffEndpoints`, `UseBff`, `AsBffApi`/`[BffApi]`, `AddBffAccessTokenHandler`, `IBffSessionStore`,
    `IBffTokenAccessor`, `IBffLoginGate`, `IBffSessionValidator` and `IdTokenValidationResult.Identity`. Link
    ADR-0289–0293 and issue [#394](https://github.com/NCodeGroup/Identity/issues/394).
- Pattern: the existing DPoP entry in `CHANGELOG.md`.
- Done when: `if ((Select-String -Path CHANGELOG.md -Pattern 'NCode.Identity.Bff' -Quiet) -and (Select-String -Path AGENTS.md -Pattern 'NCode.Identity.Bff' -Quiet)) { exit 0 } else { exit 1 }`

### [x] 72. Cover the logout routes in the endpoint mapping tests
- Files: `tests/NCode.Identity.Bff.Tests/Endpoints/BffEndpointRouteBuilderExtensionsTests.cs` (edit)
- Change: steps 59 and 60 added `GET /logout` (`Bff.Logout`) and `POST /backchannel-logout` (`Bff.BackchannelLogout`) to `MapBffEndpoints`, so step 41's tests, which expect exactly three routes, fail. Rename `MapBffEndpoints_MapsTheThreeRoutesUnderThePrefix` to `MapBffEndpoints_MapsTheFiveRoutesUnderThePrefix` and expect `/bff/backchannel-logout`, `/bff/callback`, `/bff/login`, `/bff/logout` and `/bff/user` (sorted as the test already sorts); in `MapBffEndpoints_WithCustomPrefix_UsesIt` expect the same five under `/auth`. Also assert that `/backchannel-logout` is a `POST` route and `/logout` a `GET` route, if the test already inspects HTTP methods for the other routes; otherwise leave methods alone. Change nothing in `src/`.
- Pattern: the existing assertions in the same file.
- Done when: `dotnet test tests/NCode.Identity.Bff.Tests/NCode.Identity.Bff.Tests.csproj -- RunConfiguration.TreatNoTestsAsError=true`

## Review checklist
- No token, refresh token, handle, state, code, initiator secret or logout guard reaches a log, an audit event, an
  `HttpContext.Items` entry or a response. The one exception is the guard inside `/bff/user`'s `logoutUrl`.
- The callback takes the transaction before it decrypts it, deletes the login cookie on every path, and always mints a
  new handle. The `iss` check runs before the code exchange.
- A tampered metadata field (an expiry) fails decryption in `BffSessionCodec`. Test this explicitly.
- `UseBff()` placed after `UseAuthentication()`, or before routing, can never serve a `[BffApi]` or `/bff/user`
  request without the guard. Steps 37, 56 and 63 prove it.
- The refresh lease is never released after its expiry margin. No code path sends a refresh token that the store no
  longer holds. A failed write after a refresh is retried and never dropped.
- A store failure answers `503` everywhere, never `401`. Back-channel logout answers `400` on a store failure and
  records no `jti`.
- `NCode.Identity.Bff` references only `*.Abstractions` (step 3's test), and the protocol-client edit (step 30) is
  additive.
- PublicAPI files list every public symbol, and nothing internal is public.
- Run `./build/dod.ps1 -Fast -Affected` once at the end; it must print `Definition of Done: PASSED`.

## Follow-ups
- BFF turnkey composition root: add an `AddIdentityBff(IConfiguration)` composition package that registers the secrets, JOSE, JWT, protocol-client, events, shared-state and BFF libraries in one call (design goal G5), because `NCode.Identity.Bff` may reference only abstractions (ADR-0016) and hosts now compose six calls.
- BFF session claim and /bff/user contributors: add the `§5.11` strategy collections that shape the session principal once at login and add computed `/bff/user` fields outside the reserved keys, so apps can enrich sessions without replacing the callback.
- BFF UserInfo at login: add a UserInfo request to `NCode.Identity.OpenId.Client` and an opt-in call at the BFF callback (design §5.1), for OPs that keep profile claims out of the ID token.
- BFF meter and ActivitySource: add the `NCode.Identity.Bff` meter and trace source of design §5.13 (token-endpoint, store and lease-wait durations, with bounded tags) and list them in `docs/metrics.md`; phase 2 emits only events and logs.
- NCode OP returns refresh_token_expires_in: have the token endpoint add `refresh_token_expires_in` when it issues a refresh token with an absolute lifetime, so a BFF can cap its session up front (design §11.2, ADR-0290).
- BFF browser end-to-end suite: add a Playwright suite that checks the session cookie attributes, `SameSite` on cross-site navigation, CORS preflight denial and Fetch Metadata rejection in real browsers (design §10).
- BFF interop test against a second OP: run the BFF against a containerized non-NCode OP in CI to prove it depends only on standard metadata (design §10, goal G3).
