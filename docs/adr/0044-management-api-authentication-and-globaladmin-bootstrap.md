# 44. The management API authenticates self-issued bearer tokens, and the first GlobalAdmin is bootstrapped from configuration

- **Status:** Proposed (the bootstrap administrator `ISystemTenantSeeder` decided here was reshaped into an
  `ICommandHandler<SeedTenantCommand>` by [ADR-0045](0045-tenant-provisioning-and-seeding-pipeline.md); the bootstrap
  policy itself is unchanged)
- **Date:** 2026-10-04
- **Deciders:** NCode Group

## Context

The authorization _model_ for the management API is fully decided: granular `{verb}:{family}` scopes on a single
reserved resource server `urn:ncode:management` ([ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md),
[ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md)), two claim-based realms (`GlobalAdmin`,
`TenantAdmin`) plus per-instance ownership via persisted role assignments
([ADR-0034](0034-persisted-role-assignments-and-ownership.md)), an abstract principal resolved to a server-owned
`PrincipalId` ([ADR-0035](0035-federated-principals-and-identity-resolution.md)), and plane-aware seeding that places the
control-plane scope families only in the root tenant ([ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md)).

Three things the model assumes are not yet realized, and no ADR covers them:

- **Inbound authentication is absent.** The management endpoints under `/api` are mapped and their handlers are
  implemented, but nothing turns an incoming request into an authenticated `ClaimsPrincipal`. The composition root calls
  `AddAuthentication()` with no scheme and registers no bearer handler, so `HttpContext.User` is always anonymous.
- **The implemented authorization therefore always denies.** The handlers do run — the endpoints invoke resource-based
  authorization imperatively (`IAuthorizationService.AuthorizeAsync`, and validators that take `HttpContext.User`), and
  `AuthorizationFailed` already maps an anonymous caller to `401` and an authenticated-but-unauthorized caller to `403`.
  But the realm handlers (`GlobalAdminHandler.IsInRole`, `TenantAdminHandler.IsInRole` + `tid`) and the
  `OwnershipHandler` (`IPrincipalResolver` → `PrincipalId` → assignments) evaluate an anonymous principal, so in a
  Release build every call is denied; only a `#if DEBUG` bypass inside `GlobalAdminHandler` makes the surface usable
  today. The missing piece is authentication, not a second enforcement mechanism.
- **The signing keys are per-tenant and resolved late.** A token is signed with the resolved tenant's keys under the
  tenant's issuer, and the tenant is materialized by the OpenID request-environment endpoint filter
  ([ADR-0036](0036-unified-openid-request-environment.md)) that runs _inside_ endpoint execution — after ASP.NET Core's
  authentication and authorization middleware. A pre-routing bearer middleware therefore cannot know which keys or
  issuer to validate against; authentication has to happen where the tenant is known.
- **No principal ever carries `GlobalAdmin`.** The realm is a `role` claim, but no issued token is ever stamped with
  it, and a `client_credentials` token has no subject at all. A fresh deployment therefore has no way to make the first
  authorized call that would configure the server and provision further administrators — the classic first-admin
  bootstrap gap.

A deployment must be able to stand up with nothing but configuration and reach the control plane: an operator provides a
credential out of band (an environment variable injected from a secret store), the server recognizes it, and the
operator exchanges it through the normal token endpoint for a management access token that authorizes the control-plane
scopes. Logging a server-generated token at startup is rejected on its face — a JWT expires minutes after boot, cannot
be revoked individually, and would have to be scraped from logs.

## Decision

**The management API is a resource server for its own issuer: it authenticates inbound requests by validating the
server's self-issued JWT access tokens for the `urn:ncode:management` audience _within the OpenID request environment_,
and confers the first `GlobalAdmin` on a configuration-seeded bootstrap administrator client. The existing imperative
resource-based authorization is retained; authentication populates the principal it already reads. The composition root
(`NCode.Identity.Server`) owns the wiring; a host only supplies configuration.**

