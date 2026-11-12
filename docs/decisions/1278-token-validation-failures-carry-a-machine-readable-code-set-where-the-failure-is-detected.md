# 1278. Token validation failures carry a machine-readable code set where the failure is detected

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0111](project-template/0111-failures-carry-a-machine-readable-code.md)

## Context

[project-template ADR-0111](project-template/0111-failures-carry-a-machine-readable-code.md) gives a failure that
callers act on a stable, snake_case machine-readable code, set where the failure is detected and passed through by the
layers above. This record applies it to JWT validation and to the protocol client's token validation.

The backend-for-frontend design ([§5.2](../backend-for-frontend.md)) names a failed login from an open vocabulary
(`id_token_invalid`, `nonce_mismatch`, …), so a caller needs to tell one failure from another without matching text
(issue #343).

Issuer, audience and lifetime are checked inside `IJwtValidator`, as requirements
([ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)).
A handler's failure is text, and the validator joins every failure into one `JwtValidationException`, so a failed
requirement needs an identity that a caller can read.

The conventions forbid validating a received JWT outside `IJwtValidator`, and bounding its lifetime by hand
([ADR-1187](1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md),
[ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)).
The protocol client therefore cannot re-check `iss`, `aud` or `exp` to learn which one failed.

## Decision

**A token validation failure carries a machine-readable `FailureCode`, set where the failure is detected: the JWT
validator's handlers name a code from `JwtValidationFailureCodes`, and the protocol client passes it through or names
one of its own from `TokenValidationFailureCodes`.** The general rule is
[project-template ADR-0111](project-template/0111-failures-carry-a-machine-readable-code.md)'s; this record fixes the
vocabularies and where each code is set.

1. **Both vocabularies are open string vocabularies
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).** Each is a `public static class` of
   `public const string` names, never an enum. A custom handler may pass a code of its own.
2. **JWT codes.** `JwtValidationFailureCodes` is in `NCode.Identity.Jwt.Validation`
   (`NCode.Identity.Jwt.Abstractions`):

   | Constant | Value | Reported when |
   | --- | --- | --- |
   | `InvalidToken` | `invalid_token` | the token is malformed or structurally unacceptable; the default |
   | `AlgorithmNotPermitted` | `algorithm_not_permitted` | an algorithm is outside the allow-list, or `alg` is `none` |
   | `KeyNotFound` | `key_not_found` | no candidate key exists to verify or decrypt with |
   | `InvalidSignature` | `invalid_signature` | no candidate key verified the signature |
   | `DecryptionFailed` | `decryption_failed` | no candidate key decrypted the token |
   | `InvalidClaim` | `invalid_claim` | a claim-value requirement failed (its default code) |
   | `InvalidIssuer` | `invalid_issuer` | the `iss` requirement (`ValidateIssuer`) failed |
   | `InvalidAudience` | `invalid_audience` | the `aud` requirement (`ValidateAudience`) failed |
   | `InvalidTokenType` | `invalid_token_type` | the `typ` header is missing or not allowed |
   | `Expired` | `expired` | `exp` has passed, beyond the clock skew |
   | `NotYetValid` | `not_yet_valid` | `nbf` or `iat` is in the future, beyond the clock skew |
   | `InvalidLifetime` | `invalid_lifetime` | a lifetime claim is malformed, missing or out of order, or too long |
   | `InvalidCertificate` | `invalid_certificate` | the signing certificate is outside its validity |
   | `AssertionFailed` | `assertion_failed` | an assertion requirement returned `false` |

3. **`JwtValidationContext.Fail(string failureCode, string reason)` records the code with the reason.** The context
   keeps the first recorded code in `FailureCode`, `HasFailed` is `FailureCode is not null`, and every reason is joined
   into the exception message.
4. **`JwtValidationException.FailureCode` is an `init` property that defaults to `invalid_token`.**
   `JwtDecodeException` defaults to `invalid_signature`, `JwtSecretKeyNotFoundException` to `key_not_found`, and the
   validator's decryption path sets `decryption_failed`. `JwtValidationResult.FailureCode` returns the exception's code,
   `invalid_token` for any other exception, and `null` when the token is valid, so the fallback lives in one place.
