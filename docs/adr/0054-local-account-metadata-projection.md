# 54. Local-account metadata projects into tokens through the subject-claims pipeline

- **Status:** Proposed (metadata home and stamping seam amended by [ADR-0055](0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md))
- **Date:** 2026-10-09
- **Deciders:** NCode Group

## Amendment (2026-10-09): metadata is a principal-level concept, resolved at issuance

This ADR anchors `ProfileMetadata` / `SystemMetadata` on `LocalAccount` and stamps them in the ROPC subject builder
(`DefaultCreatePasswordGrantSubjectHandler`). [ADR-0055](0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md)
supersedes that placement: because `LocalAccount` exists only for local connections and the ROPC builder is the only
stamping site, external-IdP connections and the interactive/refresh flows never receive the bags. ADR-0055 inlines the
bags onto the `FederatedPrincipal` actor and resolves them fresh at issuance through a pluggable
`IPrincipalMetadataProvider` keyed by the resolved `PrincipalId`, so every flow and connection kind projects uniformly.
**The projection half of this ADR — whole-bag nesting, the reserved claim-type constants, the six per-destination opt-in
settings, and the subject-claims pipeline — is unchanged;** only where the metadata lives and when it is resolved
change (nothing is stamped on the subject at authentication).

## Context

A `LocalAccount` ([ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md)) carries three kinds of
subject data that the persisted `PersistedLocalAccount` round-trips but that, until now, never reached a token or the
UserInfo response:

- **`Claims`** — structured, individually-addressable claim assertions (`given_name`, `email`, …), stored as
  `type`/`value` rows.
- **`ProfileMetadata`** — a free-form JSON object the **account owner** may read and update.
- **`SystemMetadata`** — a free-form JSON object managed **only by the server or an administrator**, never end-user
  writable, which may carry authorization-relevant data.

This mirrors the model the pre-release Auth0-parity posture targets ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)):
normalized profile claims, `user_metadata`, and `app_metadata`. The two metadata bags are the same storage shape and
differ on a single axis — **writability / trust** (owner vs server); `Claims` differ on a second axis —
**addressability** (individually named vs opaque bag). Those two axes are orthogonal and irreducible: collapsing claims
into a bag loses the per-type addressability that scopes, the `claims` request parameter, and authorization require;
spreading a bag into individual claims loses nesting and — for the owner-writable bag — is a privilege-escalation
hazard. The three sources are therefore kept distinct at storage and unified only at the runtime `Claim` boundary.

The open question was how to surface the two metadata bags, given the strong Auth0-parity constraint that metadata is
_opt-in_ and _collision-proof_, that the owner/server trust boundary must survive into the wire format, and that hosts
will eventually want arbitrary, code-driven claim shaping (Auth0 "Actions").

## Decision

**Each metadata bag is projected as a single JSON-object claim under a reserved claim type, carried on the subject
principal and emitted per-destination behind opt-in settings, through the existing subject-claims handler pipeline —
which is the first-class extension seam for custom claim shaping.**

### Whole-bag nesting, never flattening (the default)

A bag is emitted as one claim — `profile_metadata` / `system_metadata` (the reserved claim types on
`AccountConstants`) — whose value is the entire JSON object (`JsonClaimValueTypes.Json`, so it re-serializes as a nested
object in the JWT and as a nested object in UserInfo). The default never spreads a bag's keys into top-level claims,
for three reasons:

- **Security (decisive).** `ProfileMetadata` is end-user-writable. Flattening its keys onto the top-level claim set
  would let a user inject arbitrary claims — `{"role":"admin"}`, `{"email_verified":true}` — that a resource server or
  relying party may trust. Nesting under a reserved claim contains that: a consumer always knows `profile_metadata.*`
  is user-asserted and can never mistake it for a root claim. (A stray `SystemMetadata` key such as `sub`/`aud`/`iss`
  would likewise shadow a registered claim.)
- **Provenance.** Two delimited objects keep the owner-vs-server trust boundary visible on the wire: a verifier may
  treat `system_metadata.*` as server-vouched and `profile_metadata.*` as user-asserted. Flattening erases which bag a
  value came from — exactly the distinction authorization must not lose.
- **Fidelity.** Metadata is arbitrary JSON (nested objects, arrays, typed scalars); a `Claim` is `type`+`string`.
  Emitting the bag whole round-trips losslessly, whereas any flattening scheme invents a lossy convention.

Per-key flattening — Auth0's `api.idToken.setCustomClaim` pattern — is a deliberate, namespaced, collision-aware act,
and belongs in a custom pipeline handler (below), **not** in the default. Any such handler must namespace its claims and
must not surface owner-writable keys as bare top-level claims.

### Carry on the principal, then filter (same model as ordinary claims)

The subject builder stamps each non-empty bag onto the subject `ClaimsPrincipal` **unconditionally**, exactly as it
stamps `account.Claims`. The principal is the only subject representation that survives every grant flow (the
`LocalAccount` is gone by the time a refresh-token or authorization-code request issues a token), so the carrier must be
populated for projection to be possible at all; gating it would break cross-flow projection. Nothing is client-visible
at this point — the principal is server-internal (and already persists the full claim set in the session/grant, so the
bags add no new category of stored data).

