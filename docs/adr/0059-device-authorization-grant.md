# 59. Device Authorization Grant (RFC 8628) over the persisted-grant store

- **Status:** Proposed
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

<see href="https://datatracker.ietf.org/doc/html/rfc8628">RFC 8628</see> (the OAuth 2.0 Device Authorization Grant)
lets a browserless or input-constrained client — a smart TV, a CLI, an IoT device — obtain tokens by having the user
authorize on a secondary device (a phone or laptop). The flow has three network actors and two short-lived secrets:

- The device `POST`s to a **device authorization endpoint** and receives a `device_code` (machine secret), a short
  human-typable `user_code`, a `verification_uri`, an `expires_in`, and a polling `interval`.
- The user visits the **verification URI** on a second device, enters the `user_code`, authenticates, and approves or
  denies.
- Meanwhile the device **polls the token endpoint** with `grant_type=urn:ietf:params:oauth:grant-type:device_code` and
  its `device_code`, receiving `authorization_pending` / `slow_down` until the user acts, then either tokens or
  `access_denied` / `expired_token`.

This server already has every seam the flow needs: grant handlers plug into the token endpoint through
`ITokenGrantHandler` and are dispatched by `grant_type` ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)),
endpoints self-register and self-advertise in discovery ([ADR-0009](0009-endpoint-families-own-their-route-group.md)),
and `IPersistedGrantService` persists "any type of grant and its payload" as a tenant-scoped, hash-keyed, expiring,
status-bearing `PersistedGrant` envelope. The grant-type constant (`OpenIdConstants.GrantTypes.DeviceCode`) and the
`device_code` / `user_code` parameter names already exist.

The one thing a device authorization request is that an issued grant (authorization code, refresh token) is not: a
**pending request with a mutable approval state** that is read by two different keys. The device polls by `device_code`;
the user is only ever holding a `user_code`. And the record transitions `pending → approved | denied` out of band
between those reads, so its payload is mutated after creation — something no existing grant does.

## Decision

**Model a pending device authorization as a `PersistedGrant` payload, keyed by `device_code`, with a companion
pointer grant keyed by `user_code`; add one general-purpose `UpdatePayloadAsync<TPayload>` to `IPersistedGrantService`
for the out-of-band approval transition. Implement the grant as a `DefaultDeviceCodeGrantHandler : ITokenGrantHandler`
plus two self-advertising endpoints (device authorization + user verification), all registered by an
`AddDeviceAuthorizationGrant()` endpoint-group extension.** No new persistence table, store, or EF migration is
introduced — the device flow rides the existing grant store, which is the cohesive home for "a grant and its payload."

Concretely:

- **Persistence — two rows, one store.** The device authorization request is a `PersistedGrant` of type
  `PersistedGrantTypes.DeviceCode`, keyed by the `device_code`, whose JSON payload (`DeviceCodeGrant`) carries the
  client id, requested scopes/resources, the approval `Status` (`Pending` / `Approved` / `Denied`), the approving
  `SubjectAuthentication` once approved, and the `LastPolledWhen` timestamp used for `slow_down`. A second
  `PersistedGrant` of type `PersistedGrantTypes.DeviceUserCode`, keyed by the `user_code`, is a thin **pointer** whose
  payload is the `device_code`; it is how the verification endpoint — which only has the `user_code` — finds the request.
  Both rows share the device-code lifetime and are tenant-scoped and hash-keyed exactly like every other grant. This
  reuses the entire grant store, its EF entity/configuration, concurrency handling, and tenant scoping unchanged.
- **Mutable approval via `UpdatePayloadAsync`.** `IPersistedGrantService` gains a generic
  `UpdatePayloadAsync<TPayload>(context, grantId, payload, ct)` that rewrites the payload of an **active** grant (the
  envelope's `UpdateAsync` already backs `ConsumeOnceOrDefault`). The verification endpoint uses it to write the
  approve/deny decision and the approving subject; the token handler uses it to stamp `LastPolledWhen` on each poll.
  This is a neutral, reusable capability, not device-specific surface on the service.
- **Terminal success is consume-once.** When the device polls and the request is `Approved`, the handler claims it with
  `ConsumeOnceOrDefault` so a `device_code` is redeemed for tokens exactly once; a second poll after success sees it
  consumed and fails `invalid_grant`. The `user_code` pointer is revoked at approval time so a code cannot be entered
  twice.
- **`user_code` generation.** User codes are drawn from a transcription-resistant base-20 alphabet
  (`BCDFGHJKLMNPQRSTVWXZ` — no vowels, no digits that collide with letters) in a settings-controlled length, formatted
  for display (e.g. `WDJB-MJHT`) but normalized (case-folded, separators stripped) before hashing and lookup so the
  user may type it in any case or spacing. The `device_code` is a high-entropy opaque server id like every other grant
  key.
- **Polling discipline.** The handler enforces the `interval`: a poll that arrives sooner than `interval` since
  `LastPolledWhen` returns `slow_down` (RFC 8628 §3.5) without advancing state; otherwise it returns
  `authorization_pending` while `Pending`, `access_denied` when `Denied`, `expired_token` when past expiry, and tokens
  when `Approved`.
- **Verification is a first-class interaction, not a new auth stack.** The user verification endpoint reuses the same
  subject-authentication and consent seam the authorization endpoint uses; approving a device request is the same
  "authenticate the user, capture a consent decision" operation, just keyed by `user_code` instead of an authorization
  request. (Consent persistence itself remains backlog **F2**; the device flow consumes whatever consent decision the
  interaction produces.)
- **New error codes.** `OpenIdConstants.ErrorCodes` gains `authorization_pending`, `slow_down`, and `expired_token`
  (`access_denied` already exists), returned as `400` OAuth error responses from the token handler.
- **Settings & discovery.** New settings — `device_code_lifetime`, `device_code_polling_interval`,
  `user_code_length` — follow the descriptor/merge conventions ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md),
  [ADR-0038](0038-setting-merge-lattice-duals.md)). The device authorization endpoint advertises itself as
  `device_authorization_endpoint`, and `urn:ietf:params:oauth:grant-type:device_code` joins the default
  `grant_types_supported`, so both appear in `.well-known/openid-configuration` automatically.

