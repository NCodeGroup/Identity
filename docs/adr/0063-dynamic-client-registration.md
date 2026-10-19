# 63. Dynamic Client Registration (RFC 7591/7592) over the persisted-grant store

- **Status:** Accepted (metadata storage and the registration `scope` amended by
  [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md): informational metadata are registered client settings, the
  requested `scope` is not stored, and the response reports the scopes the client may request)
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

<see href="https://datatracker.ietf.org/doc/html/rfc7591">RFC 7591</see> (OAuth 2.0 Dynamic Client Registration) lets a
client register itself with the authorization server by `POST`ing a JSON client-metadata document to a
`registration_endpoint` and receiving an assigned `client_id`, an optional `client_secret`, and — together with
<see href="https://datatracker.ietf.org/doc/html/rfc7592">RFC 7592</see> (the registration management protocol) — a
`registration_access_token` and a `registration_client_uri` the client then uses to `GET` / `PUT` / `DELETE` its own
registration. It is a distinct OpenID Provider certification profile (**Dynamic OP**), and it removes manual client
seeding: without it the only way to create a client is the management API or provisioning/seeding.

The server already has every seam the feature needs. Clients are persisted as a `PersistedClient` (a disabled flag, a
JSON settings document, and a secret collection) through `IClientStore`; the management API creates them exactly this
way. Client secrets are generated server-side and revealed once by `ISecretGenerator`
([ADR-0050](0050-secret-material-revealed-once-on-create.md)). Protocol endpoints self-register and self-advertise in
discovery ([ADR-0009](0009-endpoint-families-own-their-route-group.md)). Settings cascade server → tenant → client
through a merge lattice ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md),
[ADR-0038](0038-setting-merge-lattice-duals.md)). And `IPersistedGrantService` persists "any type of grant and its
payload" as a tenant-scoped, **hash-keyed**, optionally-expiring, status-bearing envelope — the same machinery the
authorization-code, refresh-token, and device-code flows ride.

Two design questions are genuinely open and deserve a recorded decision:

1. **Who may register?** RFC 7591 permits both open (anonymous) registration and protected registration gated by an
   _initial access token_. Open registration is an abuse/DoS vector; the Dynamic OP conformance profile, by contrast,
   wants anonymous registration so its harness can provision clients unattended.
2. **How is a per-client `registration_access_token` stored and authenticated** for the RFC 7592 management calls, which
   are **not** standard client authentication — the bearer is a token bound to one specific client, long-lived, and
   presented without a `client_id`.

## Decision

**Implement the registration and management endpoints as one self-advertising OpenID endpoint family in the
authentication library, project RFC 7591 client metadata onto the existing `PersistedClient` settings document, reveal
the `client_secret` through the existing one-time secret generator, and model both the `registration_access_token`
(RFC 7592) and the `initial_access_token` (RFC 7591) as persisted-grant payloads.** No new persistence table, store, or
EF migration is introduced — dynamic registration rides the client store and the grant store, which are the cohesive
homes for "a client" and "a token bound to a client."

Concretely:

- **Two routes, one family.** `POST /oauth2/register` is the registration endpoint; it advertises itself as
  `registration_endpoint` in `.well-known/openid-configuration`. `GET` / `PUT` / `DELETE /oauth2/register/{client_id}`
  is the per-client management endpoint (RFC 7592); it is **not** discoverable (the client reaches it through the
  `registration_client_uri` returned at registration).
- **Metadata maps onto the settings document.** Functional metadata is stored under the client settings the server
  already resolves at runtime — `redirect_uris`, `grant_types_supported` (from `grant_types`),
  `response_types_supported` (from `response_types`), and `scopes_supported` (from the space-delimited `scope`).
  Informational metadata that has no runtime setting (`client_name`, `client_uri`, `logo_uri`, `contacts`, `tos_uri`,
  `policy_uri`, `jwks_uri`, `jwks`, `software_id`, `software_version`, and the chosen `token_endpoint_auth_method`) is
  stored verbatim in the same JSON document so a registration round-trips through the management endpoint. There is no
  second storage shape: the settings document is the single home, and a `GET` reconstructs the client-information
  response from it.
- **The `client_secret` is the existing one-time secret.** A confidential client (any `token_endpoint_auth_method`
  other than `none`) receives a server-generated symmetric secret via `ISecretGenerator`; its one-time material is the
  `client_secret` returned in the registration response and never again. This is the same secret a client presents for
  `client_secret_basic` / `client_secret_post`, so client authentication works unchanged
  ([ADR-0050](0050-secret-material-revealed-once-on-create.md)). The secret is stored with a non-expiring lifetime and
  reported as `client_secret_expires_at: 0` ("does not expire"). A public client (`none`) receives no secret.
- **Both DCR token types are persisted grants.** The `registration_access_token` is a `PersistedGrant` keyed by the
  token value whose payload binds it to its `client_id`, written with **no expiration**; an RFC 7592 call authenticates
  by looking the bearer up as that grant and confirming its payload `client_id` matches the `{client_id}` in the route.
  Because the grant store keys on a **salted hash of the token**, the plaintext token is never stored — the same
  posture the authorization-code and device-code grants already have. The RFC 7591 _initial access token_ is modeled
  the same way (`initial_access_token` grant); it is minted through the management API (see Consequences).
- **Authorization is configurable, protected by default.** A new discoverable-as-non-standard setting
  `registration_endpoint_allow_anonymous` (default `false`, merged with the boolean **meet** `And` so a parent that
  disallows anonymous registration cannot be loosened downward) governs the endpoint. When `false`, the caller must
  present a valid initial access token as a Bearer credential, validated through an injectable
  `IInitialAccessTokenValidator` seam — deployment-specific, because "what counts as a valid initial access token" is a
  deployment choice. The default validator treats it as an `initial_access_token` persisted grant; a host may replace
  the seam to validate, for example, an access token carrying a required scope. When `true`, registration is anonymous
  (the posture the Dynamic OP conformance harness needs).
