# T3 — Reachable HTTPS Playground host for the external harness

Status: **planned, not yet implemented.** Detailed plan for backlog item `T3`. Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #1, #5, and the "Windows
developer notes"). Catalog entry: [`BACKLOG.md`](../../BACKLOG.md) group `T`.

## Goal

Run the Playground OP as a **real, externally reachable HTTPS process** whose discovery document's
`issuer` **byte-matches** the origin the conformance suite uses to reach it — so the suite's nginx/Selenium
front end and its direct token/userinfo/jwks/discovery calls all target one consistent origin.

## Current state (verified)

- **The integration-test host is in-process only.**
  [`PlaygroundApplicationFactory`](../../tests/NCode.Identity.OpenId.IntegrationTests/Infrastructure/PlaygroundApplicationFactory.cs)
  boots the Playground through `WebApplicationFactory`/`TestServer` — not a socket the suite's containers
  can reach.
- **A launchable process already exists.**
  [`launchSettings.json`](../../src/NCode.Identity.OpenId.Playground/Properties/launchSettings.json) has a
  `Project` profile with `applicationUrl = https://localhost:5001;http://localhost:5000` and a `Docker`
  profile. `Program.cs` calls `app.UseHttpsRedirection()`.
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

Expose the Playground on a single origin the suite containers can reach — on Windows/Docker that is
`https://host.docker.internal:<port>` — and make `issuer` equal that exact origin.

### Reachability

- Launch a real process (`dotnet run` with the `Project` profile, or the `Docker` profile), not
  `TestServer`.
- From the suite's Docker network, the host is reachable at `host.docker.internal`. Keep the bound port
  stable (the templates in [`conformance-testing.md`](../conformance-testing.md) Appendix D assume
  `:5001`).
- Add the suite hostname to the hosts file where required (`127.0.0.1 localhost.emobix.co.uk`), per the
  parent doc's Windows notes; port-proxy only if a profile needs `443`.

### Issuer byte-match (pick one, document it)

- **Pin the issuer (recommended, explicit):** set `tenant_issuer` to the exact reached origin (e.g.
  `https://host.docker.internal:5001`) via configuration for the conformance run. Deterministic and
  independent of proxy headers.
- **Follow the request host:** ensure the request `Host`/`Scheme` the OP sees equals the reached origin —
  which means configuring forwarded-headers handling if anything terminates TLS or rewrites the host in
  front of Kestrel. More moving parts; prefer the pin.

### TLS trust

- Use the ASP.NET Core dev cert for local runs; the suite relaxes verification via
  `--fintechlabs.devmode=true` / `DISABLE_SSL_VERIFY`. No public CA chain is needed locally.

### Packaging

- Add a dedicated **`Conformance` launch profile** (or a documented env overlay) that fixes the port,
  pins `tenant_issuer`, selects the conformance tenant/seeding (`T4`), and enables the interactive dev UI
  (`T2`). This profile is the single "run the OP for conformance" entry point; the full orchestration
  script (launch → compose up → run-test-plan → teardown) is part of `T1` Phase D, not T3.

## Work breakdown

1. Define the `Conformance` launch profile / env overlay: stable HTTPS port, `tenant_issuer` pin,
   conformance tenant selection, dev-UI + seeding toggles.
2. Verify the running OP is reachable from a container at `host.docker.internal:<port>` and serves
   `/.well-known/openid-configuration` + `/oauth2/jwks`.
3. Assert `issuer` in the discovery document byte-matches the origin in the Appendix D
   `server.discoveryUrl`.
4. Confirm the dev cert path works with the suite's dev-mode SSL relaxation.
5. Record the exact run recipe in the config templates / orchestration notes consumed by `T1`.

## Acceptance criteria

- The suite (in Docker) can fetch `/.well-known/openid-configuration` and `/oauth2/jwks` from the
  Playground over HTTPS.
- The discovery `issuer` equals the origin the suite uses (`server.discoveryUrl` origin) — exact byte
  match.
- The reachable origin is produced by a single documented profile/recipe, not ad-hoc flags.

## Dependencies & links

- **Blocks** end-to-end verification in `T2` and the first live plans in `T1` (Phase C).
- **Pairs with** `T4` (the reachable host must serve the seeded conformance tenant/clients) and `T5`
  (discovery must be correct on that origin).
- Config/issuer specifics: [`conformance-testing.md`](../conformance-testing.md) Appendix D and
  "Windows developer notes".
