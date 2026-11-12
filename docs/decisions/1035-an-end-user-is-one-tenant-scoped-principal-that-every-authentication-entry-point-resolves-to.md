# 1035. An end user is one tenant-scoped principal that every authentication entry point resolves to

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

Authority, ownership, grants, consents, sessions and usage metering have to name an end user. Keying them on the
upstream `sub` claim fails in three ways:

- A `sub` is unique only per issuer, other token profiles carry the subject elsewhere, and a non-human actor has no
  `sub` at all.
- An externally provided value as the key couples authority to external-identity churn, and contradicts
  [ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md), under
  which every identifier is server-generated and opaque.
- When the same human signs in through a Google login, a username and password and a SAML federation, a model that
  treats each connection identity as the principal counts that human as several actors, so revocation, consent and
  billing split.

A second force is that the principal must be the same value at every place an authenticated caller enters the system.
If one entry point stamped the raw local account identifier into the token `sub` while another stamped the resolved
principal, one human's grants, consent and sessions would be keyed two ways, "revoke everything for this subject" could
not span both, and an internal identifier would leak as the `sub`.

## Decision

**A principal is a server-owned, tenant-scoped entity with an opaque, stable `PrincipalId` that is distinct from the
external identities it authenticates through. Every authentication entry point resolves the caller to that
`PrincipalId`, and `SubjectAuthentication.PrincipalId` is the single authoritative subject.**

### The model

- The human principal, `PersistedFederatedPrincipal` (`IFederatedPrincipalStore`), aggregates many connection
  identities, `PersistedFederatedIdentity` (`IFederatedIdentityStore`). It is the durable twin of `ClaimsPrincipal` and
  `ClaimsIdentity`: a request that carries one identity still resolves to the principal that owns the human's other
  identities.
- An identity carries its own `FederatedIdentityId`, the upstream `Issuer` and `Subject` natural key, and its
  principal's `PrincipalId`. The natural key is unique per tenant and is used only to find an identity, never to
  identify anything the system owns. The issuer is an upstream IdP for a social or enterprise connection, or
  `AccountConstants.SelfIssuer` (`urn:ncode:identity:local-account`) for a local account.
- A principal references everything else by its `PrincipalId`: role assignments, ownership, grants, consents, sessions
  and metering never hold an external value, so a human counts once.
- Principals and identities are tenant-scoped with the fail-closed tenant filter
  ([ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md)).
  Resolution runs in the ambient tenant, the `PrincipalId` and the `(issuer, subject)` key are unique per tenant, and
  the same human in two tenants is two principals.
- "Subject" names only the upstream value and the emitted `sub` claim. The `sub` value is the `PrincipalId` or its
  pairwise projection
  ([ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md)).

### Resolution

`DefaultPrincipalResolver` (`IPrincipalResolver`) takes the subject from `OpenIdContext.GetSubjectId` and tries these in
order:

1. An existing `PrincipalId`.
2. The principal of a client acting for itself
   ([ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md)). It is
   never provisioned, and it resolves to none while the client is disabled.
3. The upstream `(issuer, subject)` through `GetByIssuerSubjectAsync`, with the issuer read from the claim that the
   tenant setting `principal_issuer_claim` names.
4. Otherwise it provisions the principal and its identity on first sight.

A caller that has no subject or no issuer claim is not provisioned: its raw subject is its principal reference, so
non-federated schemes keep working.

`ResolvePrincipalIdOrDefaultAsync` only looks up. The overload
`ResolvePrincipalIdAsync(OpenIdContext, ClaimsPrincipal, CancellationToken)` owns its store unit of work and has no
persistence types in its signature, so the persistence-free interactive package resolves through it.

### Every entry point resolves

- The interactive login resolves after the `IInteractiveCredentialVerifier` runs, so any pluggable verifier inherits
  it, and before the session and the cookie exist. The cookie's subject claim is the `PrincipalId`, and the session and
  every later authorization request key on it with a single resolution per login.
- The password grant (`DefaultAuthenticatePasswordGrantHandler`), bearer and UserInfo re-authentication
  (`DefaultAuthenticateSubjectHandler`) and the management authorization path (`OwnershipHandler`,
  `DefaultResourceOwnershipService`) each resolve the caller.

### One subject, one accessor

- `SubjectAuthentication.PrincipalId` is the authoritative subject. The `Subject` `ClaimsPrincipal` is raw
  authentication material.
- `SubjectAuthentication.Create` normalizes the raw principal's subject claim to the `PrincipalId` through
  `OpenIdContext.NormalizeSubjectId`, which calls `OpenIdEnvironment.NormalizeSubjectId` and is configured by the
  `OpenIdOptions.NormalizeSubjectId` delegate. The seam is tenant-aware and host-replaceable, so `sub == PrincipalId`
  at every ticket-construction site. No accessor reads the `sub` claim.
- The persisted subject fields (`PersistedGrant.SubjectId`, `GrantEntity.SubjectId`, `CreateSessionRequest.SubjectId`)
  hold the resolved `PrincipalId`.

### Linking

`IFederatedIdentityLinkingPolicy` (`DefaultFederatedIdentityLinkingPolicy`) decides whether a new identity attaches to
an existing principal. The tenant settings are:

- `federated_identity_join_claim` names the claim that is the join key.
- With `federated_identity_require_verified`, the claim that `federated_identity_verified_claim` names must be `true`,
  or nothing is recorded.
- `federated_identity_explicit_only` records the key but never attaches.

Otherwise a new identity attaches to the principal of an existing identity with the same join key. Two established
principals are never merged automatically, because that is where account-takeover risk concentrates.

## Options considered

- **The upstream `sub` as the principal key.** Rejected: it couples authority to external-identity churn, breaks
  server-owned opaque identifiers and counts each connection as a separate actor.
- **Organization-scoped principals.** Rejected: a human in two organizations duplicates, which is the problem this
  record exists to avoid.
- **A cross-tenant principal.** Rejected: tenant isolation is preferred over a singleton that spans tenants.
- **Explicit-only linking by default.** Rejected: it leaves duplicates until someone links them. Deterministic
  attachment on a verified join key is the default, and explicit-only is a setting.
- **Naming the actor `Subject`.** Rejected: "subject" is the OpenID term for the upstream `sub`, so reusing it for the
  aggregate overloads the word.
- **A getter that reads the `sub` claim.** Rejected: a second accessor beside the resolved field would restore a
  second source of truth.
- **A principal per human with many identities, resolved at every entry point (chosen).** One human is one principal
  per tenant, account linking is the native data shape, and the identifier is ours and stable.

## Consequences

- Authority is decoupled from external-identity churn, and one human is one principal, metered once.
- Revocation, consent and sessions span every connection through which the human authenticated.
- A `LocalAccountId` is never the `sub`: the token `sub` is always the opaque principal identifier or its pairwise
  projection.
- A first login writes the principal and its identity, on every entry point alike, and is subject to the same linking
  policy.
- The same human in two tenants has two principals, so cross-tenant correlation does not exist.
- Tests assert that the `sub` is stable and opaque rather than a raw account identifier, and that every entry point
  builds its ticket through `SubjectAuthentication.Create`.

## References

- [ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md): opaque
  server-generated identifiers.
- [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md): the
  fail-closed tenant filter.
- [ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md): the pairwise
  projection of the `PrincipalId`.
- [ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md): a client
  acting for itself is a principal.
- Code: `DefaultPrincipalResolver`, `IFederatedIdentityLinkingPolicy`, `SubjectAuthentication`,
  `InteractiveAuthenticationEndpointHandler`, `OwnershipHandler`.