5. **A requirement declares its code; nothing is inferred from a claim name.** `ClaimValueRequirement.FailureCode` is
   `init` and defaults to `invalid_claim`; `ValidateIssuer` sets `invalid_issuer` and `ValidateAudience` sets
   `invalid_audience`. An assertion that wants its own code calls `Fail(code, reason)` before it returns `false`.
6. **Client codes.** `TokenValidationFailureCodes` is in `NCode.Identity.OpenId.Client.Abstractions`, which references
   `NCode.Identity.Jwt.Abstractions`, so a shared code is an alias (`= JwtValidationFailureCodes.X`) and each string is
   defined once. The aliases are `InvalidToken`, `AlgorithmNotPermitted`, `InvalidSignature`, `DecryptionFailed`,
   `InvalidIssuer`, `InvalidAudience`, `Expired`, `NotYetValid`, `InvalidLifetime`, `InvalidTokenType` and
   `InvalidClaim`. The client adds `subject_mismatch`, `nonce_mismatch`, `max_age_exceeded`, `token_hash_mismatch` and
   `token_replayed`.
7. **The client passes a JWT code through with no mapping table.** The one exception is an unverifiable signature,
   which is `invalid_signature`, or `decryption_failed` when it arose before the signing keys were resolved
   ([ADR-1280](1280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md)).
8. **`IdTokenValidationResult` and `LogoutTokenValidationResult` carry the same `FailureCode`.** Their failure
   constructors take `(string failureCode, string failureReason)`, and `IsValid` is `FailureCode is null`.
9. **`FailureReason` is human-readable text that never contains the token.** Code acts on `FailureCode`, never on the
   text, as the production conventions say.

## Options considered

- **An enum of failure kinds.** The vocabulary is open: a custom handler names a code of its own
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). Rejected.
- **Re-check `iss`, `aud` and `exp` in the client.** It duplicates the lifetime handler and bounds a lifetime outside
  the lifetime requirement, against the conventions
  ([ADR-1187](1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md)).
  Rejected.
- **A separate client vocabulary with a mapping table.** A hard-coded relationship between two vocabularies loses any
  code it does not list, including one a custom handler names. Rejected.
- **Codes set at the failure, aliased and passed through.** Chosen.

## Consequences

- A consumer such as the backend-for-frontend maps `FailureCode` to its own reason names and logs `FailureReason`.
- A custom handler names a code for every failure, and an assertion names its own code by calling `Fail` before it
  returns `false`.
- Code that acts on why a token failed reads `JwtValidationResult.FailureCode` or a result's `FailureCode`, never the
  message text.
- `NCode.Identity.OpenId.Client.Abstractions` references `NCode.Identity.Jwt.Abstractions`.
- The OP's validation reports codes too; a revoked session in `DefaultIssuedTokenValidator` reports `invalid_token`,
  the exception default.

## References

- `src/NCode.Identity.Jwt.Abstractions/Validation/JwtValidationFailureCodes.cs`,
  `src/NCode.Identity.Jwt.Abstractions/Validation/JwtValidationContext.cs`,
  `src/NCode.Identity.Jwt.Abstractions/Validation/JwtValidationResult.cs`
- `src/NCode.Identity.OpenId.Client.Abstractions/TokenValidationFailureCodes.cs`,
  `src/NCode.Identity.OpenId.Client.Abstractions/IdTokenValidationResult.cs`,
  `src/NCode.Identity.OpenId.Client.Abstractions/LogoutTokenValidationResult.cs`
- `src/NCode.Identity.OpenId.Client/Tokens/DefaultIdTokenValidator.cs`,
  `src/NCode.Identity.OpenId.Client/Tokens/DefaultLogoutTokenValidator.cs`
- [Backend-for-frontend design §5.2](../backend-for-frontend.md): the login failure names
- [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md):
  requirements and handlers
- [ADR-1187](1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md): the
  lifetime requirement
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): open vocabularies
- [ADR-1273](1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md): the
  ID-token validator
- [ADR-1274](1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md): the
  logout-token validator
- Issue [#343](https://github.com/NCodeGroup/identity/issues/343)
