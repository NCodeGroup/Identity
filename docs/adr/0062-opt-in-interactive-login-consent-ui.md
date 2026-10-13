# 62. An opt-in interactive login/consent UI for the OpenID Provider

- **Status:** Accepted
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

The OpenID Provider has no interactive surface. There are no `.cshtml` / `.razor` / `.html` assets and no
HTML-returning endpoints anywhere in the family; the authorization flow authenticates and challenges
through standard ASP.NET Core authentication schemes and expects the **host** to supply the login
experience. [`DefaultAuthenticateHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs)
calls `httpContext.AuthenticateAsync(scheme)` and
[`DefaultChallengeHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultChallengeHandler.cs)
calls `httpContext.ChallengeAsync(scheme)`, where `scheme` resolves from the
`authorization_authenticate_scheme` / `authorization_challenge_scheme` settings (default
`IdentityConstants.ApplicationScheme`, `"Identity.Application"`, declared in
[`DefaultSettingDescriptorDataSource`](../../src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs)).
The reference [`Playground`](../../src/NCode.Identity.OpenId.Playground/Program.cs) calls
`UseAuthentication()` / `UseAuthorization()` but registers **no** authentication scheme, so a challenge
today has nowhere to redirect. Integration tests sidestep this entirely with
[`TestSubjectAuthenticationHandler`](../../tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/TestSubjectAuthenticationHandler.cs),
which fabricates a subject from an `X-Test-Subject` header.

Independent conformance validation against the OpenID Foundation Conformance Suite (backlog `T1`,
[`conformance-testing.md`](../conformance-testing.md)) changes this from "host's problem" to "the
reference OP must be certifiable out of the box." The suite drives the interactive flow with a headless
Selenium `BrowserControl` that locates fields by stable id/name and completes login (and optionally
consent) before the callback. A header-driven fake is not browser-drivable, and a UI-less OP cannot
complete an authorization-code flow under automation even when the protocol is perfectly correct. The
reference host therefore needs a real, scriptable login/consent surface — the one prerequisite neither
Dynamic Client Registration (`F6`) nor any harness plumbing provides.

This is a genuinely new concern for a family that has, by design, been UI-free. The forces in tension:
the surface must be driveable and deterministic, must not fork the existing authentication seam or the
local-account credential path, must not pull a production-grade identity UI into a library whose job is
the protocol, and must stay out of the hermetic `dod.ps1` inner loop.

## Decision

**Introduce an opt-in, dev/conformance-grade interactive authentication module — a cookie scheme plus a
login endpoint and a consent endpoint — that satisfies the OP's existing challenge/authenticate seam and
exposes a fixed set of stable element locators. The server-driven cookie challenge flow is preserved
unchanged; the UI is only the form surface, not a new authentication model. The stable-locator contract
is the invariant; the rendering technology is an implementation choice behind it.** Concretely:

- **Satisfy the existing seam, do not replace it.** Register a cookie scheme named
  `IdentityConstants.ApplicationScheme` so it lines up with the `authorization_authenticate_scheme` /
  `authorization_challenge_scheme` defaults with no per-client override, and set its `LoginPath` so the
  OP's existing `ChallengeAsync` redirects into the login page. The authorize → authenticate → challenge
  pipeline is untouched.
- **Reuse the local-account credential path.** The login endpoint verifies credentials through the same
  local-account store the resource-owner password grant already uses
  (`AddLocalAccountAuthentication()` / `AddLocalAccountEntityFramework()`,
  [ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md)); it introduces
  no second credential verifier. On success it issues the cookie via `SignInAsync` and returns to a
  validated local `ReturnUrl`.
- **A stable locator contract is the architectural invariant.** The rendered DOM must expose explicit,
  stable ids — `username`, `password`, `login`, and `consent-allow` / `consent-deny` — matching the
  `browser` automation config ([`conformance-testing.md`](../conformance-testing.md) Appendices B and D).
  Whatever renders the form must set these ids explicitly.
