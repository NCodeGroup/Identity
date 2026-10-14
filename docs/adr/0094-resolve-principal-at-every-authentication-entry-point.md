# 94. Resolve the principal at every authentication entry point; `SubjectAuthentication.PrincipalId`

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

[ADR-0035](0035-federated-principals-and-identity-resolution.md) established the intended model: the subject is a
**stable, server-owned principal identifier** — opaque, and distinct from the external/local connection identities it
authenticates through — and it is "the value carried into grants and emitted as the `sub` claim." `IPrincipalResolver`
turns the raw authenticated `(issuer, subject)` into that `PrincipalId`, provisioning on first sight.

The implementation only **half** honored this. Resolution ran on two of the three authentication entry points, so
`SubjectAuthentication.SubjectId` held two different _kinds_ of value depending on how the end-user authenticated:

| Entry point                                                                                                              | What populated the ticket's subject | Resolved? |
| ------------------------------------------------------------------------------------------------------------------------ | ----------------------------------- | --------- |
| Interactive / authorization-code (`DefaultAuthenticateHandler`, cookie from `LocalAccountInteractiveCredentialVerifier`) | the raw `LocalAccountId`            | **no**    |
| Resource-owner password (`DefaultAuthenticatePasswordGrantHandler`)                                                      | the resolved `PrincipalId`          | yes       |
| Bearer / UserInfo re-auth (`DefaultAuthenticateSubjectHandler`)                                                          | the resolved `PrincipalId`          | yes       |

This produced two concrete defects:

- **Inconsistent grant keying → incomplete revocation (security).** The same human's grants, consent, and sessions were
  keyed by `LocalAccountId` when they logged in interactively but by the opaque `PrincipalId` when they used ROPC, so
  "revoke everything for this subject" and consent reuse could not span both.
- **Raw-identifier leakage.** The interactive flow emitted the internal `LocalAccountId` as the token `sub`, contrary to
  ADR-0035's "never an external value."

Separately, the field name `SubjectId` was itself a trap: it suggested "the `sub` claim," but it actually held the
resolved principal — a second, divergent source of truth next to the principal's own `sub` claim.

## Decision

**Resolve the principal uniformly at every authentication entry point, make `PrincipalId` the single authoritative
subject, and rename the field to say so.**

- **Interactive login resolves and normalizes.** `InteractiveAuthenticationEndpointHandler` now resolves the verified
  credential to its `PrincipalId` and **re-stamps the cookie's subject claim to that id** _after_ any pluggable
  `IInteractiveCredentialVerifier` runs — so LDAP/external verifiers inherit it — and _before_ the session is
  established and the cookie is issued. The server-side session and every subsequent authorization request therefore
  key on the same `PrincipalId`, with a single resolution per login (the cookie is authoritative thereafter).
- **A self-contained resolver overload.** `IPrincipalResolver` gains
  `ResolvePrincipalIdAsync(OpenIdContext, ClaimsPrincipal, CancellationToken)`, which manages its own store unit of work
  and falls back to the raw subject when there is no resolvable upstream identity. Its signature carries **no
  persistence types**, so the persistence-free interactive leaf package can resolve without taking a persistence
  dependency, and the duplicated "open a unit of work, resolve, save" helpers collapse onto it.
- **One subject, one accessor.** `SubjectAuthentication.SubjectId` is renamed **`PrincipalId`**. The `Subject`
  `ClaimsPrincipal` is explicitly documented as raw authentication _material_ (inputs); `PrincipalId` is the single
  authoritative identifier that grants, consent, sessions, revocation, and token `sub` projection all reference. The
  pairwise projection ([ADR-0093](0093-pairwise-subject-identifiers.md)) now projects `PrincipalId` directly, dropping
  the transitional "read the principal's `sub` claim" fallback that only existed to paper over the inconsistency.
- **A `sub`-reading convenience was deliberately _not_ added.** Unlike `SessionId` (whose `sid` lives only as a claim
  and has one unambiguous source), the subject is a first-class resolved field; a second accessor that read the `sub`
  claim would resurrect the divergence. The `Subject.sub` == `PrincipalId` invariant is the direction of travel, not a
  parallel getter.

The persistence/request surface keeps the name `SubjectId` (`PersistedGrant.SubjectId`, `GrantEntity.SubjectId`,
`CreateSessionRequest.SubjectId`): it is a reasonable generic term for "the subject this record belongs to," it now
**uniformly holds the resolved `PrincipalId`** (documented as such), and renaming the columns would force a schema
migration for no behavioral gain. The boundary reads self-documentingly — `SubjectId = subjectAuthentication.PrincipalId`.

## Consequences

- Revocation, consent reuse, and session enumeration span a user's activity regardless of how they authenticated, and
  the internal `LocalAccountId` is never emitted — the token `sub` is always the opaque principal id (or its pairwise
  projection per ADR-0093).
- Interactive login now performs a store write (resolve/provision the federated principal) on first login — the same
  cost ROPC already paid — and is subject to the same `IFederatedIdentityLinkingPolicy` (verified-join-key linking),
  which is the intended uniform behavior.
- **Behavioral change:** a resource-owner-password (and interactive) `sub` is now the resolved opaque principal id, not
  the raw `LocalAccountId`. Greenfield/pre-release, so no compatibility shim ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md));
  tests assert the stable-and-opaque property instead of the raw id.
- `IPrincipalResolver` and `SubjectAuthentication` are public API; the overload addition and the `SubjectId` →
  `PrincipalId` rename are expected `PublicAPI.Unshipped.txt` churn.
- The `Subject.sub` == `PrincipalId` invariant is enforced for the interactive path (re-stamp) but the ROPC/bearer
  principals still carry their raw `sub` claim on the (unread-for-emission) `Subject`; fully enforcing the invariant at
  every construction site is a tracked follow-up, not required for correctness since emission reads `PrincipalId`.
