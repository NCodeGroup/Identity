# 1085. CIBA is a pending request in the grant store, delivered by poll, ping or push

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

[OpenID Connect CIBA](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html) lets a
**consumption device** (a point-of-sale terminal, a call-center agent's console, a server) start authentication for a
user who authenticates on a **separate authentication device** (the user's phone), with no browser redirect on the
consumption device. It is the decoupled sibling of the Device Authorization Grant
([ADR-1059](1059-the-device-authorization-grant-is-a-pending-request-in-the-grant-store-approved-out-of-band.md)):

- The client `POST`s to a **backchannel authentication endpoint** with the requested `scope`, one hint that identifies
  the user (`login_hint`, `login_hint_token` or `id_token_hint`) and, for a non-poll delivery mode, a
  `client_notification_token`. It receives an `auth_req_id`, an `expires_in` and a polling `interval`.
- The OP resolves the hint to a subject and notifies that subject's authentication device out of band; the user
  approves or denies there.
- In **poll** mode the client polls the token endpoint with `grant_type=urn:openid:params:grant-type:ciba` and its
  `auth_req_id` until the user acts. In **ping** mode the OP notifies the client's `client_notification_endpoint` when
  the request completes and the client polls once. In **push** mode the OP delivers the token response itself to that
  endpoint.

The flow is the device flow's "pending request with a mutable approval state, polled for completion", which a
`PersistedGrant` payload and `IPersistedGrantService.UpdatePayloadAsync` already solve. CIBA differs in two ways: no
`user_code` pointer row exists, because a hint identifies the user up front, and approval happens on a decoupled
authentication device that the OP must notify over a transport that is deployment-specific. Resolving an opaque
`login_hint` to a local subject is deployment-specific as well.

A client that waits for a ping or a push must also learn when its request is denied, or it waits for a request that
cannot be approved until the request expires.

## Decision

**CIBA is a `DefaultCibaGrantHandler : ITokenGrantHandler` that serves `urn:openid:params:grant-type:ciba`, plus a
self-advertising `backchannel_authentication_endpoint` at `/oauth2/bc-authorize`. The pending request is a
`PersistedGrant<CibaGrant>` keyed by `auth_req_id`, with no table of its own. The hint resolver, the
authentication-device transport and the approval transition are injectable seams, and a client receives its result by
poll, ping or push.**

1. **One row in the existing grant store.** The request is a `PersistedGrant` of type `ciba`, keyed by the
   high-entropy `auth_req_id`, with no `user_code` pointer. Its payload, `CibaGrant`, holds the client id, the scopes,
   the `Resources`, the approval `Status` (`DeviceAuthorizationStatus`: `Pending`, `Approved` or `Denied`), the approving
   `SubjectAuthentication`, `LastPolledWhen`, `DeliveryMode`, `ClientNotificationToken`, `BindingMessage` and a
   `[JsonExtensionData]` bag. The row rides the grant store's EF entity, tenant scoping and concurrency control.
2. **A thin endpoint over replaceable commands.** The endpoint authenticates the client, loads a
   `BackchannelAuthenticationRequest` (`OpenIdMessage<T>`), and drives four commands whose handlers are replaceable
   ([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
   [ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md)):
   - `LoadBackchannelAuthenticationRequestCommand` resolves the request parameters;
   - `ValidateBackchannelAuthenticationRequestCommand` checks that the grant is allowed, that exactly one hint is
     present, the delivery mode and the notification token;
   - `AuthenticateBackchannelRequestCommand` resolves the hint to a subject and answers an
     `AuthenticateSubjectDisposition`;
   - `CreateBackchannelAuthenticationRequestCommand` persists the pending grant, notifies and produces the
     `auth_req_id`.
3. **The hint resolver is a seam.** The default `AuthenticateBackchannelRequestCommand` handler answers
   `unknown_user_id`, so a deployment registers its own resolver that maps an opaque hint to a local subject. It has the
   shape of the resource-owner-password grant's `AuthenticatePasswordGrantCommand`.
4. **A signed `request` object.** A signed `request` is verified with an asymmetric algorithm that the client's
   settings permit within `backchannel_authentication_request_signing_alg_values_supported`, and an encrypted one is
   rejected ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)). Its claim and lifetime
   rules are those of [ADR-1187](1187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md).
5. **Polling mirrors the device grant.** The token handler answers `slow_down` for a poll that comes too soon,
   stamping `LastPolledWhen` through `UpdatePayloadAsync`, `authorization_pending` while the request is `Pending`,
   `access_denied` when it is `Denied` and `expired_token` past its expiry. An `auth_req_id` is redeemed only by the
   client that started the request (`invalid_grant` otherwise), and the subject is validated before the spend
   ([ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md)). `ConsumeOnceOrDefault` claims an
   `Approved` request once, before `ITokenService` issues tokens. A push-mode client never polls, so a poll for it is
   `unauthorized_client`.
6. **Approval is a first-class service.** `ICibaRequestService.ApproveAsync` and `DenyAsync` decide only a `Pending`
   request, through `UpdatePayloadAsync` ([ADR-1222](1222-a-grants-payload-is-changed-by-a-read-modify-write.md)), and
   report whether this call decided it. An approval validates the approving subject first, and a refused subject denies
   the request. A deployment's authentication-device callback calls the service; the OP serves no approval page,
   because the approval happens on the separate device.
7. **The authentication-device transport is a seam.** `ICibaNotificationService.NotifyAuthenticationDeviceAsync`
   notifies the user's authentication device. Its default is inert, and a deployment supplies the transport, as it does
   for `IBackChannelLogoutNotifier` ([ADR-1074](1074-session-management-and-logout.md)).
8. **Client delivery is durable.** A `CibaDeliveryJob` on `IJobDispatcher` carries each delivery and retries it
   ([ADR-1242](1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md)).
   - Ping `POST`s `auth_req_id` to `client_notification_endpoint`, with `client_notification_token` as the bearer
     credential.
   - Push `POST`s the token response from `ICibaTokenResponseService`, the same tokens a poll issues, to the same
     endpoint with the same credential.
9. **A denial reaches a ping or push client once.** Only the call that decides a request notifies:
   `DenyAsync`, also when it denies a refused approval, dispatches a `CibaDeliveryJob` only when its own update decided
   the request. A call that finds the request decided already dispatches nothing.
   - A ping carries `auth_req_id`, and the token endpoint answers `access_denied` when the client polls.
   - A push carries `auth_req_id` and `error=access_denied`, with no tokens and no `error_description`. The job holds
     the code in `CibaDeliveryJob.Error`, and `CibaDeliveryJobHandler` adds it to the body.
   - A poll client learns the denial at the token endpoint.
10. **Settings, discovery and the catalog.**
    - `backchannel_token_delivery_modes_supported` (`Intersect`, discoverable) has the baseline `poll`, `ping` and
      `push`.
    - `backchannel_token_delivery_mode` defaults to `poll` per client.
    - `backchannel_user_code_parameter_supported` defaults to `false`.
    - `ciba_request_lifetime` and `ciba_polling_interval` are not discoverable.
    - The grant type is in the default `grant_types_supported`.
    - The error `unknown_user_id` and the parameters `auth_req_id`, `login_hint_token`, `binding_message`,
      `client_notification_token` and `requested_expiry` are in the catalog.

## Options considered

- **A dedicated CIBA store, EF entity and migration.** Rejected, as for the device flow: the request is a grant
  payload, and `IPersistedGrantService` persists any grant and its payload with tenant scoping, hashing, expiry and
  concurrency. A parallel store duplicates that plumbing.
- **A built-in hint resolver or authentication-device transport.** Rejected: both are deployment-specific. A built-in
  username lookup would assume what a hint means, and a built-in transport would assume the authentication-device
  channel. Injectable seams with safe defaults keep the protocol correct and the environment pluggable, as the
  credential-verifier seam does ([ADR-1070](1070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md)).
- **An OP-hosted approval page that mirrors the device verification endpoint.** Rejected: CIBA's defining property is
  that approval happens on a separate authentication device, so approval is an `ICibaRequestService` operation that the
  device's callback invokes.
- **An `error_description` in a denial.** Rejected: CIBA makes it optional, and the end user may not be the party that
  denied the request.
- **Notifying on every denial call.** Rejected: a concurrent decision would notify twice, once for each caller.
- **The chosen design:** a `PersistedGrant<CibaGrant>` with seams for the two deployment-specific concerns and one
  delivery job for ping and push.

## Consequences

- A decoupled backchannel-authentication flow works end to end with no schema change: the endpoint, the `auth_req_id`
  token path, the settings and discovery ride the existing grant store and endpoint machinery.
- A deployment must register an `AuthenticateBackchannelRequestCommand` handler, and an `ICibaNotificationService` for
  real authentication-device delivery, to make CIBA functional. Until then the endpoint is advertised and answers
  `unknown_user_id`, which is the secure default.
- CIBA and the device flow share the pending-request model and `UpdatePayloadAsync`. A third such grant would justify
  promoting the shared shape into a helper; two do not.
- A durable dispatcher persists no tokens for a denial, because the job carries only the error.
- A ping or push client learns of a denial at once and stops waiting for the request.
- A client that redeems another client's `auth_req_id`, or a push client that polls, gets an error without consuming
  the request.
- Tests cover the polling states, one-time redemption, the client binding, the subject validation, concurrent
  decisions, the delivery payloads of approval and denial, and discovery.

## References

- [OpenID Connect CIBA Core 1.0](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html),
  §10.2 (ping callback), §10.3 (push callback) and §12 (the error payload of a push request).
- Issues [#55](https://github.com/NCodeGroup/identity/issues/55) (push delivery and signed request objects),
  [#24](https://github.com/NCodeGroup/identity/issues/24) (durable delivery) and
  [#291](https://github.com/NCodeGroup/identity/issues/291) (a denial reaches the client).
- [ADR-1059](1059-the-device-authorization-grant-is-a-pending-request-in-the-grant-store-approved-out-of-band.md): the
  device authorization grant whose pending-request pattern CIBA reuses.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): the
  grant-handler and command shapes.
- [ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md):
  endpoints own their route and discovery advertisement.
- [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md): the algorithms of a signed
  request.
- [ADR-1187](1187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md): a request object's claims.
- [ADR-1222](1222-a-grants-payload-is-changed-by-a-read-modify-write.md): only the call that decides a request acts.
- [ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md): the subject check of approval and
  redemption.
- [ADR-1242](1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md):
  the delivery job.
- [ADR-1074](1074-session-management-and-logout.md): the injectable-transport seam that the notifier mirrors.
- [ADR-1070](1070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md): the credential-verifier
  seam.
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.DefaultCibaRequestService`,
  `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery.CibaDeliveryJob`,
  `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery.CibaDeliveryJobHandler`.
