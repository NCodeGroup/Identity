# 1285. The protocol client is a framework-free library over the family's JOSE stack and named key rings

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md), §4.1, §5.1 and §9, issue #123) is an OpenID Connect
relying party that runs on the server. Its protocol work (discovery, JWKS retrieval, client authentication, and the token
and revocation requests) must be reusable outside the BFF, so it lives in its own packages,
`NCode.Identity.OpenId.Client` and `NCode.Identity.OpenId.Client.Abstractions`. The design says the client uses the
family's JOSE stack, not a second one.

The client does no ASP.NET Core work. An ASP.NET Core framework reference would buy a constants class and a query-string
helper, yet it would make every console app or worker that uses the client install the ASP.NET Core runtime. The design
calls the client useful to any server-side client, and
[ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)
scopes the framework reference to the packages that genuinely need HTTP types (issue
[#321](https://github.com/NCodeGroup/identity/issues/321)).

## Decision

**The protocol client is a plain .NET library built on the family's JOSE, JWT and secrets stack. A caller names the client
in a `ClientRegistration` on every call, and the client's credentials come from a named key ring, not from
configuration. No package of the client references an ASP.NET Core shared framework.**

1. **Packages.** `.Client.Abstractions` references `Jose.Abstractions`, `Jwt.Abstractions`, `Protocol.Abstractions`,
   `Secrets.Abstractions`, `NCode.Identity.Json` and `NCode.Identity.Registration`. `NCode.Identity.OpenId.Client`
   references only `.Client.Abstractions`, `Jwt.Abstractions`, `SharedState.Abstractions` and `NCode.Identity.Json`
   ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)),
   plus six `Microsoft.Extensions.*` packages (`DependencyInjection.Abstractions`, `Http`, `Logging.Abstractions`,
   `Options`, `Primitives` and `Diagnostics`) and `Polly.Core`. The namespace is `NCode.Identity.OpenId.Client`, and the
   options are in `NCode.Identity.OpenId.Client.Options`.
2. **No ASP.NET Core runtime.** A console app or worker needs only `Microsoft.NETCore.App`. Status codes are the internal
   named `int` constants of `ProviderStatusCodes` (`Status200OK`, `Status201Created`) in
   `NCode.Identity.OpenId.Client.Http`, and authorization-request query values are percent-encoded with
   `Uri.EscapeDataString`, appended with `?` or, when the endpoint already has a query, `&`.
   `ProtocolPackageDependencyTests` guards the rule: no `Microsoft.AspNetCore.*` assembly, no `Microsoft.AspNetCore.App`
   runtime in the test host's runtimeconfig, no `Microsoft.Extensions.Telemetry.*` package and no OP package.
3. **Which client a call is for.** A consumer builds a `ClientRegistration(authority, clientId, authenticationMethod)`
   with an optional `KeyRing` and passes it to every call. The protocol client binds no configuration; the BFF binds its
   own section and validates the names at startup.
4. **Credentials come from a named key ring.** `KeyRing` names a keyed `ISecretKeyCollectionProvider`
   (`AddKeyedSingleton<ISecretKeyCollectionProvider>(name, …)`), read through `IClientKeyRingProvider`. The keys rotate
   and can be held in a key management service
   ([ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md)).
5. **The endpoint rule.** An authority or endpoint URL is `https`, or `http` only on a loopback host (`Uri.IsLoopback`).
   The check applies to the raw string as well as the parsed `Uri`: the string starts with its scheme, contains no
   whitespace and has no fragment, and an authority also has no query. Loopback `http` reaches only the same machine, and
   it lets the client run against the in-process OP in tests.
6. **Discovery.** The client fetches `<authority without trailing '/'>/.well-known/openid-configuration`. The returned
   `issuer` equals the authority string ordinally (OIDC Discovery §4.3). `token_endpoint` and `jwks_uri` are required, and
   every endpoint member that is present passes the endpoint rule. A violation is an `OpenIdProviderException`.
