# 1217. A reused refresh token revokes its family unless it is a retry within an opt-in interval

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

With refresh-token rotation, each use of a refresh token issues a replacement and spends the one presented
([ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md)).
If a refresh token leaks, the attacker and the legitimate client both present the same value, and whichever arrives
second presents a spent token. The server cannot tell which party is legitimate, but the replay is an unambiguous
signal that the token is compromised. The OAuth 2.0 Security Best Current Practice
([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) §4.14.2) says that on a reuse the authorization server SHOULD
revoke all refresh tokens of that authorization grant, the whole rotation family, which is what stops the attack.

A family needs an identity. A grant is keyed by its own token value, and `ClientId`, `SubjectId` and `SessionId` are
the only groupings, which are too coarse: a subject can hold several independent refresh-token families at one client,
such as several devices with no server-side session.

A legitimate client can also present a rotated token again. A client that lost the response of a rotation does, and so
do two tabs of one browser that refresh at the same time, so each of them would lose its session. Auth0 offers a reuse
interval and Okta a grace period for this, and FAPI 2.0 §5.3.2.1 Note 1 requires an authorization server that rotates
refresh tokens to let a client retry (issue [#270](https://github.com/NCodeGroup/identity/issues/270)).

## Decision

**A reused refresh token revokes its whole rotation family, unless the client opted in to a reuse interval and the
token is presented within it, which is a retry.**

1. **Lineage.** Every refresh-token family shares a `RefreshTokenLineageId`. A fresh issuance (authorization code,
   password, device code, CIBA and so on) generates it with `CryptoService.GenerateResourceId()`, and a rotation or a
   retry inherits it through `CreateSecurityTokenRequest.RefreshTokenLineageId`. The id lives on the grant envelope
   (`PersistedGrant`), not in the refresh-token payload, because it is queryable metadata like `SubjectId` and
   `SessionId`. It is a nullable column indexed on `(TenantId, RefreshTokenLineageId)`, null for grants that are not
   refresh tokens.
2. **Reuse policy.** The `refresh_token_reuse_policy` setting takes `none` (refuse and audit) or `revoke_all` (refuse,
   audit and revoke the family). It defaults to `revoke_all`, merges with `Replace` and is not advertised, because it is
   server-side security behavior that a client does not negotiate.
3. **What a reuse is.** A reuse is a rotated token presented outside the reuse interval, or a spend that lost a race
   ([ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md)).
   It is answered `invalid_grant` (`400`) and audited as `token.refresh.replay`. Under `revoke_all`,
   `IPersistedGrantService.RevokeByRefreshTokenLineageAsync` revokes the family's active grants. It loads and mutates
   the matched rows rather than issuing a set-based update, the bulk-revocation pattern of
   [ADR-1020](1020-a-tenants-grants-are-listed-inspected-and-revoked-by-opaque-identifier-never-by-their-key.md), so the
   interceptor stamps the concurrency token of each changed row
   ([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)),
   and a lost race reads again, up to three attempts in total
   ([ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md)). Rows that are already revoked are untouched.
4. **The reuse interval.** `refresh_token_reuse_interval` is a `TimeSpan` setting for clients that merges with
   `Replace`, is not discoverable, defaults to zero, which turns it off, and is limited to 0 to 60 seconds
   (`OpenIdSettingRules.RefreshTokenReuseInterval`, `SettingRules.Between`). A longer window weakens reuse detection,
   Okta caps its grace period at 60 seconds, and RFC 9700 makes no exception for a retry, which Auth0 and Okta make
   opt-in.
5. **What a retry is.** A presented grant is a retry when it is revoked with `ConsumedWhen` set, has not expired, and
   the current time is before `ConsumedWhen` plus the interval. `ReplaceOnceOrDefault` sets `ConsumedWhen` to the
   creation time of the successor, and `TryRevokeOnceAsync`, which spends a token without a successor, does not, so
   only a grant that a rotation spent qualifies. A rotation that loses to a concurrent rotation of the same token reads
   the grant again, finds it consumed by the winner, and is answered as a retry too, so two tabs that refresh at the
   same time both keep their session.
6. **A retry is answered only while it can issue another refresh token.** `offline_access` has to be in the effective
   scopes, the grant has to name a lineage, and a grant of that lineage has to be active. Otherwise the answer is
   `invalid_grant` with no replay audit event and no revocation of the family.
7. **A retry branches the family.** `ITokenService.ReissueRefreshTokenAsync` stores the branch through
   `IPersistedGrantService.AddToRefreshTokenLineageOrDefault`, which in one unit of work reads the active grants of
   the lineage (`IGrantStore.GetActiveByRefreshTokenLineageAsync`), updates each of them unchanged so that it gets a
   new concurrency token, and adds the successor. A lost race reads again, up to three attempts in total. A concurrent
   revocation of the lineage, the session, the subject or the grant conflicts with the branch either way: if the
   revocation commits first, the read finds nothing active, and if the branch commits first, the revocation reads again
   and revokes the branch too.
8. **A spend reads again after a write that left the grant active.** The update of decision 7 changes the concurrency
   token of the grants that stay active, so `ReplaceOnceOrDefault` and `TryRevokeOnceAsync` read the grant again after
   a lost race, up to three attempts in total. A read that finds the grant inactive is a reuse, and after the last
   conflict the exception propagates. Without the read, a concurrent rotation would look like a reuse.
9. **The retry response.** It carries a new access token, an ID token when `openid` is requested, and a new refresh
   token in the same lineage. The first successor stays valid, so the lineage branches.
10. **A retry is audited.** An answered retry records `RefreshTokenRetryAuditEvent` (action `token.refresh.retry`,
    outcome `success`) through `IAuditEventRecorder.RecordRefreshTokenRetryAsync`, so an operator can tell a retry from
    a rotation and from a reuse. A retry that decision 6 refuses records nothing. Both retry paths, a presented grant
    within the interval and a rotation that lost to a concurrent rotation, are answered and audited through one method.
11. **A client that must allow a retry has an interval.** `refresh_token_rotation_retry_required` is a `bool` setting
    for clients that merges with `Or`, defaults to `false` and is not discoverable. The FAPI 2.0 security profile sets
    it through its ceiling. `RefreshTokenRotationRetryCoherenceRule` refuses a client whose effective setting is
    `true`, whose refresh tokens can rotate and whose effective `refresh_token_reuse_interval` is not positive
    ([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)).

## Options considered

- **Revoke by subject and client, with no new column.** Rejected: it over-revokes. A subject with several concurrent
  families at one client would lose all of them when any one is replayed, which is coarser than the authorization grant
  that RFC 9700 names.
- **Revoke by session (`sid`).** Rejected: many families exist outside any server-side session, so `sid` does not
  generalize. Ending a session revokes by `sid` for its own purpose
  ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)).
