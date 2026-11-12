# 1273. The protocol client validates ID tokens and their token hashes under OpenID Connect Core

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md), issue #123) must validate the ID token the OP returns
before it trusts the user's identity. The OpenID protocol client
([ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md)) needs an ID-token
validator, and issue #335 asks for the `at_hash`, `c_hash` and `s_hash` checks on top of it. The server that issues the
hash claims and the client that checks them must compute them the same way for every signing algorithm, including
Ed25519 and Ed448, whose hash is not named by the `alg` suffix.

The signature policy, the provider keys and their rotation, the failure of a provider and the text of a failure reason
are those of the shared provider-JWT core in
[ADR-1280](1280-the-protocol-client-validates-and-decrypts-every-provider-issued-jwt-through-one-core-under-registered-algorithms.md). A JWT is validated only under
an explicit policy ([ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)),
and [ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md) makes
`SignatureAlgorithm.GetTokenHashFunction` the source of the hash each signing algorithm defines.

## Decision

**The protocol client validates an ID token as a signed JWT through the shared provider-JWT core, checks the OpenID
Connect claims and the token hashes, and returns an invalid token as data.**

1. **The validator.** `IIdTokenValidator` (`NCode.Identity.OpenId.Client.Abstractions`, implemented by
   `DefaultIdTokenValidator`) validates an ID token through the shared provider-JWT core and returns
   `IdTokenValidationResult`. An invalid token is data: the result carries a `FailureCode` and a `FailureReason`.
2. **The request.** `IdTokenValidationRequest` carries `IdToken` and the optional `Nonce`, `MaxAge`, `AccessToken`,
   `AuthorizationCode`, `State` and `Subject` the caller holds.
3. **Per-client algorithms.** `ClientRegistration.IdTokenSigningAlgorithms` (default `null`) mirrors the
   `id_token_signed_response_alg` the client registered at the provider. It is per client because two clients of one
   process can register different algorithms. Its `init` accessor rejects a blank entry, `none` and a duplicate. An
   empty list accepts any registered asymmetric algorithm, because an ID token is always signed.
4. **JWT-level claims.** The JWT validator checks `iss` equal to the discovered issuer, `aud` containing the client id,
   and `exp` and `iat` (both required), with a clock skew of 5 minutes.
5. **OpenID Connect claim checks, in this order.** After the JWT validator succeeds:
   - When `typ` is present it must be `JWT`.
   - `sub` must be a non-empty string, and when the request supplies an expected subject it must equal it ordinally
     (OIDC Core §12.2), otherwise the failure code is `subject_mismatch`.
   - When `azp` is present it must equal the client id.
   - When the request supplies a nonce, `nonce` must equal it ordinally, otherwise the failure code is
     `nonce_mismatch`.
   - When the request supplies `max_age`, the current time must not exceed `auth_time + max_age` plus the 5-minute
     skew, otherwise the failure code is `max_age_exceeded`.
   - Last come the hash claims of point 6.
6. **Hash claims: present means it must match, absent is accepted.** For each pair (`at_hash`, access token),
   (`c_hash`, authorization code) and (`s_hash`, state): when the request supplies the value and the claim is present,
   the claim must verify, otherwise the failure code is `token_hash_mismatch`. An absent claim is accepted, because
   these claims are optional in the code flow (OIDC Core §3.1.3.6, §3.1.3.8). The hybrid and implicit flows, which
   require them, are not supported.
7. **One shared hash helper.** The public static class `TokenHash` in `NCode.Identity.Jose.Abstractions`
   (`NCode.Identity.Jose.Algorithms`) has `Compute` and `Verify`. Each takes a `HashFunctionDelegate` and its size from
   `GetTokenHashFunction` ([ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md)).
   The server's ID-token payload handler computes `at_hash`, `c_hash` and `s_hash` through `Compute`, and the client
   verifies them through `Verify`, so the left-most-half encoding has one implementation.
8. **Constant-time comparison.** `Verify` compares ASCII bytes with `CryptographicOperations.FixedTimeEquals`, because
   the hash covers the ASCII octets of the value (OIDC Core §3.1.3.6). It returns `false` for non-ASCII input, and
   `Compute` throws `ArgumentException` for non-ASCII input. The three claims of a token share one caller-supplied
   stack buffer.
9. **`ITokenClient` does not validate.** The caller runs the token request, then calls `IIdTokenValidator` with the
   nonce, code, access token and state it holds.

## Options considered

- **Deriving the hash from the `alg` name.** Rejected: the digest named by the `alg` suffix is wrong for Ed25519 and
  Ed448, which [ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md) defines through
  `GetTokenHashFunction`.
- **Requiring the hash claims.** Rejected: they are optional in the code flow, so requiring them would reject valid
  tokens from conforming providers.
- **Throwing on an invalid token.** Rejected: the BFF needs the outcome as a value, as it does for an OAuth error
  response.
- **Validating through `IIdTokenValidator` with a shared hash helper, results as data, and absent hash claims
  accepted.** Chosen.

## Consequences

- A consumer calls `IIdTokenValidator` after the token request, with the values it holds, and reads
  `IdTokenValidationResult`.
- The server that issues `at_hash`, `c_hash` and `s_hash` and the client that checks them compute them the same way
  for every algorithm.
- A symmetric or `none` ID token is refused whatever the provider advertises.
- The tests cover each claim check and its failure code, the three hash claims present, absent and wrong, and
  `TokenHash` round trips for every registered hash function, including non-ASCII input.

## References

- [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md) — JWT validation
  policy
- [ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md) — the protocol client
- [ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md) — fully specified signature
  algorithms and their token hash; the token hash
  function of a signature algorithm
- [ADR-1280](1280-the-protocol-client-validates-and-decrypts-every-provider-issued-jwt-through-one-core-under-registered-algorithms.md) — the shared provider-JWT core
- OpenID Connect Core 1.0 §3.1.3.6–3.1.3.8 (ID token validation) and §12.2 (refresh token response)
- Issue #335
