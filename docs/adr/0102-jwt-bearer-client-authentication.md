# 102. JWT-bearer client authentication (`private_key_jwt` and `client_secret_jwt`)

- **Status:** Accepted (assertion-replay storage amended by
  [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md):
  assertions are recorded atomically, scoped by tenant, in the shared-state port, backed by Valkey for several replicas)
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

Before this change the token endpoint recognized only `client_secret_basic`, `client_secret_post`, and `none`
client authentication. The **FAPI** security profiles (backlog `F16`) require **strong client authentication** —
`private_key_jwt` or mutual-TLS — and OpenID Connect Core §9 /
[RFC 7523](https://datatracker.ietf.org/doc/html/rfc7523) define the JWT-bearer family (`private_key_jwt` and
`client_secret_jwt`), where the client proves its identity by presenting a signed JWT **`client_assertion`** rather than
a bare secret. The two methods differ only in the signing key: `private_key_jwt` is signed with the client's **private
key** (asymmetric, FAPI-eligible); `client_secret_jwt` is an **HMAC** keyed by the client secret (symmetric, **not**
FAPI-eligible). Both carry `client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer`.

The existing client-authentication seam is a strategy collection: each `IClientAuthenticationHandler` advertises an
`AuthenticationMethod` and returns `Undefined` to defer, an error, or a resolved client; the
`token_endpoint_auth_methods_supported` discovery metadata is **derived from the registered handlers**, so adding a
handler advertises its method automatically.

## Decision

**Add JWT-bearer client authentication as two handlers over a shared base, selected by the assertion's signing-key
family, validated against the client's registered keys, and single-use by `jti`.**

- **Two handlers, one base.** `ClientAssertionClientAuthenticationHandler` holds the common flow;
  `PrivateKeyJwtClientAuthenticationHandler` and `ClientSecretJwtClientAuthenticationHandler` differ only in the
  signing-key family they own. The base reads the `client_assertion` / `client_assertion_type` from the POST body,
  defers (`Undefined`) when no JWT-bearer assertion is present, then inspects the assertion's `alg` header and classifies
  it via `IAlgorithmCollectionProvider` (`SignatureAlgorithm.KeyType == typeof(SymmetricSecretKey)` ⇒ symmetric). A
  handler **owns** the assertion only when the algorithm matches its family and otherwise defers to its sibling, so each
  assertion is handled exactly once and each method advertises itself in discovery independently.

- **The client is identified from the assertion, not a separate `client_id`.** OpenID Connect Core §9 requires
  `iss == sub == client_id`; the base reads `sub` from the (as-yet-unverified) payload only to **locate** the client's
  keys, then verifies the signature against `OpenIdClient.SecretKeys`. `iss` and `sub` must be present and equal.

- **Audience accepts the issuer or the receiving endpoint URL.** FAPI 2.0 §5.3.2.1 requires the **issuer identifier**
  as the `aud`; OpenID Connect Core §9 also permits the **token-endpoint URL**. Both are accepted so standard and FAPI
  clients interoperate (the tenant issuer equals the discovery `issuer`, guaranteeing a match).

- **Replay is rejected.** `exp` is required; `jti` is recorded single-use through a new `IClientAssertionReplayCache`
  (default backed by `IDistributedCache`, mirroring the DPoP proof replay cache — per-instance by default, shared across
  a cluster when a distributed provider is registered), keyed by `client_id` + `jti` (RFC 7523 §3).

- **`none` of `alg`, symmetric algorithms, and unknown algorithms are refused** for the asymmetric method; the symmetric
  method owns only HMAC algorithms. The advertised `token_endpoint_auth_signing_alg_values_supported`, when configured,
  is enforced against the assertion's `alg`.

- **`client_secret_jwt` is included but is non-FAPI.** It completes the OpenID Connect §9 family and reuses the client's
  stored symmetric secret as the HMAC key, but symmetric client authentication is excluded by the FAPI profiles; those
  deployments use `private_key_jwt` or mTLS.

## Options considered

- **A single handler advertising both methods.** Rejected. `token_endpoint_auth_methods_supported` is derived from the
  set of handlers' `AuthenticationMethod` values, so one method per handler keeps discovery truthful without a bespoke
  advertisement path; the small cost is parsing the assertion header in both handlers (cheap).

- **Require a separate `client_id` form parameter.** Rejected. FAPI `private_key_jwt` clients send only the assertion;
  §9 already pins `iss == sub == client_id`, so reading `sub` is both sufficient and spec-aligned.

- **Accept only the issuer as `aud` (strict FAPI).** Rejected as the default. Accepting the issuer **or** the endpoint
  URL interoperates with both OIDC-generic and FAPI clients; a FAPI **profile** (backlog `F16`) can tighten this to
  issuer-only where required.

## Consequences

- The OP authenticates confidential clients via `private_key_jwt` and `client_secret_jwt`, advancing the strong-client-
  authentication leg of FAPI readiness (`F16`); both methods are advertised automatically.
- `private_key_jwt` requires the OP to hold the client's **public** verification key. That capability is provided by the
  verify-only asymmetric key primitive ([ADR-0103](0103-public-only-asymmetric-secret-keys.md)); onboarding a client's
  public key via its `jwks_uri` remains the Phase-0 follow-up in `F16`.
- New public surface: `OpenIdConstants.ClientAssertionTypes.JwtBearer` and the
  `IClientAssertionReplayCache` seam (a cluster swaps in a shared `IDistributedCache`).
- The replay guard is correct per-instance and across a cluster only when a distributed cache is registered — the same
  operational note as DPoP proof replay ([ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)).
