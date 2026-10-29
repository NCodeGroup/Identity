# 291. The BFF refreshes a session single-flight under a lease on its refresh token

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

A backend-for-frontend session ([ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md))
holds one refresh token, and a browser fires parallel API calls that may land on different replicas. When the access
token is about to expire, each of them could refresh it. The NCode OP rotates a refresh token that is not
sender-constrained ([ADR-0216](0216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md)), and presenting a
rotated token twice is reuse, which revokes the whole family (RFC 9700 §4.14.2). The design (§5.5, D6) chose
cluster-wide single-flight refresh in the BFF over a reuse grace window at the OP. This ADR records how phase 2
(issue #394) builds it, how it maps refresh failures, how it binds tokens with DPoP and how applications ask for a
token. It also settles design §11.2, refresh-token lifetime discovery.

## Decision

**A refresh token is presented to the provider at most once: inside a replica a lock per session serializes the
refreshes of that session, and across replicas a lease keyed by the refresh token's fingerprint admits one refresher
while the others wait for its result.**

1. **The local lock.** A lock per session store key (`BffRefreshLocks`, reference-counted and dropped when its last
   waiter leaves) serializes a session's refreshes inside a replica, so a replica never takes its own lease twice and a
   wait on another replica's lease delays only that session.
2. **The lease.** Across replicas, the lease key is `{storeKey}:{SHA-256 fingerprint of the refresh token}`, so it
   guards the invariant itself: one refresh token, one presentation. The lease is an `IExpiringValueSet` entry held
   through `IBffSessionStore` (`bff:lease:`), not an `IExpiringKeySet` key, because it must be released when the
   provider does not rotate. It lasts `Tokens:RefreshLeaseDuration` (40 s, exactly twice the protocol client's 15 s
   request timeout plus 10 seconds), and `AddIdentityBff` refuses at startup a `RefreshLeaseDuration` shorter than
   twice `OpenIdClientOptions.RequestTimeout` plus 10 seconds, because a DPoP nonce retry sends the refresh request
   twice under one lease. Under the local lock the replica re-reads the session and serves a token that another request
   has just refreshed; otherwise it takes the lease for the refresh token it read. The winner releases the lease only
   when it finished more than 5 s before the lease expires, so it never removes a lease that expired and another
   replica took.
3. **The losers.** A replica that cannot take the lease polls the session with backoff (50 ms, doubling to 800 ms) until
   a fresh token appears. When the refresh token changes or the lease is gone, it tries to take the lease for the
   refresh token it now reads. It gives up with `503` (`refresh_wait_timeout`) at the lease duration, and never
   presents a refresh token whose lease another replica holds.
4. **The write-back.** The refreshed tokens are written with the session's read-modify-write. A write that loses a
   version race (to an idle slide, for example) is merged onto the newer version and retried, never dropped, because
   dropping it would lose the only copy of the rotated refresh token.
5. **Failures.** `invalid_grant` ends the session (`refresh_failed`) and answers `401`. Any other OAuth error answers
   `502` (`provider_error`). An `OpenIdProviderException`, an `HttpRequestException` or a timeout answers `503`
   (`provider_unavailable`). A session that needs a new token but holds no refresh token ends (`refresh_failed`) and
   answers `401`. Every failed refresh request is audited as `bff.token.refresh_failed`, with the OAuth error or the
   failure reason, and every ended session as `bff.session.ended`.
6. **When to refresh.** A token is refreshed once it is within `Tokens:RefreshBeforeExpiry` (60 s) of its expiry. A
   token response without `expires_in` counts as already expiring, so it is refreshed on first use. `offline_access` is
   always requested at login.
7. **The token cache.** The session caches access tokens keyed by (requirement, resource, scope), compared ordinally,
   with `null` for absent. The code-exchange token is cached under (`user`, `null`, `null`). The cache holds at most 32
   entries; the entry that expires soonest is evicted first.
8. **DPoP.** `Bff:DPoP:Mode` is `when_supported` (the default) or `required`, and `Bff:DPoP:KeyRing` names the key
   ring; they map to the protocol client's `ClientRegistration.DPoPKeyRing` and `DPoPBoundAccessTokens`
   ([ADR-0286](0286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md)). The session pins
   the `jkt` that the code exchange reported and passes it as `TokenRequest.DPoPJwkThumbprint` on every refresh, so the
   session keeps its key across key-ring rotation. `BffAccessTokenHandler` sends `Authorization: DPoP` with a resource
   proof from `IDPoPProofFactory` (with `ath`), records every `DPoP-Nonce`, and retries a `use_dpop_nonce` challenge
   once, with the request content buffered first.
9. **Asking for a token.** `IBffTokenRequirement` is an open strategy collection
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)); phase 2 registers only `user`.
   `IBffTokenAccessor.GetAccessTokenAsync(httpContext, request, ct)` resolves a requirement by name, returns a
   `BffTokenResult` with the token or a status and reason, and throws `InvalidOperationException` for an unknown name, a
   configuration error. `AddBffAccessTokenHandler(BffTokenRequest)` attaches the token to a named `HttpClient`; a failure
   there throws `BffTokenUnavailableException(status, reason)`, which `UseBff()` answers as a problem.
