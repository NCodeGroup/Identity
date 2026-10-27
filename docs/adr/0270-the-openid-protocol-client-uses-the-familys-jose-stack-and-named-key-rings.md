# 270. The OpenID protocol client uses the family's JOSE stack and named key rings

- **Status:** Accepted
- **Date:** 2026-10-26
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md), §4.1, §5.1 and §9, issue #123) is an OpenID Connect
relying party that runs on the server. Its protocol work — discovery, JWKS retrieval, client authentication, and the
token and revocation requests — must be reusable outside the BFF, so it lives in its own packages,
`NCode.Identity.OpenId.Client` and `NCode.Identity.OpenId.Client.Abstractions`. Design decision D5 of the design says the
client uses the family's JOSE stack, not a second one.

This ADR covers phase 1a. Phase 1b remains: PAR, DPoP with nonces, mTLS client authentication (`tls_client_auth` and
`self_signed_tls_client_auth`), and ID-token and logout-token validation.

## Decision

**The protocol client is built on the family's JOSE, JWT and secrets stack. A caller names the client in a
`ClientRegistration` on every call, and the client's credentials come from a named key ring, not from configuration.**

1. **Packages.** `.Abstractions` references `NCode.Identity.Jose.Abstractions`, `NCode.Identity.Secrets.Abstractions` and
   `NCode.Registration`. `NCode.Identity.OpenId.Client` references `.Abstractions` and
   `NCode.Identity.Jwt.Abstractions` only, because an implementation package depends only on abstractions
   ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)). This is why the design table's
   `.Jose, .Jwt, .Secrets, .SharedState` becomes `.Jwt.Abstractions`; SharedState arrives in phase 1b for the
   logout-token `jti` replay check. The namespace is `NCode.Identity.OpenId.Client`, and the options are in
   `NCode.Identity.OpenId.Client.Options`.
2. **Which client a call is for.** A consumer builds a `ClientRegistration(authority, clientId, authenticationMethod)`
   with an optional `KeyRing` and passes it to every call. The protocol client binds no configuration; the BFF binds its
   own section and validates the names at startup.
3. **Credentials come from a named key ring.** `KeyRing` names a keyed `ISecretKeyCollectionProvider`
   (`AddKeyedSingleton<ISecretKeyCollectionProvider>(name, …)`), read through `IClientKeyRingProvider`. The keys can
   rotate and can live in a KMS, and a store-backed source can replace the seam. The design sketch's `SecretId` becomes
   `KeyRing`.
4. **The endpoint rule.** An authority or endpoint URL must be `https`, or `http` only on a loopback host
   (`Uri.IsLoopback`). The check applies to the raw string as well as the parsed `Uri`: the string starts with its
   scheme, contains no whitespace and has no fragment, and an authority also has no query. Loopback `http` reaches only
   the same machine, and it lets the client run against the in-process OP in tests.
5. **Discovery.** The client fetches `<authority without trailing '/'>/.well-known/openid-configuration`. The returned
   `issuer` must equal the authority string ordinally (OIDC Discovery §4.3). `token_endpoint` and `jwks_uri` are
   required, and every endpoint member that is present must pass the endpoint rule. A violation is an
   `OpenIdProviderException`.
6. **Caching.** Metadata is cached per authority and the JWKS per `jwks_uri`, in one internal `RefreshingCache<TValue>`:
   - A value is refreshed after `MetadataRefreshInterval` (1 hour).
   - Concurrent misses collapse into one fetch.
   - A failed refresh serves the last good value while it is younger than `StaleLifetime` (24 hours), and does not retry
     for `MinimumRefreshInterval` (5 minutes).
   - A forced JWKS refresh (an unknown `kid`, used in phase 1b) is rate-limited by the same `MinimumRefreshInterval`.
   - A first fetch that fails throws, and the next call retries.
   - Validation is never relaxed to serve a stale value.
7. **JWKS import** follows RFC 7517 §5, through `ISecretKeyTypeCatalog`:
   - A key the client cannot use is skipped: not an object, not a parseable JWK, `use` other than `sig`, `key_ops`
     without `verify`, a non-asymmetric or unknown key type, or material that fails to import.
   - A set that carries private or symmetric material, or two usable keys with the same `kid`, is refused as a whole.
   - A set with no usable key is refused.
