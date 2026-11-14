# 1437. The management console is a confidential client whose own BFF host serves only the console

- **Status:** Accepted
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The management console is the family's most privileged surface: it creates tenants, clients, keys and role assignments.
A browser public client holds its tokens where script that runs in the page can read them
([RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017) §5), so an injected script on the console's origin reads
the tokens of the most privileged user the platform has.

The BFF library serves several frontends from one host
([ADR-1442](1442-a-bff-serves-the-frontends-its-configuration-lists-each-selected-per-request-by-an-open-rule.md)), so the console can be a
BFF frontend: a confidential client whose tokens never reach the browser. The design leaves open where its BFF runs
([`docs/backend-for-frontend.md`](../backend-for-frontend.md) §11 question 6): in the OP host, under a path of the OP's
origin, or in a host of its own.

The forces:

- Every frontend of the family is served from an origin of its own
  ([ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md)), and a
  path prefix is no security boundary
  ([ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)).
- The management API authenticates access tokens of the control-plane tenant
  ([ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md));
  a console that reaches it keeps that one authentication model.
- A BFF is a client of the OP. The OP's own service collection carries `AddIdentityServer`, and a BFF host's carries
  `AddIdentityBff`; the two are never composed together.
- The console calls the management API with a DPoP-bound access token. The BFF signs the proof's `htu` over the URL it
  sends the request to, which is `ManagementConsole:ManagementApiUrl` plus the path, and the OP compares a proof's `htu`
  only with its tenant's published URLs
  ([ADR-1365](1365-no-url-the-server-publishes-or-compares-takes-its-origin-from-the-requests-host.md)).
  `TenantRequestUri` builds the URL it compares from the control-plane tenant's issuer scheme and host plus the
  request's path, and the management API's route prefix is `/api` at the server's root
  (`ManagementApiDefaults.RoutePrefix`). So the only URL the OP accepts from the console is
  `<origin of the authority>/api/...`.
- The console's `__Host-Http-bff.mgmt` cookie has path `/`, so the browser sends it with every request to the console's
  host. A path of that host routed to the OP hands the console's session handle to another process and shares the
  console's origin with an OP surface.

## Decision

**The management console's BFF runs in its own process and origin, `NCode.Identity.ManagementConsole.Host`, as the
confidential client `mgmt-console` of the control-plane tenant. The host serves only the console: the console reaches the
management API at `/api` on the origin of its authority.**

1. **A separate host, for these reasons.**
   - The console's script never shares an origin with the OP's login, consent and account pages.
   - The management API keeps one authentication model: access tokens of the control-plane tenant,
     which the BFF obtains and attaches on the browser's behalf.
   - Neither process holds the other's keys: the OP holds no key of the console, and the console holds no key, database
     or signing credential of the OP.
   - The console is a client of the OP exactly as a third party's BFF is (design goal G3).
   - `AddIdentityServer` and `AddIdentityBff` never share a service collection.
2. **Composition.** The packable `NCode.Identity.ManagementConsole` holds the console's server and is a composition root
   like `NCode.Identity.Bff.Server`, which is why it references the BFF implementation.
   - `AddManagementConsole(configuration)` composes `AddIdentityBff`, binds the `ManagementConsole` section strictly
     (`ManagementApiUrl`, `BundleRootPath`, `Features` and `Branding`) and returns `IManagementConsoleBuilder`, which
     exposes the `IdentityBffBuilder`.
   - `MapManagementConsole()` maps the console's endpoints.
   - The host's `Program.cs` holds composition only.
3. **Topology.** The console is served at the root of its origin as the fallback BFF frontend `mgmt`
   (`ManagementConsoleDefaults.FrontendName`), so no selection rule exists, and its cookie is `__Host-Http-bff.mgmt`.
   `ManagementConsoleFrontendValidator` refuses at start a host without that frontend. The management API is forwarded
   at `/management/api/*` (`ManagementConsoleDefaults.ApiPath`) to `ManagementConsole:ManagementApiUrl` with the `user`
   token requirement and the resource `urn:ncode:management`, so the console's API base is `/management`.
4. **The API URL is the authority's origin plus `/api`.** `ManagementConsole:ManagementApiUrl` is the origin of the
   console's authority followed by `/api`, because that is the only URL the OP accepts from a DPoP-bound call. The host
   chart derives it and has no override value ([ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)).
   The console derives nothing itself: it reads the setting as configuration, and `ManagementConsoleOptionsValidator`
   refuses at start a value that is not an absolute `https` URL without user info, query or fragment (`http` is
   accepted only when `LocalDevelopment:Enabled` is set).
5. **The console's host routes every path to the console.** Its cookie never reaches another process. A machine caller
   uses `/api` on the origin of the control-plane tenant's issuer, which the server's Ingress serves. On the hosted
   platform ([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md))
   the management API is at `auth.<apex>/api`, and `manage.<apex>` serves only the console.
