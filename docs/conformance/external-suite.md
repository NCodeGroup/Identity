# External conformance suite as the oracle (full automation)

The **OpenID Foundation Conformance Suite** — the certified, spec-author-maintained oracle — is the authoritative gate
behind the fast in-house probe-runner pre-flight, driven by one committed, runnable command. This document describes
that harness and how the OP fares against it. Parent design: [`docs/conformance-testing.md`](../conformance-testing.md).
Decision record: [ADR-1084](../decisions/1084-spec-coverage-conformance-harness.md) (the `browser` / `probe-runner` /
`external-suite` exercise boundary). Tracking issues: [#129](https://github.com/NCodeGroup/identity/issues/129) and
[#124](https://github.com/NCodeGroup/identity/issues/124).

## The harness

The harness is [`build/conformance/`](../../build/conformance):

- **[`run-conformance.ps1`](../../build/conformance/run-conformance.ps1)** — the orchestration. It launches the OpenID
  host on the reachable HTTPS origin (`host.docker.internal:5001`, the `Conformance` launch profile —
  [#126](https://github.com/NCodeGroup/identity/issues/126)), asserts the discovery `issuer` byte-matches that origin,
  runs the in-house probe runner as a **fast pre-flight** that fails early before the slow external suite is started,
  brings the suite up from **prebuilt Docker images** (no Java build), drives the Config OP + Basic OP plans (or, with
  `-Fapi1Advanced`, the FAPI 1.0 Advanced Final plans) headlessly via the suite's `run-test-plan.py` gated on the
  committed baselines, and tears everything down — surfacing one non-zero exit code on any unexpected failure.
- **Config templates** resolved against the deterministic seed ([#127](https://github.com/NCodeGroup/identity/issues/127)):
  [`oidcc-basic.template.json`](../../build/conformance/oidcc-basic.template.json) (with the `browser` automation block
  whose selectors — `username` / `password` / `login` / `consent-allow` — match the `apps/auth` surface,
  [#125](https://github.com/NCodeGroup/identity/issues/125)), [`oidcc-config.json`](../../build/conformance/oidcc-config.json)
  and [`fapi1-advanced.template.json`](../../build/conformance/fapi1-advanced.template.json).
- **Baselines** [`expected-failures.json`](../../build/conformance/expected-failures.json) /
  [`expected-skips.json`](../../build/conformance/expected-skips.json): an entry codifies only a documented, intentional
  deviation and links an issue or ADR.

The pre-flight is `@ncode/app-conformance-runner` (ADR-1084), which exercises the confidential and server-initiated legs
a browser cannot (`client_credentials`, introspection/revocation with client auth, device-code polling, CIBA,
`private_key_jwt`, DPoP/mTLS, back-channel logout) and exits non-zero on any failed probe. Running it first gives fast
local feedback before the heavier suite spins up.

## Exercise boundary (why both)

Per [ADR-1084](../decisions/1084-spec-coverage-conformance-harness.md), coverage is split across three actors and the
harness runs the first two locally, pre-flighting the third:

| Actor            | Drives                                                                    | In this harness                                  |
| ---------------- | ------------------------------------------------------------------------- | ------------------------------------------------ |
| `browser`        | CORS-reachable, credential-free discovery/authorize legs                  | `apps/conformance` dashboard (not scripted here) |
| `probe-runner`   | confidential / server-initiated legs (secrets, device/CIBA, back-channel) | `run-conformance.ps1` step 2 (pre-flight)        |
| `external-suite` | the certified spec catalog (the authoritative judgment)                   | `run-conformance.ps1` steps 3–4                  |

## Not part of `dod.ps1`

The suite needs Docker + Python 3 and is slow, so the harness is an **opt-in / nightly** gate, not part of the
hermetic [`build/dod.ps1`](../../build/dod.ps1) inner loop — consistent with
[`docs/conformance-testing.md`](../conformance-testing.md) prerequisite #9.

## Pending work

- **Nightly CI pipeline** ([#129](https://github.com/NCodeGroup/identity/issues/129)). No workflow runs the harness: a
  separate, opt-in / nightly job (`docker compose` + Python 3, no local Java) that invokes `run-conformance.ps1` and
  publishes the `.results/` zips as artifacts is wired together with the repository's broader CI rather than as a
  one-off.
- **Profile expansion** beyond Basic + Config (Form Post / Dynamic OP / FAPI) as those capabilities are verified
  end-to-end. The FAPI 1.0 Advanced Final plans are wired (`run-conformance.ps1 -Fapi1Advanced`, see the
  [harness README](../../build/conformance/README.md#run)); running them against the suite and committing their baseline
  is tracked as issue [#124](https://github.com/NCodeGroup/identity/issues/124).

## Basic OP and Config OP results

The Basic OP plan runs end-to-end under **Dynamic Client Registration**: the config carries no static `alias` or
`client`/`client2`, so the suite registers a client per module
([ADR-1063](../decisions/1063-dynamic-client-registration.md), with `registration_endpoint_allow_anonymous=true` in
`appsettings.Conformance.json`). The `Verify Complete` callback match is `/test/*/callback*` (DCR has no `/a/<alias>/`
segment), and the `Login` task is `optional`, so no-login `prompt=none` / `max_age` flows do not error. The login and
consent pages serve a no-JavaScript progressive `<form>`, which the suite's default `BrowserControl` — a no-JavaScript
**HtmlUnitDriver** — drives.

The Config OP plan passes cleanly (`44 successes, 0 failures, 0 warnings`). The Basic OP (DCR) and Config OP plans
together finish with `31 PASSED, 0 WARNING, 3 REVIEW, 4 SKIPPED, 1 FAILED` across 39 modules and a green gate
(`Conformance run PASSED`): the failure is the expected one, codified below, and a `REVIEW` result with no failing
conditions does not gate the run. The disposition of each module verdict is tracked in issue
[#131](https://github.com/NCodeGroup/identity/issues/131).

### Behavior the suite verifies

Each module below exercises OP behavior that a specification requires; the OP behaves as described.

- **Dynamic client registration lifecycle** — the deregistration `DELETE /oauth2/register/{client_id}` succeeds for a
  client that owns a secret, and a generated `client_secret` authenticates, because the secret is stored in the form
  its client's method needs: a one-way verifier the presented secret is compared with, or the UTF-8 bytes a
  `client_secret_jwt` assertion is keyed from (`IClientSecretIssuer` —
  [ADR-1391](../decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).
- **`oidcc-refresh-token`** — the consent screen (no-JS form + SPA) pre-selects every requested scope, so a first-time
  `prompt=consent` + `offline_access` consent records `openid`/`offline_access` and the token response carries the
  `id_token` and `refresh_token`.
- **Token-endpoint `Cache-Control: no-store`** — every token-endpoint response carries `Cache-Control: no-store` and
  `Pragma: no-cache`, so tokens are never cached (RFC 6749 §5.1/§5.2).
- **`oidcc-prompt-none-not-logged-in`** — the inline `prompt=none` → `login_required` (and no-consent-surface →
  `interaction_required`) errors echo the request `state` (OIDC Core §3.1.2.6).
- **`oidcc-max-age-1`** — authentication-age freshness is a pure elapsed-time comparison (no `clock_skew` allowance) and
  runs only at the authorization endpoint, via `ValidateSubjectAuthenticationCommand.EnforceAuthenticationAge`, so the
  token and refresh endpoints never re-evaluate it against a later clock.
- **`oidcc-prompt-none-logged-in` / `oidcc-id-token-hint`** — `SettingDescriptor<TValue>.HasDefault` tracks an explicit
  flag, so a value-type setting with no declared default is absent. `IReadOnlySettingCollection.GetValueOrNull<TValue>()`
  (struct constraint, genuine `Nullable<TValue>`) reads optional values. A 32-concurrent `prompt=none` integration test
  guards it.
- **`oidcc-ensure-registered-redirect-uri`** — the OP refuses an unregistered `redirect_uri` with a 400
  `unauthorized_client` and renders the non-redirectable error page (OIDC Core §3.1.2.4); 4xx `OpenIdException`s log at
  Warning. A per-module `override` block in
  [`oidcc-basic.template.json`](../../build/conformance/oidcc-basic.template.json) waits for that error page (matched by
  the `unauthorized_client` error code in the rendered body) instead of the happy-path `submission_complete` callback and
  snapshots it as the review placeholder, so the module lands on the `REVIEW` the suite expects ("Show redirect URI
  error page", OIDCC-3.1.2.1).
- **UserInfo scope claims and ID-token claim placement (`oidcc-server`, `oidcc-scope-{profile,email,address,phone,all}`,
  `oidcc-alternate-happy-flow`, `oidcc-claims-essential`)** — per OIDC Core §5.4 the `profile`/`email`/`address`/`phone`
  scope claims belong in the **UserInfo** response, not the `id_token`, whenever an access token is issued (the code
  flow):
    - `DefaultGetIdTokenSubjectClaimsHandler` emits the scope-driven claim set only when no access token is issued (a
      non-null `AccessToken` on the token request signals the code flow); requested `claims.id_token` claims, `sub`,
      `auth_time`, `acr`, `idp` and `amr` are unaffected.
    - The account-sourced UserInfo enricher (`DefaultGetLocalAccountUserInfoClaimsHandler`, registered by
      `AddLocalAccountAuthentication` / `AddLocalAccountInteractiveAuthentication`) resolves the caller's local account
      from its self-issued connection and returns the scope-permitted claims from the account store. It also honors
      individual claims requested through the OIDC `claims` parameter's `userinfo` member (OIDC Core §5.5) regardless of
      scope, and coerces the standard typed claims (`updated_at` → number, `address` → JSON object,
      `phone_number_verified` → boolean).
    - The conformance seed provisions the **full** standard `profile`/`address`/`phone` claim set
      (`ConformanceSeedOptions.TestUserGivenName` / `TestUserFamilyName` for the name parts, deterministic values for the
      rest).
    - The ID token carries the standard `azp` and never the `tid` or `client_id` claims, which are JWT **access-token**
      claims (RFC 9068 §2.2) (`DefaultGetIdTokenPayloadClaimsHandler`).
- **`acr` emission (`oidcc-ensure-request-with-acr-values-succeeds`)** — the conformance environment advertises
  `acr_values_supported` and sets a `default_acr` baseline (platform settings in `appsettings.Conformance.json`), so the
  password login's resolved `acr` ([ADR-1039](../decisions/1039-requested-claims-acr-and-auth-time-follow-the-request-and-the-authentication-event.md)) is emitted
  and matches the suite's requested `acr_values`.
- **UserInfo access token in the POST body (`oidcc-userinfo-post-body`)** — `DefaultAuthenticateSubjectHandler` accepts
  the bearer token as an `access_token` form parameter (RFC 6750 §2.2 / OIDC §5.3.1) for a non-GET
  `application/x-www-form-urlencoded` request, in addition to the preferred `Authorization: Bearer` header (which takes
  precedence when both are present); the URI-query form (§2.3) is unsupported because RFC 9700 deprecates it.
- **`alg:none` request object → `request_not_supported`** — OIDC Core §6.1: `DefaultLoadAuthorizationRequestHandler`
  detects an unsigned (`alg:none`) request object (inline `request` or a fetched `request_uri`) up front and returns
  exactly `request_not_supported`; a malformed request object returns `invalid_request_object`.
- **Access-token revocation on authorization-code reuse (`oidcc-codereuse-30seconds`)** — RFC 6749 §4.1.2 says the OP
  SHOULD revoke the tokens issued from a reused code _"when possible"_. The OP rejects the second code use with
  `invalid_grant` (one-time codes), enforces PKCE `S256`, and treats a second redemption of a consumed code as a
  compromise signal: the token endpoint revokes the end-user **session's** grants (the access and refresh tokens issued
  from that code) through the session-scoped grant revocation the logout seam uses — without the
  back-channel-logout/audit side effects — and the OP's own resource endpoints (UserInfo, introspection, management API)
  reject an access token whose `sid` resolves to a revoked session (`invalid_token`). Every subject-bound access token
  carries `sid`. There is no global access-token denylist and no per-token store: one indexed session lookup on
  first-party access-token authentication and one session-scoped revocation on the rare replay path. External resource
  servers treat the JWT as self-contained. See [ADR-1112](../decisions/1112-authorization-code-replay-revokes-session.md)
  (refines [ADR-1028](../decisions/1028-the-revocation-endpoint-revokes-refresh-tokens-and-their-families-and-always-answers-200.md)).

### Codified intentional deviations

[`expected-failures.json`](../../build/conformance/expected-failures.json) holds one intentional deviation: every
authorization code is bound to PKCE ([ADR-1144](../decisions/1144-authorization-codes-are-bound-to-pkce-by-default.md)),
and the conformance host relaxes `require_pkce` and attests the suite's clients with `pkce_nonce_exemption`, so its
nonce-carrying requests need no `code_challenge` — but `oidcc-ensure-request-without-nonce-succeeds-for-code-flow`
sends neither a challenge nor a nonce, which leaves the code unprotected against injection, and the OP refuses it with
`invalid_request` (OAuth 2.1 §7.5.1.1) on `CheckIfAuthorizationEndpointError`.

[`expected-skips.json`](../../build/conformance/expected-skips.json) holds the four `SKIPPED` modules
(`oidcc-idtoken-unsigned` and the three request-object `…-supported-correctly-or-rejected-as-unsupported` /
`oidcc-ensure-request-object-with-redirect-uri` tests). They are spec-conformant optional-feature skips: the OP does not
advertise the OPTIONAL `alg:none` value in `id_token_signing_alg_values_supported` /
`request_object_signing_alg_values_supported`, because asymmetric signing is the secure default.

## Harness mechanics

- **Headless auto-fill of screenshot-review placeholders (`oidcc-prompt-login`, `oidcc-max-age-1`).** These two
  double-login modules complete every automated assertion, then park in `WAITING` on a REVIEW "browser-interaction
  placeholder" (OIDCC-3.1.2.1: _"a screenshot of the second login … must be uploaded"_); the suite itself calls the
  request "a little pointless" (`OIDCCPromptLogin.createPlaceholder()`). The harness uses the **suite-native
  configuration knob**: per-module `override` blocks in
  [`oidcc-basic.template.json`](../../build/conformance/oidcc-basic.template.json) add the suite's
  `update-image-placeholder-optional` action to the second login's scripted-browser `wait` command
  (`["wait","xpath","//*",10,"Sign in","update-image-placeholder-optional"]`), so `BrowserControl` snapshots the login
  page as the placeholder image inline while it drives the re-login — the regexp `Sign in` matches the OP's login page
  (`<h1>Sign in</h1>`, `InteractiveAuthenticationPageRenderer`). This mirrors the suite's own
  `.gitlab-ci/local-provider-oidcc-conformance-config.json` override for the same two modules and is deterministic (no
  polling, no separate authenticated POST). Both modules reach `FINISHED`/`REVIEW` in about 3 s with 0 failing
  conditions, and the Basic OP plan runs in about 46 s.
- **A hard wall-clock bound on each plan.** `run-conformance.ps1` runs each `run-test-plan.py` invocation under a
  watchdog (`Invoke-WithDeadline`, `-PlanTimeoutSeconds`, default `1800`) that kills the plan's whole process tree and
  fails fast on overrun, so a suite container that drops out mid-plan cannot hang the run behind the suite script's
  reconnect loop. It also pins the suite script's own retry bounds (`CONFORMANCE_RETRY_TIMEOUT=120`,
  `CONFORMANCE_RESTART_RETRIES=2`, `CONFORMANCE_MAX_CONSECUTIVE_FAILURES=3`) so the suite aborts quickly rather than
  depending on the cloned upstream's defaults. The watchdog is the outer bound and does not depend on those upstream
  knobs. Why the prebuilt suite can become unreachable under the browser plan (container OOM, Docker Desktop limits) is
  an operational tuning concern, not a correctness one.
- **Parallel modules (`parallel=2`).** The Basic OP plan runs two modules at once, and the isolation that makes that
  correct is structural on both sides:
    - **The suite isolates browser state per test module.** Each module gets its own `BrowserControl` (constructed with
      the module's `testId`) carrying a per-instance `CookieManager` (`BrowserControl.java` — the manager is _"shared
      between all webrunners for this testmodule instance"_, and `client.setCookieManager(cookieManager)` applies it to
      every `WebRunner`'s `WebClient`). Concurrent modules therefore do **not** share the OP's login cookie.
    - **Each module registers its own client.** The plan runs under `client_registration=dynamic_client`, so there is no
      shared `alias` and every module has its own client with its own `redirect_uri` — the OP's callback target is bound
      to the request's client, not to any ambient origin-wide value.
    - **The OP's interactive login is request-bound, not ambient.** `PostLoginAsync` round-trips the `ReturnUrl`
      (carrying the parked request's `state` grant key) through the login form and redirects to that validated local
      URL; there is no server-side "current pending request" that a concurrent flow could cross into
      (`InteractiveAuthenticationEndpointHandler.PostLoginAsync`).

    With per-module cookie jars, per-module DCR clients and request-bound login, cross-module contamination is
    structurally impossible, and full harness runs are green at `parallel=2` (`Conformance run PASSED`, 39 modules,
    `0 failures`).
