# 35. Federated principals: one actor, many connection-identities, with a server-owned stable id

- **Status:** Accepted (amended 2026-10-07)
- **Date:** 2026-10-02
- **Deciders:** NCode Group

## Amendment (2026-10-07): principals and identities are tenant-scoped

A later decision establishes that **all persisted OpenID data is tenant-scoped except the server root**. This
supersedes the "global" framing used below: a `FederatedPrincipal` and its `FederatedIdentity` connections are owned by
a tenant and carry a tenant foreign key, so the same human authenticating in two tenants is two principals — tenant
isolation is preferred over a cross-tenant singleton. The "one actor, many connection-identities" shape is unchanged
_within_ a tenant; only the uniqueness scope narrows: the server-owned `PrincipalId` and the `(issuer, subject)`
natural key are unique **per tenant**, not globally, and resolution runs within the ambient tenant. The persistence
layer enforces this with a denormalized tenant discriminator and the fail-closed tenant-scope global query filter
([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md),
[ADR-0052](0052-entity-framework-persistence-is-a-framework-plus-slices.md)).

## Context

The authorization model ([ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md)) grants authority to a
_principal_, and the persisted assignments ([ADR-0034](0034-persisted-role-assignments-and-ownership.md)) reference that
principal. The initial realization identifies the principal by the `sub` claim and persists that external value
directly as the lookup/relationship key. Two problems follow:

- **The source is hard-coded to `sub`.** Across federated issuers a `sub` is unique only per issuer (the durable
  identity is the `(issuer, subject)` pair), other token profiles carry the subject elsewhere, and a non-human actor
  has no `sub` at all. A single hard-coded claim is not a durable principal identity.
- **An externally-provided value is the key.** Using the upstream `sub` as the relationship key couples authority to
  external-identity churn (issuer migration, re-provisioning) and contradicts
  [ADR-0014](0014-server-generated-opaque-resource-ids.md) (identifiers are server-generated and opaque), which the
  rest of the system follows.

There is also a modeling hazard to avoid: when the same human signs in through different connections (a Google login,
a username/password, a SAML federation), a model that treats each connection-identity _as_ the principal counts that
human as several distinct actors — the duplicate-user / double-billing problem that account-linking then patches over.
The requirement is to make "one human, many connection-identities" the **native** shape, not a bolt-on.

## Decision

**A principal is a server-owned entity with its own opaque, stable identifier, distinct from the external identities it
authenticates through. The human principal — a `FederatedPrincipal` — aggregates many `FederatedIdentity` connection
identities, mirroring .NET's `ClaimsPrincipal` / `ClaimsIdentity` but persisted and durable. Authority references the
principal's server-generated id, never an external value.**

### The principal is abstract; the human is one kind of it

```
Principal (abstract)                 — PrincipalId, PrincipalType; what authority references
 ├── FederatedPrincipal (human)      — one global human; owns many FederatedIdentity
 └── ServicePrincipal   (service)    — a client/service actor (future)
```

`Principal` is the abstract actor that authority is granted to. `FederatedPrincipal` is the human realization; a
`ServicePrincipal` (a client acting on its own behalf) is a future sibling. Both are principals sharing one
`PrincipalId` space, so the authorization layer is written once against the abstraction.

### The human and its identities

- **`FederatedPrincipal`** is a single, **global** representation of a human — not scoped under an organization, and
  with no `organization → users` hierarchy, so there is no structural path to duplicate a human. An organization is an
  attribute/link on a connection identity, not a container of the principal.
- **`FederatedIdentity`** is one external connection-identity; a principal has many. It carries the upstream
  `(issuer, subject)` and, optionally, the organization and verification context of that connection.

This is the persisted analogue of `ClaimsPrincipal` (the actor) with its `ClaimsIdentity` collection (the authenticated
identities); the `Federated` prefix marks it as this model's own type, distinct from the runtime .NET types. The
difference in lifetime is deliberate: `ClaimsPrincipal.Identities` is a per-request, in-memory aggregation, whereas a
`FederatedPrincipal` accumulates its identities durably over time — so a request that carries one identity still
resolves to the principal that owns the human's other identities.

### Identifiers

Each entity owns a server-generated opaque canonical id ([ADR-0014](0014-server-generated-opaque-resource-ids.md)) over
an internal surrogate key:

| entity               | identifiers and keys                                                                                                                                                                                     |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `FederatedPrincipal` | surrogate `Id`; canonical **`PrincipalId`** — the principal's stable public id, and the value emitted as the `sub` claim.                                                                                |
| `FederatedIdentity`  | surrogate `Id`; canonical **`FederatedIdentityId`** (its own public id); `Issuer` + `Subject` — the upstream `(iss, sub)`, unique and **lookup-only**; a foreign key to the owning `FederatedPrincipal`. |

The term **subject** keeps its colloquial OpenID meaning only: the upstream value on a `FederatedIdentity`, and the
`sub` claim emitted at the issuance boundary (whose value is the `PrincipalId`). It is never an entity name nor one of
this system's identifiers. The external `(issuer, subject)` pair is a natural key used to _find_ an identity, never to
_identify_ anything the system owns.

### Resolution

