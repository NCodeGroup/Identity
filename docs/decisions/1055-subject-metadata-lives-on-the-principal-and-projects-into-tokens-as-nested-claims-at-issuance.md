# 1055. Subject metadata lives on the principal and projects into tokens as nested claims at issuance

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** NCode Group

## Context

A subject carries three kinds of data that a token or the UserInfo response may need:

- **`Claims`**: structured, individually addressable claim assertions (`given_name`, `email`, ...), stored as
  `type`/`value` rows on a local account.
- **`ProfileMetadata`**: a free-form JSON object that the account owner may read and write.
- **`SystemMetadata`**: a free-form JSON object that only the server or an administrator writes, never the end user, and
  that may carry authorization-relevant data.

This mirrors the model the Auth0-parity posture targets
([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)):
normalized profile claims, `user_metadata` and `app_metadata`. The two bags share a storage shape and differ on one
axis, who may write them. `Claims` differ on a second axis, addressability. The axes are orthogonal: collapsing claims
into a bag loses the per-type addressability that scopes, the `claims` request parameter and authorization need, and
spreading a bag into individual claims loses nesting and, for the owner-writable bag, opens a privilege-escalation
hazard. The three stores therefore stay distinct in storage and unify only at the runtime `Claim` boundary.

In Auth0, metadata belongs to the normalized user: present for every connection type and surfaced on every issuance. In
this model the actor is the `FederatedPrincipal`
([ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md)),
the single per-human entity that every connection resolves to. A local account exists only for a local connection, and an
external-IdP connection has none, so metadata kept on the account would leave a human who signs in only through an
external IdP with nowhere to hold it. Metadata stamped at one authentication site, such as the password grant's subject
builder, would reach only that flow: interactive login and refresh read the principal elsewhere and never see it.

The requirements are that metadata is a per-human concept that flows identically for every connection kind and every
grant flow, that it is opt-in and collision-proof, that the owner-versus-server trust boundary survives onto the wire,
and that a host can shape claims with its own code.

## Decision

**Subject metadata is a property of the actor. Both bags are inlined on the `FederatedPrincipal` and resolved at token
and UserInfo issuance through a pluggable `IPrincipalMetadataProvider` keyed by the resolved `PrincipalId`. Each enabled
bag is emitted whole as one nested JSON claim under a reserved claim type, through the subject-claims handler
pipeline.**

### The bags live on the principal

`ProfileMetadata` and `SystemMetadata` are `JsonElement` members of `PersistedFederatedPrincipal`, stored as columns of
`FederatedPrincipalEntity`, and default to an empty object. A human has exactly one principal however many connections
they hold, so there is one of each bag per human for every connection kind, and no question of which connection's value
applies. Two small JSON columns, usually `{}`, add negligible cost to the principal row and are read only when a
projection is enabled.

### Resolution at issuance through a provider

By issuance every subject has resolved to a durable `PrincipalId`, carried as `sub`
([ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md)).
`IPrincipalMetadataProvider.GetMetadataAsync(OpenIdContext, principalId, ct)` returns a `PrincipalMetadata`, a readonly
record struct with an `Empty` value. The default `DefaultPrincipalMetadataProvider` opens its own store manager and reads
`IFederatedPrincipalStore` by the `PrincipalId`. Nothing is stamped at authentication, so the password grant,
interactive login (local or external IdP) and refresh all project identically, and a host replaces the provider to source
metadata from anywhere.

### Projection through the subject-claims pipeline

Projection is three ordinary command handlers on the mediator pipeline that every claim already flows through
([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)):

| Handler                                    | Command                           |
| ------------------------------------------ | --------------------------------- |
| `DefaultGetAccessTokenMetadataClaimsHandler` | `GetAccessTokenSubjectClaimsCommand` |
| `DefaultGetIdTokenMetadataClaimsHandler`     | `GetIdTokenSubjectClaimsCommand`     |
| `DefaultGetUserInfoMetadataClaimsHandler`    | `GetUserInfoClaimsCommand`           |

Six opt-in settings gate them, each defaulting to `false`: `send_{profile,system}_metadata_in_{access_token,id_token,user_info}`.
Opt-in by default matches Auth0, where nothing is in a token until it is surfaced, and avoids leaking personal or
authorization data by accident.

### Whole-bag nesting, never flattening

An enabled bag is emitted as one `JsonClaimValueTypes.Json` claim under `SubjectMetadataClaimTypes.ProfileMetadata` or
`SystemMetadata`, whose types are `profile_metadata` and `system_metadata` (namespace `NCode.Identity.OpenId.Principals`
in `NCode.Identity.OpenId.Abstractions`). The claim re-serializes as a nested object in the JWT and in UserInfo. An empty
bag is not emitted, and a claim type that another handler already supplied is not duplicated.

