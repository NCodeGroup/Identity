# 1059. The device authorization grant is a pending request in the grant store, approved out of band

- **Status:** Accepted
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

[RFC 8628](https://datatracker.ietf.org/doc/html/rfc8628) (the OAuth 2.0 Device Authorization Grant) lets a browserless
or input-constrained client, such as a smart TV, a CLI or an IoT device, obtain tokens by having the user authorize on a
secondary device. The flow has three network actors and two short-lived secrets:

- The device `POST`s to a device authorization endpoint and receives a `device_code` (the machine secret), a short
  human-typable `user_code`, a `verification_uri`, an `expires_in` and a polling `interval`.
- The user visits the verification URI on a second device, enters the `user_code`, authenticates, and approves or
  denies.
- Meanwhile the device polls the token endpoint with `grant_type=urn:ietf:params:oauth:grant-type:device_code` and its
  `device_code`, receiving `authorization_pending` or `slow_down` until the user acts, then either tokens or
  `access_denied` or `expired_token`.

The server already has every seam the flow needs. Grant handlers plug into the token endpoint through
`ITokenGrantHandler` and are dispatched by `grant_type`
([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)). Endpoints
self-register and self-advertise in discovery
([ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md)).
`IPersistedGrantService` persists any type of grant and its payload as a tenant-scoped, hash-keyed, expiring,
status-bearing `PersistedGrant` envelope.

A device authorization request differs from an issued grant such as an authorization code or a refresh token in one
way: it is a pending request with a mutable approval state that is read by two different keys. The device polls by
`device_code`, while the user holds only a `user_code`. The record moves from pending to approved or denied out of band
between those reads, so its payload changes after creation.

## Decision

**A pending device authorization is a `PersistedGrant` payload keyed by the `device_code`, with a companion pointer grant
keyed by the `user_code`; the user approves or denies it out of band, and the device's poll of the token endpoint
completes it.** No table, store or migration of its own exists: the flow rides the grant store, the cohesive home for a
grant and its payload.

RFC 8628 is served by default in the OpenID endpoint group:

- the device authorization endpoint `/oauth2/device_authorization` (`DefaultDeviceAuthorizationEndpointHandler`),
  advertised in discovery as `device_authorization_endpoint`;
- the verification endpoint `/oauth2/device` (`DefaultDeviceVerificationEndpointHandler`);
- `DefaultDeviceCodeGrantHandler`, the `ITokenGrantHandler` for `urn:ietf:params:oauth:grant-type:device_code`.

**Persistence: two rows, one store.**

- The request is a `PersistedGrant` of type `device_code`, keyed by the high-entropy `device_code`. Its `DeviceCodeGrant`
  payload holds the client id, the scopes and resources, the `Status` (`DeviceAuthorizationStatus`: `Pending`,
  `Approved` or `Denied`), the approving `SubjectAuthentication`, `LastPolledWhen` and a `[JsonExtensionData]` bag.
- A pointer grant of type `device_user_code`, keyed by the `user_code`, holds `DeviceUserCodeGrant(DeviceCode)`. It is how
  the verification endpoint, which has only the `user_code`, finds the request.
- Both rows share the device-code lifetime and are tenant-scoped and hash-keyed like every other grant. They ride the
  grant store with no table of their own.

**User codes.** The replaceable `IUserCodeGenerator` (`DefaultUserCodeGenerator`) draws the `user_code` from the
transcription-resistant alphabet `BCDFGHJKLMNPQRSTVWXZ`, which has no vowels, at the length `user_code_length` sets. It
displays the code grouped (`WDJB-MJHT`) and normalizes it, case-folded with separators stripped, before hashing and
lookup, so the user may type it in any case or spacing.

**Verification.** The verification endpoint authenticates the end user through `AuthenticateSubjectCommand` and validates
the session of the cookie
([ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md)). `GET` answers a JSON prompt (client,
scopes, user code and status) for the host to present. `POST` decides, with `action=approve` or `action=deny`. A
decision changes only a `Pending` request, through the read-modify-write `UpdatePayloadAsync`
([ADR-1222](1222-a-grants-payload-is-changed-by-a-read-modify-write.md)), and revokes the `user_code` pointer with
`SetRevokedOnceAsync`, so a code is entered once.

**Polling.**

- A poll within `device_code_polling_interval` of `LastPolledWhen` is answered `slow_down`.
- `LastPolledWhen` is recorded only on a `Pending` request.
- Otherwise the answer is `authorization_pending`, `access_denied` or `expired_token`, each a `400` OAuth error, or
  tokens.

**Redemption.** A device code is redeemed only by its client; any other client gets `invalid_grant`, checked right after
the lookup. The subject is validated before the spend
([ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md)). `ConsumeOnceOrDefault` claims an
approved request exactly once, so a second poll after success fails with `invalid_grant`.

**Settings and discovery.** `device_code_lifetime`, `device_code_polling_interval` and `user_code_length` follow the
descriptor and merge conventions
([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)).
The device-code grant type is in the default `grant_types_supported`, so discovery lists it.

## Options considered

- **A dedicated pending-authorization store (data contract, store interface, EF entity, configuration and migration).**
  Rejected. It is the textbook model for a request with a mutable state machine read by two keys, but
  `IPersistedGrantService` exists to persist any type of grant and its payload, and the device request is a grant
  payload. A parallel store would duplicate the tenant scoping, hash keying, concurrency handling and EF plumbing of the
  grant store, and would force an out-of-band migration
  ([ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md))
  for a record the existing schema already fits.
- **One row with a second indexed key column for the `user_code` hash.** Rejected. It changes the shared grant entity,
  the schema and the store's lookup contract for one grant type's benefit, and pushes device-flow specifics into the
  generic persistence layer. The pointer row keeps the device-flow shape inside the device-flow payload and leaves the
  envelope untouched.
- **Embedding the `device_code` in the `user_code`, with no pointer row.** Rejected. The `user_code` must stay short and
  human-typable, so it cannot also carry a high-entropy `device_code`. The two secrets differ in entropy and are kept as
  independent records.
- **A separate poll rate-limiter, a cache or row per client.** Rejected. The state that the `slow_down` decision needs,
  `LastPolledWhen`, travels with the request, is tenant-scoped and expiring for free, and needs no second subsystem.
- **A pending request in the grant store, keyed by `device_code` with a `user_code` pointer.** Chosen. It reuses the whole
  grant store, and the pointer row is a standard secondary-index technique.

## Consequences

- The input-constrained-client flow works end to end with no schema change: the device authorization endpoint, the
  verification endpoint and the `device_code` token path all ride the grant store, discovery and the settings machinery.
- The verification and polling endpoints share only the persisted record, never memory, so the "authorize on a second
  device" topology works across nodes.
- The grant store holds two short-lived rows per device flow. Both expire with the device-code lifetime, and the grant
  prune removes the ended rows
  ([ADR-1218](1218-ended-grants-are-removed-after-a-retention-period.md)), so the extra row is bounded.
- CIBA reuses the pattern of a pending request persisted as a grant payload, approved out of band and polled for
  completion ([ADR-1085](1085-ciba-grant.md)).
- Smell: if retention of short-lived pending rows becomes a hot-path cost, or a third grant needs the same shape with
  materially different columns, promote pending authorizations into a store of their own. The payload-first model keeps
  that change local.

## References

- [RFC 8628](https://datatracker.ietf.org/doc/html/rfc8628), the OAuth 2.0 Device Authorization Grant.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md), the
  grant-handler and mediator shape the device grant follows.
- [ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md),
  endpoint families own their routes and discovery advertisement.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md),
  the settings descriptor and merge conventions.
- [ADR-1028](1028-the-revocation-endpoint-revokes-refresh-tokens-and-their-families-and-always-answers-200.md), the
  `PersistedGrant` revoke and consume semantics.
- [ADR-1073](1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md),
  why avoiding a new migration matters.
- [ADR-1222](1222-a-grants-payload-is-changed-by-a-read-modify-write.md), the read-modify-write of a grant's payload.
- [ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md), subject validation and the session
  check.
- [ADR-1218](1218-ended-grants-are-removed-after-a-retention-period.md), the prune of ended grants.
- [ADR-1085](1085-ciba-grant.md), the CIBA grant that reuses this pattern.
