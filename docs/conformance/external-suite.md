# T6 / T1 — External conformance suite as the oracle (full automation)

Status: **implemented (committed harness); nightly CI pipeline deferred.** Detailed plan for the remaining
Phase 4 of backlog `T6` and Phase D of `T1`. Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md). Decision record:
[ADR-0084](../adr/0084-spec-coverage-conformance-harness.md) (the `browser` / `probe-runner` / `external-suite`
exercise boundary). Catalog entry: [`BACKLOG.md`](../../BACKLOG.md) group `T`.

## Goal

Wire the external **OpenID Foundation Conformance Suite** — the certified, spec-author-maintained oracle — as
the authoritative gate behind the fast in-house probe-runner pre-flight, as one committed, runnable command.
This lifts the copy-ready templates in [`docs/conformance-testing.md`](../conformance-testing.md) Appendices
D–H out of the doc and into version-controlled, executable infrastructure.

## What was implemented

The committed harness is [`build/conformance/`](../../build/conformance):

- **[`run-conformance.ps1`](../../build/conformance/run-conformance.ps1)** — the orchestration (Phase D). It
  launches the OpenID host on the reachable HTTPS origin (`host.docker.internal:5001`, the `Conformance`
  launch profile — `T3`), asserts the discovery `issuer` byte-matches that origin, runs the in-house probe
  runner as a **fast pre-flight** that fails early before the slow external suite is started, brings the suite
  up from **prebuilt Docker images** (no Java build), drives the Config OP + Basic OP plans headlessly via the
  suite's `run-test-plan.py` gated on the committed baselines, and tears everything down — surfacing one
  non-zero exit code on any unexpected failure.
- **Config templates** resolved against the deterministic seed (`T4`):
  [`oidcc-basic.json`](../../build/conformance/oidcc-basic.json) (with the `browser` automation block whose
  selectors — `username` / `password` / `login` / `consent-allow` — match the `apps/auth` surface, `T2`) and
  [`oidcc-config.json`](../../build/conformance/oidcc-config.json).
- **Baselines** [`expected-failures.json`](../../build/conformance/expected-failures.json) /
  [`expected-skips.json`](../../build/conformance/expected-skips.json) (start empty; an entry codifies only a
  documented, intentional deviation and must link a `BACKLOG.md` item or ADR).

The pre-flight is the already-shipped `@ncode/app-conformance-runner` (ADR-0084 Phase 3), which exercises the
confidential and server-initiated legs a browser cannot (`client_credentials`, introspection/revocation with
client auth, device-code polling, CIBA, `private_key_jwt`, DPoP/mTLS, back-channel logout) and exits non-zero
on any failed probe. Running it first gives fast local feedback before the heavier suite spins up.

## Exercise boundary (why both)

Per [ADR-0084](../adr/0084-spec-coverage-conformance-harness.md), coverage is split across three actors and the
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

## Remaining

- **Nightly CI pipeline** (a `T6` remainder). No `.github/workflows` exists yet; a separate, opt-in /
  nightly job (`docker compose` + Python 3, no local Java) that invokes `run-conformance.ps1` and publishes the
  `.results/` zips as artifacts is the last step. It is intentionally deferred from the committed harness so the
  wiring is introduced alongside the repo's broader CI bootstrap rather than as a one-off.