10. **Refresh-token lifetime (§11.2).** Whenever a token response carries the de-facto `refresh_token_expires_in` member,
    at the code exchange or at a refresh, the BFF caps the session's absolute expiry and the cookie's `Max-Age` at it.
    The cap only ever shortens a session, never extends it. Without the member the session still ends at
    `Session:AbsoluteLifetime` or on `invalid_grant`.
11. **Nothing cancels a won refresh.** Once the lease is won, the token request and the write-back run with
    `CancellationToken.None`, bounded by the client's request timeout. A caller that aborted after the provider rotated
    the token would leave the old refresh token in the session, and the next refresh would present it again, which the
    provider treats as reuse.

## Options considered

- **A lease keyed by the record version.** Rejected: an idle-slide write bumps the version, which would admit a second
  refresher with the same refresh token.
- **An `IExpiringKeySet` lease.** Rejected: a key set entry cannot be released, so against a provider that does not
  rotate every later refresh would wait out the lease.
- **A reuse grace window at the OP (design D6 (b)).** Rejected: it weakens reuse detection for every client and does
  not exist at other providers.
- **Turning off rotation for BFF clients (D6 (c)).** Rejected: it gives up a defense RFC 9700 recommends.
- **A background refresher.** Rejected: it refreshes sessions nobody is using and still needs the same single-flight
  guarantee.
- **Losers retrying with the old refresh token after a timeout.** Rejected: that is exactly the reuse the design
  prevents.
- **Striped locks.** Rejected: a stripe held while waiting out another replica's lease blocks every session hashed to
  it.

## Consequences

- Concurrent refreshes from two replicas never trip reuse detection; an end-to-end test drives eight parallel calls
  across two replicas against the NCode OP and asserts it.
- A refresh blocked by a dead winner costs the losers at most one lease duration, then `503`.
- The NCode OP sends `refresh_token_expires_in` as its rotation family's fixed horizon
  ([ADR-0297](0297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md)), so a
  session in front of it is capped from its first token response; other providers are capped only when they send it.
- The `client`, `user_or_client`, `exchange` and `none` requirements arrive with remote APIs in phase 3.

## References

- [Backend-for-frontend design](../backend-for-frontend.md) §5.5, §11.2, D6 and D7
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0216](0216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md) — refresh-token rotation
- [ADR-0267](0267-replicas-share-versioned-expiring-values-and-membership-indexes.md) — versioned values
- [ADR-0286](0286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md) — DPoP in the protocol client
- [ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) — the session record
- RFC 9700 §4.14.2 (refresh-token rotation), RFC 9449 §7 and §8 (DPoP resource proofs and nonces)
- Issues #123 and #394
