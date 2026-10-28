# 284. A logout token is recorded only after the client handles it

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

[ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) point 5 rejects a replayed logout
token by adding the token's `jti` to `IExpiringKeySet` during validation. Validation therefore consumes the `jti`
before the client has done anything with the token.

The OP's `BackChannelLogoutDeliveryJobHandler` retries every non-success answer with the same `logout_token`. Back-Channel
Logout §2.8 has the relying party answer `400 Bad Request` when logout fails, which asks the OP to retry. When the
client's attempt to end the sessions fails after validation, the retry carries the same `jti`, which is already
recorded, so it is refused as a replay and the sessions are never ended (issue #349).

A handled token must still be refused (Back-Channel Logout §2.6 step 8), so the `jti` has to be recorded, but only
once the client has acted on the token.

## Decision

**Validation looks the `jti` up and never records it. A separate operation, `ILogoutTokenValidator.TryRecordHandledAsync`,
records it once the client has handled the token.**

1. **`ValidateAsync` only looks the replay key up.** The replay check stays the last check, but it calls
   `IExpiringKeySet.ContainsAsync` on the ADR-0274 replay key. A present key fails with `token_replayed` and the reason
   `"The logout token has already been handled."`. It never adds the key, so a token whose delivery failed validates
   again on the OP's retry.
2. **`TryRecordHandledAsync` records it.** `ValueTask<bool> TryRecordHandledAsync(ClientRegistration registration,
   LogoutTokenValidationResult result, CancellationToken cancellationToken)` adds the same key with the same expiry
   (`ExpiresAt` plus the 5-minute skew). It returns `true` when it added the key and `false` when the key was already
   present (a concurrent delivery recorded it) or the expiry has passed. It throws `ArgumentNullException` for null
   arguments and `ArgumentException` for a result whose `IsValid` is `false`. The `Try` prefix follows
   `IExpiringKeySet.TryAddAsync` and `IDPoPProofReplayCache.TryRecordAsync`.
3. **The method lives on the validator.** It is not on the result and not on a new service. The result stays plain data
   that consumers and test doubles construct, and the replay-key scheme and the skew stay in the one class that both
   looks them up and records them.
4. **The result carries the verified issuer.** `LogoutTokenValidationResult` has `string? Issuer`, the verified `iss`,
   non-null when valid. Recording therefore needs no discovery call and cannot throw `OpenIdProviderException` after the
   sessions are ended. The BFF also needs `iss` for Back-Channel Logout §2.6 steps 9-11.
5. **Concurrent deliveries are harmless.** Deliveries of one token before it is recorded both validate. Ending sessions
   is idempotent, and at most one `TryRecordHandledAsync` returns `true`.
6. **The caller's order is fixed.** Validate, end the sessions, `TryRecordHandledAsync`, then answer `200`. When the
   client fails to end the sessions it answers `400` without recording, so the OP retries and the retry validates again.

## Options considered

- **A commit operation on the result.** The result would hold the key set and the registration, and it would stop being
  plain data that consumers and test doubles construct. Rejected.
- **A separate replay service.** The validator and the service would have to agree on the key and the skew, two
  contracts for one fact. Rejected.
- **Keep validate-once.** It is the ADR-0274 behavior, and a failed delivery is never retried successfully. Rejected.

## Consequences

- A delivery the client failed to handle validates again when the OP retries it, and a handled token is refused.
- A caller that validates a token must call `TryRecordHandledAsync` after it handles the token, or the token validates
  again until its replay window passes. This is a new obligation on the BFF.
- `ILogoutTokenValidator` and `LogoutTokenValidationResult` change in the public API
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) accepts that before release).
- Two deliveries of one token that run at the same time can both end the sessions; that is idempotent.

## References

- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) - the expiring key set
- [ADR-0274](0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) - point 5, amended here
- [ADR-0280](0280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md) - decrypting a logout token
- OpenID Connect Back-Channel Logout 1.0 §2.6 step 8 and §2.8
- [Issue #349](https://github.com/NCodeGroup/Identity/issues/349)