### Inbound authentication — validate self-issued management tokens inside the environment

Because the signing keys and issuer are per-tenant and the tenant is resolved by the request-environment endpoint filter
([ADR-0036](0036-unified-openid-request-environment.md)), management authentication runs as a step in that environment
(an endpoint filter on the `/api` route group, ordered after the environment filter that publishes the
`IOpenIdContextFeature`), not as pre-routing middleware. The step reads the `Authorization: Bearer` token and validates a
token the server itself issued, reusing the same validation path as token introspection
([ADR-0029](0029-token-introspection-endpoint.md)):

- **Issuer** is the resolved tenant's issuer; **audience** must contain `urn:ncode:management`
  ([ADR-0027](0027-access-token-audience-from-resource-servers.md) makes the audience the resource servers that own a
  token's scopes, so a management token names the management audience).
- **Signature** is verified against the resolved tenant's `SecretsProvider` collection — the same keys the server
  publishes at JWKS and signs with ([ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md) makes the
  runtime, JWKS, and management API one source of truth), so no second key configuration is introduced.
- On success the step sets `HttpContext.User` to the token's claims; on a missing or invalid token it leaves the
  principal anonymous, and the existing imperative authorization returns `401`/`403`.
- The validated principal flows into the existing pipeline unchanged: `role` claims (`GlobalAdmin`/`TenantAdmin`) drive
  the realm handlers, `tid` gates `TenantAdmin`, and `sub` resolves through `IPrincipalResolver` to the `PrincipalId`
  the `OwnershipHandler` reads.

This closes the management loop on one issuer: the server mints the token, and the server validates it. It is the
same self-issued-resource-server shape ([ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md),
[ADR-0027](0027-access-token-audience-from-resource-servers.md)) already used to gate scopes, now applied to the
management surface's own audience.

### Enforcement — the existing imperative authorization, now reachable

Authorization is not re-plumbed. Every management endpoint already gates itself through imperative resource-based
authorization (the realm and ownership handlers via `IAuthorizationService.AuthorizeAsync`, and the create validators),
which runs after the environment filter has materialized the context — exactly where `HttpContext.GetOpenIdContext()`
and the resolved tenant are available. Authentication populating the principal is what makes that enforcement
meaningful; the `#if DEBUG` bypass remains the only development affordance and is never a Release path.
`.RequireAuthorization()` middleware is deliberately **not** used: it runs before the environment filter resolves the
tenant, so it would both lack the principal and precede the context it depends on.

### Bootstrap — the first GlobalAdmin is a configuration-seeded client

A **bootstrap administrator client** is seeded from configuration on first run, and its management-audience tokens carry
the `GlobalAdmin` role claim:

- **Configuration, not a generated secret.** The operator supplies a client id and secret through configuration
  (environment variables sourced from a secret store). This is the industry-standard first-admin pattern (Keycloak's
  `KEYCLOAK_ADMIN*`, Grafana's `GF_SECURITY_ADMIN_*`), and it is deterministic, rotatable, and never logged.
- **Idempotent seeding on the tenant-resolution path.** The bootstrap client is seeded by a tenant seeder
  (`ISystemTenantSeeder`) invoked on the lazy, resolve-time path that provisions tenants and seeds the system resource
  servers ([ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md)) — not a startup hosted
  service, which ADR-0031 deliberately avoids. It runs on **every** resolution (not only when the tenant is first
  provisioned), creating the client only when it is absent (`IClientStore.GetOrDefaultAsync`), so it seeds an
  already-provisioned tenant too. It writes through the normal persistence path so the credential is DbContext-agnostic
  ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)) and visible to the management API like any
  other client ([ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md) is the precedent for
  seed-through-persistence). The secret is protected with Data Protection before it is stored and is never emitted to a
  log; only the outcome (a client id) is logged.
