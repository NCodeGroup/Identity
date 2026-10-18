# T4 — Deterministic conformance tenant, clients, and test user seeding

Status: **implemented.** Detailed plan for backlog item `T4`. Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #3 and Appendix C). Catalog
entry: [`BACKLOG.md`](../../BACKLOG.md) group `T`.

The config-driven startup seeder landed as `AddConformanceSeed(IConfiguration)` in `NCode.Identity.Server`
([`ConformanceSeedRegistration`](../../src/NCode.Identity.Server/ConformanceSeedRegistration.cs),
[`ConformanceSeedHandler`](../../src/NCode.Identity.Server/ConformanceEnvironment/ConformanceSeedHandler.cs),
[`ConformanceSeedOptions`](../../src/NCode.Identity.Server/ConformanceEnvironment/ConformanceSeedOptions.cs),
[`ConformanceClientSeed`](../../src/NCode.Identity.Server/ConformanceEnvironment/ConformanceClientSeed.cs)). It is
wired into the host
([`Program.cs`](../../src/NCode.Identity.OpenId.Host/Program.cs)) and activated by the `Conformance`
launch profile plus the
[`appsettings.Conformance.json`](../../src/NCode.Identity.OpenId.Host/appsettings.Conformance.json)
overlay, which seeds the three Basic-OP clients and the `conformance-user`. The remaining conformance
prerequisite is the reachable HTTPS host with a byte-matched `issuer` (`T3`).

## Goal

Make a running host start up with a **deterministic conformance tenant**, the **client
registrations** the Basic OP plan needs, and a **fixed test user** — reproducibly, with no manual API
calls — so the suite and the login UI (`T2`) always see the same identities.

## Current state (verified)

- **Seeding exists only in test infrastructure.**
  [`HostApplicationFactory`](../../tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/HostApplicationFactory.cs)
  exposes `SeedPublicClientAsync`, `SeedConfidentialClientAsync(clientId, secret, grantTypes…)`,
  `SeedLocalAccountAsync(userName, password, email?, emailVerified?)`, and
  `SeedResourceServerWithClientGrantAsync(…)`. These run in-process against a per-test in-memory database
  — not available to a launched host.
- **The only startup seeding is the bootstrap admin client.**
  [`BootstrapAdminSeedHandler`](../../src/NCode.Identity.Server/BootstrapAdminSeedHandler.cs) seeds a
  `bootstrap-admin` client from configuration
  ([`launchSettings.json`](../../src/NCode.Identity.OpenId.Host/Properties/launchSettings.json):
  `BootstrapAdmin__ClientId` / `BootstrapAdmin__ClientSecret`). There is **no** startup seeding of a
  conformance tenant, end-user clients, or a test user.
- **Tenant default.** The host runs a `StaticSingle` tenant (`TenantPath: "/"`,
  [`appsettings.json`](../../src/NCode.Identity.OpenId.Host/appsettings.json)); the test factory
  uses tenant id `"default"`.

## Required registrations (Basic OP, manual/non-DCR path)

Per [`conformance-testing.md`](../conformance-testing.md) Appendix C, the Basic OP plan needs **three**
client registrations (all registering the suite callback as a redirect URI):

- **`client` (primary):** `token_endpoint_auth_method = client_secret_basic`; grants
  `authorization_code` + `refresh_token`; scopes `openid profile email`.
- **`client2` (second confidential):** a **distinct** `client_id`, same shape — exists so the suite can
  prove auth codes are bound to the issuing client.
- **`client_secret_post` client:** `token_endpoint_auth_method = client_secret_post` (may be the same
  record as `client` if it accepts either method).
- **Redirect URI (all):** `https://<SUITE_BASE>/test/a/<ALIAS>/callback`
  (local: `https://localhost.emobix.co.uk:8443/test/a/<ALIAS>/callback`).
- **PKCE:** allow `S256`.

Plus a **deterministic test user** (e.g. `conformance-user` + fixed password) in the conformance tenant,
consumed by the `T2` login page.

## Approach

Add a **config-driven startup seeder** for the conformance scenario, modeled on the existing bootstrap
admin seeder (idempotent, declarative, dev/conformance-only), reusing the same store-level operations the
test factory already exercises.

- Introduce a `Conformance` seeding section (clients + user + redirect URIs + scopes) bound from
  configuration, enabled only under the conformance profile from `T3`. Keep secrets as dev-only fixed
  values.
- Make seeding **idempotent**: on startup, upsert the tenant, the three clients, and the test user; a
  second run must not duplicate or fail.
- Reuse the credential/store paths behind `SeedConfidentialClientAsync` / `SeedLocalAccountAsync` so the
  seeded identities are byte-for-byte what the login (`T2`) and token endpoints expect — do not fork a
  second seeding mechanism.
- **If/when `F6` (Dynamic Client Registration) lands**, the three static client registrations become
  unnecessary (the suite registers clients itself and can run modules in parallel); the **tenant + test
  user** seeding is still required. Keep the client-seeding portion cleanly separable so it can be
  dropped once DCR is available.

## Work breakdown

1. Define the conformance seeding configuration shape (tenant id, clients with auth methods/grants/scopes,
   redirect URIs, test user credentials).
2. Implement an idempotent startup seeder (bootstrap-admin-style) that upserts the tenant, the three
   clients, and the test user, gated on the conformance profile.
3. Wire the seeder into the `T3` conformance launch profile; confirm it runs against the chosen database
   provider (in-memory vs. persistent).
4. Verify the seeded clients satisfy Appendix C (two `client_secret_basic` distinct clients + one
   `client_secret_post`, correct redirect URI, `S256`).
5. Keep the client-seeding portion isolated so `F6` can retire it without touching tenant/user seeding.

## Acceptance criteria

- A freshly launched conformance host exposes the conformance tenant, the three required clients, and the
  fixed test user with no manual API calls.
- Re-running startup is idempotent (no duplicates, no failures).
- The seeded test user authenticates through the `T2` login page; the seeded clients complete a Basic OP
  authorization-code exchange.

## Dependencies & links

- **Provides** the seeded identity consumed by `T2` and the clients used by `T1` (Phase C).
- **Runs on** the `T3` reachable host/profile.
- **Narrowed by** `F6` (DCR) — DCR removes the static client-seeding half but not tenant/user seeding.
- Registration details: [`conformance-testing.md`](../conformance-testing.md) Appendix C.
