# T2 — Browser-automatable login + consent UI (with a seeded conformance identity)

Status: **implemented.** Detailed plan for backlog item `T2`. Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #2 and Appendix B). Decision
records: [ADR-0062](../adr/0062-opt-in-interactive-login-consent-ui.md) (seam + stable-locator contract),
[ADR-0065](../adr/0065-interactive-login-consent-op-hosted-auth-surface.md) (realized as the OP-hosted
`apps/auth` surface), [ADR-0070](../adr/0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md)
(leaf capability package + pluggable credential verifier). Catalog entry:
[`BACKLOG.md`](../../BACKLOG.md) group `T`.

The interactive login/consent surface landed as the opt-in leaf capability package
[`NCode.Identity.OpenId.Authentication.Interactive`](../../src/NCode.Identity.OpenId.Authentication.Interactive)
(+ [`.Abstractions`](../../src/NCode.Identity.OpenId.Authentication.Interactive.Abstractions)): a single
`AddInteractiveAuthentication` / `UseInteractiveAuthentication` registration
([`DefaultRegistration`](../../src/NCode.Identity.OpenId.Authentication.Interactive/DefaultRegistration.cs))
registers the `Identity.Application` cookie scheme (`LoginPath = /account/login`) the authorize challenge
redirects into, and maps the `/account/*` endpoints through an `IEndpointProvider`
([`InteractiveAuthenticationEndpointHandler`](../../src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointHandler.cs)).
Credential verification is a pluggable `IInteractiveCredentialVerifier` seam whose local-account
implementation ([`LocalAccountInteractiveCredentialVerifier`](../../src/NCode.Identity.OpenId.Accounts.Authentication/LocalAccountInteractiveCredentialVerifier.cs),
registered via `AddLocalAccountInteractiveAuthentication()`) reuses the resource-owner-password credential
path. The render surface is the React + MUI `apps/auth` bundle ([`src/frontend/apps/auth`](../../src/frontend/apps/auth)),
served same-origin under `/account/*`, exposing the Appendix B locators (`username`, `password`, `login`,
`consent-allow`, `consent-deny`). The Playground opts in
([`Program.cs`](../../src/NCode.Identity.OpenId.Playground/Program.cs)) and the endpoints are covered by
integration and unit tests
([`InteractiveLoginAntiforgeryTests`](../../tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/InteractiveLoginAntiforgeryTests.cs),
[`InteractiveLogoutTests`](../../tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/InteractiveLogoutTests.cs),
[`InteractiveAuthenticationEndpointHandlerTests`](../../tests/NCode.Identity.OpenId.Authentication.Interactive.Tests/InteractiveAuthenticationEndpointHandlerTests.cs)).
Consent went beyond the auto-approve stopgap below: durable, granular consent now enforces through this
same surface ([ADR-0076](../adr/0076-consent-persistence-and-enforcement.md),
[ADR-0081](../adr/0081-granular-consent-and-authorized-applications.md)). The remaining conformance
prerequisite is the reachable HTTPS host with a byte-matched `issuer` (`T3`).

The rest of this document is the original planning-time plan, retained as a record.

## Goal

Give the OpenID Provider an **interactive login and consent experience** that the OpenID Foundation
Conformance Suite's headless `BrowserControl` (Selenium) can drive end-to-end: stable element locators, a
deterministic signed-in session, and a consent outcome — so a protocol-correct authorization flow can
actually complete under automation. Without this, full conformance automation is impossible even though
the wire protocol is correct.

## Current state at planning time (superseded by the implementation summary above)

- **No interactive UI exists at all.** There are zero `.cshtml` / `.razor` / `.html` assets and no
  minimal-API HTML endpoints anywhere in `src/` or `tests/`. The authorization flow has no login screen
  and no consent screen.
- **The challenge is wired but has nowhere to go.** The authorize flow authenticates and challenges
  through standard ASP.NET Core schemes:
    - [`DefaultAuthenticateHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs)
      calls `httpContext.AuthenticateAsync(scheme)` where `scheme` comes from the
      `authorization_authenticate_scheme` setting (default `IdentityConstants.ApplicationScheme` =
      `"Identity.Application"`).
    - [`DefaultChallengeHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultChallengeHandler.cs)
      calls `httpContext.ChallengeAsync(scheme)` where `scheme` comes from the
      `authorization_challenge_scheme` setting (same default).
    - Scheme defaults are declared in
      [`DefaultSettingDescriptorDataSource`](../../src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs).
- **The Playground never registers an authentication scheme.** [`Program.cs`](../../src/NCode.Identity.OpenId.Playground/Program.cs)
  calls `app.UseAuthentication()` / `app.UseAuthorization()` but has **no `AddAuthentication(...)`
  registration**, so a challenge today has no cookie handler and no `LoginPath` to redirect to.
- **Integration tests bypass the UI.** Tests use
  [`TestSubjectAuthenticationHandler`](../../tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/TestSubjectAuthenticationHandler.cs),
  which reads an `X-Test-Subject` header. This is header-driven, not browser-drivable, so it cannot
  satisfy the suite's Selenium login task.
- **Consent is an open feature gap.** `DefaultAuthorizeHandler` carries `// TODO: check consent`; durable
  consent persistence/enforcement is tracked separately as `F2`. T2 does **not** depend on `F2` — see
  "Consent strategy" below.

## Approach

