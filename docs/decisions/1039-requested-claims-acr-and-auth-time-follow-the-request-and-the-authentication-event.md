# 1039. Requested claims, acr and auth_time follow the request and the authentication event

- **Status:** Accepted
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

The OpenID Connect `claims` request parameter ([OIDC Core 5.5](https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter))
lets a client request individual claims, with optional `essential`, `value` and `values` constraints, for the ID Token
(`claims.id_token`) and the UserInfo endpoint (`claims.userinfo`), additive to the claims a scope implies. The ID Token
is minted during token issuance, where the authorization request is still in hand. The UserInfo endpoint is reached
later with only an access token, so honoring `claims.userinfo` needs the requested claims to survive to that call.

Two authentication-context claims depend on what happened at authentication. `acr` is an opaque string, agreed between
the OP and the relying party, that names the authentication context class a login satisfied. OpenID Connect Core 1.0 §2
defers its meaning and its set of values to the deployment; the conventions are the `0` value and the PAPE policy URNs
such as `http://schemas.openid.net/pape/policies/2007/06/multi-factor`. `amr` (RFC 8176) lists the methods performed,
and only the authenticating component knows them. `auth_time` is mandatory in the ID token only when a `max_age`
request is made or `auth_time` is requested as an essential claim; otherwise it is optional. RFC 9068 includes it in the
access token only when available. The session ([ADR-1074](1074-session-management-and-logout.md)) already carries
`auth_time`, `amr` and `idp` on the `SessionGrant`, so the authentication context extends it.

## Decision

**Honor the `claims` request parameter end to end, record the authentication context (`amr` and `acr`) where
authentication happens, and emit `auth_time` when it is known or required.**

- **The parameter.** The `claims` parameter is parsed into `IRequestClaims`, exposed as `IAuthorizationRequest.Claims`
  and persisted with the authorization code. `claims_parameter_supported` applies to tenants and clients, defaults to
  `true`, merges with `And` (a tenant or client may opt out but cannot enable beyond the server) and is advertised.
- **ID Token (`claims.id_token`).** `CreateSecurityTokenRequest` carries the request's `IRequestClaims`, and
  `DefaultGetIdTokenSubjectClaimsHandler` adds the requested claims on top of the scope-driven claims.
- **UserInfo (`claims.userinfo`).** The requested claims are persisted as a `userinfo_claims` grant
  (`OpenIdConstants.PersistedGrantTypes.UserInfoClaims`) keyed by the access token's `jti`, with the token's lifetime
  and `sid`. The UserInfo endpoint resolves the grant by the validated access token's `jti` and adds the claims on top
  of the scope-driven claims. The access token gains no claim.
- **`value`, `values` and `essential`.** `value` and `values` are advisory: the OP returns the subject's actual claim
  and never fabricates one, and a host may impose stricter semantics with a higher-priority claims handler. `essential`
  is best-effort: a missing essential claim does not fail issuance or the UserInfo response. The exception is an
  essential `id_token.acr` request with `values` that the principal's `acr` does not satisfy:
  `DefaultValidateSubjectAuthenticationHandler` refuses it with `access_denied`. The `acr_values` parameter stays
  voluntary.
- **`amr` is recorded where authentication happens.** The local-account interactive verifier and the password grant
  stamp `amr=["pwd"]` (RFC 8176) on the principal.
- **`acr` is resolved at authentication.** The replaceable, asynchronous `IAuthenticationContextReferenceResolver`
  (`TryAddSingleton`) maps the satisfied methods, the `idp` and the effective settings to an `acr` value. The default
  returns the PAPE multi-factor URN when the methods show several factors, else the `default_acr` setting (tenants,
  `Replace`), and omits `acr` when that is unset. The resolver is asynchronous so a replacement can perform IO.
- **The context is sticky.** The resolved `acr` is stamped on the principal and on
  `SessionGrant.AuthenticationContextClassReference`, and is emitted as a subject claim in the ID token and the access
  token (RFC 9068), alongside `amr`.
