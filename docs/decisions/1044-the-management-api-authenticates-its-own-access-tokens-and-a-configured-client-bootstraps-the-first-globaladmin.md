# 1044. The management API authenticates its own access tokens and a configured client bootstraps the first GlobalAdmin

- **Status:** Accepted
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The management API under `/api` is a resource server for its own issuer. The forces on how it knows its caller:

- **The keys and the issuer are per tenant and known late.** A token is signed with the resolved tenant's keys under the
  tenant's issuer, and the request environment materializes the tenant inside endpoint execution
  ([ADR-1036](1036-one-factory-materializes-every-openid-context-for-a-request-an-addressed-tenant-or-a-job.md)),
  after ASP.NET Core's authentication and authorization middleware. A pre-routing bearer middleware cannot know which
  keys or issuer to validate against.
- **Authorization reads `HttpContext.User`.** The endpoints authorize imperatively
  (`IAuthorizationService.AuthorizeAsync` and validators that take `HttpContext.User`), so a principal that does not
  come from a management token acts on the management API. A host can make another authentication scheme its default,
  and a session cookie is such a principal that a browser also sends on a cross-site navigation.
- **The who-am-I routes describe a caller.** `GET /api/me`, `/api/me/teams`, `/api/me/tenants` and
  `/api/capabilities` report a caller's principal, platform authority, role assignments and deployment capabilities
  ([ADR-1465](1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md),
  [ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md)). A principal from
  another scheme is not a management caller.
- **A protected resource answers a missing or invalid token `401`.** RFC 6750 section 3 defines the response of a
  resource that receives a request without a valid access token.
- **No principal carries `GlobalAdmin` on a fresh deployment.** A deployment must stand up with nothing but
  configuration and still reach the control plane: an operator provides a credential out of band, such as an
  environment variable injected from a secret store, and exchanges it through the token endpoint for a management
  access token. A JWT logged at startup expires minutes after boot, cannot be revoked individually and would have to be
  scraped from logs.

## Decision

**The management API authenticates the access tokens its own issuer mints, inside the request environment, and the
authentication filter is the only source of a route's caller. The first `GlobalAdmin` is a client that configuration
seeds.**

### Inbound authentication

1. **`ManagementAuthenticationEndpointFilter` runs on `/api` right after the environment filter.** It validates the
   `Authorization` header's `Bearer` or `DPoP` token through `AuthenticateAccessTokenCommand`, as a self-issued JWT
   access token of the control-plane tenant
   ([ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md)) whose audience contains
   `urn:ncode:management`, under `IIssuedTokenValidator`
   ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)) and the tenant's own signing keys.
2. **It runs inside the request environment, because only there are the keys and the issuer known.** The API has no
   pre-routing or ASP.NET Core authentication scheme and no `.RequireAuthorization()`.
3. **The filter sets `HttpContext.User` on every management request.** It is the principal a valid management token
   authenticates, or `new ClaimsPrincipal(new ClaimsIdentity())` for any other request, whatever principal a host
   scheme, such as a session cookie, set. Every authorizing route reads `HttpContext.User`, so none trusts a foreign
   principal.
4. **The filter publishes `IManagementCallerFeature` only for an authenticated caller.** The public feature in
   `NCode.Identity.OpenId.Management.Abstractions` carries the caller's `ClaimsPrincipal`, and a capability package's
   route reads its caller there. For every other request the feature is `null`.
5. **Authentication-only routes fail closed.** `GET /api/capabilities`, `/api/me`, `/api/me/teams` and
   `/api/me/tenants` read `IManagementCallerFeature`, never `HttpContext.User.Identity.IsAuthenticated`, and answer
   `ManagementProblems.AuthenticationRequired()` (`401`) when the feature is absent, also when the filter did not run.
   They have no authority check, so they declare `401` and no `403`
   ([ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md),
   [ADR-1481](1481-the-composition-layer-serves-the-api-reference-only-where-a-host-enables-it-and-each-management-problem-says-when-it-occurs.md)); an authenticated caller that
   resolves to no principal gets `200` with a null `principalId`, because the answer describes the caller.
6. **Every other route authorizes imperatively from `HttpContext.User`.** It uses `IAuthorizationService.AuthorizeAsync`
   and the validators, answers `401` when anonymous and `403` without authority
   ([ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md)), and decides on
   role assignments and the bootstrap `GlobalAdmin` claim, never on token scopes, which only bind the audience.

### The bootstrap administrator