- **Basic OP now runs end-to-end under Dynamic Client Registration.** The **Config OP** plan passes cleanly
  (`44 successes, 0 failures, 0 warnings`). The **Basic OP** browser profile originally interrupted at
  `Unable to locate element with ID: 'username'` (the suite's default `BrowserControl` is a no-JavaScript
  **HtmlUnitDriver** and the login surface was a client-rendered SPA); that is **fixed** by the no-JS progressive
  `<form>`. The config was then switched to **Dynamic Client Registration** (dropping the static `alias` + `client`/
  `client2` so the suite registers a fresh client per module — [ADR-0063](../adr/0063-dynamic-client-registration.md),
  with `registration_endpoint_allow_anonymous=true` in `appsettings.Conformance.json`), which removed the
  `Stopping test due to alias conflict` serialization. The DCR switch surfaced and **fixed** two further backend bugs:
  the deregistration `DELETE /oauth2/register/{client_id}` failing on a client that still owned a secret
  (`ClientStore.RemoveAsync` guard was blind to same-unit-of-work pending deletes), and **generated `client_secret`s
  failing `invalid_client`** because a symmetric secret was stored as raw bytes instead of the UTF-8 bytes client
  authentication compares against (new `ISecretGenerator.GenerateSharedSecret` —
  [ADR-0110](../adr/0110-shared-secret-vs-cryptographic-key-storage.md)). Two conformance-config fixes were also needed:
  the `Verify Complete` callback match uses `/test/*/callback*` (DCR has no `/a/<alias>/` segment), and the `Login` task
  is `optional` (so no-login `prompt=none` / `max_age` flows do not error). **First full Basic OP (DCR) run:**
  `18 PASSED, 11 WARNING, 4 SKIPPED, 4 FAILED` across 38 modules with no mass-abort. Triaging those dispositions is
  tracked as backlog `T8`.
- **Profile expansion** beyond Basic + Config (Form Post / Dynamic OP / FAPI) as those capabilities are
  verified end-to-end.

## Live-run dispositions (`T8`) — fixes applied and open follow-ups

Backlog catalog entry: [`BACKLOG.md`](../../BACKLOG.md) `T8`. This section is the detailed disposition log for the
Basic OP (DCR) browser-driven run — the module verdicts produced by the external suite, each investigated against the
suite's per-module log and classified as a **backend gap to fix**, a **harness/config refinement**, or a **documented,
intentional deviation** codified in [`expected-failures.json`](../../build/conformance/expected-failures.json) /
[`expected-skips.json`](../../build/conformance/expected-skips.json).

The first full run completed `18 PASSED, 11 WARNING, 4 SKIPPED, 4 FAILED` (38 modules, no mass-abort). **After the
fixes below the plan runs clean** — `32 PASSED, 0 WARNING, 3 REVIEW, 4 SKIPPED, 0 FAILED` (39 modules incl. the Config
OP) with a green gate (`Conformance run PASSED`); a `REVIEW` result with no failing conditions does not gate the run.

`expected-failures.json` holds one intentional deviation: since every authorization code is bound to PKCE
([ADR-0144](../adr/0144-authorization-codes-are-bound-to-pkce-by-default.md)), the conformance host relaxes
`require_pkce` and attests the suite's clients with `pkce_nonce_exemption`, so its nonce-carrying requests need no
`code_challenge` — but `oidcc-ensure-request-without-nonce-succeeds-for-code-flow` sends neither a challenge nor a nonce,
which leaves the code unprotected against injection, and the OP refuses it with `invalid_request` (OAuth 2.1 §7.5.1.1).
The first run with this baseline completed `31 PASSED, 0 WARNING, 3 REVIEW, 4 SKIPPED, 1 FAILED` (39 modules incl. the
Config OP; the failure is the expected one, on `CheckIfAuthorizationEndpointError`) with a green gate.

### Fixed backend bugs (see [`CHANGELOG.md`](../../CHANGELOG.md))

- **`oidcc-refresh-token`** — the consent screen (no-JS form + SPA) pre-checked a requested scope's checkbox only when
  it was _previously_ granted, so a first-time `prompt=consent` + `offline_access` consent posted an empty `scope` set;
  the recorded grant lost `openid`/`offline_access` and the token response dropped the `id_token` and `refresh_token`.
  The consent screen now pre-selects all requested scopes.
- **Token-endpoint `Cache-Control: no-store`** — RFC 6749 §5.1/§5.2 require it (and `Pragma: no-cache`) so tokens are
  never cached; the OP omitted them. Now set on every token-endpoint response. Revealed by `oidcc-refresh-token` once
  the consent fix let its refresh flow reach the refresh request (56 → 157 passing conditions).
- **`oidcc-prompt-none-not-logged-in`** — the inline `prompt=none` → `login_required` (and no-consent-surface →
  `interaction_required`) errors omitted `state` (they bypass the `Authenticate`/`Authorize` post-processors that stamp
  it). Both now echo the request `state` (OIDC Core §3.1.2.6).
