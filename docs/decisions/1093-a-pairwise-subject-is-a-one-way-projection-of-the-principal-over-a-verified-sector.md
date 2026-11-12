# 1093. A pairwise subject is a one-way projection of the principal over a verified sector

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

OpenID Connect Core 1.0 §8 defines two `subject_type` values a client may be registered with:

- **`public`** — the same `sub` value is returned to every client.
- **`pairwise`** — a different, non-correlatable `sub` value is returned per **sector** (a group of clients), so two
  colluding relying parties in different sectors cannot determine that two tokens describe the same end-user.

Two facts about the architecture shape the design:

- **The stable identifier is server-owned and internal.** The value a public client receives as `sub` is a
  `PrincipalId`, a server-generated opaque id ([ADR-1035](1035-federated-principals-and-identity-resolution.md)), that
  also keys grants, consent, sessions and revocation. Those joins are deliberately **cross-client**: "revoke everything
  for this user" must find every client's grants. If the pairwise value replaced the internal identifier, those
  operations would break, which is precisely the correlation pairwise is designed to prevent.
- **A pairwise value cannot be reversed.** Any design that makes the pseudonym the identifier the OP reads back must
  keep a persisted reverse map or accept that the OP's own re-resolution sees the pseudonym.

OpenID Connect Core §8.1 also defines how clients share a sector. When a client registers a `sector_identifier_uri`:

- the URI **MUST** use the `https` scheme;
- the OP **fetches** the document, which is a **JSON array of `redirect_uri` strings**;
- the OP **verifies** that every one of the client's registered `redirect_uris` is present in that array.

The check lets several clients share a sector, and so a stable pairwise `sub`, only when a party that controls the
`sector_identifier_uri` document has vouched for their redirect URIs; otherwise any client could claim any host as its
sector and either correlate or impersonate another sector's pseudonyms. §8.1 also requires a `sector_identifier_uri`
for a pairwise client whose `redirect_uris` span multiple hosts, because the sector would otherwise be ambiguous.

A server-side fetch of a client-supplied URL is a textbook server-side request forgery (SSRF) sink: left unguarded, a
caller could point `sector_identifier_uri` at `http://169.254.169.254/…` (cloud metadata), `http://127.0.0.1/…` or an
internal host and use the OP as a confused deputy. Dynamic Client Registration
([ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md)) makes the fetch reachable by untrusted callers, so it must be
hardened.

## Decision

**Keep the stable `PrincipalId` as the single internal subject everywhere, and project it to the client-facing `sub` at
the moment it leaves the server, with a stateless, deterministic, one-way derivation over a sector that registration
verifies.**

### The projection

- **Projection, not substitution.** `SubjectAuthentication.PrincipalId`, the persisted grant, consent and session
  subject, revocation and all internal resolution carry the stable `PrincipalId`. Pairwise only ever changes the
  **emitted** `sub`.
- **One replaceable service.** `ISubjectIdentifierService` (`TryAddSingleton`, DI-overridable) has one method,
  `ProjectSubjectId(context, client, principalId)`. It is applied at the ID-token and access-token subject-claims
  handlers, the back-channel `logout_token` and refresh-token introspection. UserInfo echoes the access token's
  projected `sub`, so the UserInfo `sub` equals the ID token's by construction.
- **Inbound and outbound are distinct seams.** `OpenIdContext.GetSubjectId(principal)` **extracts** the stable subject
  from an inbound principal; `ProjectSubjectId` **projects** that stable subject to a client-facing pseudonym. They are
  not inverses, because the derivation is one-way, and neither method calls the other. The deliberately different names
  keep call sites unambiguous.
- **Deterministic, stateless derivation.** The pairwise `sub` is
  `base64url(SHA-256(sector ∥ principalId ∥ tenantId ∥ pairwise_salt))` (the non-normative example of OpenID Connect
  Core §8.1), with `\0` separators because that character cannot appear in the URL-safe id and host components, so the
  composition is unambiguous. Because it is a pure function of its inputs, the same `(sector, principal)` pair always
  yields the same pseudonym and **no mapping is stored**: no table, entity, store or migration. The `tenantId` isolates
  tenants. The optional secret `pairwise_salt` hardens the derivation against a guessed-principal preimage attack; the
  principal is itself a server-opaque id that a pairwise client never sees, so the salt is defense in depth, and the
  derivation works without it.
- **Sector resolution follows §8.1.** The sector is the host of `sector_identifier_uri` when set; otherwise the common
  host of the client's `redirect_uris`; otherwise the `client_id`, when the redirect hosts are absent or ambiguous. The
  fallback treats such a client as its own sector, so it still receives a stable, non-correlatable `sub` rather than an
  error at issuance.
- **Both types are advertised.** `subject_types_supported` defaults to `["public", "pairwise"]`, and `subject_type`
  defaults to `public`, so an unconfigured client receives the public subject.
- **Server-side state recovers the stable subject** wherever the OP acts on a presented pseudonym: end session
  ([ADR-1380](1380-logout-ends-the-session-found-in-server-state-confirmed-when-unsure-and-notifies-relying-parties.md)) and token exchange
  ([ADR-1101](1101-token-exchange-issues-delegation-and-impersonation-tokens-from-tokens-the-tenant-issued.md)). A pseudonym is never reversed.

