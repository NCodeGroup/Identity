# 1274. The protocol client validates logout tokens and records each only after the client handles it

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

The backend-for-frontend ([design](../backend-for-frontend.md) §5.8, issue #123) ends a user's sessions when the OP
sends a back-channel logout request, and it must not act on a `logout_token` before the token is validated.
[ADR-1270](1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) puts logout-token
validation, and the `jti` check against the shared state, in the protocol client, and
[ADR-1273](1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md) sets the
signature policy for ID tokens. `NCode.Identity.OpenId.Client` validates a logout token under OpenID Connect
Back-Channel Logout 1.0 §2.6.

A logout token has different required claims from an ID token (`events`, `jti`, `sid` or `sub`, and no `nonce`) and a
different `typ`. The OP signs it with the client's `id_token_signed_response_alg`, so the ID-token signature policy
applies to it.

A handled token must be refused when it is replayed (§2.6 step 8), so its `jti` has to be recorded. The OP's
`BackChannelLogoutDeliveryJobHandler` retries every non-success answer with the same `logout_token`, and §2.8 has the
relying party answer `400 Bad Request` when logout fails, which asks the OP to retry. A `jti` recorded during validation
would make that retry look like a replay and the sessions would never be ended (issue #349). The `jti` is therefore
recorded only once the client has acted on the token.

## Decision

**The protocol client validates a logout token as a signed JWT under the ID-token signature policy, checks the
Back-Channel Logout claims, refuses a `jti` it has recorded as handled, and records the `jti` only when the caller
reports that it handled the token. An invalid token is returned as data.**

1. **A separate validator.** `ILogoutTokenValidator` is a contract of its own, not a mode of `IIdTokenValidator`, so
   each contract keeps its claim rules exact. It validates under Back-Channel Logout 1.0 §2.6 through the shared
   provider-JWT core of [ADR-1280](1280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md), with the
   ID-token signature policy.
2. **Token checks.** The JWT validator checks the signature, `iss`, `aud`, `exp` and `iat` (both required), with a
   5-minute clock skew and a 10-minute `MaxLifetime` measured from the earlier of `iat` and `nbf`. The specification
   prefers at most two minutes and the NCode OP issues two; 10 minutes tolerates OPs that issue longer-lived tokens
   while it bounds the replay window and the number of entries the replay store holds.
3. **Claim checks, in this order.** After the JWT validator succeeds:
   - `typ`, when present, is `logout+jwt`, which refuses an ID token's `JWT`.
   - `sub` and `sid`, when present, are non-empty, and at least one of them is present.
   - `sid` is present when `ClientRegistration.BackchannelLogoutSessionRequired` is set.
   - `events` is an object whose `http://schemas.openid.net/event/backchannel-logout` member is an object.
   - `nonce` is absent.
   - `jti` is a non-empty string and `exp` is numeric, because the result and the replay key read them.
   - The replay lookup of point 5 comes last.
4. **`BackchannelLogoutSessionRequired` on `ClientRegistration`.** The property (default `false`) mirrors the registered
   `backchannel_logout_session_required`, as `IdTokenSigningAlgorithms` mirrors `id_token_signed_response_alg`. It is
   per client, not process configuration, because two clients of one process can register differently. The BFF sets it
   from its frontend option, which defaults to `true`.
5. **The replay key.** `logout_token_jti:` followed by the lowercase hexadecimal SHA-256 of the UTF-8 text
   `$"{issuer.Length}:{issuer}{clientId.Length}:{clientId}{jti}"`, held in `IExpiringKeySet`
   ([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)).
   A `jti` is therefore scoped to its issuer and client, distinct component lists never collide, and request text never
   reaches the store. There is no hash tag, because each operation touches a single key. The client package references
   only `NCode.Identity.SharedState.Abstractions`
   ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
6. **`ValidateAsync` only looks the key up.** The replay check calls `IExpiringKeySet.ContainsAsync`. A present key
   fails validation with the code `token_replayed` and the reason `"The logout token has already been handled."`.
   Validation never adds the key, so a token whose delivery failed validates again on the OP's retry.
7. **`TryRecordHandledAsync` records the key.**
   `ValueTask<bool> TryRecordHandledAsync(ClientRegistration registration, LogoutTokenValidationResult result, CancellationToken cancellationToken)`
   on the validator adds the same key with the expiry `ExpiresAt` plus the 5-minute skew, the last instant the token
   could validate. It returns `true` when it added the key and `false` when the key was already present (a concurrent
   delivery recorded it) or the expiry has passed. It throws `ArgumentNullException` for null arguments and
   `ArgumentException` for a result whose `IsValid` is `false`. The `Try` prefix follows `IExpiringKeySet.TryAddAsync`
   and `IDPoPProofReplayCache.TryRecordAsync`. The method lives on the validator, not on the result or a new service,
   so the result stays plain data that consumers and test doubles construct, and the key scheme and the skew stay in the
   one class that both looks them up and records them.
8. **The caller's order is fixed.** Validate, end the sessions, `TryRecordHandledAsync`, then answer `200`. When ending
   the sessions fails, the caller answers `400` without recording, so the OP retries (Back-Channel Logout §2.8) and the
   retry validates again.
9. **The result carries what the caller needs.** `LogoutTokenValidationResult` has `Payload`, `Issuer` (the verified
   `iss`, non-null when valid, so recording needs no discovery call and cannot throw `OpenIdProviderException` after the
   sessions are ended), `TokenId` (the `jti`), `ExpiresAt`, `Subject`, `SessionId`, `FailureCode`, `FailureReason` and
   `IsValid`, shaped like `IdTokenValidationResult`. The request is `LogoutTokenValidationRequest { required string LogoutToken }`,
   which mirrors `IdTokenValidationRequest`, so fields can be added without a signature change. An invalid token is
   data, in the errors-as-data style of ADR-1270; a provider failure still throws `OpenIdProviderException`, and
   cancellation still throws `OperationCanceledException`.
10. **Matching is the BFF's.** Matching `iss`, `sub` and `sid` against a recent ID token (§2.6 steps 9 to 11) belongs to
    the BFF's session lookup, not the protocol client.
11. **Concurrent deliveries are harmless.** Deliveries of one token before it is recorded both validate. Ending sessions
    is idempotent, and at most one `TryRecordHandledAsync` returns `true`.

## Options considered

- **A mode on `IIdTokenValidator`.** A logout token follows different claim rules, so the ID-token contract would need
  conditional requirements. Rejected.
- **Leaving the replay check to the BFF.** Every consumer would re-implement §2.6 step 8, and the design and ADR-1270
  put the check in the protocol client. Rejected.
- **A 2-minute maximum lifetime.** It matches the specification's preference but is too strict for OPs that issue
  longer-lived logout tokens. Rejected.
- **Recording during validation (validate once).** A delivery the client failed to handle is refused as a replay on the
  OP's retry, and the sessions are never ended. Rejected.
- **A commit operation on the result.** The result would hold the key set and the registration, and it would stop being
  plain data that consumers and test doubles construct. Rejected.
- **A separate replay service.** The validator and the service would have to agree on the key and the skew, two
  contracts for one fact. Rejected.
- **Chosen: validation looks the key up, and `TryRecordHandledAsync` on the validator records it after the client
  handles the token.**

## Consequences

- A consumer calls `ILogoutTokenValidator` when the OP posts a logout token and reads `LogoutTokenValidationResult`. A
  delivery the client failed to handle validates again when the OP retries it, and a handled token is refused.
- A caller that validates a token calls `TryRecordHandledAsync` after it handles the token, or the token validates again
  until its replay window passes. The BFF follows the order of point 8.
- A replay is refused on every replica that shares the `IExpiringKeySet`, and the store holds an entry only for the
  window in which its token could still validate.
- Two deliveries of one token that run at the same time can both end the sessions; that is idempotent.

## References

- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md) — implementation packages depend only on
  abstractions
- [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md) — the atomic expiring key set
- [ADR-1270](1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) — the protocol client
- [ADR-1273](1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md) — ID-token validation
  and the signature policy
- [ADR-1280](1280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md) — the shared provider-JWT core
  and decrypting a logout token
- OpenID Connect Back-Channel Logout 1.0 §2.4 (logout token), §2.6 (logout token validation), §2.8 and §4 (security
  considerations)
- Issue #123
- [Issue #349](https://github.com/NCodeGroup/identity/issues/349)
