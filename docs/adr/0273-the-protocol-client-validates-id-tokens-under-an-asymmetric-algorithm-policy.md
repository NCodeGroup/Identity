# 273. The protocol client validates ID tokens under an asymmetric algorithm policy

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md), issue #123) must validate the ID token the OP returns
before it trusts the user's identity. Phase 1b of [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)
left ID-token validation open, and issue #335 asked for the `at_hash`, `c_hash` and `s_hash` checks on top of an
ID-token validator the client did not yet have. This ADR records the validator itself, `IIdTokenValidator` in
`NCode.Identity.OpenId.Client`, and the hash check it shares with the server.

Two earlier decisions shape it. [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)
requires a JWT to be validated under an explicit algorithm and key policy, and
[ADR-0272](0272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md) makes
`SignatureAlgorithm.GetTokenHashFunction` the source of the hash each signing algorithm defines: SHA-512 for Ed25519
and SHAKE256 with 114 bytes of output for Ed448.

## Decision

**The protocol client validates an ID token as a signed JWT under an asymmetric-only algorithm policy, checks the
OpenID Connect claims, and returns an invalid token as data.**

1. **Full ID-token validation.** The issue assumed the client already validated ID tokens. It did not, so the client
   gains `IIdTokenValidator`, the ID-token part of #123 phase 1b, and #335 closes with it.
2. **One shared hash helper.** The public static class `TokenHash` in `NCode.Identity.Jose.Abstractions`
   (`NCode.Identity.Jose.Algorithms`) has `Compute` and `Verify`, each taking a `HashFunctionDelegate` and its output
   size. The server's ID-token payload handler computes `at_hash`, `c_hash` and `s_hash` through `Compute`, and the
   client checks them through `Verify`, so the left-most-half encoding has one implementation.
3. **Constant-time comparison.** `Verify` builds the expected base64url string and compares its ASCII bytes with the
   claim's bytes using `CryptographicOperations.FixedTimeEquals`. The hash covers the ASCII octets (OIDC Core §3.1.3.6).
   A non-ASCII value or claim makes `Verify` return `false`, and `Compute` throws `ArgumentException` for a non-ASCII
   value.
4. **Hash claims: present means it must match, absent is accepted.** For each pair (`at_hash`, access token),
   (`c_hash`, authorization code) and (`s_hash`, state): when the request supplies the value and the claim is present,
   the claim must be a string that verifies. An absent claim is accepted, because these claims are optional in the
   code flow (OIDC Core §3.1.3.6, §3.1.3.8). The hybrid and implicit flows, which require them, are not supported.
5. **Asymmetric-only algorithm policy.** The permitted `alg` values are the registered `SignatureAlgorithm`s whose key
   type is an `AsymmetricSecretKey`, never `none`, so a symmetric or unsecured ID token is never accepted. The list is
   intersected with `OpenIdClientOptions.IdTokenSigningAlgorithms` when that is non-empty, and with the provider's
   `id_token_signing_alg_values_supported` when the provider advertises it. An empty result fails validation. The
   validator passes the result as the JWT validator's valid signature algorithms, so the header is checked before any
   cryptography.
6. **New option.** `OpenIdClientOptions.IdTokenSigningAlgorithms` (default empty) is validated like
   `ClientAssertionSigningAlgorithms`: no blank, no `none`, no duplicate.
7. **Keys and rotation.** The provider's keys come from `IProviderKeySetResolver`. When the header names a `kid` the
   set lacks, the validator refreshes the signing keys once, which the cache's minimum refresh interval rate-limits
   ([ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)), and uses the key
   if the refreshed set has it. Otherwise it supplies no key and validation fails. Without a `kid`, every key is a
   candidate and the JWT validator filters them by `use` and `alg`.
8. **Encrypted ID tokens are rejected.** The validator passes no decryption keys, so a JWE is refused (ADR-0154).
9. **Claim checks, in this order.** After the JWT validator succeeds:
   - The JWT validator checks `iss` (equal to the discovered issuer), `aud` (contains the client id), `exp` and `iat`
     (both required), with a clock skew of 5 minutes.
   - When `typ` is present it must be `JWT`, which rejects `logout+jwt`, `at+jwt` and the other JWT types.
   - `sub` must be a non-empty string, and when the request supplies an expected subject it must equal it ordinally
     (OIDC Core §12.2, for a refreshed ID token).
   - When `azp` is present it must equal the client id.
   - When the request supplies a nonce, `nonce` must be a string equal to it ordinally.
   - When the request supplies `max_age`, `auth_time` must be a number and the current time must not exceed
     `auth_time + max_age` plus the 5-minute skew.
   - Last come the hash claims of point 4.
10. **Results and errors.** An invalid ID token is data: `IdTokenValidationResult` with `IsValid == false` and a
    `FailureReason`, in the errors-as-data style of ADR-0270. A provider failure (discovery, JWKS) still throws
    `OpenIdProviderException`, and cancellation still throws `OperationCanceledException`. The validator rethrows both
    from the JWT validation result, because the JWT validator catches every exception, including those thrown while the
    keys are resolved.
11. **No token text in a failure reason.** A `JwtDecodeException` message embeds the whole token, so a
    `JwtDecodeException` or `JwtSecretKeyNotFoundException` maps to a fixed text that says the signature could not be
    verified with the provider's signing keys. Any other validation exception maps to
    `"The ID token is invalid: "` followed by its message.
12. **`ITokenClient` is unchanged.** The caller runs the token request, then calls `IIdTokenValidator` with the nonce,
    code, access token and state it holds.

## Options considered

- **Deriving the hash from the `alg` name.** Rejected: the digest named by the `alg` suffix is wrong for Ed25519 and
  Ed448, which [ADR-0272](0272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md) defines
  through `GetTokenHashFunction`.
- **Requiring the hash claims.** Rejected: they are optional in the code flow, so requiring them would reject valid
  tokens from conforming providers.
- **Throwing on an invalid token.** Rejected: the BFF needs the outcome as a value, as it does for an OAuth error
  response.

## Consequences

- A consumer calls `IIdTokenValidator` after the token request, with the values it holds, and reads
  `IdTokenValidationResult`.
- The server that issues `at_hash` and `c_hash` and the client that checks them compute them the same way, for every
  algorithm that defines its own hash.
- A symmetric or `none` ID token is refused whatever the provider advertises.
- Encrypted ID tokens, logout-token validation, PAR, DPoP and mTLS remain open phase 1b work.

## References

- [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) — JWT validation
  policy
- [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) — the protocol client
- [ADR-0272](0272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md) — fully specified signature
  algorithms and their token hash
- OpenID Connect Core 1.0 §3.1.3.6–3.1.3.8 (ID token validation) and §12.2 (refresh token response)
- Issue #335
