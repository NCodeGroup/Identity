# 1101. Token exchange issues delegation and impersonation tokens from tokens the tenant issued

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

[OAuth 2.0 Token Exchange (RFC 8693)](https://datatracker.ietf.org/doc/html/rfc8693) lets a client present a
`subject_token` and, for delegation, an `actor_token` to the token endpoint and receive a token minted for a different
audience or scope. It underpins service-to-service API chaining and the difference between impersonation and
delegation: an impersonation token says "Alice", while a delegation token says "the calendar app, acting for Alice" by
carrying an `act` claim, so a resource server can audit and constrain what the actor does on the subject's behalf.

The token endpoint dispatches to any registered `ITokenGrantHandler` whose `GrantTypes` includes the request's
`grant_type`, the RFC 6749 section 4.5 extension-grant seam, so the grant needs no endpoint change. Subject resolution,
token minting, scope and audience resolution and proof of possession already exist, because the authorization-code,
refresh-token, CIBA, device-code, client-credentials and password grants are built on them.

The open question is the trust boundary of the presented tokens. Mature implementations restrict `subject_token` to
tokens the authorization server itself issued. Accepting tokens minted by external or enterprise identity providers is
a separate, much larger capability: identity brokering, with per-tenant trusted-issuer configuration, discovery and
caching of keys, and for SAML an XML assertion stack.

## Decision

**`DefaultTokenExchangeGrantHandler` serves `urn:ietf:params:oauth:grant-type:token-exchange` for both impersonation and
delegation, and accepts only tokens the tenant issued, validated through the replaceable `ISubjectTokenValidator`.**

- **Extension-grant seam.** The handler advertises `GrantTypes = { token-exchange }` and the token endpoint routes to
  it with no endpoint change. The grant is not in the default `grant_types_supported`: like `client_credentials` and
  `password` it is a powerful grant an operator opts into, which is the secure baseline of
  [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md).
- **Tokens the tenant issued, behind a validator seam.** `ISubjectTokenValidator`, whose default is
  `DefaultSubjectTokenValidator`, validates `subject_token` and `actor_token` through `IIssuedTokenValidator` and
  resolves each to a `ClaimsPrincipal`. It accepts a token of type `access_token` under the access-token policy, of
  type `id_token` under the ID-token policy, and of type `jwt` only when it validates as one of those two. Any other
  type is `invalid_request`. An external issuer is added by registering another validator; the core accepts none.
- **Requester entitlement.** The requester must be a confidential client, and the presented token must be issued to it
  (`client_id`) or name it in `aud`, so a client cannot exchange a token never meant for it.
- **Impersonation and delegation.** Without an `actor_token` the exchange is impersonation. With one it is delegation:
  the actor's token is validated and must belong to the requester, the subject token's `may_act.sub` must equal the
  actor's `sub` (with `client_id` and `iss` matching when present), the subject and the actor must differ, and the
  issued token carries `act`, nesting any `act` the subject token already holds.
- **Forward subject resolution.** The subject is resolved from the validated claims forward through
  `IPrincipalResolver`, never by reversing a pairwise `sub`. It passes subject validation
  (`DefaultValidateTokenExchangeGrantHandler`, [ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md))
  and carries its tenant in `AuthenticationProperties` (`OpenIdConstants.AuthenticationPropertyItems.TenantId`). The
  actor is not subject-validated.
- **Outputs.** `requested_token_type` defaults to `access_token`. `refresh_token` also returns an access token, is
  gated per client by `token_exchange_allow_refresh_token` and starts its family at the subject token's `auth_time`
  ([ADR-1297](1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md)).
  `id_token` is returned in `access_token` with `token_type` `N_A`, per RFC 8693 section 2.2.1. The response carries
  `issued_token_type`.
- **Scopes and audience.** The effective scopes are the requested, validated scopes, or else the subject token's scopes
  intersected with the client's allowed scopes, so an exchange never widens the requester's authority. `resource` and
  `audience` narrow the issued audience through the resource-server resolver.
- **Policy seam.** `ValidateTokenGrantCommand<TokenExchangeGrant>` lets a host add exchange policy per client.
- **Encrypted tokens.** A token encrypted to a resource server's own key cannot be exchanged.

## Options considered

- **Accept external or enterprise IdP tokens in the core.** Rejected: it is a separate brokering capability with its
  own trusted-issuer configuration, key discovery and assertion formats, and it would widen the trust boundary of every
  tenant by default.
- **Tokens the tenant issued, behind a replaceable validator.** Chosen: it matches mature implementations, keeps the
  trust boundary to keys the tenant owns, and leaves registration of another validator as the extension point.

## Consequences

- The OP mints delegation and impersonation tokens for service-to-service chaining from the tokens it issued, with
  spec-correct `act` and `may_act` handling and an opt-in grant.
- An external issuer is added by registering an `ISubjectTokenValidator`, with no change to the grant handler.
- A pairwise `sub` is never reversed, so an exchange whose subject token carries one resolves only when
  `IPrincipalResolver` can resolve it forward; the default public subject type resolves directly.
- Tests cover each accepted and rejected token type, the requester entitlement, the `may_act` and `act` rules, the
  three requested token types and the scope intersection.

## References

- [RFC 8693](https://datatracker.ietf.org/doc/html/rfc8693), [RFC 8707](https://datatracker.ietf.org/doc/html/rfc8707)
  and [RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749) section 4.5.
- `DefaultTokenExchangeGrantHandler`, `ISubjectTokenValidator`, `DefaultSubjectTokenValidator`,
  `IIssuedTokenValidator`, `DefaultValidateTokenExchangeGrantHandler`, `ValidateTokenGrantCommand<TokenExchangeGrant>`.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md),
  [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md),
  [ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md),
  [ADR-1297](1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md).