What actually leaves the server is decided one layer later, by the projection handlers, behind six opt-in settings
(default `false`): `send_{profile,system}_metadata_in_{access_token,id_token,user_info}`. This is the identical
**carry-then-filter** shape the standard claims already use — the access-token handler copies only `idp`/`sub`/
`auth_time`/`amr`, the id-token handler is scope-driven — the six flags are simply metadata's version of that filter.
Opt-in-by-default matches Auth0 (nothing in a token until explicitly surfaced) and avoids leaking PII or authorization
data by accident.

### The pipeline is the Actions-equivalent extension seam

Projection is three ordinary `ICommandHandler<…>` handlers on the existing
`Get{AccessToken,IdToken}SubjectClaimsCommand` / `GetUserInfoClaimsCommand` commands — the same priority-ordered
mediator pipeline every claim already flows through ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)).
A host adds an `ICommandHandler` to read anything (including the structured bags, via the public
`ClaimsPrincipal.TryGetMetadata` helper) and emit, transform, or drop arbitrary claims. That pipeline **is** the NCode
equivalent of Auth0 Actions; the built-in whole-bag projection is just its off-the-shelf default. A richer first-class
Actions surface (a strongly-typed shaping context, fresh-metadata reload, ordering UI) layers on this same pipeline
additively and is recorded as pending work in [`BACKLOG.md`](../../BACKLOG.md).

### Reserved claim names are constants, not settings

The claim type is primarily a **correlation key** tying together four sites (the subject builder that stamps it, the
access- and id-token handlers, and `TryGetMetadata`). A constant guarantees all four agree. Promoting it to a setting
would invite a silent-failure footgun, because those sites read settings at different scopes — the builder and UserInfo
handler at **tenant** scope, the access/id handlers at **client** scope — so a client-overridable name would let the
producer stamp under one name while a consumer looks for another and emits nothing. Custom claim names are, per
Auth0-parity, an Actions/handler concern, not a core dial. (A future tenant-scoped name setting, resolved identically
everywhere, remains an additive option.)

### The write-path invariant lives at the store

Projection is a read path; it cannot enforce that `SystemMetadata` is server-only. That invariant —
`SystemMetadata` is never end-user-writable — must be enforced by the management/provisioning layer, the same way Auth0
protects `app_metadata`. It is called out here and tracked as management work (group `A`) in [`BACKLOG.md`](../../BACKLOG.md).

## Options considered

- **Flatten each bag's keys into top-level claims (chosen against).** Rejected on security (owner-writable claim
  injection), provenance loss (owner vs server), and fidelity (nested JSON). Per-key flattening is available as an
  explicit, namespaced custom handler instead.
- **Gate the subject-build stamping too, for symmetry (chosen against).** The builder is tenant-scoped and the
  projection flags are per-client, and the principal is shared across clients/flows, so there is no clean gate there;
  gating would also starve cross-flow projection, which only has the persisted principal.
- **Make the reserved claim names configurable settings (chosen against, for now).** A client-overridable name is a
  silent-correlation footgun across the tenant/client scope split. A tenant-scoped name setting is a clean additive
  option if demand appears; custom names are otherwise a handler concern.
- **Normalize metadata → claims in the source adapter and drop the typed bags from `LocalAccount` (chosen against).**
  Considered to remove the "which source" ambiguity, but it pre-serializes the structured data a custom subject-builder
  would want and buries the owner/server split; the structured bags stay on `LocalAccount`, and the single assembly
  seam is the subject builder.
- **A single setting per bag listing destinations (chosen against).** More "data-driven," but introduces a new value
  shape and merge semantics; six booleans match the established `send_id_claims_in_access_token` pattern.

## Consequences

- `LocalAccount` carries `ProfileMetadata` / `SystemMetadata` as typed `JsonElement` bags; the EF reference source maps
  them from storage, and the ASP.NET Core Identity source leaves them empty (it has no metadata concept).
- Six new per-destination settings (default `false`) join the surface, plus two reserved claim-type constants and a
  public `ClaimsPrincipal.TryGetMetadata` helper; three new pipeline handlers perform the default projection.
- Metadata is opt-in and, when enabled, emitted as a nested object that cannot collide with a registered claim and
  preserves the owner/server trust boundary on the wire.
- Custom, Auth0-Actions-style claim shaping is a registered `ICommandHandler` away, with structured access to the bags;
  a first-class Actions surface is deferred.
- The `SystemMetadata` write-path invariant (never end-user-writable) is the management layer's responsibility, tracked
  in the backlog.

## References

- [ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md) — the `LocalAccount` payload and
  `ILocalAccountSource` seam whose metadata this ADR projects.
- [ADR-0035](0035-federated-principals-and-identity-resolution.md) — the subject/principal model the stamped `sub` and
  carried claims resolve through.
- [ADR-0030](0030-userinfo-endpoint-and-subject-authentication-seam.md) — the UserInfo endpoint and claims-enricher
  seam one of the projection handlers extends.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the mediator pipeline that is the claim-shaping
  (Actions-equivalent) extension seam.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the Auth0-parity-plus posture: database-connection
  metadata (`user_metadata` / `app_metadata`) surfaced as opt-in, namespaced-or-nested claims.
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — why this reasoning lives in an ADR rather than
  chat or agent memory.