A bag is never spread into top-level claims, for three reasons:

- **Security.** `ProfileMetadata` is end-user-writable. Flattening its keys would let a user inject claims such as
  `{"role":"admin"}` or `{"email_verified":true}` that a resource server may trust, and a stray `SystemMetadata` key such
  as `sub` or `aud` would shadow a registered claim. Nesting under a reserved claim lets a consumer know that
  `profile_metadata.*` is user-asserted.
- **Provenance.** Two delimited objects keep the trust boundary visible on the wire: a verifier can treat
  `system_metadata.*` as server-vouched and `profile_metadata.*` as user-asserted.
- **Fidelity.** Metadata is arbitrary JSON, while a `Claim` is a type and a string. Emitting the bag whole round-trips
  losslessly, and any flattening scheme invents a lossy convention.

The two claim types are constants, not settings. Producers and consumers read settings at different scopes (tenant
versus client), so a client-overridable name would let one site emit under one name while another looks for a different
one and finds nothing.

### The pipeline is the claim-shaping seam

A host registers an `ICommandHandler` on the same commands to emit, transform or drop claims, the equivalent of Auth0
Actions. Per-key flattening is a deliberate, namespaced act that belongs in such a handler, and it never surfaces
owner-writable keys as top-level claims.

### Always fresh

The provider reads the principal's bags on every issuance. The read is a single indexed lookup, incurred only when a
projection setting is on. Reading fresh is correct by default, because `SystemMetadata` can drive authorization and an
administrator's edit must take effect on the next token on every node, and it is cluster-consistent because the store is
the single source of truth. A host that trades freshness for fewer reads replaces the provider with a decorator over a
distributed cache keyed by `(tenant, principalId)`.

### Write paths

`SystemMetadata` is never end-user-writable. Its only write paths are the administrator-gated management create and
metadata endpoints
([ADR-1057](1057-local-account-management-is-an-opt-in-capability-that-provisions-the-principal-eagerly-and-authorizes-per-tenant.md)).

## Options considered

- **Flatten each bag's keys into top-level claims.** Loses on security (owner-writable claim injection), provenance and
  fidelity. Namespaced flattening remains available as a custom handler.
- **Keep the bags on the local account or on the connection identity.** A local account exists only for local
  connections, so external-IdP humans get nothing; a human with several connections would carry several bags that need
  merge semantics, and neither matches Auth0's per-user model.
- **A 1:1 `PrincipalMetadata` table.** For two small bags it adds a table and a join or second query with no gain in
  relatability, which foreign keys provide. The companion pattern suits large, optional or independently versioned
  payloads.
- **Stamp the bags onto the subject at authentication.** It requires hooking every principal-resolution point, which
  differs by flow, and persisting metadata into the grant. Resolving once at issuance is simpler and flow-agnostic.
- **Configurable claim names.** A silent-correlation hazard across the tenant and client scope split; custom names are
  a handler concern.
- **One destination-list setting per bag.** It introduces a new value shape and merge semantics; six booleans match the
  established `send_id_claims_in_access_token` pattern.
- **A per-node `IMemoryCache` in the provider.** It serves one node a stale snapshot while another reloads, so the same
  principal's authorization-relevant metadata differs by node.
- **Inline the bags on the principal and resolve them fresh at issuance (chosen).** Per-human, present for every
  connection kind, uniform across grant flows, with the principal already the foreign-key target of other entities.

## Consequences

- External-IdP connections and the interactive and refresh flows hold and project server-owned metadata exactly as
  local accounts do through the password grant.
- Metadata is opt-in and, when enabled, a nested object that cannot collide with a registered claim and that preserves
  the owner-versus-server boundary on the wire.
- Every token and UserInfo response that enables a bag costs one store read; with all six settings off, it costs
  nothing.
- A host shapes claims by registering a command handler, and caches by replacing the provider.
- The handlers, the provider and the claim-type constants are covered by unit tests of the projection, and the store
  mapping by the persistence tests.

## References

- `NCode.Identity.OpenId.Abstractions`: `IPrincipalMetadataProvider`, `PrincipalMetadata`, `SubjectMetadataClaimTypes`.
- [ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md): the
  `FederatedPrincipal` actor whose `PrincipalId` keys the provider.
- [ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md): the local account
  that holds `Claims`.
- [ADR-1057](1057-local-account-management-is-an-opt-in-capability-that-provisions-the-principal-eagerly-and-authorizes-per-tenant.md): the management endpoints that write
  `SystemMetadata`.
- [ADR-1401](1401-userinfo-authenticates-an-access-token-returns-enricher-built-claims-and-audits-disclosures-on-opt-in.md):
  the UserInfo endpoint that one projection handler extends.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): the mediator
  pipeline.
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md): the
  Auth0-parity-plus posture.