7. **It is opt-in through `BootstrapAdminOptions`.** `BootstrapAdmin:ClientId`, `BootstrapAdmin:ClientSecret`
   (Base64Url-encoded) and `BootstrapAdmin:TenantId` are required once configured, and `TenantId` names a host's static
   tenant or the root tenant. Without an id and a secret nothing is seeded or conferred.
8. **`BootstrapAdminSeedHandler` seeds the client.** It is an `ICommandHandler<SeedTenantCommand>`
   ([ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md)) that
   seeds the client into exactly the tenant `BootstrapAdmin:TenantId` names, for `client_secret_basic`, checked against
   the tenant's settings first. For an existing client it stores the configured secret again under the tenant's current
   master key
   ([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)), so a stale secret
   heals. It never logs the secret.
9. **The operator's first `client_credentials` request seeds the client before client authentication runs.**
10. **`BootstrapAdminAccessTokenClaimsHandler` stamps the `GlobalAdmin` role claim** on that client's tokens in that
    tenant only. `GlobalAdminHandler` succeeds on it, and it is the only role claim that confers management authority.
11. **`BootstrapAdminHostManagedClientPolicy` (an `IHostManagedClientPolicy`) marks the client host-managed.** The API
    answers `409` to any change to it after authorizing the caller, `GlobalAdmin` included, so no root-tenant
    administrator obtains a `GlobalAdmin` token through it
    ([ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md): a route answers `409`
    from what it read and declares it).
12. **A host enables `client_credentials` at the platform**, under `OpenId:Platform:Settings:grant_types_supported`
    ([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)),
    because the baseline omits it and the setting is an intersect-merged ceiling. Until then the token request is
    refused `unauthorized_client`.
13. **The bootstrap client provisions durable administrators**, human or machine, as role assignments. A lost
    credential is recovered by seeding again from configuration.

## Options considered

- **Leave inbound authentication to each host.** Rejected: every host would re-wire the scheme and the published server
  would ship an unauthenticated management API.
- **A pre-routing JWT bearer middleware.** Rejected: the per-tenant keys and issuer are known only after the
  environment filter, which runs inside endpoint execution.
- **An authentication scheme for the management API.** Rejected: a scheme's challenge and forbid can redirect to an
  interactive page, which a management API never answers.
- **Trust another scheme's principal.** Rejected: a session cookie, which a cross-site navigation carries, would act on
  the management API.
- **Log a generated token at startup.** Rejected: a JWT expires, cannot be revoked and a secret in logs is a worse
  posture than an operator-supplied, out-of-band credential.
- **A persisted `GlobalAdmin` assignment or a human as the first administrator.** Rejected: operators automate cold
  start with a machine credential, and on a fresh deployment no administrator exists to write the assignment. A
  durable `GlobalAdmin` is a role assignment at the platform node, which the bootstrap client grants.
- **An empty `200` for an anonymous who-am-I request.** Rejected: the answer describes nobody and hides a missing or
  expired token from a client that would sign in again.
- **Chosen: the filter authenticates the issuer's own tokens in the environment, is the only source of the caller, and a
  configured client carries the `GlobalAdmin` claim.** One issuer mints and validates, reuses the tenant's signing keys,
  and needs no second trust configuration.

## Consequences

- A session cookie or another host scheme's principal never acts on the management API.
- An anonymous request to a who-am-I route is answered with a `401` problem document, and the routes' OpenAPI documents
  list `401` and no `403`. `ManagementRouteProblemDeclarationTests` names them as the authenticated-caller routes.
- A management client with a valid token is the caller of every route it requests, and each route authorizes it from
  its role assignments.
- A deployment reaches the control plane from configuration alone: the bootstrap credential is durable, rotatable and
  idempotent across restarts and replicas, and never logged. Scope it to cold start and provision durable
  administrators promptly.
- `GlobalAdmin` authority is carried by the token's role claim, not its scope, because its authority is cross-plane and
  no single requestable scope represents it.
- A smell: a route that reads `HttpContext.User.Identity.IsAuthenticated` to decide an authentication-only answer
  trusts a principal the filter may not have set.

## References

- [ADR-1036](1036-one-factory-materializes-every-openid-context-for-a-request-an-addressed-tenant-or-a-job.md): the
  request environment.
- [ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md): the control-plane tenant.
- [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md): the issued-token validator.
- [ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md): the seed
  command.
- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md): the tenant master key.
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md): the
  platform settings.
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md): the 401/403/404
  rule.
- [ADR-1465](1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md),
  [ADR-1067](1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md),
  [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md): the
  authenticated-caller routes.
- RFC 6750 section 3.
- [#715](https://github.com/NCodeGroup/identity/issues/715),
  [#716](https://github.com/NCodeGroup/identity/issues/716)
