# Reachable HTTPS host for the external harness

The OpenID host runs as a **real, externally reachable HTTPS process** whose discovery document's `issuer`
**byte-matches** the origin the conformance suite uses to reach it — so the suite's nginx/Selenium front end and its
direct token/userinfo/jwks/discovery calls all target one consistent origin. Tracking issue:
[#126](https://github.com/NCodeGroup/Identity/issues/126). Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md) (prerequisites #1 and #5, and the "Windows developer notes").

## The `Conformance` launch profile

The [`Conformance` launch profile](../../hosts/NCode.Identity.OpenId.Host/Properties/launchSettings.json) is the single
"run the OP for the external suite" entry point:

- **Binds all interfaces on the stable port.** `applicationUrl = https://0.0.0.0:5001;http://0.0.0.0:5000`, so the
  suite's Docker containers reach it via `host.docker.internal:5001` (a process bound only to `localhost` is **not**
  reachable through `host.docker.internal`, which maps to the host's network interface, not loopback). The templates in
  [`conformance-testing.md`](../conformance-testing.md) Appendix D assume `:5001`.
- **Pins the issuer to the reached origin.** `OpenId__TenantResolution__StaticSingle__Issuer =
https://host.docker.internal:5001` makes the discovery `issuer` byte-match the origin in the Appendix D
  `server.discoveryUrl`, deterministically and independent of request host / proxy headers. The issuer is a property
  of the tenant ([ADR-1129](../decisions/1129-the-tenant-issuer-is-the-single-authoritative-origin.md)): every served
  tenant has one and nothing derives it from the request
  ([ADR-1165](../decisions/1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)), so the pin
  is required. The issuer is stored on the static tenant when it is first provisioned, so a database that holds the
  tenant keeps its stored issuer until it is reset ([`build/dev-db-reset.ps1`](../../build/dev-db-reset.ps1)). The pin
  lives in the launch profile (not `appsettings.Conformance.json`) because the shared `Conformance` environment overlay
  is also loaded by the Aspire-orchestrated `ConformanceAspire` dashboard instance on `:5003`, whose own launch profile
  pins `https://localhost:5003`. Both conformance profiles serve their own `conformance` tenant (the overlay's
  `StaticSingle:TenantId`), so the Aspire instance never shares the development host's `default` tenant — and its
  stored issuer — in the shared Postgres.
- **Reuses the conformance tenant/seed ([#127](https://github.com/NCodeGroup/Identity/issues/127)) and the interactive
  UI ([#125](https://github.com/NCodeGroup/Identity/issues/125)).** `ASPNETCORE_ENVIRONMENT = Conformance` loads
  `appsettings.Conformance.json` (the `ConformanceSeed` section → `AddConformanceSeed`), and
  `InteractiveAuthentication__BundleRootPath` serves the `apps/auth` login/consent bundle under the tenant's
  `/account/*`.

Run it with `dotnet run --project hosts/NCode.Identity.OpenId.Host --launch-profile Conformance` after
`./build/conformance/Get-ConformanceSeedCredentials.ps1 -SetEnvironment` in the same shell: the seeded clients' secrets
and the test user's password are generated per machine and passed in the environment, never committed
([#514](https://github.com/NCodeGroup/Identity/issues/514)). The full orchestration that also brings the suite up and
down is [`run-conformance.ps1`](../../build/conformance/run-conformance.ps1)
([`external-suite.md`](external-suite.md)).

## How the issuer anchors every URL

- **The integration-test host is in-process only.**
  [`HostApplicationFactory`](../../tests/NCode.Identity.OpenId.Integration.Tests/Infrastructure/HostApplicationFactory.cs)
  boots the host through `WebApplicationFactory`/`TestServer` — not a socket the suite's containers can reach — which is
  why the external suite runs against the `Conformance` launch profile.
- **The issuer is always the tenant's own.** In
  [`DefaultOpenIdTenantFactory`](../../src/NCode.Identity.OpenId.Core/Tenants/DefaultOpenIdTenantFactory.cs),
  `GetTenantIssuerAsync` returns the tenant's stored `Issuer` and fails closed when it has none, and
  `GetTenantBaseAddressAsync` parses the base address from that issuer, so the request's `Host` never reaches a
  published URL.
- **Discovery is anchored to the tenant.** Discovery endpoints are emitted via `GetTenantUriByName` in
  [`DefaultDiscoverMetadataHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Discovery/Handlers/DefaultDiscoverMetadataHandler.cs),
  which takes the authority from the tenant's base address, so pinning the issuer also pins every endpoint URL.

## Reachability and TLS

- Add the suite hostname and the issuer hostname to the hosts file (`127.0.0.1 localhost.emobix.co.uk`,
  `127.0.0.1 host.docker.internal`), per the parent doc's Windows notes — host-side callers (the harness readiness
  probe, the pre-flight) need the latter, and a fresh Docker Desktop install may not write it; port-proxy only if a
  profile needs `443`.
- Local runs use the ASP.NET Core dev cert; the suite relaxes verification via `--fintechlabs.devmode=true` /
  `DISABLE_SSL_VERIFY`. No public CA chain is needed locally.

## Acceptance criteria

- The reachable origin is produced by a single documented profile (`Conformance`), not ad-hoc flags.
- The discovery `issuer` equals the origin the suite uses (`server.discoveryUrl` origin), byte for byte, through the
  static tenant's issuer.
- The suite (in Docker) fetches `/.well-known/openid-configuration` and `/oauth2/jwks` from the host over HTTPS.

## Dependencies & links

- **Serves** end-to-end verification for [#125](https://github.com/NCodeGroup/Identity/issues/125) and the live plans
  in [#124](https://github.com/NCodeGroup/Identity/issues/124).
- **Pairs with** [#127](https://github.com/NCodeGroup/Identity/issues/127) (the reachable host serves the seeded
  conformance tenant/clients) and [#128](https://github.com/NCodeGroup/Identity/issues/128) (discovery is correct on
  that origin).
- Config/issuer specifics: [`conformance-testing.md`](../conformance-testing.md) Appendix D and "Windows developer
  notes".
