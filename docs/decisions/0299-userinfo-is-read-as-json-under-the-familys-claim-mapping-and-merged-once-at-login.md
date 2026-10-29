# 299. UserInfo is read as JSON under the family's claim mapping and merged once at login

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

Some providers keep profile claims (`email`, `name`, groups) out of the ID token and serve them only from the UserInfo
endpoint (OpenID Connect Core 1.0 §5.3). The protocol client
([ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)) had no UserInfo
request, and the BFF built a session's claims from the ID token alone
([ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) decision 10).

A UserInfo request presents an access token, so it must be sent the way that token was issued: as a bearer token, or
with a DPoP proof of the key it is bound to (RFC 9449 §7), and over mutual TLS to the endpoint's alias when the client
authenticates with it, so a certificate-bound token is accepted (RFC 8705 §5). OIDC Core §5.3.2 requires the client to
check that the response's `sub` is exactly the ID token's, because a response about another user must not be used. The
response is JSON, or a signed or encrypted JWT when the client registered `userinfo_signed_response_alg`.

The family maps JSON claims to `Claim`s once, in the JWT stack, typing each claim by its JSON value
([ADR-0122](0122-jwt-claim-values-map-to-json-by-declared-type.md)), but only a JWT's validation could reach that
mapping.

## Decision

1. **One claim mapping, a service.** `IClaimsIdentityFactory` (`NCode.Identity.Jwt.Abstractions`) maps any JSON claim
   set to a `ClaimsIdentity`, with an issuer for a claim set that carries no `iss`. `DefaultJwtValidator` maps a JWT's
   payload through it, and the protocol client maps UserInfo through it, so a UserInfo claim and an ID-token claim of
   the same JSON value are the same `Claim`.
2. **`IUserInfoClient`.** `GetUserInfoAsync(registration, UserInfoRequest)` sends a `GET` to `userinfo_endpoint`, or to
   its mTLS alias for a client that authenticates with mutual TLS (the same resolution as its token requests). A token
   whose `UserInfoRequest.DPoPJwkThumbprint` is set is presented as `Authorization: DPoP` with a proof of that key, and
   a `use_dpop_nonce` challenge is retried exactly once with the nonce (RFC 9449 §9); any other token is a bearer token.
3. **The result.** A JSON object whose `sub` equals `UserInfoRequest.ExpectedSubject` is a `UserInfoResult` with the
   JSON and its `Identity`. A `WWW-Authenticate` challenge's `error` (RFC 6750 §3), or an OAuth error body, is a
   `ProtocolError`. Another subject is `subject_mismatch`, a response that is not a JSON object with a string `sub` is
   `invalid_response`, and an `application/jwt` response is `unsupported_response` (`UserInfoFailureCodes`). An
   endpoint that answers with neither claims nor an error throws `OpenIdProviderException`, and a provider without a
   UserInfo endpoint throws `InvalidOperationException`.
4. **Signed and encrypted UserInfo responses are not yet validated.** The client refuses them as unsupported rather
   than reading claims it has not verified; validating them under the client's registered algorithms, as ID tokens are,
   is tracked as issue [#421](https://github.com/NCodeGroup/Identity/issues/421).
5. **The BFF merges UserInfo once, at login, when asked.** `Bff:LoadUserInfo` (default `false`) requests UserInfo at
   the callback with the new access token, after the ID token validates and before the session claim contributors and
   the login gates. Each UserInfo claim type replaces the ID token's claims of that type, because UserInfo is the
   provider's current profile; the claims that anchor the session to the authentication (`iss`, `sub`, `sid`,
   `auth_time`, `acr`, `amr`) and the validation-only claims are never taken from it. A UserInfo error or invalid
   response ends the login with `userinfo_failed`, and an unreachable provider with `provider_unavailable`. UserInfo is
   not requested again at refresh; a session keeps the claims it was created with.

## Options considered

- **Map UserInfo JSON in the client or the BFF.** Rejected: a second mapping would drift from the JWT stack's.
- **Add UserInfo claims only when the ID token lacks the type.** Rejected: the ID token can carry a stale value, and
  two values of `email` from two sources would leave the app to guess which to trust.
- **Request UserInfo at every refresh.** Rejected: it costs a request per refresh, and a session's claims would change
  under the app between requests; the session claim contributors are the place for data that must be fresh.
- **Accept signed UserInfo responses without validating them.** Rejected: an unverified JWT is no better than an
  unauthenticated document.
- **POST to the UserInfo endpoint.** Rejected: OIDC Core allows both, `GET` is the common form, and the token travels
  in the `Authorization` header either way.

## Consequences

- A BFF in front of a provider that keeps profile claims out of the ID token gets them with one setting.
- Any server-side client of the family can read UserInfo with the same proof-of-possession and mTLS handling as its
  token requests.
- A provider configured to sign UserInfo responses cannot be used with `Bff:LoadUserInfo` until the follow-up lands.
- `IClaimsIdentityFactory` is new public surface of the JWT stack, and `DefaultJwtValidator` takes it.

## References

- [OpenID Connect Core 1.0 §5.3](https://openid.net/specs/openid-connect-core-1_0.html#UserInfo) — UserInfo endpoint
- [RFC 6750 §3](https://www.rfc-editor.org/rfc/rfc6750#section-3) — the `WWW-Authenticate` error
- [RFC 9449 §7, §9](https://www.rfc-editor.org/rfc/rfc9449#section-7) — DPoP-bound access tokens and resource nonces
- [RFC 8705 §5](https://www.rfc-editor.org/rfc/rfc8705#section-5) — mTLS endpoint aliases
- [ADR-0122](0122-jwt-claim-values-map-to-json-by-declared-type.md) — claim mapping
- [ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) — BFF sessions
