# 39. The OpenID Connect `claims` request parameter is honored end-to-end

- **Status:** Accepted
- **Date:** 2026-10-03
- **Deciders:** NCode Group

## Context

The OpenID Connect `claims` request parameter ([OIDC Core 5.5](https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter))
lets a client request individual claims — with optional `essential`, `value`, and `values` constraints — to be returned
in the ID Token (`claims.id_token`) and/or from the UserInfo endpoint (`claims.userinfo`), additive to the claims a
scope already implies.

The request model is already implemented and tested: the parameter is parsed into `IRequestClaims` / `IRequestClaim`,
exposed as `IAuthorizationRequest.Claims`, round-trip serialized, and the whole `IAuthorizationRequest` (including
`Claims`) is persisted with the authorization-code grant. `DefaultValidateAuthorizationRequestHandler` already rejects a
request that uses the parameter when `claims_parameter_supported` is `false`. What is missing is the final leg —
**honoring** the requested claims — marked by explicit `TODO`s at the ID-token claims handler, the authorization-code
grant handler, and (by absence of any request context) the UserInfo pipeline. Because the parameter is unhonored,
`claims_parameter_supported` is advertised `false` and locked off.

The ID Token is minted during token issuance, where the authorization request (and thus `Claims`) is still in hand. The
UserInfo endpoint, however, is reached later with only an access token, so honoring `claims.userinfo` requires the
requested claims to survive to that call.

## Decision

**Honor the `claims` request parameter end-to-end**, with the requested claims threaded to ID-token issuance in-process
and to UserInfo through a stateful server-side grant — never by inflating the access token.

- **ID Token (`claims.id_token`).** `CreateSecurityTokenRequest` carries the request's `IRequestClaims`; the
  authorization-code and authorization-endpoint issuance paths populate it from the authorization request, and
  `DefaultGetIdTokenSubjectClaimsHandler` adds the requested ID-token claims on top of the scope-driven claims.
- **UserInfo (`claims.userinfo`).** When an access token is issued for a request that carried `claims.userinfo`, the
  requested UserInfo claims are persisted as a stateful grant (`OpenIdConstants.PersistedGrantTypes.UserInfoClaims`)
  **keyed by the access token's `jti`**, with a lifetime equal to the access token's. The UserInfo endpoint reads `jti`
  from the validated access token and resolves that grant (repeatable `IPersistedGrantService.GetOrDefaultAsync`); when
  present, a default `GetUserInfoClaims` handler adds the requested claims on top of the scope-driven claims. The access
  token gains **no** new payload claim — it already carries `jti`.
- **`value` / `values` are advisory.** The OP returns the subject's actual claim when present; it does not filter,
  fabricate, or substitute values to match a requested `value`/`values`. A host may impose stricter semantics with a
  higher-priority claims handler.
- **`essential` is best-effort.** An essential claim is returned when available; a missing essential claim does **not**
  fail token issuance or the UserInfo response (authentication has already succeeded).
- **The setting is flipped only once honoring exists.** `claims_parameter_supported` becomes `default true` and keeps
  its `And` ceiling (the server advertises support; a tenant/client may opt out but cannot enable beyond the server),
  and discovery advertises `true`. Until then it stays locked `false` and the validator keeps rejecting the parameter,
  so the parameter is never accepted-but-ignored.

## Options considered

- **Embed the requested claims in the access token.** Rejected: it bloats a token presented to every resource server
  and leaks request intent broadly; it is not how production OPs carry post-issuance request state.
- **A full session-management subsystem (`sid`).** Rejected as premature: honoring `claims.userinfo` needs only a
  short-lived grant keyed by the token's existing `jti`, not a general session store. (That subsystem, if later needed
  for front/back-channel logout, can subsume this.)
- **Re-use the refresh-token grant.** Rejected: a refresh token is not always issued (it requires `offline_access`), so
  it cannot carry UserInfo state for every access token.

## Consequences

- Clients can request specific claims and receive them in the ID Token and from UserInfo, additive to scopes.
- Issuing an access token for a request with `claims.userinfo` writes one short-lived grant; UserInfo performs one
  indexed lookup by `jti`. A request without `claims.userinfo` writes and reads nothing.
- `value`/`values`/`essential` are advisory; hosts needing strict enforcement register a higher-priority handler. This
  is documented so the behavior is not mistaken for a gap.
- The access token is unchanged, so resource servers and token size are unaffected.

## References

- Code: `IRequestClaims`, `CreateSecurityTokenRequest`, `DefaultGetIdTokenSubjectClaimsHandler`,
  `DefaultAuthorizationCodeGrantHandler`, `DefaultTokenService`, `DefaultUserInfoEndpointProvider`,
  `GetUserInfoClaimsCommand`, `IPersistedGrantService`.
- Prior art: [ADR-0030](0030-userinfo-endpoint-and-subject-authentication-seam.md) (UserInfo enricher seam),
  [ADR-0028](0028-token-revocation-refresh-tokens-only.md) (persisted grants).
- Spec: [OIDC Core 5.5 — Requesting Claims using the "claims" Request Parameter](https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter).
