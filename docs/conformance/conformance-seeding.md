# Deterministic conformance tenant, clients, and test user seeding

A host launched for conformance starts up with a **deterministic conformance tenant**, the **client registrations** the
static-client plans need, and a **fixed test user** — reproducibly, with no manual API calls — so the suite and the
login UI ([#125](https://github.com/NCodeGroup/identity/issues/125)) always see the same identities. Tracking issue:
[#127](https://github.com/NCodeGroup/identity/issues/127). Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #3 and Appendix C).

## The seeder

The config-driven startup seeder is `AddConformanceSeed(IConfiguration)` in `NCode.Identity.Server`
([`ConformanceSeedRegistration`](../../src/NCode.Identity.Server/ConformanceSeedRegistration.cs),
[`ConformanceSeedHandler`](../../src/NCode.Identity.Server/ConformanceEnvironment/ConformanceSeedHandler.cs),
[`ConformanceSeedOptions`](../../src/NCode.Identity.Server/ConformanceEnvironment/ConformanceSeedOptions.cs),
[`ConformanceClientSeed`](../../src/NCode.Identity.Server/ConformanceEnvironment/ConformanceClientSeed.cs)), modeled on
the bootstrap admin seeder ([`BootstrapAdminSeedHandler`](../../src/NCode.Identity.Server/BootstrapAdminSeedHandler.cs)):
idempotent, declarative, and conformance-only. It is wired into the host
([`Program.cs`](../../hosts/NCode.Identity.OpenId.Host/Program.cs)) and activated by the `Conformance` launch profile
([#126](https://github.com/NCodeGroup/identity/issues/126)) plus the
[`appsettings.Conformance.json`](../../hosts/NCode.Identity.OpenId.Host/appsettings.Conformance.json) overlay, whose
`ConformanceSeed` section declares the `conformance` tenant, the three Basic-OP clients, the FAPI 1.0 Advanced clients
and the `conformance-user`.

Client secrets and the test user's password are not in the overlay: they are generated once per machine, outside the
repository, and passed to the host as `ConformanceSeed__` environment variables by the LocalDev AppHost and by
[`run-conformance.ps1`](../../build/conformance/run-conformance.ps1), through
[`Get-ConformanceSeedCredentials.ps1`](../../build/conformance/Get-ConformanceSeedCredentials.ps1)
([#514](https://github.com/NCodeGroup/identity/issues/514)). The host stores each seeded secret as a PBKDF2 verifier,
from which the secret cannot be read back, unless its client registers `client_secret_jwt`, which keys its assertion
from the secret itself, so the host stores that secret reversibly
([ADR-1391](../decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).

On every startup the seeder adds each declared client and the test user that the conformance tenant does not hold yet,
so a second run neither duplicates nor fails. Before storing a client it validates the client's settings against the
registered setting descriptors and the client coherence rules, and imports its key set under the policy a dynamically
registered `jwks` follows; the test user's password must meet the tenant's password policy. A refused value stops the
seeding with an error naming the client or the test user, rather than storing an identity that fails every request
([ADR-1140](../decisions/1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)).

## Basic OP registrations

Per [`conformance-testing.md`](../conformance-testing.md) Appendix C, a Basic OP run with static clients uses **three**
client registrations, all registering the suite callback as a redirect URI:

- **`client` (primary):** `token_endpoint_auth_method = client_secret_basic`; grants `authorization_code` +
  `refresh_token`; scopes `openid profile email` (available to every client through the system OpenID Connect
  resource server, so the seed carries no scopes).
- **`client2` (second confidential):** a **distinct** `client_id`, same shape — it lets the suite prove auth codes are
  bound to the issuing client.
- **`client-secret-post`:** `token_endpoint_auth_method = client_secret_post` (a separate record: a client
  authenticates only with the one method it registers).
- **Redirect URI (all):** `https://<SUITE_BASE>/test/a/<ALIAS>/callback`
  (local: `https://localhost.emobix.co.uk:8443/test/a/ncode-basic/callback`).
- **PKCE:** `S256`. Every authorization code is bound to PKCE by default
  ([ADR-1144](../decisions/1144-authorization-codes-are-bound-to-an-s256-pkce-challenge-unless-an-attested-client-uses-the-nonce.md)); because the suite sends PKCE in
  only one module, the conformance host relaxes `require_pkce` and attests its clients with `pkce_nonce_exemption`, so a
  confidential client's request that carries a `nonce` may omit the challenge. Each seeded client registers one
  `TokenEndpointAuthMethod` as its `token_endpoint_auth_method`, which makes it confidential and is the only method it
  authenticates with ([ADR-1145](../decisions/1145-a-client-authenticates-only-with-its-registered-method.md)).

The committed Basic OP plan runs under Dynamic Client Registration
([ADR-1063](../decisions/1063-dynamic-client-registration.md)), so the suite registers its own clients; the **tenant
and test user** seeding is what that plan needs, and the static client seeds stay separable from it.

The **deterministic test user** (`conformance-user`, with a per-machine password and the full standard
`profile`/`address`/`phone` claim set) lives in the conformance tenant and is consumed by the login page.

## Acceptance criteria

- A freshly launched conformance host exposes the conformance tenant, the declared clients, and the fixed test user
  with no manual API calls.
- Re-running startup is idempotent (no duplicates, no failures).
- The seeded test user authenticates through the login page; the seeded clients complete a Basic OP
  authorization-code exchange.

## Dependencies & links

- **Provides** the seeded identity consumed by [#125](https://github.com/NCodeGroup/identity/issues/125) and the clients
  used by [#124](https://github.com/NCodeGroup/identity/issues/124).
- **Runs on** the [#126](https://github.com/NCodeGroup/identity/issues/126) reachable host/profile.
- Registration details: [`conformance-testing.md`](../conformance-testing.md) Appendix C.