- **`auth_time`.** `always_include_auth_time` (clients, `And`, default `true`, not advertised) governs whether the ID
  token carries `auth_time` whenever it is known. `auth_time` is always included when required: a `max_age` request
  (`AuthTimeRequired`, set by the authorization ticket handler) or an essential `auth_time` claim request.
  `EnsureAuthTime` synthesizes `auth_time` only for the ID token and only when it is required and missing. The access
  token carries `auth_time` only when the principal has it.

## Options considered

- **Embed the requested claims in the access token.** Rejected: it bloats a token presented to every resource server
  and leaks request intent broadly.
- **Reuse the refresh-token grant.** Rejected: a refresh token is issued only with `offline_access`, so it cannot carry
  UserInfo state for every access token.
- **Hard-code an `acr` level at login.** Rejected: it fabricates a deployment policy that the specification defers to
  the OP and that a deployment could not override.
- **Resolve `acr` lazily at issuance from `amr`.** Rejected: `acr` is a property of the authentication event, like
  `auth_time`, so recording it at authentication and storing it on the session is consistent with `amr` and `idp`.
- **A synchronous resolver.** Rejected: a real policy may need a database or external lookup.
- **Unconditional `auth_time`, or a conditional one with no toggle.** Rejected: the first ignores the specification's
  conditional contract and RFC 9068's guidance for the access token, and the second removes a widely expected default.
- **Chosen:** the claims threaded to the ID token in process and to UserInfo through a short-lived grant, the `acr`
  resolver seam with an honest default, and a default-`true` `auth_time` toggle plus an always-honored required case.

## Consequences

- Clients request specific claims and receive them in the ID Token and from UserInfo, additive to scopes.
- Issuing an access token for a request with `claims.userinfo` writes one short-lived grant, and UserInfo performs one
  indexed lookup by `jti`. A request without `claims.userinfo` writes and reads nothing. The access token and its size
  are unchanged.
- A host that needs strict `value`, `values` or `essential` enforcement registers a higher-priority claims handler.
- `acr` and `amr` are emitted and stable. The `acr` policy is an asynchronous seam that configuration, a replacement
  resolver or a custom verifier recording its own `amr` can own. The session stores the context as an additive JSON
  property, so it needs no migration.
- The default resolver omits `acr` for a single-factor password login unless `default_acr` is set; a deployment that
  wants an always-present `acr` configures it or replaces the resolver.
- `auth_time` follows OpenID Connect Core and RFC 9068.
- Tests cover the ID Token and UserInfo claim paths, the resolver, the essential `acr` refusal and the `auth_time`
  decisions.

## References

- Code: `IRequestClaims`, `CreateSecurityTokenRequest`, `DefaultGetIdTokenSubjectClaimsHandler`,
  `DefaultAuthorizationCodeGrantHandler`, `DefaultTokenService`, `DefaultUserInfoEndpointProvider`,
  `GetUserInfoClaimsCommand`, `IPersistedGrantService`, `IAuthenticationContextReferenceResolver`,
  `DefaultValidateSubjectAuthenticationHandler`, `SessionGrant`.
- Issues: [#49](https://github.com/NCodeGroup/identity/issues/49), [#57](https://github.com/NCodeGroup/identity/issues/57).
- Records: [ADR-1030](1030-userinfo-endpoint-and-subject-authentication-seam.md) (the UserInfo enricher seam),
  [ADR-1028](1028-the-revocation-endpoint-revokes-refresh-tokens-and-their-families-and-always-answers-200.md)
  (persisted grants), [ADR-1074](1074-session-management-and-logout.md) (the session),
  [ADR-1220](1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) (the session's lifetime).
- Specifications: [OIDC Core 5.5](https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter), OpenID
  Connect Core 1.0 §2 and §5.5.1.1, RFC 8176, RFC 9068.