- **Conferral is a claim at issuance.** The composition root maps the configured bootstrap client id(s) to the
  `GlobalAdmin` role claim when a management-audience token is issued for that client. `GlobalAdminHandler` is unchanged
  — it still reads a `role` claim — so the bootstrap is a thin issuance-time bridge, not a new authorization path. The
  operator calls the token endpoint with `client_credentials`, receives a management token stamped `GlobalAdmin`, and
  uses it to configure the server and provision durable administrators.
- **Explicit opt-in.** Absent the configuration the seeder does nothing; a bootstrap credential is never created by the
  default registrations, mirroring the explicit-opt-in posture of development keys
  ([ADR-0002](0002-ephemeral-development-keys.md)).
- **The `client_credentials` grant is a separate host opt-in.** The baseline `grant_types_supported` ships with
  `client_credentials` **off** (it permits only `authorization_code`, `implicit`, and `refresh_token`), and the setting
  is an intersect-merged ceiling ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)) that cannot be
  widened per-client. A host that uses the bootstrap administrator must therefore enable the grant **at the server
  level** — for example `OpenId:Server:Settings:grant_types_supported` in configuration, whose value replaces the root
  baseline (configuration is applied last when the root settings are assembled, so it widens rather than narrows). Until
  it is enabled the bootstrap client authenticates successfully but the token request is rejected `unauthorized_client`.

The bootstrap is a seam, not the destination: it exists to create the first durable administrator. The data-driven
`(principal, role, resource-node)` assignments and service principals ([ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md)
Phase 2, [ADR-0034](0034-persisted-role-assignments-and-ownership.md),
[ADR-0035](0035-federated-principals-and-identity-resolution.md)) generalize and ultimately supersede claim-minted
conferral; the bootstrap client remains only as the cold-start path.

### Discoverability — OpenAPI declares the security scheme

The server's OpenAPI document declares the bearer security scheme (and the OAuth2 `client_credentials` flow against the
token endpoint) and references it from the management operations, so a renderer such as Scalar presents an authentication
affordance and attaches the token to requests. The composition root owns the document
([ADR-0043](0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md) and the existing `AddOpenApi`
registration on `IdentityServerBuilder`), and the host configures the renderer.

## Options considered

- **JWT bearer validating self-issued tokens (chosen).** One issuer mints and validates; reuses the server's signing
  keys and the resource-server/audience model already in place; no second key or trust configuration. Introspection of
  the server's own token would add a network round trip to the same server for no benefit.
- **Leave inbound authentication entirely to the host.** Rejected: every host would re-wire the scheme, the published
  server would ship an unauthenticated management API, and there would be no default that the bootstrap and Scalar auth
  could build on. The composition root owning cross-cutting wiring is the established pattern
  ([ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md)).
- **Pre-routing JWT bearer middleware as the host default scheme.** Rejected: the per-tenant signing keys and issuer are
  only known after the request-environment filter resolves the tenant ([ADR-0036](0036-unified-openid-request-environment.md)),
  which runs inside endpoint execution; a scheme that runs before routing cannot select the keys or issuer to validate
  against. Authenticating as an environment step keeps one tenant-resolution flow and reuses the introspection
  validation path ([ADR-0029](0029-token-introspection-endpoint.md)).
- **Bootstrap by minting the `GlobalAdmin` role claim for a configured client (chosen).** Keeps the claim-based
  `GlobalAdminHandler` unchanged, needs no service-principal model, and is a clearly bounded cold-start bridge.
- **Bootstrap by seeding a persisted `GlobalAdmin` role assignment.** Rejected for now: `GlobalAdmin` is a claim-based
  realm and client principals (service principals) are future work
  ([ADR-0035](0035-federated-principals-and-identity-resolution.md)); honoring a persisted `GlobalAdmin` assignment
  would require generalizing the realm handler, which is Phase 2 ([ADR-0034](0034-persisted-role-assignments-and-ownership.md)),
  not the bootstrap.