- **`oidcc-max-age-1`** — authentication-age freshness added the 5-min `clock_skew` (so `max_age=1` never forced
  re-auth) **and** was re-evaluated at the token/refresh endpoints against a later clock (rejecting valid exchanges with
  `access_denied`). It now uses a pure elapsed-time comparison and runs only at the authorization endpoint via
  `ValidateSubjectAuthenticationCommand.EnforceAuthenticationAge`.
- **`oidcc-prompt-none-logged-in` / `oidcc-id-token-hint` (intermittent, all-or-nothing per process)** — a
  value-type-default bug in `SettingDescriptor<TValue>` made `HasDefault` always `true` for a value-type `TValue` (the
  `notnull`-constrained `TValue?` is not `Nullable<TValue>`), so the root settings source loaded `default(TValue)` for
  every value-type descriptor lacking an explicit default. For `subject_max_age` that was `TimeSpan.Zero` — a
  zero-second freshness window that rejected every just-completed login and returned `login_required` to a logged-in
  `prompt=none` request. `HasDefault` now tracks an explicit flag, so an unset value-type setting stays genuinely
  absent. A new `IReadOnlySettingCollection.GetValueOrNull<TValue>()` (struct constraint, genuine `Nullable<TValue>`) is
  the reusable optional-value reader. Guarded by a 32-concurrent `prompt=none` integration regression test.
- **`oidcc-ensure-registered-redirect-uri`** — the OP correctly refuses an unregistered `redirect_uri` with a 400
  `unauthorized_client`, but the exception handler logged that expected 4xx client error at Error level as "an
  unexpected error". 4xx `OpenIdException`s now log at Warning. The scripted-browser timeout (its `Verify Complete` step
  waited for a happy-path `submission_complete` callback that correctly never comes) is resolved with a per-module
  `override` block in [`oidcc-basic.json`](../../build/conformance/oidcc-basic.json) that drops the callback wait and
  instead waits for the error page (matched by the `unauthorized_client` error code in the rendered body) and snapshots
  it as the review placeholder — so the module lands on the `REVIEW` the suite expects ("Show redirect URI error page",
  OIDCC-3.1.2.1) instead of `FAILED`, and its `expected-failures.json` baseline was removed.
- **UserInfo scope-claims + ID-token claim placement (`WARNING`: `oidcc-server`,
  `oidcc-scope-{profile,email,address,phone,all}`, `oidcc-alternate-happy-flow`, `oidcc-claims-essential`)** — per OIDC
  Core §5.4 the `profile`/`email`/`address`/`phone` scope claims belong in the **UserInfo** response, not the
  `id_token`, whenever an access token is issued (the code flow). Fixed as a cohesive change: (a)
  `DefaultGetIdTokenSubjectClaimsHandler` now gates the scope-driven claim set on _no access token issued_ (a non-null
  `AccessToken` on the token request signals the code flow), so the ID token no longer carries them — requested
  `claims.id_token` claims, `sub`, `auth_time`, `acr`, `idp`, `amr` are unaffected; (b) a new account-sourced UserInfo
  enricher (`DefaultGetLocalAccountUserInfoClaimsHandler`, registered by `AddLocalAccountAuthentication` /
  `AddLocalAccountInteractiveAuthentication`) resolves the caller's local account from its self-issued connection and
  returns the scope-permitted claims from the account store — it also honors individual claims requested via the OIDC
  `claims` parameter's `userinfo` member (OIDC Core §5.5) regardless of scope, and coerces the standard typed claims
  (`updated_at` → number, `address` → JSON object, `phone_number_verified` → boolean); and (c) the conformance seed now
  provisions the **full** standard `profile`/`address`/`phone` claim set (new `ConformanceSeedOptions.TestUserGivenName`
  / `TestUserFamilyName` for the name parts, deterministic values for the rest). Separately, the ID token no longer
  carries the `tid` and `client_id` claims at all — those are JWT **access-token** claims (RFC 9068 §2.2), not ID-token
  claims, so `DefaultGetIdTokenPayloadClaimsHandler` now emits only the standard `azp`; this cleared the residual
  `oidcc-server` "id_token contains non-requested claim" warning.
