# 82. Playground flow explorer, conformance posture dashboard, and surface-aware bootstrap

- **Status:** Accepted (developer SPA client seeding amended by
  [ADR-0146](0146-a-development-seeder-registers-only-the-clients-its-configuration-declares.md): the seed has no
  built-in clients, and the host declares them beside the client ids it publishes; surface declaration amended by
  [ADR-0156](0156-shared-app-shell-containers-live-in-ncode-shell.md): each app declares its surface in `main.tsx`, and
  `SurfaceKind` is an open vocabulary)
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

The frontend presentation platform ([ADR-0064](0064-frontend-presentation-platform.md)) landed with the Management
console (`apps/mgmt`) and the interactive login/consent surface (`apps/auth`), plus two **scaffold** apps —
`apps/playground` and `apps/conformance` — that only rendered a placeholder card. Issue [#135](https://github.com/NCodeGroup/Identity/issues/135) is to flesh those
two scaffolds into real surfaces and give them a serving model.

Two design questions had to be settled first:

- **What should each surface actually be?** The Playground is clearly "an interactive OAuth/OIDC flow explorer". The
  conformance surface was described in point-in-time issue text as "the browser-drivable harness control surface
  that [#124](https://github.com/NCodeGroup/Identity/issues/124)/[#125](https://github.com/NCodeGroup/Identity/issues/125) drive" — but the Selenium-driven login/consent surface the external suite automates is already
  `apps/auth` ([#125](https://github.com/NCodeGroup/Identity/issues/125)). Per the greenfield, security-practice-default posture
  ([repository instructions](../../.github/instructions/repository.instructions.md),
  [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)),
  existing prose is a point-in-time artifact, so the surface was (re)defined by what a modern, security-aware identity
  platform should offer.
- **How does each standalone SPA learn its own public client id?** Each interactive surface is its own public client
  with its own exact-match redirect URIs, but the platform's "one bundle, every tier" invariant forbids baking tier
  configuration at build time — coordinates are discovered at boot from `/config/bootstrap.json`. That single endpoint
  previously published exactly one client id (the Management console's), which cannot serve multiple surfaces.

## Decision

**Build both surfaces as real, standalone Vite SPAs on the shared `@ncode/*` platform, and make the host bootstrap
document surface-aware so one endpoint can hand each surface its own coordinates.** Concretely:

- **`apps/playground` — an interactive OAuth 2.1 / OpenID Connect flow explorer.** It discovers the provider and walks
  the **authorization-code + PKCE (S256)** flow end to end, surfacing every artifact (the PKCE verifier/challenge,
  `state`, OIDC `nonce`, the authorize URL, the token response, decoded JWT header/payload claims), then exercises
  UserInfo, refresh, and RP-Initiated Logout. It deliberately offers **only** the authorization-code flow — implicit,
  hybrid front-channel-token, and resource-owner-password flows are not selectable, matching the security-practice
  default. The flow orchestration lives in a UI-free, unit-tested module (`flow.ts`) composed from the shared
  `@ncode/auth` primitives; the surface adds `nonce` support to `@ncode/auth`'s `buildAuthorizeUrl` (OIDC replay
  binding).
- **`apps/conformance` — a conformance & security-posture dashboard.** It fetches the provider's full published
  discovery metadata and audits it (UI-free, unit-tested `audit.ts`) against current authoritative guidance — OAuth
  2.1, the Security BCP (RFC 9700), OpenID Connect Core/Discovery, and RFC 7636/8414/7009/7662/9126 — flagging
  insecure or deprecated configuration (implicit/hybrid response types, the `implicit`/`password` grants, missing or
  `plain`-only PKCE, unsigned or symmetric ID-token signing, plaintext endpoints) and confirming required Config-OP
  metadata. It derives Basic/Config **OP profile readiness** from the findings and surfaces the external OpenID
  Foundation Conformance Suite's configuration (issuer, alias, the RP callback URL the OP must accept). It is
  read-only and discovery-driven, so it needs **no** OAuth client. It does **not** re-implement the Selenium-driven
  login/consent surface — that remains `apps/auth` ([#125](https://github.com/NCodeGroup/Identity/issues/125)).
- **Serving model — standalone Vite SPAs, like `apps/mgmt`.** Both are pinned-port Vite dev servers (`playground` →
  `5174`, `conformance` → `5175`) that proxy `/config/bootstrap.json` to the OP host and join the Aspire AppHost graph
  as one `AddViteApp` line each, health-gated on the API ([ADR-0071](0071-aspire-local-dev-orchestrator.md)). The
  Playground drives a real redirect round trip, so it has a seeded public `playground-spa` client (its `5174` callback)
  and a developer CORS origin; the conformance dashboard needs only the discovery read (CORS origin, no client).
- **Surface-aware bootstrap.** `@ncode/config`'s `loadRuntimeConfig` takes the bundle's `SurfaceKind` and sends it as
  the `?surface=` query parameter; each app declares its surface in its `bootstrap.ts` (`mgmt`, `playground`,
  `conformance`). The host (`FrontendBootstrapOptions.Surfaces`) maps a surface name to a per-surface override whose
  **client id and scopes replace** the default (so a read-only surface can publish _no_ client) while feature flags
  merge and branding falls back. An unknown or absent surface receives the host default. The multi-surface SPA client
  seeding generalized `DeveloperInteractiveSeedOptions` from one SPA client to a list (`SpaClients`).

## Options considered

- **Conformance surface = the login/consent locator surface.** Rejected: that is `apps/auth` ([#125](https://github.com/NCodeGroup/Identity/issues/125)); duplicating it
  would fork the stable-locator contract. A read-only posture/readiness dashboard is the non-overlapping, modern
  value-add.
- **Backend-served bundles (like `apps/auth`) instead of standalone Vite.** Rejected: both surfaces are operator/dev
  tools with no reason to be OP-hosted; standalone HMR dev servers match `apps/mgmt` and keep the OP host focused.
- **Per-app build-time client id (env/`.env`).** Rejected: violates the platform's "one bundle, every tier" invariant
  ([ADR-0064](0064-frontend-presentation-platform.md)); the client id is a deployment coordinate discovered at runtime.
- **A separate bootstrap endpoint (or path) per surface.** Rejected in favor of one endpoint keyed by a `?surface=`
  query parameter: fewer routes, and the `SurfaceKind` union already modeled the four surfaces, so the query parameter
  is the smallest cohesive extension. (Origin-keying cannot work because the standalone dev servers proxy the bootstrap
  request, so the host sees its own origin, not the SPA's.)
- **Playground offers every response type/flow for completeness.** Rejected: the security-practice default is to expose
  only the recommended flow; an explorer that can emit implicit/hybrid would normalize insecure patterns.

## Consequences

- The family gains a developer flow explorer and a security-posture/conformance dashboard, both on the shared platform,
  inheriting its theme, discovery, store, and testing conventions.
- The bootstrap contract is now surface-aware: the host publishes per-surface coordinates and bundles must declare
  their surface. `DeveloperInteractiveSeedOptions` changed shape (`SpaClients` list; the single `SpaClientId`/
  `SpaRedirectUris`/`SeedSpaClient` members were removed) — expected `PublicAPI.Unshipped` churn under the pre-release
  posture.
- The conformance dashboard encodes our reading of the security specs in `audit.ts`; it complements, but does not
  replace, the independent external OpenID Foundation Conformance Suite run ([#124](https://github.com/NCodeGroup/Identity/issues/124)). The suite remains the authoritative
  oracle; this dashboard is the fast, local pre-flight.
- The Aspire full-stack launch now brings up four UI surfaces (`mgmt`, `auth`, `playground`, `conformance`) behind the
  health-gated API.

## References

- Issues: [#135](https://github.com/NCodeGroup/Identity/issues/135) (frontend), [#124](https://github.com/NCodeGroup/Identity/issues/124)/[#125](https://github.com/NCodeGroup/Identity/issues/125) (conformance).
- Platform: [ADR-0064](0064-frontend-presentation-platform.md) (presentation platform and its invariants),
  [ADR-0071](0071-aspire-local-dev-orchestrator.md) (Aspire local-dev orchestrator),
  [ADR-0062](0062-opt-in-interactive-login-consent-ui.md) / [ADR-0065](0065-interactive-login-consent-op-hosted-auth-surface.md)
  (the `apps/auth` login/consent surface this does not duplicate).
- Posture: [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) and the security-practice default in
  [`.github/instructions/repository.instructions.md`](../../.github/instructions/repository.instructions.md).
- Conformance plan: [`docs/conformance-testing.md`](../conformance-testing.md).
- Platform guide: [`src/frontend/README.md`](../../src/frontend/README.md).
