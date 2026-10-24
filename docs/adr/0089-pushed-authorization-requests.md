# 89. Pushed Authorization Requests (PAR, RFC 9126)

- **Status:** Accepted
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

The authorization endpoint accepted the request only as front-channel parameters: query-string values (GET) or form
values (POST), optionally bundled into a signed request object passed by value (`request`, JAR) or by reference
(`request_uri` fetched over HTTPS, RFC 9101). Front-channel parameters travel through the user agent, so they are
visible in browser history, server logs, and `Referer` headers, and — absent a signed request object — are not
integrity-protected: anyone who can observe or tamper with the redirect can read or alter the request. Large requests
(rich `claims`, many `scope`/`resource` values, `authorization_details`) also bump against URL length limits.

[RFC 9126 — OAuth 2.0 Pushed Authorization Requests (PAR)](https://www.rfc-editor.org/rfc/rfc9126) closes this gap: the
client **pushes** its authorization request directly to the authorization server over an authenticated back channel
**before** redirecting the user agent, and receives an opaque one-time `request_uri` it then hands to the authorization
endpoint. The request parameters never traverse the front channel, the push is client-authenticated, and the server
controls (and can revoke) the reference. PAR is also a hard prerequisite for the **FAPI** security profiles (backlog
`F16`) and is the foundation the OAuth 2.1 direction and the Security BCP ([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700))
steer interactive clients toward.

Two different mechanisms now use the `request_uri` authorization parameter, and they must not be conflated: a JAR
`request_uri` (RFC 9101) is an **HTTPS URL** the server fetches to obtain a signed request-object JWT, whereas a PAR
`request_uri` (RFC 9126) is an **opaque reference** (`urn:ietf:params:oauth:request_uri:<reference>`) the server
resolves from its own storage. They are distinguished by the `urn:ietf:params:oauth:request_uri:` prefix.

## Decision

**Add a `pushed_authorization_request_endpoint` (`/oauth2/par`) that client-authenticates the push, validates the
request exactly as the authorization endpoint would, persists it as a single-use grant, and returns an opaque
`request_uri`; expand that `request_uri` back into the original parameters at the authorization endpoint, before any
parameter is trusted.**

Concretely:

- **Endpoint shape.** `/oauth2/par` is a thin `IEndpointProvider` following the same shape as the device-authorization
  and CIBA endpoints: it requires `POST` with `application/x-www-form-urlencoded`, authenticates the client through the
  shared `IClientAuthenticationService` (so confidential clients authenticate identically to the token endpoint and
  public clients are identified by `client_id`), then dispatches. It **rejects** a pushed request that itself carries a
  `request_uri` parameter (RFC 9126 §2.1). It reuses the authorization endpoint's own
  `LoadAuthorizationRequestCommand` + `ValidateAuthorizationRequestCommand` so the PAR-time validation is the authorize
  endpoint's static validation — not a parallel copy that could drift. On success it answers `201 Created` with
  `{ request_uri, expires_in }`.

- **Storage as a single-use grant.** The pushed parameters are persisted through the existing `IPersistedGrantService`
  under the `pushed_authorization_request` grant type, keyed by a high-entropy reference
  (`CryptoService.GenerateUrlSafeKey()`), with the issued `request_uri` being the
  `urn:ietf:params:oauth:request_uri:` prefix followed by that reference. The grant carries the pushing client's
  `ClientId`. It is consumed with `ConsumeOnceOrDefault` at the authorization endpoint, so a pushed request is
  **single-use**: a replayed `request_uri` is rejected. The lifetime is the per-client/tenant
  `pushed_authorization_request_lifetime` setting (default **90 seconds** — long enough for the immediate redirect,
  short enough to bound exposure).

- **Secrets are never persisted.** Client-authentication material that rides the push form (`client_secret`,
  `client_assertion`, `client_assertion_type`) is stripped before the parameters are stored, so it is never written to
  the grant store and never replayed. It authenticated the push itself and has no role at the authorization endpoint.

- **Expansion at the authorization endpoint, before `redirect_uri` is trusted.** The authorization endpoint resolves a
  PAR `request_uri` into the stored parameters **in the endpoint handler, immediately after loading the raw request
  values and before the response delivery is derived**. This placement is deliberate: the delivery resolver reads
  `state`, `response_mode`, and `redirect_uri` from the request values to decide whether an error may be redirected to
  the client, so the stored parameters must already be in place when that decision is made. Expanding later (inside
  `LoadAuthorizationRequestCommand`) would leave the delivery resolver looking at an empty request.

- **Client binding and non-redirectable failures.** A PAR `request_uri` is bound to the client that pushed it: the
  authenticated `client_id` at the authorization endpoint must match the grant's stored `ClientId`, else the request is
  rejected with `invalid_request_uri`. Because a failed `request_uri` lookup means the server could not recover the
  request (and therefore cannot trust any `redirect_uri` to send an error to), these failures are returned **directly
  as HTTP 400**, never redirected.

- **`require_pushed_authorization_requests` policy.** A new per-client/tenant boolean setting (secure opt-in, default
  `false`, advertised in discovery per RFC 9126 §5) makes PAR mandatory for a client: when set, a direct authorization
  request that does **not** carry a pushed `request_uri` is rejected with `invalid_request`.

- **Discovery.** The endpoint is auto-advertised as `pushed_authorization_request_endpoint` through the standard
  endpoint-name discovery mechanism; `require_pushed_authorization_requests` is advertised as a discoverable setting.

## Consequences

- Clients gain an integrity-protected, client-authenticated, back-channel way to submit authorization requests, with no
  front-channel exposure of the parameters and no URL-length ceiling. This is the FAPI/OAuth 2.1-aligned default for
  sensitive interactive clients and unblocks the FAPI readiness epic (`F16`).
- Two `request_uri` semantics now coexist; they are kept unambiguous by the `urn:ietf:params:oauth:request_uri:`
  prefix. A JAR `request_uri` without that prefix continues to be fetched over HTTPS as before.
- The pushed request is intentionally **single-use and short-lived**; a deployment that needs a longer window raises
  `pushed_authorization_request_lifetime` (a per-client ceiling), and one that wants to force the flow sets
  `require_pushed_authorization_requests`.
- The grant store sees one short-lived row per pushed request. These expire by their lifetime and are consumed on first
  use, so they do not accumulate.
