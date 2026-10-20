# 101. OAuth 2.0 Token Exchange (RFC 8693) over the extension-grant seam, self-issued-token only

- **Status:** Accepted (amended by
  [ADR-0167](0167-the-server-validates-the-tokens-it-issued-under-one-policy.md): a subject token is validated under the
  policy its type names, and a `jwt` subject token must be an access token or an ID token)
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

[OAuth 2.0 Token Exchange (RFC 8693)](https://datatracker.ietf.org/doc/html/rfc8693) lets a client present a
`subject_token` (and, for delegation, an `actor_token`) to the token endpoint and receive a newly minted token for a
different audience/scope. It underpins service-to-service **API chaining** and the **delegation vs impersonation**
distinction: an _impersonation_ token simply says "Alice"; a _delegation_ token says "the calendar app, acting for
Alice" by carrying an `act` claim, so a resource server can audit and constrain what the actor does on the subject's
behalf.

The token endpoint already dispatches to any registered `ITokenGrantHandler` whose `GrantTypes` includes the request's
`grant_type` (the RFC 6749 §4.5 extension-grant seam), so no endpoint change is needed. The subject-resolution,
token-minting, scope/audience, and proof-of-possession machinery all already exist (authorization-code, refresh-token,
CIBA, device-code, client-credentials, and password grants are built on them).

The open design question was the **trust boundary** of the presented tokens. The mature, _supported_ implementations in
the market restrict `subject_token` to tokens the authorization server itself issued (Keycloak's Standard Token Exchange
V2 accepts only self-issued access tokens; its delegation is still preview). Accepting tokens minted by **external /
enterprise identity providers** (EntraID, Ping, Okta, ADFS/SAML) is a separate, much larger capability — an
identity-brokering / enterprise-connections subsystem with per-tenant trusted-issuer configuration, JWKS discovery and
caching, and (for SAML) an XML assertion stack — that other platforms also treat as a distinct feature (Keycloak's
identity brokering, Auth0's Custom Token Exchange).

## Decision

**Implement token exchange as a `DefaultTokenExchangeGrantHandler : ITokenGrantHandler` for
`urn:ietf:params:oauth:grant-type:token-exchange`, supporting both impersonation and delegation (`act` / `may_act`
chains) with `access_token` / `refresh_token` / `id_token` outputs, and scope the presented-token trust boundary to
self-issued tokens only — behind a replaceable `ISubjectTokenValidator` seam so a future enterprise-connections epic can
add external issuers without reworking the handler.**

Concretely:

- **Extension-grant seam, no endpoint change.** The handler advertises `GrantTypes = { token-exchange }`; the token
  endpoint routes to it. The grant is **not** in the baseline `grant_types_supported` default — like
  `client_credentials` and `password`, it is a powerful grant an operator opts into (secure-by-default,
  [ADR-0083](0083-secure-by-default-discovery-metadata.md)); the `grant_types_supported` gate plus per-client grant
  enablement authorize its use.
- **Self-issued tokens only, behind a validator seam.** `ISubjectTokenValidator` validates a presented `subject_token`
  or `actor_token` and resolves it to a `ClaimsPrincipal`. The default `DefaultSubjectTokenValidator` accepts only
  self-issued JWTs — validated against the resolved tenant's own signing keys, issuer, and lifetime — and rejects any
  other issuer. Accepted `subject_token_type` / `actor_token_type` values are the JWT token-type identifiers
  (`access_token`, `id_token`, `jwt`). External/enterprise-IdP tokens are explicitly **out of scope** and tracked as a
  dedicated backlog epic; the seam exists so that epic can register additional validators (OIDC-brokered, SAML) without
  touching the grant handler.
- **Requester entitlement.** The requesting client must be **confidential**, and the presented token must have been
  issued **to that client** (`client_id` claim match) or **name it in `aud`**. This prevents a client from exchanging a
  token that was never intended for it, matching RFC 8693 intent and Keycloak's rule.
- **Impersonation vs delegation.** When no `actor_token` is present the exchange is impersonation (the issued token's
  subject is the subject token's subject). When an `actor_token` is present it is delegation: the actor token is
  validated and must also be issued to / name the requester; the subject token must **pre-authorize** the actor via a
  `may_act` claim (RFC 8693 §4.4) whose `sub` equals the actor's `sub` (and whose `client_id` / `iss`, when present,
  match); the subject and actor must differ; and the issued token carries an `act` claim identifying the actor, nesting
  any pre-existing `act` from the subject token to preserve the delegation chain (RFC 8693 §4.1).
- **Subject resolution is forward, never by reversing a projected `sub`.** The subject principal is resolved from the
  validated token's claims through the existing `IPrincipalResolver`
  ([ADR-0094](0094-resolve-principal-at-every-authentication-entry-point.md)); an unresolvable subject is rejected. For
  the default public subject type the token's `sub` _is_ the server-owned principal id and resolves directly. A
  pairwise-projected `sub` ([ADR-0093](0093-pairwise-subject-identifiers.md)) is intentionally **not** reversed; forward
  recovery of a pairwise subject (e.g. from `sid` + session state, the [ADR-0096](0096-end-session-subject-from-server-state.md)
  technique) is a documented follow-up rather than a silent mis-resolution.
- **Outputs and downscoping.** `requested_token_type` selects the output: `access_token` (default), `refresh_token`
  (which additionally returns an access token and is gated per client by the `token_exchange_allow_refresh_token`
  setting, matching Keycloak), or `id_token` (returned in the `access_token` field with `token_type` = `N_A`, per
  RFC 8693 §2.2.1). The response carries `issued_token_type`. Effective scopes are the requested scopes (already
  validated against the client's allowed scopes) or, when omitted, the subject token's scopes **intersected with the
  client's allowed scopes** — an exchange can never widen the requester's authority. The RFC 8707 `resource` and the
  RFC 8693 `audience` parameters both narrow the issued token's audience through the existing resource-server resolver.
- **Policy seam.** The spec-mandated authorization (confidential-client, requester entitlement, `may_act`) lives in the
  grant handler because it needs the validated token payloads; a `ValidateTokenGrantCommand<TokenExchangeGrant>`
  mediator seam lets a host layer additional per-client exchange policy.

## Consequences

- The OP can mint delegation/impersonation tokens for service-to-service chaining today, using only tokens it issued,
  with spec-correct `act` / `may_act` handling and secure-by-default opt-in.
- Enterprise/external-IdP federation (OIDC brokering, SAML/WS-Fed, trusted-issuer configuration) is a separate epic;
  the `ISubjectTokenValidator` seam is the extension point it will build on, so no token-exchange rework is required
  when it lands.
- Pairwise-subject token exchange is limited until forward `sid`-based recovery is added; the default (public) subject
  type is unaffected.
