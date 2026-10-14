# 92. Token-endpoint `401` + `WWW-Authenticate` challenge and the response-header seam

- **Status:** Accepted
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

[RFC 6749 §5.2](https://datatracker.ietf.org/doc/html/rfc6749#section-5.2) requires that when client authentication
fails **and the client attempted to authenticate via the HTTP `Authorization` request header**, the authorization
server respond with `401 Unauthorized` and include a `WWW-Authenticate` response header whose scheme matches the one the
client used. Collapsing every client-authentication failure to `400 Bad Request` (as a single status for all schemes
does) violates that MUST and is a conformance gap for the token endpoint and every other client-authenticating endpoint
(introspection, revocation, device-authorization, CIBA, PAR).

The failure is produced deep in the client-authentication layer (`IClientAuthenticationHandler`), flows back through
`ClientAuthenticationResult` as an `IOpenIdError`, and is rendered by `OpenIdResult<T>`. An `IOpenIdError` could already
carry an HTTP **status code** (`ISupportStatusCode`) but had no way to carry a **response header**, so a challenge could
not survive the trip from the handler to the rendered response.

Which schemes warrant the challenge is settled by the spec, not a product choice: only `client_secret_basic` carries
its credentials in the `Authorization` header, so only it must answer with `401` + `WWW-Authenticate: Basic`. Body-based
methods (`client_secret_post`) and assertion-based methods stay `400` — the spec's `401` requirement is scoped to the
header case.

## Decision

**Model response headers as a first-class capability of an `IOpenIdError` (a new `ISupportResponseHeaders` seam that
`OpenIdResult<T>` honors), and let each client-authentication handler decide the challenge for its own scheme — the
`client_secret_basic` handler emits `401` + `WWW-Authenticate: Basic realm="<issuer>"`, everything else stays `400`.**

Concretely:

- **`ISupportResponseHeaders` seam.** A new `ISupportResponseHeaders` interface (sibling of `ISupportStatusCode` in
  `NCode.Identity.AspNetCore.Abstractions`) exposes a lazily-allocated response-header collection plus a
  `SetResponseHeader` mutator. `IOpenIdError` extends it, so any error can carry protocol headers; `OpenIdResult<T>`
  writes them before rendering the JSON body. This is a general seam, not a one-off — the same mechanism carries the
  `WWW-Authenticate: Bearer` challenge that sender-constrained/DPoP access tokens will need (backlog `F7`).

- **Scheme-local challenge via a virtual factory.** `CommonClientAuthenticationHandler` funnels every `invalid_client`
  outcome through a `CreateInvalidClientResult` virtual (default `400`). `ClientSecretBasicClientAuthenticationHandler`
  overrides it to set `401` and a `WWW-Authenticate: Basic realm="<tenant issuer>"` challenge. Because the endpoints
  only ever default an **unset** status (`error.StatusCode ??= 400`), the handler's `401` and header survive unchanged
  through every client-authenticating endpoint with no per-endpoint code.

- **The grant-selection pipeline is the extension-grant seam.** Extension grants
  ([RFC 6749 §4.5](https://datatracker.ietf.org/doc/html/rfc6749#section-4.5)) need no token-endpoint change: an
  `ITokenGrantHandler` whose `GrantTypes` includes the extension grant's absolute-URI identifier is routed to by
  `DefaultSelectTokenGrantHandlerHandler` exactly like a built-in grant.

## Consequences

- The token endpoint (and introspection/revocation/device/CIBA/PAR, which share the `IClientAuthenticationService`
  funnel) now answer a failed `client_secret_basic` authentication with the spec-required `401` + `WWW-Authenticate`
  while keeping `400` for body/assertion-based methods.
- `IOpenIdError` gaining `ISupportResponseHeaders` is a public-surface addition; `OpenIdResult<T>` writing those headers
  is the single rendering funnel, so no endpoint re-implements header emission.
- Fixing the challenge required correcting the Basic credential decoder: RFC 7617 uses **standard** Base64 (padded),
  not Base64Url, and the decoder's byte-count was computed with a Base64Url helper and validated with a `Debug.Assert`
  — which is compiled out in Release, silently accepting a mis-sized buffer for any credential whose length is not a
  multiple of three. The decoder now sizes an upper-bound buffer, uses the actual decoded length, and treats a decode
  failure of the untrusted header as a failed authentication (`invalid_client`), never an assertion.
