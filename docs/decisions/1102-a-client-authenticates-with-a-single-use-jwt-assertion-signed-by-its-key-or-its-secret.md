# 1102. A client authenticates with a single-use JWT assertion signed by its key or its secret

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

The FAPI security profiles (issue [#54](https://github.com/NCodeGroup/identity/issues/54)) require strong client
authentication: `private_key_jwt` or mutual TLS. OpenID Connect Core §9 and
[RFC 7523](https://datatracker.ietf.org/doc/html/rfc7523) define the JWT-bearer family, where the client proves its
identity by presenting a signed JWT `client_assertion` instead of a bare secret. The two methods differ only in the
signing key: `private_key_jwt` is signed with the client's private key (asymmetric, FAPI-eligible);
`client_secret_jwt` is an HMAC keyed by the client's shared secret (symmetric, not FAPI-eligible). Both carry
`client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer`.

Client authentication is a strategy collection: each `IClientAuthenticationHandler` advertises one method through a
descriptor and returns `Undefined` to defer, an error, or a resolved client. An assertion is a bearer credential, so a
captured one must not be replayable, and a replica that did not see it must still refuse it.

## Decision

**A client authenticates with a JWT assertion handled by one of two handlers over a shared base, selected by the
assertion's signing-key family, verified against the client's keys, and accepted once.**

- **Two handlers, one base.** `PrivateKeyJwtClientAuthenticationHandler` and
  `ClientSecretJwtClientAuthenticationHandler` derive from `ClientAssertionClientAuthenticationHandler`. Each is one
  method with its own descriptor, so each is advertised independently.
- **The base defers unless the assertion is a JWT bearer.** It reads `client_assertion` and `client_assertion_type`
  from a form POST body and returns `Undefined` unless the type is `OpenIdProtocol.ClientAssertionTypes.JwtBearer`
  (`urn:ietf:params:oauth:client-assertion-type:jwt-bearer`).
- **A handler owns an assertion only by algorithm family.** The assertion's `alg` must resolve through
  `IAlgorithmCollectionProvider` to a signature algorithm of the handler's family (`AcceptsOnly<AsymmetricSecretKey>()`
  or `AcceptsOnly<SymmetricSecretKey>()`). `none`, an unknown algorithm or the other family's algorithm is left to the
  sibling, so no handler authenticates it.
- **The assertion identifies the client.** `iss` and `sub` must be present and equal, `sub` locates the client, the
  client must be registered for the handler's method ([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md)),
  and the signature is verified against the keys `IClientKeyResolver` returns. No separate `client_id` parameter is
  required.
- **`client_secret_jwt` is not FAPI-eligible.** It keys its HMAC from the client's reversible shared secret
  ([ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)). FAPI deployments use
  `private_key_jwt` or mutual TLS.
- **The `alg` is permitted by the client.** It must be in the client's effective
  `token_endpoint_auth_signing_alg_values_supported` (`GetPermittedAlgorithms`), narrowed to one value by a registered
  `token_endpoint_auth_signing_alg`. An unset list permits nothing
  ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md)).
- **The audience is the tenant's issuer or the receiving endpoint's URL** under the tenant's published origins
  (`TenantRequestUri.GetPublishedUris`,
  [ADR-1365](1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md)). A security profile may
  restrict it to the issuer only through `IssuerOnlyAssertionAudienceRequirement` (FAPI 2.0 §5.3.2.1), which
  `ISecurityProfileCatalog` evaluates after validation.
- **Validation bounds the assertion's lifetime.** It applies the client's effective `clock_skew`, requires `exp`, caps
  the lifetime at `client_assertion_max_lifetime`
  ([ADR-1187](1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md)) and
  refuses a key's `x5c` certificate outside its validity.
- **An assertion is single-use.** `jti` is required and recorded for tenant, `client_id` and `jti` through
  `IClientAssertionReplayCache`. Its default, `DefaultClientAssertionReplayCache`, adds the entry atomically to the
  shared-state `IExpiringKeySet`
  ([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md))
  until `exp` plus the clock skew, so every replica detects replay.
- **Every refusal is conclusive.** Each is an `invalid_client` counted by the failed-authentication limiter
  ([ADR-1399](1399-failed-client-authentications-are-limited-per-remote-address-by-default.md)). Success yields a
  confidential client whose credential is the verifying key.

## Options considered

- **One handler advertising both methods.** Rejected. Each method needs its own descriptor to be advertised and
  registered independently; one handler per method keeps that truthful at the small cost of reading the assertion's
  header in both handlers.
- **Require a separate `client_id` parameter.** Rejected. FAPI `private_key_jwt` clients send only the assertion, and
  OpenID Connect Core §9 pins `iss == sub == client_id`, so reading `sub` is sufficient and spec-aligned.
- **Issuer-only audience as the default.** Rejected. Accepting the issuer or the endpoint URL interoperates with both
  generic OpenID Connect and FAPI clients; a security profile tightens it to the issuer where required.
- **Two handlers over a shared base, single-use by `jti`.** Chosen: it handles each assertion exactly once, identifies
  the client from the assertion and rejects replay across replicas.

## Consequences

- Confidential clients authenticate with asymmetric keys from a stored set or a `jwks_uri`, advancing the
  strong-client-authentication leg of FAPI readiness ([#54](https://github.com/NCodeGroup/identity/issues/54)).
- Both methods are advertised independently, and a deployment that registers only one advertises only that one.
- The replay guard is correct on every replica because it lives in the shared-state port.
- A smell: a handler that authenticates an assertion whose algorithm belongs to its sibling's family.
- Tests cover deferral, family ownership, `iss`/`sub` equality, algorithm permission, audience, lifetime, replay and the
  limiter.

## References

- [RFC 7523](https://datatracker.ietf.org/doc/html/rfc7523); OpenID Connect Core §9; FAPI 2.0 §5.3.2.1; issue
  [#54](https://github.com/NCodeGroup/identity/issues/54).
- `ClientAssertionClientAuthenticationHandler`, `PrivateKeyJwtClientAuthenticationHandler`,
  `ClientSecretJwtClientAuthenticationHandler`, `IClientAssertionReplayCache`, `DefaultClientAssertionReplayCache`,
  `IssuerOnlyAssertionAudienceRequirement`.
- [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md),
  [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md),
  [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md),
  [ADR-1187](1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md),
  [ADR-1365](1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md),
  [ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md),
  [ADR-1399](1399-failed-client-authentications-are-limited-per-remote-address-by-default.md).
