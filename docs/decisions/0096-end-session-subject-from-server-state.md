# 96. End-session resolves the stable subject from server-owned state, never by reversing a pairwise `sub`

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

Pairwise subject identifiers ([ADR-0093](0093-pairwise-subject-identifiers.md)) project the stable internal
`PrincipalId` to a per-sector pseudonym at the moment the subject leaves the server, using a **one-way, stateless**
derivation (`base64url(SHA-256(sector ∥ principalId ∥ tenantId ∥ pairwise_salt))`). The projection is deliberately not
invertible and keeps **no persisted mapping** — the same security property the grant-persistence model relies on, where
a sensitive grant key (refresh token, session id) is stored only as a one-way SHA-256 hash and a presented value is
matched **forward**, never reversed.

RP-Initiated Logout (OpenID Connect RP-Initiated Logout 1.0) lets a client present an `id_token_hint` at the
end-session endpoint. The OP validates the hint's signature and issuer (but not its lifetime, since it is typically
expired at logout) and uses it to identify the session being terminated. Our ID tokens always carry a `sid`, so the
session is resolvable from the hint; the hint also carries a `sub` and the requesting client (`azp`/`aud`).

The resolved subject flows into two consumers that both require the **stable** `PrincipalId`:

- **Server-side revocation** keys grants by the stable `PrincipalId`.
- **Back-channel logout** re-projects the stable `PrincipalId` per participating client, so each relying party's
  `logout_token` carries the same `sub` it saw in its ID token (ADR-0093 §8).

The original end-session handler took the subject straight from the hint's `sub` claim. For a **pairwise** client that
`sub` is a per-sector pseudonym, not the stable `PrincipalId`. Consumed as if it were stable, it (1) matched **no**
grants during revocation (silent no-op) and (2) was **double-projected** for each back-channel recipient, producing a
`logout_token` `sub` no relying party could match — silently breaking back-channel logout for pairwise clients.
This was tracked as issue [#59](https://github.com/NCodeGroup/Identity/issues/59).

The naive fix — a stored, reversible pairwise map (pseudonym → principal) — was rejected: a reversible mapping of a
privacy pseudonym back to the stable subject is a standing correlation/de-anonymization liability (a store compromise
re-links every pairwise `sub`), and it contradicts the one-way posture the rest of the system holds.

## Decision

**At the end-session endpoint, the stable subject (`PrincipalId`) is recovered exclusively from server-owned state —
the persisted session grant, falling back to the OP login cookie — and the `id_token_hint`'s `sub` claim is never used
as the subject identifier.** The hint contributes only what it can supply unambiguously: the `sid` (to locate the
session) and the requesting `client_id` (`azp`/`aud`).

Concretely, `DefaultResolveEndSessionSubjectHandler`:

- **Reads only `sid` and `client_id` from the hint.** The `sub` claim is not parsed into the resolution at all, so a
  pairwise pseudonym can never be mistaken for the stable subject.
- **Resolves the session forward by `sid`** and takes the authoritative stable subject from the persisted session
  grant's `SubjectId` (the `PrincipalId` captured at sign-in), alongside the participating relying parties. This
  mirrors the one-way-hashed grant-key lookup: the presented `sid` is matched forward against stored state; nothing is
  reversed.
- **Falls back to the OP login cookie** for the session/subject when the hint supplied no `sid`. The cookie's `sub` is
  the stable `PrincipalId` ([ADR-0094](0094-resolve-principal-at-every-authentication-entry-point.md)).

## Consequences

- Pairwise clients' RP-initiated logout now works end to end: revocation matches the subject's grants and each
  back-channel `logout_token` carries the correctly per-client-projected `sub`. The latent double-projection and
  silent-no-op-revocation bugs are removed.
- No reverse map, table, entity, or migration is introduced; the one-way projection stays one-way, consistent with the
  grant-persistence hashing model. The replaceable `ISubjectIdentifierService`
  ([ADR-0093](0093-pairwise-subject-identifiers.md)) remains the only pairwise seam.
- A hint that carries only a pairwise `sub` with **no resolvable session** (no `sid`, an already-lapsed session, and no
  login cookie) leaves the subject unresolved — a safe, documented limitation: the OP declines to reverse a pseudonym
  rather than guess or silently revoke the wrong subject. In practice this is unreachable for OP-issued tokens, which
  always carry a `sid`.
- Because the hint's `sub` is no longer an input, end-session can never be steered by a (validly signed but) pairwise
  `sub` into acting on a mismatched subject; the terminated session is exactly the one the `sid`/cookie identifies.
