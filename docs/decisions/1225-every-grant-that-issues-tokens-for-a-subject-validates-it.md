# 1225. Every grant that issues tokens for a subject validates it

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A disabled or deleted account is refused wherever `ValidateSubjectAuthenticationCommand` is sent
([ADR-1224](1224-deleting-or-disabling-an-account-ends-its-sessions.md)). The end user's standing is its own check,
which every subject validation includes
([ADR-1229](1229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md)). Every grant that
issues tokens for an end user must therefore send the command: the authorization-code, refresh-token and password
grants, and the device-code grant
([ADR-1059](1059-the-device-authorization-grant-is-a-pending-request-in-the-grant-store-approved-out-of-band.md)), the
CIBA grant ([ADR-1085](1085-ciba-is-a-pending-request-in-the-grant-store-delivered-by-poll-ping-or-push.md)) and token
exchange ([ADR-1101](1101-token-exchange-issues-delegation-and-impersonation-tokens-from-tokens-the-tenant-issued.md)).

Several forces shape how:

- An approved device code names its subject only in its payload, because the grant has no subject identifier, so ending
  the subject's sessions does not revoke it. The grant must check the subject when the code is redeemed.
- CIBA push delivery issues the tokens at the approval, which has no protocol request to validate against.
- The device verification endpoint authenticates a cookie, and the cookie of a terminated session must not approve a
  device request.
- A device code or an `auth_req_id` is a bearer credential issued to one client, and only that client may redeem it.

## Decision

**The device-code, CIBA and token-exchange grants validate the subject they issue tokens for as every other grant does,
a CIBA approval validates its approving subject, and a stored credential that was issued to a client is redeemed only by
that client.**

1. **Every grant that issues tokens for an end user sends `ValidateSubjectAuthenticationCommand`.** That covers the
   authorization-code, refresh-token, password, device-code, CIBA and token-exchange grants, and the command includes
   the end user's standing. The device-code, CIBA and token-exchange grants send it with
   `EnforceAuthenticationAge: false`, through `ValidateTokenGrantCommand<T>` handlers:
   `DefaultValidateDeviceCodeGrantHandler`, `DefaultValidateCibaGrantHandler` and
   `DefaultValidateTokenExchangeGrantHandler`.
2. **One helper validates the subject of every token grant.** `TokenGrantSubjectValidation.ValidateAsync` in
   `NCode.Identity.OpenId.Authentication.Endpoints.Token` takes the context, the client, the request, the
   `SubjectAuthentication` and the cancellation token, sends the command and throws the refusal (`access_denied`,
   status `400`). An approved device or CIBA payload with no `SubjectAuthentication` is `invalid_grant`.
3. **A refused request leaves the credential unspent.** The device-code and CIBA grants validate before
   `ConsumeOnceOrDefault`, so a credential is spent only after every check has passed
   ([ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md)).
   `access_denied` is terminal for the polling client. The authorization-code grant spends its code first, because the
   replay of a code terminates its session
   ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)).