Add a **minimal, opt-in, dev-grade interactive authentication module** (cookie scheme + login page +
consent page) that the Playground enables and that satisfies the Appendix B locator contract. Keep it
self-contained and clearly dev/conformance-oriented; it is not a production login experience.

Recommended placement: a small reusable opt-in extension (e.g. `AddInteractiveDevAuthentication()`)
surfaced from the composition/server layer so the Playground opts in with one call, rather than
hand-rolled pages buried in the Playground. (If the reusable surface proves heavier than the payoff, a
Playground-local implementation is an acceptable fallback — the locator contract and behavior are what
conformance depends on, not the package boundary.)

### UI stack

Two shapes satisfy the conformance contract equally, so the choice is about reuse beyond conformance, not
about passing the suite:

- **Minimal server-rendered HTML** (simplest): a couple of `GET/POST` endpoints returning static forms.
  Lowest tooling cost; ideal if the pages stay conformance-only.
- **React + Material UI (MUI)** (preferred if this grows into a real end-user UI): the project's preferred
  frontend stack. A small SPA (or server-rendered shell hosting React) renders the login/consent forms.

Either way, these are **hard constraints** the stack must preserve: the server-driven cookie
challenge/`LoginPath` redirect flow stays as-is (the SPA is only the form surface, not a replacement auth
model), and the rendered DOM must expose the **exact Appendix B locators** (`id=username`, `id=password`,
`id=login`, `id=consent-allow`, `id=consent-deny`) as stable ids MUI does not mangle — set them explicitly
(e.g. MUI `id`/`inputProps`), since MUI's generated ids are not stable selectors. If a React build is
introduced, keep it dev/conformance-scoped and out of the hermetic `dod.ps1` inner loop.

### Authentication scheme

- Register a **cookie** authentication scheme named `IdentityConstants.ApplicationScheme`
  (`"Identity.Application"`) so it lines up with the existing `authorization_authenticate_scheme` /
  `authorization_challenge_scheme` defaults — no per-client setting override needed.
- Configure `LoginPath = "/account/login"`, `LogoutPath = "/account/logout"`, and a consent redirect
  (see below). On challenge, ASP.NET Core redirects to `LoginPath?ReturnUrl=<authorize-url>`.

### Login page (`GET/POST /account/login`)

- Render a minimal HTML form with the **exact locators** from Appendix B: `id=username`, `id=password`,
  and a submit control locatable as `id=login` (keep `name=login` too). Include the ASP.NET Core
  antiforgery hidden field (the Playground already calls `AddAntiforgery()` / `UseAntiforgery()`); the
  rendered form carries the token so Selenium's form POST passes.
- On POST, **verify credentials against the local-account store** — reuse the same credential path the
  resource-owner password grant already uses (the account stack registered via
  `AddLocalAccountAuthentication()` / `AddLocalAccountEntityFramework()` in
  [`Program.cs`](../../src/NCode.Identity.OpenId.Playground/Program.cs)). Do not introduce a second
  credential verifier.
- On success, `HttpContext.SignInAsync("Identity.Application", principal)` with the subject claim the OP
  expects, then redirect to the validated local `ReturnUrl`. Reject non-local `ReturnUrl` values (open
  redirect guard).

### Consent strategy

The Basic/Config OP profiles do not require a durable consent record. To minimize selector flakiness,
default the conformance client to an **auto-approve** path (no consent screen), while still shipping a
consent page for negative-path tests:

- Provide `GET/POST /account/consent` exposing `id=consent-allow` and `id=consent-deny`.
- For the seeded conformance client, auto-approve (skip the screen) by default; keep the deny control
  reachable for the suite's negative tests.
- This is intentionally decoupled from `F2` (consent persistence). If/when `F2` lands, this dev consent
  screen becomes a thin front-end over the persisted-consent decision rather than a parallel mechanism.

### Seeded identity

The login requires a deterministic user. That seeding is owned by `T4`
([`conformance-seeding.md`](conformance-seeding.md)); T2 **consumes** it (fixed username/password in the
conformance tenant) and must not re-seed independently.

## Work breakdown

1. Add the opt-in interactive-auth registration (cookie scheme + endpoints) and wire the Playground to
   call it under Development/conformance only.
2. Implement `GET/POST /account/login` with the Appendix B locators, antiforgery, local-account
   credential verification, cookie sign-in, and local-`ReturnUrl` validation.
3. Implement `GET/POST /account/consent` (`id=consent-allow` / `id=consent-deny`) plus the auto-approve
   path for the conformance client.
4. Confirm the end-to-end challenge → login → (auto-)consent → redirect-back loop against a locally
   launched host (depends on `T3`).
5. Keep the markup locators in sync with the Appendix B / Appendix D `browser` config contract.

## Acceptance criteria

- A challenge from the authorize endpoint redirects to `/account/login` and the rendered form exposes
  `id=username`, `id=password`, and `id=login`.
- Posting valid seeded credentials establishes an `Identity.Application` cookie session and returns to
  the original authorize URL, which then completes with an authorization code.
- The conformance client auto-approves consent (no screen), and `/account/consent` still renders
  `id=consent-allow` / `id=consent-deny` for negative tests.
- The whole loop is drivable headlessly by the Appendix D `browser` task block.

## Dependencies & links

- **Depends on** `T4` (seeded conformance identity) and `T3` (reachable host for end-to-end verification).
- **Independent of** `F2` (consent persistence) and `F6` (DCR).
- Locator contract: [`conformance-testing.md`](../conformance-testing.md) Appendix B; `browser` config:
  Appendix D.