### Registration and the sector document

- **`subject_type` and `sector_identifier_uri` are registration metadata.** Dynamic registration accepts both as client
  settings (`OpenIdSettingNames.SubjectType` and `SectorIdentifierUri`) and reports them back, so a registration
  round-trips through the management endpoint (RFC 7592). `subject_type` is `public` or `pairwise`.
- **The multi-host rule is enforced at registration.** Metadata validation refuses a pairwise client whose
  `redirect_uris` span several hosts without a `sector_identifier_uri`. A client created outside registration, through
  the management API or seeding, runs no registration validation, so the runtime falls back to its `client_id` as
  sector.
- **`ISectorIdentifierValidator` is the deployment seam** (`DefaultSectorIdentifierValidator`, `TryAddSingleton`,
  DI-overridable). `ValidateAsync(context, sectorIdentifierUri, redirectUris, ct)` returns `null` when valid or an
  `IOpenIdError` (`invalid_client_metadata`) otherwise. Both `POST /register` and `PUT /register/{client_id}` invoke it,
  only when a `sector_identifier_uri` is present. The default requires an absolute `https` URI whose document is a JSON
  array of strings that lists every registered redirect URI (ordinal match).
- **The fetch goes through the shared guarded outbound client** (`GuardedHttpClient` with
  `DefaultOutboundHttpForgeryGuard`, [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md)),
  which pins the connection away from private, loopback and link-local addresses and follows no redirect. The block is
  the non-negotiable floor and no setting turns it off.
- **Three tenant settings bound the fetch**, merged as
  [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)
  describes:
    - `sector_identifier_uri_allowed_hosts` merges by `Intersect`, and unset means any host;
    - `sector_identifier_uri_fetch_timeout` merges by `Min` and defaults to 10 seconds;
    - `sector_identifier_uri_max_size_bytes` merges by `Min`, defaults to 100 KB, and caps the document through
      `IOutboundHttpContentReader.ReadCappedAsync`.

## Options considered

- **Replace the internal identifier with the pseudonym.** Rejected. Grants, consent, sessions and revocation are
  cross-client joins keyed by the stable id, and substituting the pseudonym breaks them.
- **Persist a reverse map from pseudonym to principal.** Rejected. It adds a table, a store and a migration, and it
  stores exactly the link that pairwise exists to keep from correlating. A deployment that needs a stored or rotatable
  mapping replaces `ISubjectIdentifierService`.
- **Accept the sector host without fetching its document.** Rejected. Sector membership would be unverified, so a client
  could claim another party's host as its sector.
- **Inspect only the URL host string for private addresses.** Rejected. A hostname can resolve to a private address
  (DNS rebinding) and a literal can be obfuscated; only a check that resolves the address and pins the connection to it
  is robust, which is what the shared guarded client does.
- **Make the private-address block a setting.** Rejected. The block is the SSRF floor. A `sector_identifier_uri` is
  public by nature, because relying parties fetch it too, so the floor does not impede legitimate use, and a deployment
  that must reach a non-public sector host supplies its own `ISectorIdentifierValidator`.
- **Project the pseudonym from a stateless derivation and verify the sector document at registration.** Chosen. It is
  the simplest design that satisfies §8 and §8.1 and needs no persistence.

## Consequences

- Pairwise is a pure projection with no persistence, migration or reverse map. It keeps the ID-token, UserInfo,
  access-token and `logout_token` `sub` pairwise, so a resource server cannot correlate across sectors either.
- A pairwise client can share a sector only when the `sector_identifier_uri` document vouches for its redirect URIs, and
  the fetch is SSRF-hardened by default with an optional host allow-list.
- Because the derivation is one-way and unstored, the OP cannot recover the stable principal from a pairwise `sub`
  presented back to it; server-side state supplies it where the OP acts on one.
- A change of `sector_identifier_uri` or of the redirect host moves a client to a new sector and so changes its users'
  `sub` values, the standard §8.1 trade-off; a stable `sector_identifier_uri` is the mitigation.
- A deployment with a stored mapping, a rotatable one, or another sector trust model such as an internal sector registry
  replaces `ISubjectIdentifierService` or `ISectorIdentifierValidator` without touching the issuance pipeline or the
  endpoint.

## References

- OpenID Connect Core 1.0 §8 and §8.1; RFC 7591 and RFC 7592.
- Issues [#47](https://github.com/NCodeGroup/identity/issues/47) and
  [#58](https://github.com/NCodeGroup/identity/issues/58).
- [ADR-1035](1035-federated-principals-and-identity-resolution.md) (the principal),
  [ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md) (registration),
  [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)
  (setting merges),
  [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md) (the guarded client),
  [ADR-1380](1380-logout-ends-the-session-found-in-server-state-confirmed-when-unsure-and-notifies-relying-parties.md) (end session) and
  [ADR-1101](1101-token-exchange-issues-delegation-and-impersonation-tokens-from-tokens-the-tenant-issued.md) (token exchange).