- **`acr` emission (`WARNING`: `oidcc-ensure-request-with-acr-values-succeeds`)** — fixed by configuring the conformance
  environment, not the backend: the OP now advertises `acr_values_supported` and sets a `default_acr` baseline (server
  settings in `appsettings.Conformance.json`), so the password login's resolved `acr`
  ([ADR-0091](../adr/0091-acr-emission-and-conditional-auth-time.md)) is emitted and matches the suite's requested
  `acr_values`. The ACR resolver already honored `default_acr`; only the conformance environment lacked a baseline.
- **UserInfo access token in the POST body (`WARNING`: `oidcc-userinfo-post-body`)** — `DefaultAuthenticateSubjectHandler`
  now accepts the bearer token as an `access_token` form parameter (RFC 6750 §2.2 / OIDC §5.3.1) for a non-GET
  `application/x-www-form-urlencoded` request, in addition to the preferred `Authorization: Bearer` header (which takes
  precedence when both are present); the URI-query form (§2.3) stays unsupported because RFC 9700 deprecates it.
- **`alg:none` request object → `request_not_supported` (the `SKIPPED` residual)** — OIDC Core §6.1:
  `DefaultLoadAuthorizationRequestHandler` now detects an unsigned (`alg:none`) request object (inline `request` or a
  fetched `request_uri`) up front and returns exactly `request_not_supported`; a genuinely malformed request object
  still returns `invalid_request_object`.
- **Access-token revocation on authorization-code reuse (`WARNING`: `oidcc-codereuse-30seconds`)** — RFC 6749 §4.1.2
  SHOULD revoke the tokens previously issued from a reused code _"when possible"_. The OP already rejected the second
  code use with `invalid_grant` (one-time codes) and enforces PKCE `S256`, but the suite re-presented the
  previously-issued access token at the resource endpoint and expected a `4xx`. A second redemption of an
  already-consumed code is now treated as a compromise signal: the token endpoint revokes the end-user **session's**
  grants (the access and refresh tokens issued from that code) via the session-scoped grant revocation the logout seam
  uses — minus the back-channel-logout/audit side effects — and the OP's own resource endpoints (UserInfo,
  introspection, management API) reject an access token whose `sid` resolves to a revoked session (`invalid_token`).
  `sid` is now always emitted into subject-bound access tokens (the opt-in `send_session_id_in_access_token` setting is
  retired). No global access-token denylist and no per-token store: one indexed session lookup on first-party
  access-token authentication and one session-scoped revocation on the rare replay path. External resource servers still
  treat the JWT as self-contained. See [ADR-0112](../adr/0112-authorization-code-replay-revokes-session.md) (refines
  [ADR-0028](../adr/0028-token-revocation-refresh-tokens-only.md)); its `expected-failures.json` baseline was removed.

### Codified intentional deviations

The four `SKIPPED` modules (`oidcc-idtoken-unsigned` and the three request-object
`…-supported-correctly-or-rejected-as-unsupported` / `oidcc-ensure-request-object-with-redirect-uri` tests) are
spec-conformant optional-feature skips — the OP does not advertise the OPTIONAL `alg:none` value in
`id_token_signing_alg_values_supported` / `request_object_signing_alg_values_supported` (asymmetric signing is the
secure default) — now in [`expected-skips.json`](../../build/conformance/expected-skips.json).

### Harness refinements