7. **Caching.** Metadata is cached per authority and the JWKS per `jwks_uri`, in one internal `RefreshingCache<TValue>`:
   - A value is refreshed after `MetadataRefreshInterval` (1 hour).
   - Concurrent misses collapse into one fetch.
   - A failed refresh serves the last good value while it is younger than `StaleLifetime` (24 hours), and does not retry
     for `MinimumRefreshInterval` (5 minutes).
   - A forced JWKS refresh (an unknown `kid`) is rate-limited by the same `MinimumRefreshInterval`.
   - A first fetch that fails throws, and the next call retries.
   - Validation is never relaxed to serve a stale value.
8. **JWKS import** follows RFC 7517 §5, through `ISecretKeyTypeCatalog`:
   - A key the client cannot use is skipped: not an object, not a parseable JWK, `use` other than `sig`, `key_ops` without
     `verify`, a non-asymmetric or unknown key type, or material that fails to import.
   - A set that carries private or symmetric material, or two usable keys with the same `kid`, is refused as a whole.
   - A set with no usable key is refused.
9. **Client authentication is an open strategy collection**
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). `IClientAuthenticator` is selected by `MethodName`
   and registered with `TryAddEnumerable`; a duplicate name throws when the sender is constructed. The client does not
   check the method against `token_endpoint_auth_methods_supported`: the OP enforces the registered method. The built-in
   methods:
   - `client_secret_basic`: `Authorization: Basic base64(formurlencode(client_id) ":" formurlencode(secret))`
     (RFC 6749 §2.3.1).
   - `client_secret_post`: the `client_id` and `client_secret` form parameters. Both shared-secret methods read the secret
     as the UTF-8 text of the ring's first `SymmetricSecretKey`.
   - `private_key_jwt` (RFC 7523): the parameters `client_assertion_type`
     (`urn:ietf:params:oauth:client-assertion-type:jwt-bearer`), `client_assertion` and `client_id`. The assertion has
     `iss` and `sub` equal to the client id, **`aud` equal to the OP's issuer as a single string**, a `jti` of 16 random
     bytes in base64url, `iat` and `nbf` at now, and `exp` 60 seconds later. The lifetime is fixed and has no option.
   - Mutual TLS client authentication is [ADR-1300](1300-an-asymmetric-key-carries-its-issuers-certificates-and-a-tls-client-presents-them.md)'s.
10. **`private_key_jwt` algorithm choice.** The default order is PS256, ES256, PS384, ES384, PS512, ES512, RS256, RS384,
    RS512; a non-empty `OpenIdClientOptions.ClientAssertionSigningAlgorithms` replaces it. The order is filtered to what the
    OP advertises in `token_endpoint_auth_signing_alg_values_supported`, then followed by the OP's other advertised values
    except `none`. When the OP advertises none, the client's list is used as is. Only `AsymmetricSecretKey`s from the ring
    are candidates, so a MAC is never used to sign an assertion.
11. **Errors.** An OAuth error response (a JSON object with a string `error`, at any status) is data: it is returned in
    `TokenResult.Error`, or as the `ProtocolError` that revocation returns. Transport, size, JSON and protocol failures
    (an unreachable OP, a timeout, an oversized body, invalid JSON, a non-error non-2xx response, or a success response
    missing `access_token` or `token_type`) throw `OpenIdProviderException` with the cause as the inner exception.
    Cancellation by the caller propagates as `OperationCanceledException`. A missing key ring, an unknown method and a
    revocation without a `revocation_endpoint` are configuration errors and throw `InvalidOperationException`.
12. **HTTP.** One named client, `OpenIdClientDefaults.HttpClientName` (`NCode.Identity.OpenId.Client`), whose primary
    handler is `SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }`, so a redirect is never followed.
    Responses are capped by `MaxResponseSizeBytes` (524288); the time bounds are
    [ADR-1336](1336-the-protocol-client-bounds-its-calls-with-a-polly-core-pipeline.md)'s. JSON is parsed only from a
    non-empty body whose media type is `application/json` or `application/jwk-set+json`: an empty body yields no JSON
    whatever its media type, because a successful revocation response (RFC 7009 §2.2) is empty.
