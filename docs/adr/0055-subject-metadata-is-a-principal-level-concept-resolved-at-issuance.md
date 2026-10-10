# 55. Subject metadata is a principal-level concept, resolved at issuance

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** NCode Group

## Context

[ADR-0054](0054-local-account-metadata-projection.md) projects a `LocalAccount`'s `ProfileMetadata` and
`SystemMetadata` bags into tokens and the UserInfo response. Two facts about where that metadata lives and when it is
stamped make the projection reachable for only one of the many ways a subject authenticates:

- **It lives on `LocalAccount`, which is local-connections-only.** A `LocalAccount`
  ([ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md)) is the credential/profile/status payload
  of a **local** connection — a `FederatedIdentity` whose `issuer` is this server. An **external-IdP** connection
  (an Auth0-style social or enterprise connection) is a `FederatedIdentity` whose `issuer` is the upstream IdP and
  carries **no `LocalAccount` at all** (ADR-0051's connection table). A human who signs in only through an external IdP
  therefore has nowhere server-side to hold metadata.
- **It is stamped only in the ROPC subject builder.** `DefaultCreatePasswordGrantSubjectHandler` — the resource-owner
  password grant's subject builder — is the sole place the two bags are stamped onto the subject `ClaimsPrincipal`.
  The interactive authorization path (`DefaultAuthenticateHandler`) and the management path
  (`DefaultAuthenticateSubjectHandler`) read the principal straight from the host's authentication cookie and never
  consult a `LocalAccount` or re-stamp metadata; the refresh-token path replays the persisted principal. So even a
  **local** account only gets its metadata projected through ROPC, not through interactive login or refresh.

This diverges from the Auth0-parity posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)) the metadata
model targets. In Auth0 `user_metadata` / `app_metadata` is a property of the **normalized user** — present for every
connection type and surfaced on every issuance — not a property of one connection kind. The actor in this model is the
`FederatedPrincipal` ([ADR-0035](0035-federated-principals-and-identity-resolution.md)): the single per-human entity
every connection resolves to, the persisted twin of `ClaimsPrincipal`. That is the entity Auth0's per-user metadata maps
to, and it is deliberately **contentless** today — a server-owned opaque join key.

The requirement is to make `ProfileMetadata` / `SystemMetadata` a **consistent, per-human** concept that flows
identically regardless of connection kind (local or federated) and grant flow (ROPC, interactive, refresh), that stays
configurable and extensible, and that keeps the owner-vs-server trust boundary ADR-0054 established.

## Decision

**Subject metadata is a property of the actor, not of a connection, and is inlined on the `FederatedPrincipal`. The two
bags are resolved at token/UserInfo issuance through a pluggable `IPrincipalMetadataProvider` keyed by the resolved
`PrincipalId` (the `sub`), so they flow identically for every grant flow and every connection kind through the existing
subject-claims projection pipeline ([ADR-0054](0054-local-account-metadata-projection.md)). The provider reads fresh on
every issuance — cluster-consistent and matching Auth0's per-issuance behavior; caching, when a host wants it, is a
replaceable-provider concern that must be distributed. The shared `JsonElements` helper the bags rely on is promoted to
a dependency-free `NCode.Json` package.**

### Inline the bags on the actor (`FederatedPrincipal`)

`ProfileMetadata` / `SystemMetadata` move off `LocalAccount` and become columns on `FederatedPrincipal` (and its
`PersistedFederatedPrincipal` DTO). Because a human has exactly one principal regardless of how many connections they
hold, there is exactly one of each bag per human — the "which connection's value?" ambiguity ADR-0051 cites against
putting _credentials_ on the principal does not arise for metadata, which is per-user by definition. ADR-0035's
"contentless join key" reservation targets credentials and per-connection profile (which belong on the connection);
per-actor metadata is legitimately the actor's own data, so inlining it is the honest model and matches Auth0 (metadata
lives on the normalized user). Two small JSON columns — usually `{}` — add negligible cost to the principal row and are
read only when a projection is enabled. A separate 1:1 `PrincipalMetadata` companion table was considered and rejected
as ceremony that buys nothing for two bags: relatability comes from foreign keys, not from a satellite table.

### Resolve at issuance through a pluggable provider, keyed by the principal

The single point every flow and connection kind converges is **token/UserInfo issuance**: by then every subject has
resolved to a durable `PrincipalId` ([ADR-0035](0035-federated-principals-and-identity-resolution.md)) carried as `sub`.
The three metadata projection handlers (`DefaultGet{AccessToken,IdToken,UserInfo}MetadataClaimsHandler`) resolve the
bags from `IPrincipalMetadataProvider` by that `PrincipalId`, gated by the six opt-in `send_{profile,system}_metadata_in_*`
settings, and emit each enabled bag as one nested JSON-object claim. Nothing is stamped at authentication, so ROPC,
interactive login (local **or** external IdP), and refresh all project identically — the handler never needs to know
which flow produced the subject. The provider is the pluggable seam (default `DefaultPrincipalMetadataProvider` over
`IFederatedPrincipalStore`); a host replaces it to source metadata from anywhere. This is the same first-class mediator
extension pipeline claim-shaping already uses ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md),
[ADR-0054](0054-local-account-metadata-projection.md)).

### Always fresh; caching is a host concern, and must be distributed