- **Keep the lineage only in the payload.** Rejected: revoking a family needs an indexed, queryable column, and payload
  JSON is neither portably queryable nor indexable.
- **Default the policy to `none`.** Rejected: a detected compromise would leave the family usable. `revoke_all` costs
  nothing until a replay happens, and `none` stays available to a deployment that wants audit only.
- **Refuse the retry without revoking the family.** Rejected: a client that lost the response still cannot recover, and
  the successor that the lost response carried stays valid for whoever holds it.
- **Answer the retry with the same successor.** Rejected: only the hash of a refresh token is stored, so the server
  cannot return the value of the first successor again.
- **Rotate the current head of the lineage again.** Rejected: it revokes the successor that the other tab holds, and
  that tab then trips reuse detection when it refreshes.
- **Turn the interval on by default, or allow more than 60 seconds.** Rejected for the reasons of decision 4.
- **A lineage id, `revoke_all` by default and an opt-in, capped retry interval.** Chosen: it implements RFC 9700
  §4.14.2, matches Auth0's reuse detection, revokes exactly the compromised chain, and lets a client recover from a
  lost response when it opts in.

## Consequences

- A replayed refresh token invalidates its whole rotation family by default: the legitimate client's next refresh
  fails and it must authenticate again, which is the intended outcome when a token in the chain is known to be
  compromised.
- The grants table has a nullable `RefreshTokenLineageId` column and an index, populated only for refresh-token grants.
  Any other rotated grant type can populate the same column to get family revocation.
- A client that opts in recovers within the interval from a lost rotation response, and two tabs that refresh at the
  same time both keep their session.
- A stolen rotated token that is used within the interval receives a branch. Reuse detection still revokes the whole
  family when a spent token is presented outside the interval, and revoking any token of the family ends every branch.
- A retry that is answered is audited as a retry (issue [#275](https://github.com/NCodeGroup/identity/issues/275)), and
  a retry that is refused records no event.
- A client that requires a retry, as the FAPI 2.0 security profile does, and whose refresh tokens rotate has a reuse
  interval, because the coherence rule of decision 11 refuses a client without one (issue
  [#272](https://github.com/NCodeGroup/identity/issues/272)).
- `IPersistedGrantService` declares `AddToRefreshTokenLineageOrDefault` and `RevokeByRefreshTokenLineageAsync`,
  `ITokenService` declares `ReissueRefreshTokenAsync`, `IGrantStore` declares `GetActiveByRefreshTokenLineageAsync`,
  and `IAuditEventRecorder` declares `RecordRefreshTokenRetryAsync`, so every implementation of one of them implements
  the member.

## References

- [ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md) —
  which refresh tokens rotate, and a rotation spends the presented token with its successor.
- [ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md) — a revocation reads again and retries when it loses a race.
- [ADR-1028](1028-the-revocation-endpoint-revokes-refresh-tokens-and-their-families-and-always-answers-200.md) — the
  revocation endpoint revokes refresh tokens.
- [ADR-1020](1020-a-tenants-grants-are-listed-inspected-and-revoked-by-opaque-identifier-never-by-their-key.md) — the
  load-and-mutate bulk-revocation pattern.
- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) —
  the interceptor that stamps changed rows.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) —
  session-scoped grant revocation.
- [ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md) —
  coherence rules.
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) — OAuth 2.0 Security Best Current Practice (§4.14.2).
- [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009) — OAuth 2.0 Token Revocation (§2.1).
- [FAPI 2.0 Security Profile](https://openid.net/specs/fapi-security-profile-2_0-final.html) (§5.3.2.1).
- Issues [#270](https://github.com/NCodeGroup/identity/issues/270),
  [#272](https://github.com/NCodeGroup/identity/issues/272) and
  [#275](https://github.com/NCodeGroup/identity/issues/275).
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken.DefaultRefreshTokenGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Tokens.DefaultTokenService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Persistence.Stores.IGrantStore`.
