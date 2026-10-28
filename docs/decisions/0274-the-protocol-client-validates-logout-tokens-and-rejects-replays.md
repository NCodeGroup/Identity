# 274. The protocol client validates logout tokens and rejects replays

- **Status:** Accepted (amended by [ADR-0278](0278-token-validation-failures-carry-a-machine-readable-code.md): an
  invalid logout token also carries a `FailureCode`)
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md) §5.8, issue #123) ends a user's sessions when the OP
sends a back-channel logout request, and it must not act on a `logout_token` before the token is validated.
[ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) left logout-token
validation, and the `jti` check against the shared state, to phase 1b.
[ADR-0273](0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md) then set the
asymmetric-only signature policy for ID tokens. This ADR records `ILogoutTokenValidator` in
`NCode.Identity.OpenId.Client`, which validates a logout token under OpenID Connect Back-Channel Logout 1.0 §2.6.

## Decision

**The protocol client validates a logout token as a signed JWT under the ID-token signature policy, checks the
Back-Channel Logout claims, rejects a replayed `jti` through `IExpiringKeySet`, and returns an invalid token as
data.**

1. **A separate validator.** `ILogoutTokenValidator` is not a mode of `IIdTokenValidator`. A logout token has different
   required claims (`events`, `jti`, `sid` or `sub`, and no `nonce`) and a different `typ`, so a separate contract
   keeps each one exact.
2. **A shared signature policy.** The permitted algorithm list and the key resolution, including the one-time refresh
   for an unknown `kid`, move from `DefaultIdTokenValidator` into the `internal static` class `ProviderSignaturePolicy`,
   which both validators use. Back-Channel Logout §2.6 validates the signature and `alg` as with ID tokens, and the OP
   signs a logout token with the client's `id_token_signed_response_alg`, so `ClientRegistration.IdTokenSigningAlgorithms`
   and the provider's `id_token_signing_alg_values_supported` limit logout tokens too. The class is static so that
   `DefaultIdTokenValidator`'s constructor and tests do not change.
3. **Claim checks, in this order.** After the JWT validator succeeds:
   - The JWT validator checks the signature, `iss` (equal to the discovered issuer), `aud` (contains the client id),
     `exp` and `iat` (both required), a clock skew of 5 minutes and the maximum lifetime of point 4.
   - When `typ` is present it must be `logout+jwt`, which refuses an ID token's `JWT`.
   - `sub` and `sid`, when present, must be non-empty strings, and at least one of them must be present.
   - `sid` must be present when `ClientRegistration.BackchannelLogoutSessionRequired` is set (point 6).
   - `events` must be an object whose `http://schemas.openid.net/event/backchannel-logout` member is an object.
   - `nonce` must be absent.
   - `jti` must be a non-empty string, and `exp` must be numeric, because the result and the replay key read them.
   - Last comes the replay check of point 5.
4. **A 10-minute maximum lifetime.** The JWT validator's `MaxLifetime` is 10 minutes, measured from the earlier of
   `iat` and `nbf` to `exp`. The specification prefers at most two minutes and the NCode OP issues two; 10 minutes
   tolerates other OPs while it bounds the replay window and the number of entries the replay store holds.
5. **The replay check is in the validator.** The validator adds the `jti` to `IExpiringKeySet`
   ([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)), so a token replayed to
   any replica fails. The client package references only `NCode.Identity.SharedState.Abstractions`
   ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)).
   - **Key.** `logout_token_jti:` followed by the lowercase hexadecimal SHA-256 of the UTF-8 text
     `$"{issuer.Length}:{issuer}{clientId.Length}:{clientId}{jti}"`. A `jti` is therefore scoped to its issuer and
     client, distinct component lists never collide, and request text never reaches the store. There is no hash tag,
     because the check is a single-key operation.
   - **Expiry.** The entry expires at `exp` plus the 5-minute skew, the last instant the token could validate.
   - **Last.** The check runs after every other check, so only a token that passed them records its `jti`.
   - **Failure.** A key that is already present fails validation with `"The logout token has already been received."`.
   - **Validate once.** Validating the same valid token twice fails the second time, and the contract documents it.
6. **`BackchannelLogoutSessionRequired` on `ClientRegistration`.** The property (default `false`) mirrors the
   registered `backchannel_logout_session_required`, as `IdTokenSigningAlgorithms` mirrors `id_token_signed_response_alg`
   (ADR-0273 point 6). It is per client, not process configuration, because two clients of one process can register
   differently. The BFF sets it `true`.
7. **The result carries what the BFF needs.** `LogoutTokenValidationResult` has `Payload`, `TokenId` (the `jti`),
   `ExpiresAt`, `Subject`, `SessionId`, `FailureReason` and `IsValid`, shaped like `IdTokenValidationResult`.
   `LogoutTokenValidationRequest { required string LogoutToken }` mirrors `IdTokenValidationRequest`, so fields can be
   added without a signature change. An invalid token is data, in the errors-as-data style of ADR-0270; a provider
   failure still throws `OpenIdProviderException`, and cancellation still throws `OperationCanceledException`, which
   the validator rethrows from the JWT validation result.
8. **Encrypted logout tokens are rejected.** The validator passes no decryption keys, so a JWE is refused, as for ID
   tokens (ADR-0273 point 8).
9. **Optional steps 9 to 11 are left to the BFF.** Matching `iss`, `sub` and `sid` against a recent ID token (§2.6
   steps 9 to 11) belongs to the BFF's session lookup, not the protocol client.
10. **No token text in a failure reason.** A `JwtDecodeException` message embeds the whole token, so a
    `JwtDecodeException` or `JwtSecretKeyNotFoundException` maps to the fixed text `"The logout token signature could
    not be verified with the provider's signing keys."`. Any other validation exception maps to
    `"The logout token is invalid: "` followed by its message.

## Options considered

- **Leaving the replay check to the BFF.** Rejected: every consumer would re-implement §2.6 step 8, and the design and
  ADR-0270 put the check in the protocol client.
- **A mode on `IIdTokenValidator`.** Rejected: a logout token follows different claim rules, so the ID-token contract
  would need conditional requirements.
- **A 2-minute maximum lifetime.** Rejected: it matches the specification's preference but is too strict for OPs that
  issue longer-lived logout tokens.

## Consequences

- A consumer calls `ILogoutTokenValidator` when the OP posts a logout token and reads `LogoutTokenValidationResult`; it
  must validate each token once, because a second validation of the same token fails as a replay.
- A replay is rejected on every replica that shares the `IExpiringKeySet`, and the store holds an entry only for the
  window in which its token could still validate.
- PAR, DPoP, mTLS and encrypted ID and logout tokens remain phase 1b work.

## References

- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — implementation packages depend only on
  abstractions
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — the atomic expiring key set
- [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) — the protocol client
- [ADR-0273](0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md) — ID-token validation
  and the signature policy
- OpenID Connect Back-Channel Logout 1.0 §2.4 (logout token), §2.6 (logout token validation) and §4 (security
  considerations)
- Issue #123