13. **Token requests are an open type hierarchy.** `TokenRequest` is abstract and public, and carries `Scope` and
    `Resources` (RFC 8707 `resource`, repeated). Its sealed subclasses add the grant's parameters (authorization code,
    refresh token, client credentials, token exchange), and another grant subclasses it. The authorization-code request
    requires a PKCE `code_verifier`.
14. **Observability.** Two warning logs, `ProviderMetadataFetchFailed` and `ProviderKeySetFetchFailed`, in the event-ID
    band 18000–18999. Outbound calls are measured by `System.Net.Http`, and the retry and timeout counters of
    [ADR-1336](1336-the-protocol-client-bounds-its-calls-with-a-polly-core-pipeline.md) are on the client's meter; a
    signal has one owner ([ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md)).

## Options considered

- **`Microsoft.IdentityModel` or the ASP.NET Core OpenID Connect handler.** Rejected per the design and
  [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md):
  it is a second JOSE stack with a second key and algorithm policy.
- **Credentials in configuration.** Rejected: it gives no rotation and puts secrets in configuration.
- **`aud` equal to the token endpoint URL.** Rejected: RFC 7523bis and FAPI 2.0 require the issuer.
- **Throwing on OAuth error responses.** Rejected: the BFF needs `invalid_grant` as a value, not as an exception.
- **The ASP.NET Core framework reference.** Rejected: it forces the ASP.NET Core runtime onto every non-web consumer for a
  constants class and a query helper.
- **Casting `HttpStatusCode` members to `int` (`(int)HttpStatusCode.OK`).** Rejected: named constants read better than enum
  casts.
- **Typing `ProviderHttpResponse.StatusCode` as `HttpStatusCode`.** Rejected: the public `ProtocolError.StatusCode` is an
  `int`, so the value would cast straight back.
- **The chosen option:** a framework-free library on the family's JOSE stack, with credentials from named key rings.

## Consequences

- A consumer builds a `ClientRegistration` and registers a keyed key ring under the name it gives as `KeyRing`.
- A console app or worker that uses the client needs only `Microsoft.NETCore.App`. An ASP.NET Core host such as the
  backend-for-frontend resolves the same `Microsoft.Extensions.*` assemblies from its shared framework.
- The framework cannot return unnoticed. Restoring with the framework reference fails with NU1510, because the direct
  `Microsoft.Extensions.*` references stay unpruned, so the build itself refuses it; the guard test fails too.
- Percent-encoding with `Uri.EscapeDataString` follows RFC 3986 (everything but `A-Z a-z 0-9 - . _ ~`), so `! * ( ) @` in
  authorization-request query values are encoded; a provider decodes them to the same values.
- The guard test reads the runtimeconfig with `File.ReadAllText(path, SecureEncoding.UTF8)`, because the banned-API
  analyzer ([ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md))
  forbids the encoding-less overload.
- The "no `ConfigureAwait(false)`" rule holds: the client's callers are server-side hosts without a
  `SynchronizationContext`.
- The in-process OP is reachable in tests over loopback `http`, with no change to the endpoint rule for any other host.
- A new client authentication method is a registered `IClientAuthenticator`; no library code changes.
- The client does not follow redirects and caps every response, so a hostile or misconfigured OP cannot redirect it or
  exhaust its memory.

## References

- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md):
  implementation packages depend only on abstractions
- [ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md):
  runtime `jwks_uri` resolution and the shared SSRF guard
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): open vocabularies
- [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md):
  JWT validation policy
- [ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md): metrics
- [ADR-1273](1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md): ID-token
  validation
- [ADR-1288](1288-the-protocol-client-authenticates-with-a-tls-client-certificate-from-its-key-ring.md): mutual TLS client
  authentication
- [ADR-1300](1300-an-asymmetric-key-carries-its-issuers-certificates-and-a-tls-client-presents-them.md): client
  certificates
- [ADR-1336](1336-the-protocol-client-bounds-its-calls-with-a-polly-core-pipeline.md): time bounds
- [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md): key management service
  keys
- [Backend for frontend](../backend-for-frontend.md)
- `src/NCode.Identity.OpenId.Client.Abstractions/`, `src/NCode.Identity.OpenId.Client/`