8. **Client authentication is an open strategy collection** ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
   `IClientAuthenticator` is selected by `MethodName` and registered with `TryAddEnumerable`; a duplicate name throws when
   the sender is constructed. The client does not check the method against `token_endpoint_auth_methods_supported`: the
   OP enforces the registered method. The three built-in methods:
   - `client_secret_basic`: `Authorization: Basic base64(formurlencode(client_id) ":" formurlencode(secret))`
     (RFC 6749 §2.3.1).
   - `client_secret_post`: the `client_id` and `client_secret` form parameters. Both shared-secret methods read the
     secret as the UTF-8 text of the ring's first `SymmetricSecretKey`.
   - `private_key_jwt` (RFC 7523): the parameters `client_assertion_type`
     (`urn:ietf:params:oauth:client-assertion-type:jwt-bearer`), `client_assertion` and `client_id`. The assertion has
     `iss` and `sub` equal to the client id, **`aud` equal to the OP's issuer as a single string**, a `jti` of 16 random
     bytes in base64url, `iat` and `nbf` at now, and `exp` 60 seconds later. The lifetime is fixed and has no option.
9. **`private_key_jwt` algorithm choice.** The default client order is `PS256, ES256, PS384, ES384, PS512, ES512, RS256,
   RS384, RS512`; `OpenIdClientOptions.ClientAssertionSigningAlgorithms` replaces it when set (entries are validated: no
   blank, no `none`, no duplicate). The order is filtered to what the OP advertises in
   `token_endpoint_auth_signing_alg_values_supported`, then
   followed by the OP's other advertised values except `none`. When the OP advertises none, the client's list is used
   as is. Only `AsymmetricSecretKey`s from the ring are candidates, so a MAC is never used to sign an assertion.
10. **Errors.** An OAuth error response (a JSON object with a string `error`, at any status) is data: it is returned in
    `TokenResult.Error`, or as the `ProtocolError` that revocation returns. Everything else throws
    `OpenIdProviderException` with the cause as the inner exception: an unreachable OP, a timeout, an oversized body,
    invalid JSON, a non-error non-2xx response, or a success response missing `access_token` or `token_type`.
    Cancellation by the caller propagates as `OperationCanceledException`. A missing key ring, an unknown method, and a
    revocation without a `revocation_endpoint` are configuration errors and throw `InvalidOperationException`.
11. **HTTP.** One named client, `OpenIdClientDefaults.HttpClientName` (`NCode.Identity.OpenId.Client`), whose primary
    handler is `SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }`, so a redirect is never followed.
    Each request is bounded by `RequestTimeout` (15 seconds) and its response by `MaxResponseSizeBytes` (524288). JSON is
    parsed when the media type is `application/json` or `application/jwk-set+json`. An empty body is never parsed: it
    yields no JSON whatever its media type, because a successful revocation response (RFC 7009 §2.2) is empty.
12. **Token requests are an open type hierarchy.** `TokenRequest` is abstract and public, and carries `Scope` and
    `Resources` (RFC 8707 `resource`, repeated). Its sealed subclasses add the grant's parameters
    (authorization code, refresh token, client credentials, token exchange), and another grant subclasses it. The
    authorization-code request requires a PKCE `code_verifier`.
13. **Observability.** Two warning logs, `ProviderMetadataFetchFailed` and `ProviderKeySetFetchFailed`, in the event-ID
    band 18000–18999. There are no metrics: `System.Net.Http` already measures outbound calls, and a signal has one
    owner ([ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md)).

## Options considered

- **`Microsoft.IdentityModel` or the ASP.NET Core OpenID Connect handler.** Rejected per D5 and
  [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md): it is a second JOSE
  stack with a second key and algorithm policy.
- **Credentials in configuration.** Rejected: it gives no rotation and puts secrets in configuration.
- **`aud` equal to the token endpoint URL.** Rejected: RFC 7523bis and FAPI 2.0 require the issuer.
- **Throwing on OAuth error responses.** Rejected: the BFF needs `invalid_grant` as a value, not as an exception.

## Consequences

- A consumer builds a `ClientRegistration` and registers a keyed key ring under the name it gives as `KeyRing`.
- Phase 1b remains: PAR, DPoP with nonces, mTLS client authentication, and ID-token and logout-token validation.
- The in-process OP is reachable in tests over loopback `http`, with no change to the endpoint rule for any other host.
- A new client authentication method is a registered `IClientAuthenticator`; no library code changes.
- The client does not follow redirects and caps every response, so a hostile or misconfigured OP cannot redirect it or
  exhaust its memory.

## References

- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — implementation packages depend only on
  abstractions
- [ADR-0106](0106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md) — runtime `jwks_uri` resolution and
  the shared SSRF guard
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) — JWT validation
  policy
- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — metrics
- [Backend for frontend](../backend-for-frontend.md)
- `src/NCode.Identity.OpenId.Client.Abstractions/`, `src/NCode.Identity.OpenId.Client/`