- **Rendering technology is deferred behind the contract.** Minimal server-rendered HTML and a React +
  Material UI surface are equally acceptable to the suite; the choice is about reuse beyond conformance.
  React + MUI is the preferred stack if this grows into a real end-user UI, provided the stable ids are
  set explicitly (MUI's generated ids are not stable selectors) and any build stays dev/conformance-scoped
  and out of `dod.ps1`.
- **Consent is a decoupled stopgap.** The conformance client auto-approves consent by default (fewer
  flaky selectors), with a consent page still exposing `consent-allow` / `consent-deny` for negative
  tests. This is intentionally independent of durable consent persistence (`F2`,
  `// TODO: check consent` in `DefaultAuthorizeHandler`); when `F2` lands, this screen becomes a thin
  front-end over the persisted decision rather than a parallel mechanism.
- **Opt-in and dev-scoped.** The module is a single opt-in registration (e.g.
  `AddInteractiveDevAuthentication()`) the Playground enables under the conformance profile; it is not a
  production login experience and is never wired by default.

The remaining conformance prerequisites — a reachable HTTPS host with a byte-matched `issuer`
(`T3`/[`reachable-host.md`](../conformance/reachable-host.md)), deterministic tenant/client/user seeding
(`T4`/[`conformance-seeding.md`](../conformance/conformance-seeding.md)), and discovery-metadata
alignment (`T5`/[`discovery-alignment.md`](../conformance/discovery-alignment.md)) — are configuration and
operational work that reuse existing seams (the `tenant_issuer` setting, the bootstrap-admin-style
seeder, the data-driven discovery handler) and need no architectural decision of their own.

## Options considered

- **Header-driven fake authentication (status quo in tests).** `TestSubjectAuthenticationHandler` is
  trivial but not browser-drivable and carries no real credential check — it cannot satisfy a Selenium
  login task. Fine for integration tests, unusable for conformance.
- **No UI; rely on the integrating host.** Keeps the library UI-free but leaves the _reference_ OP
  uncertifiable out of the box and gives no canonical answer to "what does the challenge redirect to,"
  which every adopter then re-invents. Rejected because conformance is a property of the reference host.
- **Full production ASP.NET Core Identity UI.** Over-scoped for the goal and duplicative: the local-account
  store already owns credentials ([ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md)).
  A production identity UI is a separate concern, not a conformance prerequisite.
- **Placement — Playground-local pages vs. a reusable opt-in module.** A reusable module wins: it gives
  adopters a turnkey dev login, keeps the Playground thin, and localizes the locator contract. Playground-local
  pages remain an acceptable fallback if the reusable surface proves heavier than its payoff — the contract,
  not the package boundary, is what conformance depends on.
- **Rendering — server-rendered HTML vs. React + MUI.** Left open deliberately; both satisfy the suite.
  Server-rendered HTML is lowest-cost if the pages stay conformance-only; React + MUI is preferred if the
  surface becomes a real end-user UI. The decision that matters is the stable-locator contract, which both
  honor.

## Consequences

- The OpenID Provider gains its first interactive surface. It is small, opt-in, and dev-scoped, but it is
  a new concern to maintain and an obvious future target for a production-grade replacement.
- `T1` conformance automation becomes reachable: the authorize flow can complete end-to-end under
  headless Selenium once `T3`/`T4` provide a reachable host and a seeded identity.
- The stable-selector contract becomes a maintenance invariant coupled to the suite's `browser` config —
  changing an id without updating the config (or vice versa) silently breaks automation. The ids live in
  one place and are reviewed with the config.
- Consent here is a stopgap. Until `F2`, approval is not persisted; the auto-approve default must converge
  onto the persisted-consent decision when `F2` lands, or the two will drift.
- Because the module reuses the existing scheme seam and local-account store, it adds no new
  authentication model, credential store, or persistence — only a cookie scheme and two endpoints.

## References

- Backlog items `T1`–`T5`: [`BACKLOG.md`](../../BACKLOG.md) group `T`.
- Conformance design and automation plan: [`conformance-testing.md`](../conformance-testing.md)
  (Appendix A discovery, Appendix B locator contract, Appendix C client seeding, Appendix D `browser`
  config).
- Prerequisite plans: [`login-consent-ui.md`](../conformance/login-consent-ui.md) (`T2`),
  [`reachable-host.md`](../conformance/reachable-host.md) (`T3`),
  [`conformance-seeding.md`](../conformance/conformance-seeding.md) (`T4`),
  [`discovery-alignment.md`](../conformance/discovery-alignment.md) (`T5`).
- Reused seams: [ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md)
  (local-account store + dependency-inverted authentication),
  [ADR-0030](0030-userinfo-endpoint-and-subject-authentication-seam.md) (subject-authentication seam).
- Code: [`DefaultAuthenticateHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs),
  [`DefaultChallengeHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultChallengeHandler.cs),
  [`Playground/Program.cs`](../../src/NCode.Identity.OpenId.Playground/Program.cs).