The default provider reads the principal's bags fresh on every issuance. The store read is a single indexed lookup,
incurred only when a projection setting is enabled (all six default off), so the common path costs nothing. Reading
fresh is both correct-by-default — `SystemMetadata` can drive authorization, so an admin edit must take effect on the
next token across every node — and cluster-consistent, because the store is the single source of truth. A per-node
`IMemoryCache` is explicitly rejected: in a cluster it serves one node a stale snapshot while another reloads, making the
same principal's authorization-relevant metadata inconsistent by node. A host that must trade freshness for fewer reads
replaces `IPrincipalMetadataProvider` with a decorator over a **distributed** cache (`IDistributedCache`/Redis), keyed by
`(tenant, principalId)` and invalidated on `SecurityStamp` change ([ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md)'s
revocation hook) — a clean, opt-in extension of the same seam rather than a broken built-in default.

### The shared JSON helper is a dependency-free package

The `JsonElements` helper (`EmptyObject` / `OrEmptyObject` / `IsNullOrUndefined`) the bags rely on lived in
`NCode.Identity.Abstractions`, which carries the ASP.NET Core `FrameworkReference` and so cannot be referenced by the
persistence layer (or by Jose/Secrets) without dragging ASP.NET Core into packages meant to stay framework-free. It is
promoted to a new dependency-free `NCode.Json` package (namespace `NCode.Json`) that any package beneath
`NCode.Identity.*` can reference. The broader cleanup of `NCode.Identity.Abstractions` — splitting its ASP.NET Core HTTP
surface and the Settings subsystem into their own packages so the core is lean and referenceable by anything, and Jose /
Secrets stay standalone-reusable — is tracked in [`BACKLOG.md`](../../BACKLOG.md).

## Options considered

- **Inline the bags on `FederatedPrincipal` (chosen).** Per-human, present for every connection kind, resolved uniformly
  at issuance; simplest model, and the principal is already the foreign-key target other entities relate to.
- **A 1:1 `PrincipalMetadata` companion table (chosen against).** A real owned 1:1 with FK / navigation / cascade was
  considered to keep the principal "contentless," but for two small bags it is ceremony: it adds a table and a join or
  second query for no relatability gain (foreign keys, not satellite tables, provide relationships). Reserve the
  companion pattern for genuinely large, optional, or independently-versioned payloads.
- **Leave metadata on `LocalAccount`, stamped in the ROPC builder (the ADR-0054 status quo, chosen against).** Reaches
  only local connections and only the ROPC flow; external-IdP connections and interactive/refresh flows get nothing.
  This ADR supersedes that placement.
- **Anchor metadata on `FederatedIdentity` (connection-level, chosen against).** A human with several connections would
  carry several bags needing merge semantics, and it would not match Auth0's per-user model.
- **Stamp the bags onto the subject principal at authentication, then copy at issuance (chosen against).** Requires
  hooking every principal-resolution point (which differ by flow) and persisting metadata into the grant; resolving once
  at issuance keyed by `PrincipalId` is simpler and inherently flow-agnostic.
- **A tenant freshness policy backed by a per-node `IMemoryCache` (chosen against).** Per-node caching is
  cluster-inconsistent for authorization-relevant data; fresh-by-default is correct, and caching — when wanted — must be
  distributed and is a host/provider concern, not a core setting.

## Consequences

- `ProfileMetadata` / `SystemMetadata` are columns on `FederatedPrincipal` / `PersistedFederatedPrincipal`; they are
  removed from `LocalAccount`, `PersistedLocalAccount`, `LocalAccountCreationRequest`, the EF `LocalAccountEntity`, and
  the ROPC subject builder.
- A new `PrincipalMetadata` runtime value and `IPrincipalMetadataProvider` seam (default `DefaultPrincipalMetadataProvider`
  over `IFederatedPrincipalStore`) join the surface; the three `DefaultGet{AccessToken,IdToken,UserInfo}MetadataClaimsHandler`
  handlers resolve from it at issuance, gated by the existing six settings.
- External-IdP (social/enterprise) connections and the interactive / refresh flows gain server-owned metadata they
  previously could not hold, matching Auth0 per-user parity; projection is now consistent across all identities and
  flows.
- Metadata is read fresh per issuance (cluster-consistent); there is no built-in freshness setting or cache. Host-side
  caching is a replaceable-provider concern that must be distributed.
- The shared `JsonElements` helper is promoted to a dependency-free `NCode.Json` package.
- The owner-vs-server trust boundary and whole-bag nesting of [ADR-0054](0054-local-account-metadata-projection.md) are
  retained; only the metadata's home and the resolution point change.
- The `SystemMetadata` write-path invariant (never end-user-writable) remains the management layer's responsibility and
  now applies at the principal level; it stays tracked in [`BACKLOG.md`](../../BACKLOG.md).

## References

- [ADR-0054](0054-local-account-metadata-projection.md) — projects the two metadata bags into tokens/UserInfo; this ADR
  relocates their home from `LocalAccount` to the principal and resolves them fresh at issuance.
- [ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md) — the `LocalAccount` payload the bags leave,
  and the `SecurityStamp` revocation hook a distributed-cache decorator would invalidate on.
- [ADR-0035](0035-federated-principals-and-identity-resolution.md) — the `FederatedPrincipal` actor the bags inline onto
  and whose resolved `PrincipalId` keys the provider.
- [ADR-0030](0030-userinfo-endpoint-and-subject-authentication-seam.md) — the UserInfo endpoint and claims-enricher seam
  one of the projection handlers extends.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the mediator pipeline that makes projection a
  first-class extension seam.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the Auth0-parity-plus posture: per-user metadata
  available across all connection types.
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — why this reasoning lives in an ADR rather than
  chat or agent memory.
