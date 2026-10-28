# 278. Token validation failures carry a machine-readable code

- **Status:** Accepted (amended by
  [ADR-0280](0280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md): the client code
  `decryption_failed`)
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

[ADR-0273](0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md) and
[ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) report an invalid ID token or
logout token only as `FailureReason` text. The backend-for-frontend design ([§5.2](../backend-for-frontend.md)) names a
failed login from an open vocabulary (`id_token_invalid`, `nonce_mismatch`, …), so a caller that has only the text can
tell one failure from another only by matching it (issue #343).

Issuer, audience and lifetime are checked inside `IJwtValidator`, as requirements
([ADR-0120](0120-jwt-validation-requirements-and-handlers.md)). A handler's failure is text, and the validator joins
every failure into one `JwtValidationException`, so a failed requirement has no identity that a caller can read.

The conventions forbid validating a received JWT outside `IJwtValidator`, and bounding its lifetime by hand
([ADR-0124](0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md),
[ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)). The protocol client
therefore cannot re-check `iss`, `aud` or `exp` to learn which one failed.

## Decision

**A token validation failure carries a machine-readable `FailureCode`, set where the failure is detected: the JWT
validator's handlers name a code from `JwtValidationFailureCodes`, and the protocol client passes it through or names
one of its own from `TokenValidationFailureCodes`.**

1. **Codes are set where the failure is detected.** The JWT layer has its own vocabulary because that is where issuer,
   audience and lifetime fail. The client neither re-checks claims nor parses messages.
2. **Both vocabularies are open string vocabularies
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).** Each is a `[PublicAPI] public static class` of
   snake_case `public const string` names, compared ordinally. A custom handler may pass a code of its own. There is
   no enum.
3. **JWT codes.** `JwtValidationFailureCodes` is in `NCode.Identity.Jwt.Validation`
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

4. **`JwtValidationContext.Fail(string failureCode, string reason)` records the code with the reason.** The context
   keeps the **first** recorded code in `FailureCode`, `HasFailed` is `FailureCode is not null`, and every reason is
   still joined into the exception message.
5. **`JwtValidationException.FailureCode` is an `init` property that defaults to `invalid_token`.** `JwtDecodeException`
   defaults to `invalid_signature`, `JwtSecretKeyNotFoundException` to `key_not_found`, and the validator's decryption
   path sets `decryption_failed`. `JwtValidationResult.FailureCode` returns the exception's code, `invalid_token` for
   any other exception, and `null` when the token is valid, so the fallback lives in one place.
6. **A requirement declares its code; nothing is inferred from a claim name.** `ClaimValueRequirement.FailureCode` is
   `init` and defaults to `invalid_claim`; `ValidateIssuer` sets `invalid_issuer` and `ValidateAudience` sets
   `invalid_audience`. The other built-in handlers use fixed codes. `AssertionRequirement` has no code property: an
   assertion that wants its own code calls `context.Fail(code, reason)` before it returns `false`, because the first
   code wins.
7. **Client codes.** `TokenValidationFailureCodes` is in `NCode.Identity.OpenId.Client`
   (`NCode.Identity.OpenId.Client.Abstractions`), which references `NCode.Identity.Jwt.Abstractions`, so a shared code
   is an alias (`= JwtValidationFailureCodes.X`) and each string is defined once:

   | Constant | Value |
   | --- | --- |
   | `InvalidToken`, `AlgorithmNotPermitted`, `InvalidSignature` | `= JwtValidationFailureCodes.<same name>` |
   | `InvalidIssuer`, `InvalidAudience`, `Expired`, `NotYetValid` | `= JwtValidationFailureCodes.<same name>` |
   | `InvalidLifetime`, `InvalidTokenType`, `InvalidClaim` | `= JwtValidationFailureCodes.<same name>` |
   | `SubjectMismatch` | `subject_mismatch` |
   | `NonceMismatch` | `nonce_mismatch` |
   | `MaxAgeExceeded` | `max_age_exceeded` |
   | `TokenHashMismatch` | `token_hash_mismatch` |
   | `TokenReplayed` | `token_replayed` |

