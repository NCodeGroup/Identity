# 1291. The BFF refreshes a session's token single-flight under a lease its winner releases by owner token

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A backend-for-frontend (BFF) session ([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md))
holds one refresh token, and a browser fires parallel API calls that may land on different replicas. When the access
token is about to expire, each of them could refresh it. The NCode OP rotates a refresh token that is not
sender-constrained ([ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md)),
and presenting a rotated token twice is reuse, which revokes the whole family (RFC 9700 §4.14.2). The design (§5.5, D6)
chooses cluster-wide single-flight refresh in the BFF over a reuse grace window at the OP.

A lease needs an owner. `IExpiringValueSet` ([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md))
restarts a key that expires and is added again at version one (an ABA hazard), so a version the winner read cannot tell
its own lease from a newer one that another replica took after it expired. A winner that leaves its lease held until it
expires makes every replica that lost the race wait for an expiry although the refresh has ended.

A BFF session belongs to a present user: it starts at a login at the OpenID Provider (OP), is bound to the OP's login
session by the ID token's `sid`, and ends at the BFF's logout or at the OP's back-channel logout
([ADR-1293](1293-the-bff-ends-sessions-by-a-guarded-logout-form-post-and-by-back-channel-logout-through-indexes.md)). The
`offline_access` scope asks for something else: OpenID Connect Core 1.0 §11 defines it as a refresh token that reaches
the user's data "even when the End-User is not present (not logged in)", and Back-Channel Logout 1.0 §2.7 says such
refresh tokens normally survive the user's logout. Keycloak, asked for `offline_access` at login, moves the client to
an offline session and removes the login session, so ending the user's sessions there sends the BFF no back-channel
logout token. Providers also differ in what a refresh token takes: Keycloak issues one to a confidential client's code
exchange without `offline_access`, while the NCode OP
([ADR-1026](1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md)) and
several hosted providers issue one only for `offline_access`.

## Decision

**A refresh token is presented to the provider at most once: inside a replica a lock per session serializes the
refreshes of that session, and across replicas a lease keyed by the refresh token's fingerprint admits one refresher,
which releases exactly its own lease by the owner token it holds when it finishes.**

1. **The local lock.** `BffRefreshLocks` (purpose `user`, keyed by session store key, reference-counted and dropped when
   its last waiter leaves) serializes a session's refreshes inside a replica, so a replica never takes its own lease
   twice and a wait on another replica's lease delays only that session.
2. **The lease.** The lease key is `{storeKey}:{SHA-256 of the refresh token}`, so it guards the invariant itself: one
   refresh token, one presentation. The lease is an `IExpiringValueSet` entry held through `IBffSessionStore`
   (`bff:lease:`), not an `IExpiringKeySet` key, because it must be released when the provider does not rotate. Under
   the local lock the replica re-reads the session and serves a token another request has just refreshed; otherwise it
   takes the lease for the refresh token it read.
3. **The owner token.** `IBffSessionStore.TryAcquireRefreshLeaseAsync` stores a fresh 256-bit random owner token as the
   lease value and returns `BffRefreshLease(LeaseKey, Owner)`, or `null` when the lease is held.
   `ReleaseRefreshLeaseAsync(BffRefreshLease)` compare-and-removes the lease by that owner through
   `IExpiringValueSet.TryRemoveAsync`, which removes the live value only while its bytes equal the expected value
   ([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)).
   The comparison uses `SequenceEqual`, not a constant-time compare: the owner never leaves the store.
4. **The winner always releases.** `DefaultBffUserTokenService` releases a won lease when the refresh ends, in a
   `finally` with `CancellationToken.None`. A lease another replica took after this one expired has another owner, so
   the release leaves it in place. A release that fails on the store (`BffStoreUnavailableException`) is logged
   (`RefreshLeaseReleaseFailed`, event 19004, Warning) and changes nothing else: the refresh's own result or exception
   stands, and the lease expires on its own after the lease duration.
