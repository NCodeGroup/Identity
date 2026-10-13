# 85. CIBA grant (OpenID Connect Client-Initiated Backchannel Authentication) over the persisted-grant store

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

[OpenID Connect CIBA](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html) lets a
**consumption device** (a point-of-sale terminal, a call-center agent's console, a server) start authentication for a
user who authenticates on a **separate authentication device** (the user's phone), with no browser redirect on the
consumption device. It is the decoupled sibling of the Device Authorization Grant ([ADR-0059](0059-device-authorization-grant.md)):

- The client `POST`s to a **backchannel authentication endpoint** with the requested `scope`, one of
  `login_hint` / `login_hint_token` / `id_token_hint` identifying the user, and (for non-poll delivery) a
  `client_notification_token`. It receives an `auth_req_id`, an `expires_in`, and a polling `interval`.
- The OP resolves the hint to a subject and notifies that subject's **authentication device** out of band; the user
  approves or denies there.
- In **poll** mode the client polls the token endpoint with `grant_type=urn:openid:params:grant-type:ciba` and its
  `auth_req_id`, receiving `authorization_pending` / `slow_down` until the user acts, then tokens or
  `access_denied` / `expired_token`. In **ping** mode the OP additionally notifies the client's
  `client_notification_endpoint` when the request completes, and the client then polls once.

This is structurally the device flow's "pending request with a mutable approval state, polled for completion": a shape
[ADR-0059](0059-device-authorization-grant.md) already solved with a `PersistedGrant` payload plus
`IPersistedGrantService.UpdatePayloadAsync`, and explicitly called out as shared with a future CIBA grant. CIBA differs
in exactly two ways: there is **no `user_code` pointer row** (the user is identified up front by a hint, not by typing a
code), and approval happens on a **decoupled authentication device** the OP must notify — a transport that is
deployment-specific. Resolving an opaque `login_hint` to a local subject is likewise deployment-specific.

## Decision

**Implement CIBA as a `DefaultCibaGrantHandler : ITokenGrantHandler` plus a self-advertising
`backchannel_authentication_endpoint`, modeling the pending request as a `PersistedGrant<CibaGrant>` keyed by
`auth_req_id` — reusing the device flow's persisted-grant/`UpdatePayloadAsync` pattern with no new table or EF
migration — and expose the two deployment-specific concerns (hint→subject resolution, authentication-device
notification) plus the approval transition as injectable seams with safe defaults.** Scope the delivery modes to
**poll and ping** (both complete by polling the token endpoint; ping adds a completion notification); **push** — which
delivers tokens inside the notification — is deferred (backlog `F17`).

Concretely:

- **Persistence — one row, reused store.** The request is a `PersistedGrant` of type `PersistedGrantTypes.Ciba`
  (`"ciba"`), keyed by the high-entropy `auth_req_id`, whose JSON payload (`CibaGrant`) carries the client id, requested
  scopes, the approval `Status` (reusing the device flow's `DeviceAuthorizationStatus` — `Pending`/`Approved`/`Denied`),
  the approving `SubjectAuthentication` once approved, the `LastPolledWhen` timestamp for `slow_down`, the delivery
  `DeliveryMode`, the optional `ClientNotificationToken`, and the optional `BindingMessage`. No `user_code` pointer row
  is needed. The request rides the existing grant store, EF entity, tenant scoping, and concurrency unchanged.
- **Poll discipline and terminal success mirror the device grant.** The token handler enforces the `interval`
  (`slow_down` when polled too soon, stamping `LastPolledWhen` via `UpdatePayloadAsync`), returns
  `authorization_pending` while `Pending`, `access_denied` when `Denied`, `expired_token` past expiry, and claims the
  `Approved` request exactly once with `ConsumeOnceOrDefault` before issuing tokens through `ITokenService` — identical
  to the device code grant, so the two share the model rather than reinventing it.
- **A thin endpoint over a mediator pipeline.** Mirroring the token and end-session endpoints, the backchannel
  endpoint only authenticates the client and loads a strongly-typed `BackchannelAuthenticationRequest`
  (`OpenIdMessage<T>` with `KnownParameter`s), then drives three mediator commands in sequence:
  `ValidateBackchannelAuthenticationRequestCommand` (grant-allowed, exactly-one-hint, delivery-mode, and
  notification-token checks), `AuthenticateBackchannelRequestCommand` → `AuthenticateSubjectDisposition` (resolve the
  hint to a subject), and `CreateBackchannelAuthenticationRequestCommand` → `BackchannelAuthenticationResult` (persist
  the pending grant, notify, and produce the `auth_req_id`). The endpoint renders the result; all logic lives in
  replaceable handlers — no ad-hoc form parsing.
- **Hint resolution is a replaceable mediator seam, not a bespoke service.** `AuthenticateBackchannelRequestCommand`
  reuses the exact `AuthenticateSubjectDisposition` shape the resource-owner-password grant uses
  (`AuthenticatePasswordGrantCommand`): a default `ICommandResponseHandler` reports the hint as unresolvable
  (`unknown_user_id`), and a deployment registers a CIBA authentication capability that replaces it — mapping an opaque
  `login_hint` / `login_hint_token` / `id_token_hint` to a local subject is deployment-specific, so it is a seam, not a
  built-in.
- **Two injectable transport/approval services, safe by default.**
    - `ICibaNotificationService` notifies the user's authentication device that approval is pending, and (ping) notifies
      the client's `client_notification_endpoint` on completion. The default is a best-effort no-op; the real transport
      (push provider, message bus) is deployment-specific, mirroring `IBackChannelLogoutNotifier`
      ([ADR-0074](0074-session-management-and-logout.md)).
    - `ICibaRequestService` performs the out-of-band `Approve` / `Deny` transition (writing the decision and approving
      subject via `UpdatePayloadAsync`, firing the ping notification on approval). It is the first-class, testable
      approval capability a deployment's authentication-device callback invokes; the OP ships no user-facing CIBA
      approval UI because the interaction is, by definition, on the separate device.
- **New constants and errors.** `OpenIdConstants.GrantTypes.Ciba`, `PersistedGrantTypes.Ciba`,
  `EndpointNames`/`EndpointPaths.BackchannelAuthentication` (`/oauth2/bc-authorize`), the request parameters
  (`auth_req_id`, `login_hint_token`, `binding_message`, `client_notification_token`, `requested_expiry`), and the
  `BackchannelTokenDeliveryModes` constants join the existing catalog; `ErrorCodes` gains `unknown_user_id`
  (`authorization_pending` / `slow_down` / `expired_token` / `access_denied` already exist from the device flow). The
  token request message gains `auth_req_id` the same way it carries `device_code`.
- **Settings & discovery.** New server settings `backchannel_token_delivery_modes_supported` (default `poll`, `ping`)
  and `backchannel_user_code_parameter_supported` (default `false`) are discoverable; `ciba_request_lifetime` and
  `ciba_polling_interval` follow the device flow's non-discoverable, replace-merged pattern. The endpoint advertises
  itself as `backchannel_authentication_endpoint`, and `urn:openid:params:grant-type:ciba` joins the default
  `grant_types_supported`, so both appear in `.well-known/openid-configuration` automatically — which lights up the
  spec-coverage harness's `ep-ciba` / `grant-ciba` rows ([ADR-0084](0084-spec-coverage-conformance-harness.md)).

## Options considered

- **A dedicated CIBA store / EF entity / migration.** Rejected for the same reasons as the device flow
  ([ADR-0059](0059-device-authorization-grant.md)): the request _is_ a grant payload, and `IPersistedGrantService`
  already persists "any grant and its payload" with tenant scoping, hashing, expiry, and concurrency. A parallel store
  would duplicate that plumbing and force an out-of-band migration for a record the schema already fits.
- **A built-in hint resolver (e.g. `login_hint` = local username/email lookup) and a built-in push transport.**
  Rejected: both are deployment-specific. Baking in a username lookup would wrongly assume the hint's meaning, and a
  built-in transport would assume the authentication-device channel. Injectable seams with safe defaults keep the core
  protocol-correct and the environment pluggable, matching the credential-verifier and back-channel-logout seams.
- **A user-facing CIBA approval endpoint mirroring the device verification endpoint.** Rejected: CIBA's defining
  property is that approval happens on a **separate** authentication device, not via a browser on the consumption
  device. The approval is an `ICibaRequestService` operation a deployment's device callback invokes, not an OP-hosted
  page.
- **Implement push delivery now.** Deferred (`F17`): push delivers tokens inside the notification, which needs the
  notification channel to carry (and secure) a full token response. Poll and ping — which complete through the existing
  token endpoint — cover the common modes; push layers onto the same `ICibaNotificationService` seam when needed.

## Consequences

- A decoupled backchannel-authentication flow works end to end with **no schema change**: the backchannel endpoint, the
  `auth_req_id` token path, settings, and discovery all ride the existing grant store and endpoint/settings machinery.
- The spec-coverage harness's CIBA rows flip from `missing` to `implemented` (advertised), and the probe runner can
  later verify the endpoint — closing the loop the harness opened.
- A deployment must register a CIBA authentication capability (an `AuthenticateBackchannelRequestCommand` handler) and,
  for ping and for real authentication-device delivery, an `ICibaNotificationService` to make CIBA functional; until
  then the endpoint is advertised but returns `unknown_user_id`, which is the honest, secure default.
- CIBA and the device flow now share the pending-request model and `UpdatePayloadAsync`; a third such grant would
  justify promoting the shared "pending authorization" shape into a small helper, but two is not yet that signal.
- Push delivery (`F17`) and signed/encrypted backchannel request objects
  (`backchannel_authentication_request_signing_alg_values_supported`, a FAPI-CIBA concern) remain backlog follow-ups.

## References

- [ADR-0059](0059-device-authorization-grant.md) — the device authorization grant whose pending-request/`UpdatePayloadAsync`
  pattern CIBA reuses.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the `ITokenGrantHandler` grant-handler shape.
- [ADR-0009](0009-endpoint-families-own-their-route-group.md) — endpoint families own their route and discovery advertisement.
- [ADR-0074](0074-session-management-and-logout.md) — the `IBackChannelLogoutNotifier` injectable-transport seam the
  CIBA notifier mirrors.
- [ADR-0070](0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md) — the credential-verifier
  seam pattern the CIBA login-hint resolution command follows.
- [ADR-0084](0084-spec-coverage-conformance-harness.md) — the spec-coverage harness whose CIBA rows this implements.
- [OpenID Connect CIBA Core 1.0](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html).