- **Metadata validation.** `token_endpoint_auth_method` is constrained to the supported set
  (`none` / `client_secret_basic` / `client_secret_post`); `grant_types` defaults to `authorization_code` and
  `response_types` to `code` per RFC 7591; a grant type that uses a redirect (`authorization_code`, `implicit`) requires
  at least one `redirect_uris` value; and each redirect URI must be a valid absolute URI without a fragment. Failures
  return the RFC 7591 error codes `invalid_client_metadata` / `invalid_redirect_uri`.
- **Delete removes dependents first.** A client cannot be removed while it owns secrets, so `DELETE` removes the
  client's secrets, then the client, then revokes the `registration_access_token` grant.

## Options considered

- **Put registration on the management API instead of a protocol endpoint.** Rejected. The management API is an
  operator surface guarded by its own authorization model ([ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md));
  RFC 7591/7592 are _protocol_ endpoints a client calls for itself, advertised in discovery, with their own
  initial-access-token / registration-access-token authentication. They belong with the OAuth endpoints, and placing
  them there is what makes the Dynamic OP discovery advertisement and conformance profile work.
- **A dedicated store (or table/column) for registration access tokens.** Rejected. A per-client bearer token bound to
  a client, hash-keyed, optionally expiring, and revocable **is** a `PersistedGrant`; a parallel store would duplicate
  the tenant-scoping, hash-keying, and EF plumbing the grant store already provides and force an out-of-band migration
  ([ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md)) for a record the existing schema fits.
- **A separate blob for the registered metadata document, kept beside the runtime settings.** Rejected. It would
  duplicate the functional fields (which must also be runtime settings) and create two sources of truth. Storing the
  informational fields inside the one settings document the server already persists keeps a single home and a symmetric
  read-back, at the cost of a few inert keys the runtime ignores.
- **Open registration by default (or hard-coded).** Rejected. Correct-by-default forbids shipping an anonymous
  write endpoint as the baseline; a deployment opts into anonymous registration deliberately via a setting, and even
  then the merge lattice prevents a child scope from re-enabling what a parent disallowed.
- **Hash the registration/initial access tokens ourselves before storing.** Unnecessary. The grant store already keys
  on a salted hash of the grant key, so persisting the token as the grant key yields hash-at-rest for free and matches
  every other grant.

## Consequences

- Dynamic client registration and management work end to end with **no schema change**: registration writes a
  `PersistedClient` through the existing client store, the `client_secret` is the existing one-time secret, and both
  DCR token types ride the existing grant store, discovery, and settings machinery.
- The server gains the **Dynamic OP** certification profile and removes manual client seeding; the conformance harness
  can provision clients unattended (with `registration_endpoint_allow_anonymous` enabled) and run plan modules in
  parallel — the prerequisite the conformance plan records against this gap
  ([`docs/conformance-testing.md`](../conformance-testing.md)).
- `IInitialAccessTokenValidator` is the deployment seam for protected registration; a host plugs in its own policy
  without touching the endpoint. Initial access tokens are **provisioned through the management API**: a tenant-scoped
  `POST api/tenants/{tenantId}/grants/initial-access-tokens` mints a hash-keyed `initial_access_token` grant,
  returning the one-time plaintext token (revealed once, ADR-0050 posture) together with the grant id for later
  revocation through the grant management endpoint, and records an `InitialAccessTokenCreatedAuditEvent`. A deployment
  that wants protected registration either mints tokens this way or replaces the validator seam (for example, to accept
  an access token carrying a required scope); it may also enable anonymous registration. Minting reuses the shared
  `IPersistedGrantService.AddAsync`, which now returns the server-generated grant id so the management surface can hand
  it back for revocation.
- Informational metadata are registered client settings with their RFC 7591 names, so the settings document holds no
  untyped keys ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)); the management endpoint reads them back.
- RFC 7592 responses omit the `registration_access_token` on `GET` / `PUT` because the server stores only its hash and
  cannot reproduce the plaintext; the client already holds its token. Rotation on update is intentionally not performed
  (RFC 7592 makes it a MAY) and remains available as a future addition.
- A "you chose wrong" smell: if registered-metadata fields ever need first-class validation, indexing, or their own
  lifecycle distinct from runtime settings, that is the signal to promote them into a dedicated client-metadata
  contract rather than inert settings keys.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service/strategy/mediator extensibility
  model the endpoint family and the validator seam follow.
- [ADR-0009](0009-endpoint-families-own-their-route-group.md) — endpoint families own their routes and discovery
  advertisement.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md), [ADR-0038](0038-setting-merge-lattice-duals.md) —
  the settings descriptor and merge-lattice conventions the `registration_endpoint_allow_anonymous` setting follows.
- [ADR-0050](0050-secret-material-revealed-once-on-create.md) — the one-time secret reveal reused for the
  `client_secret`.
- [ADR-0059](0059-device-authorization-grant.md) — the persisted-grant-payload pattern reused for the DCR token types.
- [ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md) — why avoiding a new migration matters.
- <see href="https://datatracker.ietf.org/doc/html/rfc7591">RFC 7591</see> — OAuth 2.0 Dynamic Client Registration.
- <see href="https://datatracker.ietf.org/doc/html/rfc7592">RFC 7592</see> — OAuth 2.0 Dynamic Client Registration
  Management.