5. **The lease duration.** `Bff:Tokens:RefreshLeaseDuration` is 40 s, within 10 s to 5 min.
   `BffRefreshLeaseValidator` in `NCode.Identity.Bff.Server` refuses at start a lease shorter than twice
   `OpenIdClientOptions.TotalTimeout` plus 10 s, because a DPoP nonce retry sends the refresh request twice under one
   lease ([ADR-1336](1336-the-protocol-client-bounds-every-call-with-a-polly-core-pipeline-and-retries-only-safe-requests.md)).
6. **The losers.** A replica that cannot take the lease polls the session with backoff (50 ms, doubling to 800 ms) until
   a fresh token appears. When the refresh token changes or the lease is gone, it tries to take the lease for the
   refresh token it now reads. It never presents a refresh token whose lease another replica holds, and it answers
   `503` (`refresh_wait_timeout`) at the lease duration.
7. **The write-back.** Once the lease is won, the token request and the write-back run with `CancellationToken.None`,
   because a caller that aborted after the provider rotated the token would leave the old refresh token in the session
   and the next refresh would present it again. The refreshed tokens are written with the session's read-modify-write;
   a write that loses a version race (to an idle slide, for example) is merged onto the newer version and retried,
   never dropped, because dropping it would lose the only copy of the rotated refresh token.
8. **Failures.** `invalid_grant` ends the session (`refresh_failed`) and answers `401`. Any other OAuth error answers
   `502` (`provider_error`). An `OpenIdProviderException`, an `HttpRequestException` or a timeout answers `503`
   (`provider_unavailable`). A session that needs a token but holds no refresh token ends and answers `401`. Every
   failed refresh is audited as `bff.token.refresh_failed` and every ended session as `bff.session.ended`.
9. **When to refresh.** A token is refreshed once it is within `Bff:Tokens:RefreshBeforeExpiry` (60 s, 0 to 1 hour) of
   its expiry. A token response without `expires_in` counts as already expiring, so it is refreshed on first use.
10. **The token cache.** The session caches up to 32 access tokens keyed by (requirement, resource, scope), compared
    ordinally with `null` for absent, and evicts the one that expires soonest. The code-exchange token is cached under
    (`user`, `null`, `null`).
11. **Refresh-token lifetime.** Whenever a token response carries the de-facto `refresh_token_expires_in` member, at the
    code exchange or at a refresh, the BFF caps the session's absolute expiry and the cookie's `Max-Age` at it. The cap
    only shortens a session, never extends it. The NCode OP sends the member
    ([ADR-1297](1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md)).
12. **DPoP.** Each frontend's `DPoP:Mode` (`when_supported`, the default, or `required`) and `DPoP:KeyRing` map to the
    protocol client's registration
    ([ADR-1286](1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md)). The session pins
    the `jkt` that the code exchange reported and passes it as `TokenRequest.DPoPJwkThumbprint` on every refresh.
    `BffAccessTokenHandler` sends `Authorization: DPoP` with a resource proof carrying `ath`, records every
    `DPoP-Nonce`, and retries a `use_dpop_nonce` challenge once, with the request content buffered first.
13. **The login's scopes.** The login requests `openid` and the frontend's `Scopes`, each once, and nothing else. It
    never adds `offline_access` on its own: requesting an offline grant is a decision about the provider, so it belongs
    to the deployment's configuration. A deployment lists `offline_access` only when its OP issues a refresh token for
    that scope alone. Against an OP that issues one without it, the BFF's grant stays inside the OP's login session, so
    the OP's logout reaches the BFF through back-channel logout. Against an OP that needs it, the BFF still ends its own
    session at its absolute lifetime, at its idle timeout and on every back-channel logout the OP sends.
14. **Asking for a token.** `IBffTokenRequirement` is an open strategy collection
    ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
    `IBffTokenAccessor.GetAccessTokenAsync` returns a `BffTokenResult` with the token or a status and reason, and
    throws `InvalidOperationException` for an unknown requirement name, a configuration error.
    `AddBffAccessTokenHandler(BffTokenRequest)` attaches the token to a named `HttpClient`; a failure there throws
    `BffTokenUnavailableException`, which `UseBff()` answers as a problem.