`IPrincipalResolver` maps the caller's authenticated context to a `PrincipalId`. For a human token the
`FederatedPrincipalResolver` interprets the subject claim first as a server-owned `PrincipalId` and, failing that, as an
upstream `(issuer, subject)` that it matches to a `FederatedIdentity`, provisioning the principal (and its identity) on
first sight. The source claim is configurable (defaulting to `sub`, keyed by issuer), so the identity source is not
hard-coded; a caller with no resolvable upstream identity falls back to its raw subject value, so non-federated schemes
keep working.

Resolution happens at the two points where an authenticated caller enters the system. At the
**subject-authentication seam** (where the OpenID runtime turns the host's authenticated subject into a session),
the resolver runs so the resolved `PrincipalId` is what flows into the issued grant and is emitted as the `sub` claim —
tokens, grants, introspection, and activity therefore all carry the durable principal, not the raw external subject,
without any change to the grant's own shape. In the **management authorization path**, the resolver maps the caller to
its `PrincipalId` before ownership and role assignments are evaluated or recorded.

### Linking policy

Associating two connection-identities with the same human is a trust decision, exposed as a configurable, deterministic,
audited `IFederatedIdentityLinkingPolicy` — never a fuzzy heuristic:

- **Default:** a _new_ identity whose **verified** email, from a connection marked trustworthy for linking, matches an
  existing principal is **auto-attached** to that principal. This is what makes deduplication native rather than a
  manual afterthought.
- **Merging two already-established principals is never automatic** — it requires explicit administrative or end-user
  action, because that is where account-takeover risk concentrates.
- The join claim, the set of link-trusted connections, the verified-email requirement, and an explicit-only mode are all
  configurable. These knobs, and the resolution source/issuer claims, are tenant settings with server defaults on the
  shared request environment ([ADR-0036](0036-unified-openid-request-environment.md)), so a tenant overrides the server
  default at runtime.

### Authority references the principal

Role assignments, ownership, grants, and activity/usage metering all reference the abstract **`PrincipalId`**. Because a
human is one principal regardless of how many connections they use, the human is counted once.

### Service principals (compatibility, built later)

The design admits a second principal kind without reworking the authorization layer, guaranteed by three commitments and
no code today:

1. Authority references the abstract **`PrincipalId`**, never a `FederatedPrincipal`-specific id.
2. **`Principal`** is the abstract supertype; `FederatedPrincipal` and a future `ServicePrincipal` are kinds sharing one
   `PrincipalId` space. A `Principal` registry or a `PrincipalType` discriminator is an additive hook.
3. `IPrincipalResolver` is the abstract seam (caller context → `PrincipalId`); a `ServicePrincipalResolver` for
   client-credentials callers slots in with no change to the authorization handlers.

## Options considered

- **Persist the upstream `sub` as the principal key (the initial realization).** Rejected: couples authority to
  external-identity churn, breaks `ADR-0014`, and treats each connection-identity as a separate actor — the
  duplicate-human hazard.
- **A `FederatedPrincipal` with many `FederatedIdentity`, referenced by a server-owned opaque id (chosen).** One human
  is one principal; account-linking is the native data shape; the id is ours and stable; the model reads as the
  persisted twin of `ClaimsPrincipal`/`ClaimsIdentity`.
- **Organization-scoped principals (an `organization → users` hierarchy).** Rejected: a human in two organizations
  duplicates, reproducing exactly the problem this ADR exists to avoid. Organization is modeled as an attribute on a
  connection identity instead.
- **Explicit-only linking as the default.** Rejected as the _default_: it leaves duplicates until someone links them.
  Deterministic auto-attach of a new identity on a trusted, verified key is the default, with explicit-only available by
  configuration; auto-merging established principals stays explicit.
- **Naming the human `Subject` / `FederatedSubject`.** Rejected: "subject" is the colloquial OpenID term for the
  upstream `sub`, so reusing it for the aggregate actor overloads the word. The actor is a `Principal`; "subject" is
  reserved for the external identity and the emitted claim.

## Consequences

- Authority is decoupled from external-identity churn; account-linking is structural rather than a patch; one human is
  one principal, metered once.
- New `FederatedPrincipal` / `FederatedIdentity` entities, DTOs, and stores join the persistence layer, with
  `IPrincipalResolver` and `IFederatedIdentityLinkingPolicy` seams.
- The `sub` the server emits becomes the stable `PrincipalId`; relying parties get one durable subject for a human
  regardless of which connection authenticated the session.
- The `PrincipalId` reference that assignments, ownership, and grants already carry is retained; its persisted column is
  re-based on a principal-id length rather than the subject-id length it was initially sized with. A grant persists the
  resolved `PrincipalId` in its subject field because the subject-authentication seam resolves the principal before the
  grant is created — the value is corrected at its source rather than by renaming the grant surface.
- `ADR-0033`/`ADR-0034`'s abstract-principal note is realized here; the abstract `Principal` supertype keeps the
  service-principal path open.

## References

- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — server-generated opaque identifiers, which the principal
  and identity ids follow.
- [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md) — the authorization model whose abstract principal
  this ADR gives a durable identity.
- [ADR-0034](0034-persisted-role-assignments-and-ownership.md) — the persisted assignments that reference `PrincipalId`.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the pre-release, Auth0-parity-plus posture under which
  the principal-identity surface is refined before release.
- [ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md) — adds the local kind of connection: a
  `FederatedIdentity` whose `issuer` is this server, carrying a `LocalAccount` credential/profile/status payload. This
  completes the definition here — a connection's `issuer` is usually an external IdP, but is this server for a local
  account — without changing resolution or linking, which stay uniform across both.