- **Log a server-generated token at startup.** Rejected: a JWT expires and cannot be revoked, and a secret in logs is a
  worse security posture than an operator-supplied, out-of-band credential.
- **Seed a human subject as the first admin.** Rejected as the primary path: operators automate cold start with a
  machine credential; `client_credentials` is that path. A human bootstrap is a later, additive option.

## Consequences

- The management API is consumable end-to-end in a Release build: an operator with the bootstrap credential obtains a
  management token and configures the server, and the realm/ownership handlers finally run behind real authentication.
- The published packages gain a management authentication environment step (reusing the introspection validation path),
  an OpenAPI security scheme, and a bootstrap seeder; the existing imperative authorization is unchanged, and the
  Playground selects configuration and renders the Scalar auth affordance. New configuration options (bootstrap client
  id/secret, their presence as the opt-in) and their public API join the surface
  ([ADR-0005](0005-public-api-surface-is-tracked-and-evolves-compatibly.md)).
- The bootstrap credential is durable, rotatable, and idempotent across restarts and replicas; it is never logged and is
  protected at rest. Operators should scope it to cold start and provision durable administrators promptly, then disable
  or rotate it.
- Because seeding is on the tenant-resolution path, the operator's first `client_credentials` token request
  self-bootstraps: the request resolves the tenant — seeding the client if absent — before client authentication
  runs, so that first call succeeds with no warm-up, whether the tenant is brand new or already provisioned. This holds
  in production as well as development; the production
  prerequisites are the established ones — schema applied out of band ([ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md))
  and a persistent Data Protection key ring so the protected secret round-trips ([ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md)).
- GlobalAdmin authority is carried by the token's `role` claim, not its `scope`. The management endpoints authorize on
  role and ownership, never on the token's scopes, so the bootstrap token's scope (`read:clients` in the example) is
  incidental — it only binds the `urn:ncode:management` audience the authentication step validates. This is deliberate
  and durable: a GlobalAdmin's authority is cross-plane (the control-plane `servers`/`tenants` families plus every
  workload tenant), but the control-plane scopes are seeded only into the root tenant
  ([ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md)) and are not even requestable
  from the workload tenant the bootstrap client lives in, so no single requestable scope could represent it. Even if
  token-scope enforcement arrives for scoped delegation ([ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md)),
  GlobalAdmin keeps its role-based bypass.
- The `#if DEBUG` bypass in `GlobalAdminHandler` is now the development-only affordance behind enforced authorization,
  and is removed when the data-driven assignment model lands.
- Claim-minted `GlobalAdmin` conferral is a cold-start bridge; when persisted assignments and service principals arrive
  it is superseded, and the bootstrap client becomes a provisioning convenience rather than an authorization path.
- An operator who loses the bootstrap credential before provisioning another administrator must re-seed from
  configuration (the seeder remains idempotent on the client id), which is the intended recovery path.

## References

- [ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md) — scope enforcement via resource servers
  and client grants, extended here to the management audience.
- [ADR-0027](0027-access-token-audience-from-resource-servers.md) — the token audience model the management token names.
- [ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md) — the control-plane management
  resource server and root-tenant seeding the bootstrap admin configures.
- [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md) — the authorization model (scopes, roles,
  assignments) this authentication feeds.
- [ADR-0034](0034-persisted-role-assignments-and-ownership.md) — persisted assignments and ownership that supersede the
  claim-minted bootstrap.
- [ADR-0035](0035-federated-principals-and-identity-resolution.md) — the abstract principal and `IPrincipalResolver` the
  validated token resolves through; the future service principal for client callers.
- [ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md) — seed-through-persistence and the shared signing
  keys this authentication validates against.
- [ADR-0002](0002-ephemeral-development-keys.md) — the explicit-opt-in posture the bootstrap credential follows.
- [ADR-0043](0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md) — endpoint registration and the
  composition root owning cross-cutting wiring, including the OpenAPI document.
