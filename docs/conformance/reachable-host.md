# Reachable HTTPS host for the external harness

Status: **implemented** (the `Conformance` launch profile). Detailed plan for issue [#126](https://github.com/NCodeGroup/Identity/issues/126). Parent
design: [`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #1, #5, and the "Windows
developer notes"). Catalog entry: [#126](https://github.com/NCodeGroup/Identity/issues/126).

## Goal

Run the OpenID host as a **real, externally reachable HTTPS process** whose discovery document's
`issuer` **byte-matches** the origin the conformance suite uses to reach it — so the suite's nginx/Selenium
front end and its direct token/userinfo/jwks/discovery calls all target one consistent origin.

## What was implemented

The [`Conformance` launch profile](../../src/NCode.Identity.OpenId.Host/Properties/launchSettings.json)
is the single "run the OP for the external suite" entry point:

- **Binds all interfaces on the stable port.** `applicationUrl = https://0.0.0.0:5001;http://0.0.0.0:5000`,
  so the suite's Docker containers reach it via `host.docker.internal:5001` (a process bound only to
  `localhost` is **not** reachable through `host.docker.internal`, which maps to the host's network
  interface, not loopback).
- **Pins the issuer to the reached origin.** `OpenId__TenantResolution__StaticSingle__Issuer =
https://host.docker.internal:5001` makes the discovery `issuer` byte-match the origin in the Appendix D
  `server.discoveryUrl`, deterministically and independent of request host / proxy headers. Every served tenant has an
  issuer and nothing derives one from the request
  ([ADR-0165](../adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)), so the pin is
  required, not optional. The issuer is stored on the static tenant when it is first provisioned, so a database that
  already holds the tenant keeps its stored issuer until it is reset
  ([`build/dev-db-reset.ps1`](../../build/dev-db-reset.ps1)). The pin lives in the launch profile (not
  `appsettings.Conformance.json`) because the shared `Conformance` environment overlay is also loaded by the
  Aspire-orchestrated `ConformanceAspire` dashboard instance on `:5003`, whose own launch profile pins
  `https://localhost:5003`. Both conformance profiles serve their own `conformance` tenant (the overlay's
  `StaticSingle:TenantId`), so the Aspire instance never shares the development host's `default` tenant — and its stored
  issuer — in the shared Postgres.
- **Reuses the conformance tenant/seed ([#127](https://github.com/NCodeGroup/Identity/issues/127)) and the interactive UI ([#125](https://github.com/NCodeGroup/Identity/issues/125)).** `ASPNETCORE_ENVIRONMENT =
Conformance` loads `appsettings.Conformance.json` (the `ConformanceSeed` section → `AddConformanceSeed`),
  and `InteractiveAuthentication__BundleRootPath` serves the `apps/auth` login/consent bundle under the
  tenant's `/account/*`.

Run it with `dotnet run --project src/NCode.Identity.OpenId.Host --launch-profile Conformance` (the
full orchestration script that also brings the suite up/down is [#124](https://github.com/NCodeGroup/Identity/issues/124) Phase D, not [#126](https://github.com/NCodeGroup/Identity/issues/126)).

## Current state (verified)

- **The integration-test host is in-process only.**
  [`HostApplicationFactory`](../../tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/HostApplicationFactory.cs)
  boots the host through `WebApplicationFactory`/`TestServer` — not a socket the suite's containers
  can reach. The `Conformance` launch profile is the reachable-host answer to that gap.
- **Issuer is always the tenant's own.** In
  [`DefaultOpenIdTenantFactory`](../../src/NCode.Identity.OpenId.Core/Tenants/DefaultOpenIdTenantFactory.cs):
    - `GetTenantIssuerAsync` returns the tenant's stored `Issuer` and fails closed when it has none.
    - `GetTenantBaseAddressAsync` parses the base address from that issuer, so the request's `Host` never reaches a
      published URL. The issuer is set through `OpenId:TenantResolution:StaticSingle:Issuer`.
- **Discovery is anchored to the tenant.** Discovery endpoints are emitted via `GetTenantUriByName`
  in [`DefaultDiscoverMetadataHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Discovery/Handlers/DefaultDiscoverMetadataHandler.cs),
  which takes the authority from the tenant's base address, so pinning the issuer also pins every endpoint URL.

## Approach

Expose the host on a single origin the suite containers can reach — on Windows/Docker that is
`https://host.docker.internal:<port>` — and make `issuer` equal that exact origin.

### Reachability

- Launch a real process (`dotnet run` with the `Project` profile, or the `Docker` profile), not
  `TestServer`.
- From the suite's Docker network, the host is reachable at `host.docker.internal`. Keep the bound port
  stable (the templates in [`conformance-testing.md`](../conformance-testing.md) Appendix D assume
  `:5001`).
- Add the suite hostname and the issuer hostname to the hosts file (`127.0.0.1 localhost.emobix.co.uk`,
  `127.0.0.1 host.docker.internal`), per the parent doc's Windows notes — host-side callers (the harness
  readiness probe, the pre-flight) need the latter, and a fresh Docker Desktop install may not write it;
  port-proxy only if a profile needs `443`.

### Issuer byte-match (chosen: pin)

- **Pin the issuer (chosen, explicit):** the static tenant's issuer is set to the exact reached origin
  (`https://host.docker.internal:5001`) via the `Conformance` launch profile's
  `OpenId__TenantResolution__StaticSingle__Issuer` environment variable. The issuer is a property of the tenant
  ([ADR-0129](../adr/0129-the-tenant-issuer-is-the-single-authoritative-origin.md)), and every published URL is
  anchored to it. Deterministic and independent of proxy headers.
- **Follow the request host (no longer possible):** the request-derived issuer was removed
  ([ADR-0165](../adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)); a tenant is
  published only under its own issuer.

### TLS trust

- Use the ASP.NET Core dev cert for local runs; the suite relaxes verification via
  `--fintechlabs.devmode=true` / `DISABLE_SSL_VERIFY`. No public CA chain is needed locally.

### Packaging

- The dedicated **`Conformance` launch profile** fixes the port, pins the static tenant's issuer, selects the
  conformance tenant/seeding ([#127](https://github.com/NCodeGroup/Identity/issues/127)), and enables the interactive dev UI ([#125](https://github.com/NCodeGroup/Identity/issues/125)). This profile is the single
  "run the OP for conformance" entry point; the full orchestration script (launch → compose up →
  run-test-plan → teardown) is part of [#124](https://github.com/NCodeGroup/Identity/issues/124) Phase D, not [#126](https://github.com/NCodeGroup/Identity/issues/126).

## Work breakdown

1. ✅ Define the `Conformance` launch profile: stable HTTPS port, all-interfaces bind, issuer
   pin, conformance tenant selection, dev-UI + seeding toggles.
2. Verify the running OP is reachable from a container at `host.docker.internal:5001` and serves
   `/.well-known/openid-configuration` + `/oauth2/jwks` — a [#124](https://github.com/NCodeGroup/Identity/issues/124) Phase C step (needs the suite stack up).
3. Assert `issuer` in the discovery document byte-matches the origin in the Appendix D
   `server.discoveryUrl` — guaranteed by the issuer pin above; re-confirm live in [#124](https://github.com/NCodeGroup/Identity/issues/124) Phase C.
4. Confirm the dev cert path works with the suite's dev-mode SSL relaxation — a [#124](https://github.com/NCodeGroup/Identity/issues/124) Phase C step.
5. Record the exact run recipe (above) in the orchestration notes consumed by [#124](https://github.com/NCodeGroup/Identity/issues/124).

## Acceptance criteria

- ✅ The reachable origin is produced by a single documented profile/recipe (`Conformance`), not ad-hoc
  flags.
- ✅ The discovery `issuer` equals the origin the suite uses (`server.discoveryUrl` origin) — pinned to an
  exact byte match via the static tenant's issuer.
- The suite (in Docker) can fetch `/.well-known/openid-configuration` and `/oauth2/jwks` from the
  the host over HTTPS — exercised end-to-end when the suite stack lands in [#124](https://github.com/NCodeGroup/Identity/issues/124) Phase C.

## Dependencies & links

- **Blocks** end-to-end verification in [#125](https://github.com/NCodeGroup/Identity/issues/125) and the first live plans in [#124](https://github.com/NCodeGroup/Identity/issues/124) (Phase C).
- **Pairs with** [#127](https://github.com/NCodeGroup/Identity/issues/127) (the reachable host must serve the seeded conformance tenant/clients) and [#128](https://github.com/NCodeGroup/Identity/issues/128)
  (discovery must be correct on that origin).
- Config/issuer specifics: [`conformance-testing.md`](../conformance-testing.md) Appendix D and
  "Windows developer notes".
