# T3 — Reachable HTTPS host for the external harness

Status: **implemented** (the `Conformance` launch profile). Detailed plan for backlog item `T3`. Parent
design: [`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #1, #5, and the "Windows
developer notes"). Catalog entry: [`BACKLOG-COMPLETED.md`](../../BACKLOG-COMPLETED.md) group `T`.

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
- **Pins the issuer to the reached origin.** `OpenId__Server__Settings__tenant_issuer =
https://host.docker.internal:5001` makes the discovery `issuer` byte-match the origin in the Appendix D
  `server.discoveryUrl`, deterministically and independent of request host / proxy headers. The pin lives
  in the launch profile (not `appsettings.Conformance.json`) because the shared `Conformance` environment
  overlay is also loaded by the Aspire-orchestrated `ConformanceAspire` dashboard instance on `:5003`,
  which must keep its own request-derived issuer.
- **Reuses the conformance tenant/seed (`T4`) and the interactive UI (`T2`).** `ASPNETCORE_ENVIRONMENT =
Conformance` loads `appsettings.Conformance.json` (the `ConformanceSeed` section → `AddConformanceSeed`),
  and `InteractiveAuthentication__BundleRootPath` serves the `apps/auth` login/consent bundle under
  `/account/*`.

Run it with `dotnet run --project src/NCode.Identity.OpenId.Host --launch-profile Conformance` (the
full orchestration script that also brings the suite up/down is `T1` Phase D, not T3).

## Current state (verified)

- **The integration-test host is in-process only.**
  [`HostApplicationFactory`](../../tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/HostApplicationFactory.cs)
  boots the host through `WebApplicationFactory`/`TestServer` — not a socket the suite's containers
  can reach. The `Conformance` launch profile is the reachable-host answer to that gap.
- **Issuer is request-derived, with a pin available.** In
  [`DefaultOpenIdTenantFactory`](../../src/NCode.Identity.OpenId.Core/Tenants/DefaultOpenIdTenantFactory.cs):
    - `GetTenantBaseAddressAsync` builds the base address from `httpRequest.Scheme` + `httpRequest.Host`.
    - `GetTenantIssuerAsync` returns the `tenant_issuer` setting (`OpenIdSettingKeys.TenantIssuer`) when
      set, otherwise the base address. So the issuer can be **pinned** via the `tenant_issuer` setting, or
      left to follow the request host.
- **Discovery is origin-relative.** Discovery endpoints are emitted via `LinkGenerator`/`EndpointDataSource`
  in [`DefaultDiscoverMetadataHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Discovery/Handlers/DefaultDiscoverMetadataHandler.cs),
  so endpoint URLs track the reached host; only `issuer` needs explicit alignment.

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

- **Pin the issuer (chosen, explicit):** `tenant_issuer` is set to the exact reached origin
  (`https://host.docker.internal:5001`) via the `Conformance` launch profile's
  `OpenId__Server__Settings__tenant_issuer` environment variable. `tenant_issuer` is a parent-owned
  (`Keep`) setting ([ADR-0038](../adr/0038-setting-merge-lattice-duals.md)), so the server-level value
  flows down to the single static tenant and cannot be overridden. Deterministic and independent of proxy
  headers.
- **Follow the request host (not chosen):** ensure the request `Host`/`Scheme` the OP sees equals the
  reached origin — which means configuring forwarded-headers handling if anything terminates TLS or
  rewrites the host in front of Kestrel. More moving parts; the pin is preferred.

### TLS trust

- Use the ASP.NET Core dev cert for local runs; the suite relaxes verification via
  `--fintechlabs.devmode=true` / `DISABLE_SSL_VERIFY`. No public CA chain is needed locally.

### Packaging

- The dedicated **`Conformance` launch profile** fixes the port, pins `tenant_issuer`, selects the
  conformance tenant/seeding (`T4`), and enables the interactive dev UI (`T2`). This profile is the single
  "run the OP for conformance" entry point; the full orchestration script (launch → compose up →
  run-test-plan → teardown) is part of `T1` Phase D, not T3.

## Work breakdown

1. ✅ Define the `Conformance` launch profile: stable HTTPS port, all-interfaces bind, `tenant_issuer`
   pin, conformance tenant selection, dev-UI + seeding toggles.
2. Verify the running OP is reachable from a container at `host.docker.internal:5001` and serves
   `/.well-known/openid-configuration` + `/oauth2/jwks` — a `T1` Phase C step (needs the suite stack up).
3. Assert `issuer` in the discovery document byte-matches the origin in the Appendix D
   `server.discoveryUrl` — guaranteed by the `tenant_issuer` pin above; re-confirm live in `T1` Phase C.
4. Confirm the dev cert path works with the suite's dev-mode SSL relaxation — a `T1` Phase C step.
5. Record the exact run recipe (above) in the orchestration notes consumed by `T1`.

## Acceptance criteria

- ✅ The reachable origin is produced by a single documented profile/recipe (`Conformance`), not ad-hoc
  flags.
- ✅ The discovery `issuer` equals the origin the suite uses (`server.discoveryUrl` origin) — pinned to an
  exact byte match via `tenant_issuer`.
- The suite (in Docker) can fetch `/.well-known/openid-configuration` and `/oauth2/jwks` from the
  the host over HTTPS — exercised end-to-end when the suite stack lands in `T1` Phase C.

## Dependencies & links

- **Blocks** end-to-end verification in `T2` and the first live plans in `T1` (Phase C).
- **Pairs with** `T4` (the reachable host must serve the seeded conformance tenant/clients) and `T5`
  (discovery must be correct on that origin).
- Config/issuer specifics: [`conformance-testing.md`](../conformance-testing.md) Appendix D and
  "Windows developer notes".