- **Headless auto-fill of screenshot-review placeholders (`oidcc-prompt-login`, `oidcc-max-age-1`)** — these two
  double-login modules complete every automated assertion, then deliberately park in `WAITING` on a REVIEW
  "browser-interaction placeholder" (OIDCC-3.1.2.1: _"a screenshot of the second login … must be uploaded"_). The OP is
  not involved — it already passed all checks (e.g. `auth_time is later in the second id_token`); the suite itself calls
  the request "a little pointless" (`OIDCCPromptLogin.createPlaceholder()`). Headless, nobody uploads the screenshot, so
  each module burned the full 240 s module timeout and left the run `WAITING`/incomplete. Fixed with the
  **suite-native configuration knob**, not an out-of-band upload: per-module `override` blocks in
  [`oidcc-basic.json`](../../build/conformance/oidcc-basic.json) add the suite's `update-image-placeholder-optional`
  action to the second login's scripted-browser `wait` command
  (`["wait","xpath","//*",10,"Sign in","update-image-placeholder-optional"]`), so `BrowserControl` snapshots the login
  page as the placeholder image inline while it drives the re-login — the regexp `Sign in` matches the OP's login page
  (`<h1>Sign in</h1>`, `InteractiveAuthenticationPageRenderer`). This mirrors the suite's own
  `.gitlab-ci/local-provider-oidcc-conformance-config.json` override for the same two modules and is deterministic (no
  polling, no separate authenticated POST, no hang risk). Verified end-to-end: both modules now reach `FINISHED`/`REVIEW`
  in ~3 s with 0 failing conditions, and the Basic OP plan dropped from ~247 s to ~46 s. The earlier background
  PNG-upload watcher in `run-conformance.ps1` (`Start-PlaceholderAutoFiller`) was removed as the redundant, unverified
  predecessor of this deterministic knob.

### Open follow-ups (non-blocking for Basic OP certification)

- **Suite goes unreachable mid-run → `run-test-plan.py` retries forever (wrapper hangs). _(Fixed.)_** When the
  suite's Docker container dropped out mid-plan, the suite script's reconnect loop retried with backoff and
  `run-conformance.ps1` blocked indefinitely instead of failing fast. The wrapper now runs each `run-test-plan.py`
  invocation under a hard wall-clock watchdog (`Invoke-WithDeadline`, `-PlanTimeoutSeconds`, default `1800`) that
  kills the plan's whole process tree and surfaces a fast failure on overrun, and it pins the suite script's own
  retry bounds explicitly (`CONFORMANCE_RETRY_TIMEOUT=120`, `CONFORMANCE_RESTART_RETRIES=2`,
  `CONFORMANCE_MAX_CONSECUTIVE_FAILURES=3`) so the suite self-aborts quickly rather than depending on the
  freshly-cloned upstream's defaults. The watchdog is the durable outer bound — it does not depend on any of those
  upstream knobs still existing. (The underlying resource question — why the prebuilt suite becomes unreachable
  under the browser plan, e.g. container OOM / Docker Desktop limits — is a separate operational tuning concern, no
  longer a correctness hang.)
- **Parallel browser-session cookie collision (`oidcc-server`, `oidcc-idtoken-signature`). _(Resolved — parallelism
  retained.)_** An earlier browser-driven run observed one module's scripted login landing on another module's
  `continue` URL under `parallel=2` (a login submitted for `state=1fEiOJ…` completed on a page for `state=lTOSHx…`, then
  "Unexpected URL for Verify Complete"), raising the question of whether the Basic OP plan should drop to `parallel=1`.
  It should **not** — dropping to serial would forfeit the speed boost, and the isolation that makes the parallel run
  correct is already structural on both sides:
    - **The suite isolates browser state per test module.** Each module gets its own `BrowserControl` (constructed with
      the module's `testId`) carrying a per-instance `CookieManager` (`BrowserControl.java` — the manager is _"shared
      between all webrunners for this testmodule instance"_, and `client.setCookieManager(cookieManager)` applies it to
      every `WebRunner`'s `WebClient`). Concurrent modules therefore do **not** share the OP's login cookie.
    - **Each module registers its own client.** The plan runs under `client_registration=dynamic_client`, so there is no
      shared `alias` and every module has its own client with its own `redirect_uri` — the OP's callback target is bound
      to the request's client, not to any ambient origin-wide value.
    - **The OP's interactive login is request-bound, not ambient.** `PostLoginAsync` round-trips the `ReturnUrl` (carrying
      the parked request's `state` grant key) through the login form and redirects to that validated local URL; there is
      no server-side "current pending request" that a concurrent flow could cross into
      (`InteractiveAuthenticationEndpointHandler.PostLoginAsync`).

    With per-module cookie jars + per-module DCR clients + request-bound login, cross-module contamination is
    structurally impossible; the one earlier observation did not reproduce, and two consecutive full harness runs are
    green at `parallel=2` (`Conformance run PASSED`, 39 modules, `0 failures`). The plan therefore **keeps `parallel=2`**
    for the speed boost.