8. **The client passes a JWT-layer code through unchanged** (`result.FailureCode`). The one exception is an
   unverifiable signature (`JwtDecodeException` or `JwtSecretKeyNotFoundException`), which stays `invalid_signature`,
   as its fixed text already says. The client keeps no mapping table.
9. **`IdTokenValidationResult` and `LogoutTokenValidationResult` carry the same `FailureCode`.** They have the same
   shape and use the same vocabulary. Both failure constructors take `(string failureCode, string failureReason)`, both
   arguments non-empty, and `IsValid` is `FailureCode is null`.
10. **`FailureReason` stays human-readable text that never contains the token.** A code is a constant, so it never
    carries token text either.

## Options considered

- **An enum of failure kinds.** The vocabulary is open: a custom handler names a code of its own
  ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). Rejected.
- **Match the message text.** The text is for people and changes with wording; a caller that matches it breaks
  silently. Rejected.
- **Re-check `iss`, `aud` and `exp` in the client.** It duplicates the lifetime handler and bounds a lifetime outside
  the lifetime requirement, against the conventions
  ([ADR-0124](0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md)). Rejected.
- **Report which requirement failed.** The lifetime requirement fails for distinct reasons (expired, not yet valid,
  malformed, too long), and a caller needs the reason, not the requirement. Rejected.
- **A separate client vocabulary with a mapping table.** A hard-coded relationship between two vocabularies loses any
  code it does not list, including one a custom handler names. Rejected in favor of aliases and pass-through.
- **Report the code of every failure.** A caller acts on one outcome, and the message keeps every reason. Rejected.

## Consequences

- A consumer such as the backend-for-frontend maps `FailureCode` to its own reason names and logs `FailureReason`.
- A custom handler names a code for every failure, and an assertion names its own code by calling `Fail` before it
  returns `false`.
- Code that acts on why a token failed reads `JwtValidationResult.FailureCode` or a result's `FailureCode`, never the
  message text; the production coding conventions say so.
- `NCode.Identity.OpenId.Client.Abstractions` references `NCode.Identity.Jwt.Abstractions`.
- The OP's validation reports codes too, though no OP caller reads them yet; a revoked session in
  `DefaultIssuedTokenValidator` reports `invalid_token`.

## References

- `src/NCode.Identity.Jwt.Abstractions/Validation/JwtValidationFailureCodes.cs`,
  `src/NCode.Identity.Jwt.Abstractions/Validation/JwtValidationContext.cs`,
  `src/NCode.Identity.Jwt.Abstractions/Validation/JwtValidationResult.cs`
- `src/NCode.Identity.OpenId.Client.Abstractions/TokenValidationFailureCodes.cs`,
  `src/NCode.Identity.OpenId.Client.Abstractions/IdTokenValidationResult.cs`,
  `src/NCode.Identity.OpenId.Client.Abstractions/LogoutTokenValidationResult.cs`
- `src/NCode.Identity.OpenId.Client/Tokens/DefaultIdTokenValidator.cs`,
  `src/NCode.Identity.OpenId.Client/Tokens/DefaultLogoutTokenValidator.cs`
- [Backend-for-frontend design §5.2](../backend-for-frontend.md) — the login failure names
- [ADR-0120](0120-jwt-validation-requirements-and-handlers.md) — requirements and handlers; this adds the code a
  handler fails with
- [ADR-0124](0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md) — the lifetime requirement
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) — JWTs are validated
  only through `IJwtValidator`
- [ADR-0273](0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md) — the ID-token
  validator this amends
- [ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) — the logout-token validator this
  amends
- Issue [#343](https://github.com/NCodeGroup/Identity/issues/343)
