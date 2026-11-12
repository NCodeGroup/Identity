# 1089. A pushed authorization request is a single-use, client-bound grant expanded before anything is trusted

- **Status:** Accepted
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

The authorization endpoint takes a request as front-channel parameters: query-string values (GET) or form values (POST),
optionally bundled into a signed request object passed by value (`request`, JAR) or by reference (`request_uri` fetched
over HTTPS, RFC 9101). Front-channel parameters travel through the user agent, so they are visible in browser history,
server logs and `Referer` headers, and, absent a signed request object, are not integrity-protected: anyone who can
observe or tamper with the redirect can read or alter the request. Large requests (rich `claims`, many `scope` or
`resource` values, `authorization_details`) also bump against URL length limits.

[RFC 9126 (OAuth 2.0 Pushed Authorization Requests)](https://www.rfc-editor.org/rfc/rfc9126) closes this gap: the client
pushes its authorization request directly to the authorization server over an authenticated back channel before it
redirects the user agent, and receives an opaque one-time `request_uri` that it hands to the authorization endpoint. The
request parameters never traverse the front channel, the push is client-authenticated, and the server controls the
reference. The OAuth 2.1 direction and the Security BCP ([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700)) steer
interactive clients toward it.

Two mechanisms use the `request_uri` authorization parameter and must not be conflated. A JAR `request_uri` (RFC 9101) is
an HTTPS URL that the server fetches to obtain a signed request-object JWT. A PAR `request_uri` (RFC 9126) is an opaque
reference, `urn:ietf:params:oauth:request_uri:<reference>`, that the server resolves from its own storage.

The authorization endpoint decides whether an error may be redirected to the client from `state`, `response_mode` and
`redirect_uri`. Those values must already be the pushed ones when that decision is made, and a reference that cannot be
resolved leaves no `redirect_uri` to trust.

## Decision

**The pushed authorization request endpoint client-authenticates the push, validates the request exactly as the
authorization endpoint does, persists it as a single-use grant bound to the pushing client and returns an opaque
`request_uri`; the authorization endpoint expands that `request_uri` back into the original parameters before it trusts
any of them.**

- **Endpoint shape.** `/oauth2/par` is a thin endpoint provider, in the shape [ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md)
  gives protocol endpoints. It requires `POST` with `application/x-www-form-urlencoded` and authenticates the client
  through `IClientAuthenticationService`, so confidential clients authenticate as at the token endpoint and public clients
  are identified by `client_id`. It rejects a pushed request that itself carries a `request_uri` (RFC 9126 section 2.1).
  It reuses the authorization endpoint's own `LoadAuthorizationRequestCommand` and `ValidateAuthorizationRequestCommand`,
  with their response-delivery, redirect-URI and PKCE checks, so the validation at push time is the authorization
  endpoint's, never a parallel copy that could drift. On success it answers `201 Created` with
  `{ request_uri, expires_in }`; every validation refusal is answered `400`.
- **Storage as a single-use grant.** The pushed parameters are stored through `IPersistedGrantService` under the
  `pushed_authorization_request` grant type, keyed by `CryptoService.GenerateUrlSafeKey()` and carrying the pushing
  client's `ClientId`. The issued `request_uri` is `urn:ietf:params:oauth:request_uri:` followed by that key. The
  authorization endpoint consumes the grant with `ConsumeOnceOrDefault`, so a replayed `request_uri` is refused. The
  lifetime is the `pushed_authorization_request_lifetime` setting, which defaults to 90 seconds: long enough for the
  immediate redirect, short enough to bound exposure.
- **Secrets are never stored.** `client_secret`, `client_assertion` and `client_assertion_type` are stripped from the
  parameters before storage. They authenticated the push and have no role at the authorization endpoint.
- **Expansion before delivery is derived.** The authorization endpoint handler expands a PAR `request_uri` right after it
  loads the raw request values and before it derives the response delivery, because delivery reads `state`,
  `response_mode` and `redirect_uri`.
- **Client binding and non-redirectable failures.** The authenticated `client_id` at the authorization endpoint must
  match the grant's stored `ClientId`. A failed lookup or a mismatch is `invalid_request_uri`, answered directly as `400`
  and never redirected, because the server could not recover the request and so has no `redirect_uri` it can trust.
- **`require_pushed_authorization_requests`.** A boolean setting on tenants and clients, default `false`, that is
  discoverable. When it is set, a direct authorization request that does not carry a pushed `request_uri` is refused with
  `invalid_request`.
- **Discovery.** The endpoint is advertised as `pushed_authorization_request_endpoint`. A `request_uri` without the
  `urn:ietf:params:oauth:request_uri:` prefix is a JAR reference, fetched over HTTPS.

## Options considered

- **Expand inside `LoadAuthorizationRequestCommand`.** Rejected: the response-delivery resolver runs on the raw request
  values and would see an empty request, so it could not decide whether an error may be redirected.
- **Expand in the endpoint handler before delivery is derived.** Chosen: the stored parameters are in place when the
  redirect decision is made, and the lookup failures that follow are answered directly.

## Consequences

- Clients have an integrity-protected, client-authenticated back-channel way to submit authorization requests, with no
  front-channel exposure of the parameters and no URL-length ceiling.
- The two `request_uri` semantics stay unambiguous through the `urn:ietf:params:oauth:request_uri:` prefix.
- A pushed request is single-use and short-lived. A deployment that needs a longer window raises
  `pushed_authorization_request_lifetime`, and one that wants to force the flow sets `require_pushed_authorization_requests`.
- The grant store holds one short-lived row per pushed request. Rows expire by their lifetime and are consumed on first
  use, so they do not accumulate.

## References

- [RFC 9126, OAuth 2.0 Pushed Authorization Requests](https://www.rfc-editor.org/rfc/rfc9126)
- [RFC 9101, JWT-Secured Authorization Request](https://www.rfc-editor.org/rfc/rfc9101)
- [RFC 9700, OAuth 2.0 Security Best Current Practice](https://www.rfc-editor.org/rfc/rfc9700)
- [ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md)
