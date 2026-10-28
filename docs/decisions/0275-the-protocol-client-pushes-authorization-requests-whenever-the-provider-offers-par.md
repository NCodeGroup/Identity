# 275. The protocol client pushes authorization requests whenever the provider offers PAR

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md) §5.1, issue #123) sends its authorization requests
through Pushed Authorization Requests (PAR) when the provider advertises `pushed_authorization_request_endpoint`, and
never falls back to the front channel when PAR is required.
[ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) left PAR to phase 1b. The
NCode OP implements PAR ([ADR-0089](0089-pushed-authorization-requests.md)). This ADR
records `IAuthorizationRequestClient` in `NCode.Identity.OpenId.Client`, which turns an authorization-code + PKCE `S256`
request into the URL the browser is redirected to, under RFC 9126.

## Decision

**The protocol client has one entry point, `IAuthorizationRequestClient.CreateAuthorizationUriAsync`, that decides the
channel: it pushes the request whenever the provider advertises a PAR endpoint, requires PAR when the provider or the
client registration demands it, and returns a PAR endpoint's OAuth error as data.**

1. **One entry point that decides the channel.** `CreateAuthorizationUriAsync(registration, request, ct)` returns the
   authorization URL. It pushes whenever the provider advertises `pushed_authorization_request_endpoint` and never falls
   back to the front channel when PAR is required, so a caller cannot skip PAR. There is no public push-only API.
2. **PAR is required** when the provider advertises `require_pushed_authorization_requests` (RFC 9126 §5) or
   `ClientRegistration.RequirePushedAuthorizationRequests` is `true`, which mirrors the registered client metadata of
   the same name (RFC 9126 §6), as `BackchannelLogoutSessionRequired` mirrors its metadata
   ([ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md)). Required but no PAR endpoint
   throws `OpenIdProviderException` and nothing is sent.
3. **Code flow and PKCE `S256` only** (RFC 9700). The client always writes `response_type=code` and
   `code_challenge_method=S256`. The caller passes the `code_verifier` in `AuthorizationRequest` and the client derives
   the challenge, `BASE64URL(SHA-256(ASCII(verifier)))` (RFC 7636 §4.2), so a caller cannot send `plain` or a
   mismatched challenge. The verifier is validated at construction: 43 to 128 characters of `[A-Za-z0-9-._~]`
   (RFC 7636 §4.1). A provider that advertises `code_challenge_methods_supported` without `S256` throws
   `OpenIdProviderException`; an absent list is accepted.
4. **The caller mints `state` and `nonce`.** The BFF stores its transaction under a hash of `state` (design §5.2), so it
   owns the values and the client only writes them.
5. **Open extra parameters.** `AuthorizationRequest.AdditionalParameters` carries `prompt`, `max_age`, `login_hint`,
   `dpop_jkt` and any other extension parameter, so the vocabulary is open
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). Names the client writes itself or that would
   break the flow are rejected with `ArgumentException` at `init`: `response_type`, `client_id`, `redirect_uri`, `state`,
   `nonce`, `scope`, `resource`, `response_mode`, `code_challenge`, `code_challenge_method`, `request_uri`,
   `client_secret`, `client_assertion` and `client_assertion_type`. Empty names are rejected too. The dictionary is
   copied with ordinal comparison.
6. **Parameter order**, in the front channel and the push body alike: `response_type`, `client_id` (front channel only,
   see point 7), `redirect_uri`, `state`, `code_challenge`, `code_challenge_method`, then `nonce`, `scope` and
   `response_mode` when set, each `resource`, then each additional parameter.
7. **`client_id` appears exactly once in the push.** RFC 9126 §2.1 sends every authorization request parameter,
   `client_id` included, in the push body. The `ClientRequestSender.PostFormAsync` overload with
   `bool includeClientId` appends `client_id` after client authentication only when the authenticator did not add it, so
   `client_secret_basic` (header only) still sends it, while `private_key_jwt` and `client_secret_post` do not
   duplicate it. The token and revocation requests keep their behavior (`includeClientId: false`).
8. **Push response** (RFC 9126 §2.2, §2.3). Only `201` with a JSON object whose `request_uri` is a non-empty string and
   whose `expires_in` is a positive integer succeeds. A `201` lacking either throws `OpenIdProviderException`. An OAuth
   error object, at any status, returns `AuthorizationRequestResult` with the `ProtocolError`. Anything else throws
   `OpenIdProviderException`.
9. **The pushed URL** is `authorization_endpoint` with `client_id` and `request_uri` only (RFC 9126 §4). The
   authorization endpoint is checked with `ProviderUri.TryParseEndpoint` before anything is pushed; a missing or
   unacceptable endpoint throws `OpenIdProviderException`.
10. **Result shape.** `AuthorizationRequestResult` mirrors `TokenResult`: `AuthorizationUri`, `RequestUri`, `ExpiresIn`
    (seconds, as RFC 9126 names it), `IsPushed`, `Error` and `IsSuccess`.
11. **Location.** `DefaultAuthorizationRequestClient` is `internal` in the `Authorization/` folder of
    `NCode.Identity.OpenId.Client`, and the PKCE challenge is a private static method of it.

## Options considered

- **A separate push-only API with the channel chosen by the caller.** Rejected: every caller would re-implement the
  "required" rule and could skip PAR.
- **Accepting a caller-computed `code_challenge`.** Rejected: it allows `plain` or a challenge that does not match the
  verifier.
- **Fixed fields for `prompt`, `max_age` and similar.** Rejected: a closed vocabulary
  ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
- **Always adding `client_id` in the push body.** Rejected: `private_key_jwt` and `client_secret_post` already add it,
  so it would be duplicated.

## Consequences

- A consumer calls `CreateAuthorizationUriAsync` and redirects the browser to `AuthorizationUri`; it keeps the
  `code_verifier`, `state` and `nonce` for the callback.
- DPoP, mTLS and JAR remain; until DPoP, `dpop_jkt` passes as an additional parameter.
- The BFF callback handles the authorization response (the RFC 9207 `iss` check and the `state` lookup).

## References

- [ADR-0089](0089-pushed-authorization-requests.md) — the OP's PAR endpoint
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) — the protocol client
- [ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) — logout-token validation
- RFC 9126 §2.1, §2.2, §2.3, §4, §5 and §6 (pushed authorization requests)
- RFC 7636 §4.1 and §4.2 (PKCE)
- RFC 9700 (OAuth 2.0 security best current practice)
- Issue #123
