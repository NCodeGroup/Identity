# 1437. The management console is a confidential client served by a BFF host of its own

- **Status:** Accepted (amends
  [ADR-1064](1064-frontend-presentation-platform.md),
  [ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md),
  [ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md),
  [ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md),
  [ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md); amended by
  [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md): the
  console's image and the host chart's management console component; amended by
  [ADR-1488](1488-the-management-console-reaches-the-management-api-at-its-authoritys-origin-and-its-host-serves-only-the-console.md):
  the console's host serves only the console, and the console reaches the management API at its authority's origin)
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

The management console is the family's most privileged surface: it creates tenants, clients, keys and role assignments.
A browser public client holds its tokens where script that runs in the page can read them
([RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017) §5), so an injected script on the console's origin reads
the tokens of the most privileged user the platform has.

The BFF library serves several frontends from one host
([ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md)), so the console can be a
BFF frontend: a confidential client whose tokens never reach the browser. The design leaves open where its BFF runs
([`docs/backend-for-frontend.md`](../backend-for-frontend.md) §11 question 6): in the OP host, under a path of the OP's
origin, or in a host of its own.

The forces:

- Every frontend of the family is served from an origin of its own
  ([ADR-1064](1064-frontend-presentation-platform.md)), and a path prefix is no security boundary
  ([ADR-1193](1193-the-interactive-surface-is-served-under-the-tenant-route.md)).
- The management API authenticates sender-constrained bearer tokens of the control-plane tenant
  ([ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)); a console that reaches it must
  keep that one authentication model.
- A BFF is a client of the OP. The OP's own service collection carries `AddIdentityServer`, and a BFF host's carries
  `AddIdentityBff`; the two are never composed together.

## Decision

**The management console's BFF runs in its own process and origin, `NCode.Identity.ManagementConsole.Host`, as the
confidential client `mgmt-console` of the control-plane tenant. The console bundle, the BFF's `/bff/*` endpoints and the
forwarded management API share that host's origin.**

1. **A separate host, for these reasons.**
   - The console's script never shares an origin with the OP's login, consent and account pages.
   - The management API keeps one authentication model: sender-constrained bearer tokens of the control-plane tenant,
     which the BFF obtains and attaches on the browser's behalf.
   - The OP holds no key of the console, and the console holds no key, database or signing credential of the OP.
   - The console is a client of the OP exactly as a third party's BFF is (design goal G3).
   - `AddIdentityServer` and `AddIdentityBff` never share a service collection.
2. **Composition.** The packable `NCode.Identity.ManagementConsole` holds the console's server:
   `AddManagementConsole(configuration)` composes `AddIdentityBff` and binds the `ManagementConsole` section strictly,
   and returns `IManagementConsoleBuilder`, which exposes the `IdentityBffBuilder`; `MapManagementConsole()` maps the
   console's endpoints. The host's `Program.cs` holds composition only. `NCode.Identity.ManagementConsole` is a
   composition root like `NCode.Identity.Bff.Server`, which is why it references the BFF implementation.
3. **Topology.** The console is served at the root of its origin. Its BFF frontend `mgmt` is the fallback frontend, so no
   selection rule exists, and its cookie is `__Host-Http-bff.mgmt`. The management API is forwarded at
   `/management/api/*` to `ManagementConsole:ManagementApiUrl` (`https://localhost:5001/api` in Development) with the
   `user` token requirement and the resource `urn:ncode:management`, so the console's API base is `/management`.
   - `/api` stays free on the console's origin. The hosted platform's `manage.<apex>` routes `/api` to the OP for
     machine callers and every other path to the console host.
4. **Client registration.** `mgmt-console` is a confidential client of the control-plane tenant:
   - authentication method `private_key_jwt`, `require_pushed_authorization_requests`,
     `require_dpop_bound_access_tokens`, grant types `authorization_code` and `refresh_token`;
   - redirect URI `<origin>/bff/callback`, post-logout redirect URI `<origin>/`, back-channel logout URI
     `<origin>/bff/backchannel-logout` with `backchannel_logout_session_required`;
   - scopes `profile email offline_access read:clients` (the BFF adds `openid`) and no `resource`, so the login access
     token carries the OpenID audience that UserInfo requires
     ([ADR-1027](1027-access-token-audience-from-resource-servers.md)); the BFF loads UserInfo and allow-lists
     `preferred_username` and `email` for `/bff/user`;
   - a deployment registers `jwks_uri` = `<origin>/bff/jwks`
     ([ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md)); the developer
     seed registers the public key by value, because the OP's outbound guard never reaches a loopback address
     ([ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md) declares the clients
     the seeder registers).
   - The developer seed owns the BFF clients its configuration declares: it reconciles an existing client's key set
     and URIs with its configuration, replacing a client that differs (part of the amendment of ADR-1370).
   - The console host refuses a missing or wildcard `AllowedHosts` in the Production environment, so a deployment lists
     its own host names.
5. **Local development.** The AppHost ([ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md)) runs the console host on
   `https://localhost:5005` and `http://localhost:5004`. It generates per-machine P-256 keys `mgmt-client.pem` and
   `mgmt-dpop.pem` beside the other development secrets, hands their paths to the console as `Bff__KeyRings__…` and the
   client key's public JWKS to the OP as `DeveloperInteractiveSeed__BffClients__0__Jwks`. `apps/mgmt` is a build-watched
   bundle (`vite build --watch`) that the console host serves.

## Options considered

- **The BFF in the OP host, with the console under `/mgmt/`.** Rejected: the console's script shares an origin with the
  OP's login, consent and account pages, and a path prefix is no boundary between them. The OP carries a second
  composition, `AddIdentityBff`, and the keys of a client.
- **The BFF in the OP process, on a host name of its own.** Rejected: it isolates the origin and not the process, and
  `AddIdentityServer` and `AddIdentityBff` share one service collection.
- **A separate host.** Chosen: origin and process isolation, one authentication model for the management API, and a
  console that stands where any third party's BFF stands.

## Consequences

- One more deployable exists. Its container image and Helm chart are follow-up work.
- The console reaches the OP over HTTP like any client, so a deployment registers `mgmt-console` and its `jwks_uri`
  with the OP.
- Local development runs three processes for the console: the OP (`api`), the console host (`console`) and the `mgmt`
  bundle's build watcher.
- The OP cannot deliver a back-channel logout to the console host on a developer machine, because its outbound guard
  never reaches a loopback address; the session ends at logout in the browser, at its timeouts, or at a failed refresh.
- The hosted platform's `manage.<apex>` routes by path: `/api` for machine callers reaches the OP, and every other path
  reaches the console host.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.15 and §11
- [RFC 10017](https://datatracker.ietf.org/doc/html/rfc10017)
- [ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md)
- `src/NCode.Identity.ManagementConsole/` and `hosts/NCode.Identity.ManagementConsole.Host/`
- Issue [#158](https://github.com/NCodeGroup/identity/issues/158)