## Options considered

- **A lease keyed by the record version.** Rejected: an idle-slide write bumps the version, which would admit a second
  refresher with the same refresh token.
- **An `IExpiringKeySet` lease.** Rejected: a key set entry cannot be released, so against a provider that does not
  rotate every later refresh would wait out the lease.
- **A reuse grace window at the OP.** Rejected: it weakens reuse detection for every client and does not exist at other
  providers.
- **Turning off rotation for BFF clients.** Rejected: it gives up a defense RFC 9700 recommends.
- **A background refresher.** Rejected: it refreshes sessions nobody uses and still needs the same single-flight
  guarantee.
- **Losers retrying with the old refresh token after a timeout.** Rejected: that is exactly the reuse the design
  prevents.
- **Striped locks.** Rejected: a stripe held while waiting out another replica's lease blocks every session hashed to
  it.
- **Releasing only after a time margin.** Rejected: a slow winner still leaves its lease held until it expires, and the
  margin is a guess about how far clocks and the store's expiry differ.
- **Removing only when the version matches.** Rejected: it needs versions that never repeat, which changes the version
  semantics of every value. A lease needs identity, not order.
- **Reading the value, then `DEL` without a script.** Rejected: another replica can take the lease between the two
  commands, so it is not atomic.
- **Always requesting `offline_access`.** Rejected: it asks every OP for a grant that outlives the user's login
  session, and an OP that keeps offline grants apart from login sessions, as Keycloak does, never tells the BFF that
  the user logged out.
- **Requesting `offline_access` when the OP's `scopes_supported` lists it.** Rejected: that says the OP knows the
  scope, not whether a refresh token needs it, and Keycloak lists it.
- **Listing `offline_access` in the frontend's `Scopes` when the OP needs it.** Chosen: the deployment knows its OP, the
  default asks for the least the session needs, and the setting exists.

## Consequences

- Concurrent refreshes from two replicas never trip reuse detection; an end-to-end test drives eight parallel calls
  across two replicas against the NCode OP and asserts it.
- A winner frees its lease as soon as it finishes, and a lease another replica took after expiry is never removed.
- A refresh blocked by a dead winner, or a store outage during the release, costs the losers at most one lease
  duration, then `503`; a release failure never turns a refresh whose tokens were written into a failure and never
  hides the refresh's own failure.
- A custom `IExpiringValueSet` adapter and a custom `IBffSessionStore` implement the compare-and-remove and the owner
  token.
- A BFF against Keycloak, or any OP that issues refresh tokens for the code exchange, keeps the OP's login session and
  receives its back-channel logout; the end-to-end interop suite proves this against Keycloak. A BFF against the NCode
  OP, or another OP that issues refresh tokens only for `offline_access`, lists it in `Scopes`; without it the session
  ends when its first access token expires.
- A session in front of the NCode OP is capped from its first token response; other providers are capped only when they
  send `refresh_token_expires_in`.

## References

- [Backend-for-frontend design](../backend-for-frontend.md) §5.5, §11.2, D6 and D7
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md), [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md),
  [ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md),
  [ADR-1286](1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md),
  [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1293](1293-the-bff-ends-sessions-by-a-guarded-logout-form-post-and-by-back-channel-logout-through-indexes.md),
  [ADR-1297](1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md),
  [ADR-1336](1336-the-protocol-client-bounds-every-call-with-a-polly-core-pipeline-and-retries-only-safe-requests.md)
- RFC 9700 §4.14.2 (refresh-token rotation), RFC 9449 §7 and §8 (DPoP resource proofs and nonces)
- OpenID Connect Core 1.0 §11 (Offline Access); OpenID Connect Back-Channel Logout 1.0 §2.7
- Issues #123, #394, #422 and #462
- `DefaultBffUserTokenService.cs`, `DefaultBffSessionStore.cs`, `BffLoginHandler`, `BffOptions`
