# 93. Pairwise subject identifiers via a stateless, deterministic projection

- **Status:** Accepted
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

OpenID Connect Core 1.0 §8 defines two `subject_type` values a client may be registered with:

- **`public`** — the same `sub` value is returned to every client.
- **`pairwise`** — a different, non-correlatable `sub` value is returned per **sector** (a group of clients), so two
  colluding relying parties in different sectors cannot determine that two tokens describe the same end-user.

Only `public` was implemented: every issuance path emitted the raw, stable principal identifier as `sub`, the
`subject_type` setting was never read, and discovery advertised `subject_types_supported = ["public"]` (backlog `F9`;
`// TODO: use the subject_type setting to support PPID` in `DefaultAuthenticateHandler`).

Two facts about the existing architecture shaped the design:

- **The stable identifier is server-owned and internal.** The value emitted as `sub` is a `PrincipalId` — a
  server-generated opaque id ([ADR-0035](0035-federated-principal-and-connection-identity.md)) — that also keys grants,
  consent, sessions, and revocation. Those joins are deliberately **cross-client**: "revoke everything for this user"
  must find every client's grants. If the pairwise value replaced the internal identifier, those operations would break
  — which is precisely the correlation pairwise is designed to prevent.
- **The access token is a first-party credential the OP reads back.** The UserInfo and introspection endpoints
  re-resolve the caller from the access token's `sub`. A pairwise `sub` cannot be mathematically reversed to the
  principal, so any design that makes the access-token `sub` pairwise must either keep a persisted reverse map or accept
  that the OP's own re-resolution sees the pseudonym.

## Decision

**Keep the stable `PrincipalId` as the single internal subject everywhere, and project it to the client-facing `sub`
at the moment it leaves the server, using a stateless, deterministic pairwise derivation.** The projection lives in a
replaceable service, `ISubjectIdentifierService` (`TryAddSingleton`, DI-overridable), with one method,
`ProjectSubjectId(context, client, principalId)`.

Concretely:

- **Projection, not substitution.** `SubjectAuthentication.PrincipalId` ([ADR-0094](0094-resolve-principal-at-every-authentication-entry-point.md)),
  the persisted grant/consent/session subject, and
  all internal resolution continue to carry the stable `PrincipalId`. Pairwise only ever changes the **emitted** `sub`.
  The projection is applied at the five outbound seams: the ID-token and access-token subject-claims handlers, the
  back-channel `logout_token`, and refresh-token introspection. UserInfo needs no change — it echoes the access token's
  already-projected `sub`, which keeps UserInfo `sub` == ID-token `sub` by construction.

- **Inbound vs. outbound are distinct seams.** `OpenIdContext.GetSubjectId(principal)` **extracts** the stable subject
  from an inbound principal; `ISubjectIdentifierService.ProjectSubjectId(...)` **projects** that stable subject to a
  client-facing pseudonym. They are not inverses (the derivation is one-way) and neither delegates to the other; the
  deliberately different method names keep call sites unambiguous.

- **Deterministic, stateless derivation.** The pairwise `sub` is
  `base64url(SHA-256(sectorIdentifier ∥ principalId ∥ tenantId ∥ pairwise_salt))` (OpenID Connect Core §8.1
  non-normative example). Because it is a pure function of its inputs, the same `(sector, principal)` pair always yields
  the same pseudonym with **no persisted mapping** — no new table, entity, store, or migration. A `\0` separator is used
  because it cannot appear in the URL-safe id / host component values, so the composition is unambiguous. The `tenantId`
  isolates tenants; the optional secret `pairwise_salt` hardens the derivation against a guessed-principal preimage
  attack (the principal is itself a server-opaque id that a pairwise client never sees, so the salt is defense in depth,
  not a correctness requirement — derivation works without it).

- **Sector resolution follows §8.1.** The sector identifier is the host of the configured `sector_identifier_uri`
  setting when present; otherwise the common host of the client's `redirect_uris`; falling back to the `client_id` when
  the redirect hosts are absent or ambiguous (multiple distinct hosts without a `sector_identifier_uri`). The fallback
  treats such a client as its own sector, so it still receives a stable, non-correlatable pairwise `sub` rather than
  an error at issuance.

- **Both types are advertised.** `subject_types_supported` now defaults to `["public", "pairwise"]`
  (was `["public"]`), and `subject_type` now defaults to `public` so an unconfigured client is unaffected.

## Consequences

- No persistence, migration, or reverse-map is introduced; pairwise is a pure projection, which is the simplest design
  that satisfies the §8 requirement (ID-token and UserInfo `sub`) and additionally keeps the access-token and
  `logout_token` `sub` pairwise so a resource server cannot correlate across sectors either.
- Because the derivation is one-way and unstored, the OP cannot recover the stable principal from a pairwise `sub`
  presented **back** to it. This is immaterial for the framework flows (UserInfo re-resolution is not needed — claims
  ride the access token; grants/sessions are keyed by the internal id). Two second-order surfaces are affected and
  tracked in [`BACKLOG.md`](../../BACKLOG.md): a `sector_identifier_uri` whose JSON document is **fetched and validated**
  at registration (now done — [ADR-0095](0095-sector-identifier-uri-fetch-and-ssrf-guard.md) performs the §8.1
  fetch-and-validate behind an SSRF-guarded client), and RP-initiated end-session when an `id_token_hint` carries a
  pairwise `sub` with no `sid` (end-session resolves by `sid`, which our ID tokens always carry, so the practical path
  is covered).
- A `sector_identifier_uri` or `redirect_uris` host change moves a pairwise client to a new sector and therefore changes
  its users' `sub` — the standard §8.1 trade-off; a stable `sector_identifier_uri` is the mitigation.
- The pairwise derivation is a replaceable service, so a deployment that needs a stored/rotatable mapping can supply its
  own `ISubjectIdentifierService` without touching the issuance pipeline.
