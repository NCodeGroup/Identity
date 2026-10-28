# 211. A rotated refresh token is spent with its successor, and a lost spend is a reuse

- **Status:** Accepted (amended by [ADR-0215](0215-a-revocation-wins-a-concurrency-race.md): every revocation retries a
  lost race; [ADR-0217](0217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md): a rotation marks the
  spent grant consumed, a spend reads again after a write that left the grant active, and a retry within the reuse
  interval is not a reuse)
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

With refresh-token rotation on, a refresh presents a token and receives a successor, and a rotated token that is
presented again is a reuse ([ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md)). Rotation revoked
the presented grant with `SetRevokedOnceAsync`, which does nothing when the grant is already revoked, and stored the
successor in a later unit of work. Two concurrent refreshes of one token could therefore both receive tokens, so the
reuse went undetected (RFC 9700 §4.14.2), or the save of the loser failed and it received `500`. A lineage revocation
that answered a reuse could also run before the successor of the winner existed, which left that successor active
(issue [#260](https://github.com/NCodeGroup/Identity/issues/260)).

## Decision

**A rotation spends the presented refresh token and stores its successor in one unit of work, and a request that loses
the spend is answered as a reuse.**

1. **Rotation is one unit of work.** `ITokenService.RotateRefreshTokenAsync` mints the successor and stores it through
   `IPersistedGrantService.ReplaceOnceOrDefault`, which revokes the presented grant if it is still active and adds the
   successor in the same unit of work. A lost race, or a presented grant that is missing or not active, stores nothing
   and returns `null`, and the token service audits no issued token.
2. **A spend without a successor revokes the token once.** When rotation is on but the refreshed scopes omit
   `offline_access`, no successor exists, and `IPersistedGrantService.TryRevokeOnceAsync` spends the presented token. It
   returns `false` when the grant is missing, is not active, or the save lost a race
   ([ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md)).
3. **A lost spend is answered as a reuse.** Whether the spend ran through `ReplaceOnceOrDefault` or
   `TryRevokeOnceAsync`, the answer is the one for a revoked token that is presented again: `invalid_grant` (`400`), the
   `token.refresh.replay` audit event, and, under `refresh_token_reuse_policy = revoke_all`, the revocation of the
   lineage. The server cannot tell the attacker from the client, so it revokes the token that is still active (RFC 9700
   §4.14.2). The lineage includes the successor of the winner, because that successor was committed together with the
   winning spend, and the failure of the loser follows that commit.
4. **The spend runs after every check and before any token is minted.** The grant handler validates the presented token,
   its binding, and the request first, and spends the token before it creates an access token or an ID token, so a lost
   spend mints and audits no token.
5. **A lineage revocation wins a race.** `IPersistedGrantService.RevokeByRefreshTokenLineageAsync` reads the lineage
   again when its save loses a race, up to three attempts in total, because a rotation that committed in between has
   added a grant to the lineage.
6. **A sliding-expiration update tolerates a race.** `IPersistedGrantService.UpdateExpirationAsync` keeps the change of
   the other request when its save loses a race, instead of throwing.

## Options considered

- **A grace interval for a concurrent loser as the default.** Rejected: RFC 9700 makes no exception for a concurrent
  request, and Auth0 (reuse interval) and Okta (grace period) offer the interval as an opt-in. It is an opt-in
  ([ADR-0217](0217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md)).
- **Mint, then spend.** Rejected: a lost spend would have minted and audited tokens that are never delivered.
- **Spend and store the successor in separate units of work.** Rejected: the lineage revocation of a loser can run
  between the two and miss the successor.
- **Mark the rotation with `ConsumedWhen`.** Rejected: a consumed grant still reads as active to introspection,
  revocation, and listing, whereas a revoked grant is unusable everywhere.
- **A conditional set-based update that spends the row.** Rejected for the reasons of
  [ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md).

## Consequences

- Of two concurrent refreshes of one token, one receives tokens and the other receives `invalid_grant`, and under
  `revoke_all` the client must authenticate again.
- A client that retries a refresh after it lost the response is answered as a reuse.
- The Entity Framework InMemory provider checks no concurrency token, so the hermetic host cannot show the race; the
  unit tests cover it.
- **Breaking:** `IPersistedGrantService` declares `TryRevokeOnceAsync` and `ReplaceOnceOrDefault`, and `ITokenService`
  declares `RotateRefreshTokenAsync`, so every implementation of either interface implements them.
- This implements issue [#260](https://github.com/NCodeGroup/Identity/issues/260).

## References

- [ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md) — reuse detection and the revocation of the
  rotation family.
- [ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md) — a lost race is a
  `StoreConcurrencyException`.
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the interceptor-managed concurrency tokens that detect
  the lost race.
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) — OAuth 2.0 Security Best Current Practice (§4.14.2).
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken.DefaultRefreshTokenGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Tokens.DefaultTokenService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`.