6. **Client registration.** `mgmt-console` is a confidential client of the control-plane tenant:
   - authentication method `private_key_jwt`, `require_pushed_authorization_requests`,
     `require_dpop_bound_access_tokens`, grant types `authorization_code` and `refresh_token`;
   - redirect URI `<origin>/bff/callback`, post-logout redirect URI `<origin>/`, back-channel logout URI
     `<origin>/bff/backchannel-logout` with `backchannel_logout_session_required`;
   - a deployment registers `jwks_uri` = `<origin>/bff/jwks`
     ([ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md)); the developer
     seed registers the public key by value, because the OP's outbound guard never reaches a loopback address
     ([ADR-1370](1370-a-host-runs-the-deployment-posture-unless-configured-for-local-development-and-always-names-its-root-key-provider.md)
     holds the seeder).
7. **The `mgmt` frontend.**
   - Key rings `mgmt-client` and `mgmt-dpop`, with `DPoP:Mode` `required` and `Jwks:Enabled`.
   - Scopes `profile email offline_access read:clients` (the BFF adds `openid`) and no `resource`, so the login access
     token carries the UserInfo audience
     ([ADR-1026](1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md)).
   - `UserInfo:Load`, and `UserClaims` `preferred_username` and `email` for `/bff/user`.
   - `ReturnUrls` `/`.
8. **Host filtering.** `ManagementConsoleHostFilteringValidator` refuses an empty or wildcard `AllowedHosts` unless
   `LocalDevelopment:Enabled` is set, so a deployment lists its own host names.
9. **Local development.** The AppHost ([ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md))
   runs the console host on `https://localhost:5005` and `http://localhost:5004`. It generates per-machine P-256 keys
   `mgmt-client.pem` and `mgmt-dpop.pem` beside the other development secrets, passes their paths to the console as
   `Bff__KeyRings__…` and gives the OP the client JWKS as `DeveloperInteractiveSeed__BffClients__0__Jwks`. `apps/mgmt` is
   a build-watched bundle (`vite build --watch`) that the console host serves.

## Options considered

- **The BFF in the OP host, with the console under `/mgmt/`.** Rejected: the console's script shares an origin with the
  OP's login, consent and account pages, and a path prefix is no boundary between them. The OP carries a second
  composition, `AddIdentityBff`, and the keys of a client.
- **The BFF in the OP process, on a host name of its own.** Rejected: it isolates the origin and not the process, and
  `AddIdentityServer` and `AddIdentityBff` share one service collection.
- **`/api` on the console's host, routed to the OP.** Rejected: every DPoP-bound call fails there, because the console
  signs `htu` over the console host, which the tenant does not publish. A route only unbound bearer tokens can use
  invites the weaker token, and the console's session cookie reaches another process.
- **A second published origin in the OP.** Rejected: [ADR-1365](1365-no-url-the-server-publishes-or-compares-takes-its-origin-from-the-requests-host.md)
  compares `htu` only with the tenant's own published URLs.
- **A `managementConsole.managementApiUrl` chart value.** Rejected: the only URL the OP accepts is derived, so an
  override can only make every DPoP-bound call fail.
- **A separate host that serves only the console.** Chosen: origin and process isolation, one authentication model for
  the management API, and a console that stands where any third party's BFF stands.

## Consequences

- One more deployable exists; its container image and chart component are
  [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)'s.
- The console reaches the OP over HTTP like any client, so a deployment registers `mgmt-console` and its `jwks_uri`
  with the OP. The console's pods reach the server at its public origin, as they do for discovery, pushed authorization
  requests and the token endpoint.
- A default install's console calls succeed.
- Local development runs three processes for the console: the OP (`api`), the console host (`console`) and the `mgmt`
  bundle's build watcher.
- The OP cannot deliver a back-channel logout to the console host on a developer machine, because its outbound guard
  never reaches a loopback address; the session ends at logout in the browser, at its timeouts, or at a failed refresh.
- `allowedHosts` lists only the server's own hosts.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.15 and §11
- [RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017)
- [ADR-1442](1442-a-bff-serves-the-frontends-its-configuration-lists-each-selected-per-request-by-an-open-rule.md)
- [ADR-1365](1365-no-url-the-server-publishes-or-compares-takes-its-origin-from-the-requests-host.md)
- [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)
- `src/NCode.Identity.ManagementConsole/` and `hosts/NCode.Identity.ManagementConsole.Host/`
- `charts/ncode-identity-host`, `.github/workflows/host-chart.yml`
- Issues [#158](https://github.com/NCodeGroup/identity/issues/158) and [#720](https://github.com/NCodeGroup/identity/issues/720)