4. **A credential is redeemed only by the client it was issued to.** A device code and an `auth_req_id` that another
   client presents are `invalid_grant` ("The provided device code was issued to a different client." and "The provided
   auth_req_id was issued to a different client."). The check follows the lookup of the grant, before the expiry,
   status and poll checks (RFC 8628 §3.4, CIBA Core §11).
5. **A CIBA approval validates the approving subject.** `DefaultCibaRequestService.ApproveAsync` validates the subject
   before it records the approval, for every delivery mode, because push delivery issues tokens at the approval. A
   refused subject denies the request (`DenyAsync`) and `ApproveAsync` returns `false`.
6. **A subject can be validated outside a protocol request.** `ValidateSubjectAuthenticationCommand.OpenIdRequest` is
   `IOpenIdRequest?`, and `null` means that the subject is validated outside a protocol request, as at a CIBA approval.
   `DefaultValidateSubjectAuthenticationHandler` then skips the `max_age` of the request, the `idp:` values of its
   `acr_values` and its essential `acr` claim request. Every other check runs.
7. **A token-exchange subject carries its tenant.** `DefaultTokenExchangeGrantHandler` records the tenant in the
   `AuthenticationProperties` of the subject (`OpenIdConstants.AuthenticationPropertyItems.TenantId`), because the
   subject token was validated as a token of this tenant and an ID token carries no `tid`. The actor is not validated as
   a subject: it is named in `act` and is not the subject the tokens are issued for, and `IIssuedTokenValidator`
   validated its token. The allowed identity providers of the exchanging client apply to the `idp` of the subject
   token.
8. **The device verification endpoint checks the session of its cookie.** On the cookie path of
   `AuthenticateSubjectCommand`, `DefaultAuthenticateSubjectHandler` validates the `sid` of the cookie with
   `ISessionService.ValidateAndTouchAsync`. A `false` result returns `Undefined()`, so the endpoint challenges for a new
   login, with no audit record, as `DefaultAuthenticateHandler` does on the authorization path. The bearer path relies
   on `IIssuedTokenValidator`, which checks the session of a token.

## Options considered

- **Validate in the service that composes the CIBA tokens.** Rejected: the approval would already be recorded, so an
  approved request would never issue tokens for a refused subject, and the denial would not be recorded.
- **Spend the credential and then validate, as the authorization-code grant does.** Rejected: a refused request would
  spend the credential. The authorization-code grant spends first because the replay of a code revokes its session; a
  device code or an `auth_req_id` has no such signal.
- **Pass an empty token request at an approval.** Rejected: it fabricates a request, and a `null` request states that
  there is none.
- **Validate the subject that approves a device request.** Rejected: the session check of decision 8 applies, and the
  token grant refuses a refused subject with the terminal `access_denied`.
- **Validate the subject that a CIBA login hint names.** Rejected: the backchannel authentication endpoint identifies
  that subject and does not authenticate it, and the approval is validated.
- **Validate the subject in every grant through the shared command, before spending and with a nullable request.**
  Chosen: it gives one refusal for every grant, keeps a refused credential unspent and states honestly when no request
  exists.

## Consequences

- Each device-code, CIBA and token-exchange token request reads the records of the end user, as the other grants do.
- A refused device code or `auth_req_id` stays unspent, and every poll is refused until it expires.
- A CIBA approval callback passes a subject that carries its tenant and is authenticated, as a token request requires.
- A denied CIBA request is delivered to a ping or push client
  ([ADR-1085](1085-ciba-is-a-pending-request-in-the-grant-store-delivered-by-poll-ping-or-push.md)).
- A device code or `auth_req_id` that another client presents is `invalid_grant`, and `DefaultAuthenticateSubjectHandler`
  depends on `ISessionService`.
- A grant added later that issues tokens for an end user calls `TokenGrantSubjectValidation.ValidateAsync`; a grant that
  does not is the "you chose wrong" smell.

## References

- [ADR-1059](1059-the-device-authorization-grant-is-a-pending-request-in-the-grant-store-approved-out-of-band.md) — the device authorization grant and its verification endpoint.
- [ADR-1085](1085-ciba-is-a-pending-request-in-the-grant-store-delivered-by-poll-ping-or-push.md) — the CIBA grant and its delivery modes.
- [ADR-1101](1101-token-exchange-issues-delegation-and-impersonation-tokens-from-tokens-the-tenant-issued.md) — token exchange.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) — the replay of an authorization code, which is why that grant spends its code first.
- [ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md) — a credential is spent after every check has passed.
- [ADR-1224](1224-deleting-or-disabling-an-account-ends-its-sessions.md) — the account check that every grant shares.
- [ADR-1229](1229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md) — the end user's standing, which every subject validation includes.
- [RFC 8628](https://datatracker.ietf.org/doc/html/rfc8628) §3.4–3.5 — the device access token request and response.
- OpenID Connect CIBA Core 1.0 §10–11 — the token delivery modes and the token request:
  <https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html>
- [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693) — OAuth 2.0 Token Exchange.
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Token.DeviceCode.DefaultValidateDeviceCodeGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba.DefaultValidateCibaGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange.DefaultValidateTokenExchangeGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenGrantSubjectValidation`,
  `NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.DefaultCibaRequestService`,
  `NCode.Identity.OpenId.Authentication.Subject.DefaultAuthenticateSubjectHandler`,
  `NCode.Identity.OpenId.Authentication.Subject.ValidateSubjectAuthenticationCommand`.