## Options considered

- **A dedicated pending-authorization store (new data contract, store interface, EF entity, configuration, migration).**
  Rejected. It is the textbook model for "a request with a mutable state machine read by two keys," and a future CIBA
  grant ([ADR-0060](0060-ciba-grant.md)) shares that shape — but `IPersistedGrantService` already exists precisely to
  "persist any type of grant and their payloads," and the device request _is_ a grant payload. A parallel store would
  duplicate the tenant-scoping, hash-keying, concurrency, and EF plumbing the grant store already provides, and force an
  out-of-band migration ([ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md)) for a record the
  existing schema already fits. Reusing the grant store is the cohesive choice; the companion pointer row is a standard
  secondary-index technique, not a workaround.
- **A single row with both keys (store the `user_code` hash as a second indexed column on the grant envelope).**
  Rejected. It changes the shared grant entity/schema and the store's lookup contract for one grant type's benefit,
  pushing device-flow specifics into the generic persistence layer. The pointer row keeps the device-flow shape inside
  the device-flow payload and leaves the envelope untouched.
- **Embed the `device_code` inside the `user_code` and skip the pointer row.** Rejected. The `user_code` must stay
  short and human-typable; it cannot also carry a high-entropy `device_code`. The two secrets have independent entropy
  and lifetime semantics and are kept as independent records.
- **A bespoke poll rate-limiter (cache/row per client).** Rejected in favor of stamping `LastPolledWhen` on the request
  payload: the state the `slow_down` decision needs already travels with the request, is tenant-scoped and expiring for
  free, and needs no second subsystem.

## Consequences

- A new input-constrained-client flow works end to end with **no schema change**: the device authorization endpoint,
  the user verification interaction, and the `device_code` token path all ride the existing grant store, discovery, and
  settings machinery.
- `IPersistedGrantService` gains a general `UpdatePayloadAsync<TPayload>` that any future grant with a mutable payload
  (CIBA, consent upgrades) can reuse — the capability is intentionally not device-specific.
- The grant store now holds two short-lived rows per device flow (the request and its `user_code` pointer); both expire
  with the device-code lifetime and are cleaned up by the same retention that prunes expired grants, so the extra row is
  bounded and self-healing.
- Because the request's approval state lives in its payload, the verification endpoint and the polling token endpoint
  are fully decoupled — they never share memory, only the persisted record — which is what makes the "authorize on a
  second device" topology work across nodes.
- CIBA ([ADR-0060](0060-ciba-grant.md)) reuses this exact pattern (a pending request persisted as a grant payload,
  approved out of band, polled for completion), so the `UpdatePayloadAsync` capability and the pending-request modelling
  are shared rather than reinvented.
- A "you chose wrong" smell: if retention of short-lived pending rows ever becomes a hot-path cost, or a third grant
  needs the same shape with materially different columns, that is the signal to promote pending authorizations into
  their own store after all — the payload-first model keeps that migration localized.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the grant-handler/mediator shape the device
  grant follows.
- [ADR-0009](0009-endpoint-families-own-their-route-group.md) — endpoint families own their routes and discovery
  advertisement.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md), [ADR-0038](0038-setting-merge-lattice-duals.md) —
  the settings descriptor and merge conventions the new settings follow.
- [ADR-0028](0028-token-revocation-refresh-tokens-only.md) — the `PersistedGrant` revoke/consume semantics reused here.
- [ADR-0041](0041-host-owned-ef-migrations-deliberate-outside-development.md) — why avoiding a new migration matters.
- [ADR-0060](0060-ciba-grant.md) — the CIBA grant that reuses this pending-authorization pattern.
- <see href="https://datatracker.ietf.org/doc/html/rfc8628">RFC 8628</see> — the OAuth 2.0 Device Authorization Grant.
