# 225. Every grant that issues tokens for a subject validates it

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

[ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md) refuses a disabled or deleted account wherever
`ValidateSubjectAuthenticationCommand` is sent: the authorization request and the authorization-code, refresh-token and
password grants. Three grants that issue tokens for an end user did not send it: the device-code grant
([ADR-0059](0059-device-authorization-grant.md)), the CIBA grant ([ADR-0085](0085-ciba-grant.md)) and token exchange
([ADR-0101](0101-oauth-token-exchange-rfc8693.md)). Four gaps followed:

- An end user whose account was disabled or deleted kept redeeming an approved device code or `auth_req_id` and kept
  exchanging tokens. An approved device code names its subject only in its payload, because the grant has no subject
  identifier, so ending the subject's sessions does not revoke it.
- CIBA push delivery issues the tokens at the approval, which has no protocol request to validate against.
- The device verification endpoint authenticated its cookie without checking the server-side session of the cookie, so
  the cookie of a terminated session still approved a device request.
- A device code or an `auth_req_id` was redeemed by whichever client presented it (backlog `R92`).

## Decision

**The device-code, CIBA and token-exchange grants validate the subject they issue tokens for as every other grant does,
a CIBA approval validates its approving subject, and a stored credential that was issued to a client is redeemed only by
that client.**

1. **Three grants send `ValidateSubjectAuthenticationCommand`.** The device-code grant, the CIBA grant (poll and
   ping at the token endpoint) and the token-exchange grant send it with `EnforceAuthenticationAge: false`, through
   `ValidateTokenGrantCommand<DeviceCodeGrant>` (`DefaultValidateDeviceCodeGrantHandler`),
   `ValidateTokenGrantCommand<CibaGrant>` (`DefaultValidateCibaGrantHandler`) and
   `ValidateTokenGrantCommand<TokenExchangeGrant>` (`DefaultValidateTokenExchangeGrantHandler`). A refusal throws the
   validation error with status 400 (`access_denied`), as the refresh-token grant does. An approved device or CIBA
   payload with no `SubjectAuthentication` is `invalid_grant`.
2. **A refused request leaves the credential unspent.** The device-code and CIBA grants validate before
   `ConsumeOnceOrDefault`, so a credential is spent only after every check has passed
   ([ADR-0192](0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md)). `access_denied` is
   terminal for the polling client.
3. **A credential is redeemed only by the client it was issued to.** A device code and an `auth_req_id` that another
   client presents are `invalid_grant` ("The provided device code was issued to a different client." and "The provided
   auth_req_id was issued to a different client."). The check follows the lookup of the grant, before the expiry and
   status checks and before a poll is recorded (RFC 8628 §3.4, CIBA Core §11).
4. **A CIBA approval validates the approving subject.** `DefaultCibaRequestService.ApproveAsync` validates the subject
   before it records the approval, for every delivery mode, because push delivery issues tokens at the approval. A
   refused subject denies the request (`DenyAsync`) and `ApproveAsync` returns `false`. The denial reaches no ping or
   push client (backlog `R100`).
5. **A subject can be validated outside a protocol request.** `ValidateSubjectAuthenticationCommand.OpenIdRequest` is
   `IOpenIdRequest?`, and `null` means that the subject is validated outside a protocol request, as at a CIBA approval.
   `DefaultValidateSubjectAuthenticationHandler` then skips the `max_age` of the request, the `idp:` values of its
   `acr_values` and its essential `acr` claim request. Every other check runs.
6. **A token-exchange subject carries its tenant.** `DefaultTokenExchangeGrantHandler` records the tenant in the
   `AuthenticationProperties` of the subject (`OpenIdConstants.AuthenticationPropertyItems.TenantId`), because the
   subject token was validated as a token of this tenant and an ID token carries no `tid`. The actor is not validated:
   it is named in `act` and is not the subject the tokens are issued for, and `IIssuedTokenValidator` validated its
   token.
7. **The device verification endpoint checks the session of its cookie.** On the cookie path of
   `AuthenticateSubjectCommand`, `DefaultAuthenticateSubjectHandler` validates the `sid` of the cookie with
   `ISessionService.ValidateAndTouchAsync`. A `false` result returns `Undefined()`, so the endpoint challenges for a new
   login, with no audit record, as `DefaultAuthenticateHandler` does on the authorization path. The bearer path is
   unchanged, because `IIssuedTokenValidator` checks the session of a token.
8. **One helper validates the subject of every token grant.** `TokenGrantSubjectValidation.ValidateAsync` in
   `NCode.Identity.OpenId.Authentication.Endpoints.Token` takes the context, the client, the request, the
   `SubjectAuthentication` and the cancellation token, sends the command and throws the refusal. It replaces the private
   copies of the authorization-code, refresh-token and password grants, and the validators of decision 1 call it.

## Options considered

- **Validate in the service that composes the CIBA tokens.** Rejected: the approval would already be recorded, so an
  approved request would never issue tokens for a refused subject, and the denial would not be recorded.
- **Spend the credential and then validate, as the authorization-code grant does.** Rejected: a refused request would
  spend the credential. The authorization-code grant spends first because the replay of a code revokes its session
  ([ADR-0112](0112-authorization-code-replay-revokes-session.md)); a device code or an `auth_req_id` has no such
  signal.
- **Pass an empty token request at an approval.** Rejected: it fabricates a request, and a `null` request states that
  there is none.
- **Validate the subject that approves a device request.** Rejected: the session check of decision 7 applies, and the
  token grant refuses a refused subject with the terminal `access_denied`.
- **Validate the subject that a CIBA login hint names.** Rejected: the backchannel authentication endpoint identifies
  that subject and does not authenticate it, and the approval is validated.

## Consequences

- Each device-code, CIBA and token-exchange token request reads the records of the end user, as the other grants do.
- A refused device code or `auth_req_id` stays unspent, and every poll is refused until it expires.
- A CIBA approval callback passes a subject that carries its tenant and is authenticated, as a token request requires.
- The allowed identity providers of the exchanging client apply to the `idp` of the subject token.
- A denied CIBA request notifies no ping or push client (backlog `R100`).
- **Breaking:** `ValidateSubjectAuthenticationCommand.OpenIdRequest` is nullable, `ICibaRequestService.ApproveAsync`
  returns `false` for a refused subject, a device code or `auth_req_id` that another client presents is
  `invalid_grant`, and `DefaultAuthenticateSubjectHandler` depends on `ISessionService`.
- This implements backlog `R92`.

## References

- [ADR-0059](0059-device-authorization-grant.md) — the device authorization grant and its verification endpoint.
- [ADR-0085](0085-ciba-grant.md) — the CIBA grant and its delivery modes.
- [ADR-0101](0101-oauth-token-exchange-rfc8693.md) — token exchange.
- [ADR-0112](0112-authorization-code-replay-revokes-session.md) — the replay of an authorization code, which is why that
  grant spends its code first.
- [ADR-0192](0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md) — a credential is spent
  after every check has passed.
- [ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md) — the account check that every grant shares.
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
