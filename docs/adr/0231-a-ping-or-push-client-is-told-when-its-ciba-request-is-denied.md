# 231. A ping or push client is told when its CIBA request is denied

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

[ADR-0085](0085-ciba-grant.md) delivers an approval to a ping or push client, and
[ADR-0225](0225-every-grant-that-issues-tokens-for-a-subject-validates-it.md) denies an approval whose subject is
refused. A denial reached no client, so a ping or push client waited for a request that cannot be approved until the
request expired (backlog `R100`).

## Decision

**A denial that decides a pending CIBA request notifies a ping or push client once, through the delivery job that
carries an approval.**

1. **Only the deciding call notifies.** `ICibaRequestService.DenyAsync`, also when it denies a refused approval,
   dispatches a `CibaDeliveryJob` only when its own update of the grant decided the request. A call that finds the
   request decided already, by a concurrent approval or denial, dispatches nothing.
2. **A ping carries the request.** The body of the notification is `auth_req_id`, as it is for an approval, and the
   token endpoint answers `access_denied` when the client polls (CIBA Core 1.0 §10.2).
3. **A push carries the error.** The payload is `auth_req_id` and `error` with the value `access_denied`, without
   tokens and without `error_description`, which CIBA Core 1.0 §12 makes optional, and the end user may not be the party
   that denied the request.
4. **A poll client is not notified.** It learns the denial from the token endpoint.
5. **The job carries the error.** `CibaDeliveryJob.Error` holds the error code, and `CibaDeliveryJobHandler` adds it to
   the body. The job retries as every delivery does ([ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md)).

## Options considered

- **Add an `error_description`.** Rejected: it is optional, and the end user may not be the party that denied the
  request.
- **Notify on every denial call.** Rejected: a concurrent decision would notify twice, once for each caller.

## Consequences

- A ping or push client learns of a denial at once, and it stops waiting for the request.
- A durable dispatcher persists no tokens for a denial, because the job carries only the error.
- This implements backlog `R100`.

## References

- [CIBA Core 1.0](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html)
  §10.2 (ping callback), §10.3 (push callback) and §12 (the error payload of a push request).
- [ADR-0085](0085-ciba-grant.md) — the CIBA grant and its delivery modes.
- [ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md) — the delivery job that retries.
- [ADR-0222](0222-a-grants-payload-is-changed-by-a-read-modify-write.md) — only the call that decides a request acts.
- [ADR-0225](0225-every-grant-that-issues-tokens-for-a-subject-validates-it.md) — a refused approval denies the request.
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.DefaultCibaRequestService`,
  `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery.CibaDeliveryJob`,
  `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery.CibaDeliveryJobHandler`.
