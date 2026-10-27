# 217. A rotated refresh token may be retried within a reuse interval

- **Status:** Accepted (amended in place: a retry is audited, and a client that must allow a retry has an interval)
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A rotated refresh token that is presented again is a reuse, and under `refresh_token_reuse_policy = revoke_all` the
server revokes its lineage
([ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md),
[ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md)). A client that lost the response of a rotation
presents the rotated token again, and so do two tabs of one browser that refresh at the same time, so each of them loses
its session.

Auth0 offers a reuse interval and Okta a grace period for this, and FAPI 2.0 §5.3.2.1 Note 1 requires an authorization
server that rotates refresh tokens to let a client retry (issue [#270](https://github.com/NCodeGroup/Identity/issues/270)).

## Decision

**A client may opt in to a reuse interval, within which a rotated refresh token that is presented again is a retry, not
a reuse.**

1. **The interval is a client setting that is off by default.** `refresh_token_reuse_interval` is a `TimeSpan` setting
   that applies to clients, merges with `Replace`, is not discoverable, defaults to `TimeSpan.Zero`, which turns it off,
   and is limited to the range from zero to 60 seconds (`SettingRules.Between`).
2. **A retry is answered as the rotation was.** The response carries a new access token, an ID token when `openid` is in
   the scope, and a new refresh token in the same lineage. The lineage is not revoked, and the first successor stays
   valid, so the lineage branches.
3. **Only a grant that a rotation spent qualifies.** `ReplaceOnceOrDefault` sets `ConsumedWhen` to the creation time of
   the successor, as well as `RevokedWhen`. `TryRevokeOnceAsync`, which spends a token without a successor, does not. A
   presented grant is within the interval when its status is revoked, `ConsumedWhen` is set, `ExpiresWhen` is null or
   later than the current time, and the current time is before `ConsumedWhen` plus the interval. A rotation that loses
   to a concurrent rotation of the same token reads the grant again when the interval is not zero, finds it consumed by
   the winner, and is answered as a retry too, so two tabs that refresh at the same time both keep their session.
4. **A retry is answered only while it can issue another refresh token.** The request has to issue a refresh token
   (`offline_access` is in the effective scopes), the grant has to name a lineage, and a grant of that lineage has to be
   active. Otherwise the answer is `invalid_grant` (`400`), with no replay audit event and no revocation of the lineage.
5. **A branch is stored atomically.** `IPersistedGrantService.AddToRefreshTokenLineageOrDefault<TPayload>` does the
   following in one unit of work: it reads the active grants of the lineage
   (`IGrantStore.GetActiveByRefreshTokenLineageAsync`), updates each of them unchanged through
   `IGrantStore.UpdateAsync`, which gives it a new concurrency token through the interceptor
   ([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)), and adds the successor. A lost race reads again, up to
   three attempts in total, and a lineage with no active grant returns `null`. A concurrent revocation of the lineage,
   the session, the subject, or the grant conflicts with the branch either way: if the revocation commits first, the
   read of the branch finds nothing active, and if the branch commits first, the retry of the revocation
   ([ADR-0215](0215-a-revocation-wins-a-concurrency-race.md)) revokes the branch too.
6. **A spend reads again after a write that left the grant active.** `ReplaceOnceOrDefault` and `TryRevokeOnceAsync`
   make up to three attempts in total. A read that finds the grant inactive returns `null` or `false`, which is a reuse,
   and after the last conflict the exception propagates. The update of decision 5 changes the concurrency token of the
   grants that stay active, so without the read a concurrent rotation would look like a reuse.
7. **The token service mints the branch.**
   `ITokenService.ReissueRefreshTokenAsync(openIdContext, openIdClient, request, cancellationToken)` stores the branch
   through `AddToRefreshTokenLineageOrDefault` and audits the issued token. It throws `ArgumentException` when
   `RefreshTokenLineageId` is null or empty, and returns `null`, without an audit event, when no grant of the lineage is
   active.
8. **Revoking a refresh token revokes its rotation family.** The revocation endpoint revokes the whole lineage of a
   refresh token that names one (`RevokeByRefreshTokenLineageAsync`), and the grant itself (`SetRevokedOnceAsync`)
   otherwise. RFC 7009 §2.1 permits revoking related tokens and the underlying grant, and the revocation ends every
   branch.
9. **The interval is capped at 60 seconds and is not on by default.** A longer window weakens reuse detection, and Okta
   caps its grace period at 60 seconds. RFC 9700 makes no exception for a retry, and Auth0 and Okta make it opt-in.
10. **A retry is audited.** When a retry is answered with another refresh token, the refresh-token grant records a
    `RefreshTokenRetryAuditEvent` (action `token.refresh.retry`, outcome `success`) through
    `IAuditEventRecorder.RecordRefreshTokenRetryAsync`, so an operator can tell a client's retry from a rotation and
    from a reuse (`token.refresh.replay`). A retry that decision 4 refuses records nothing. Both retry paths, a
    presented grant that is within the interval and a rotation that lost to a concurrent rotation, are answered and
    audited through one method.
11. **A client that must allow a retry has an interval.** The client setting `refresh_token_rotation_retry_required` is
    a `bool` that applies to clients, merges with `Or`, defaults to `false`, and is not discoverable, and the FAPI 2.0
    security profile sets it through its ceiling. The coherence rule `RefreshTokenRotationRetryCoherenceRule` refuses a
    client whose effective setting is `true`, whose refresh tokens can rotate, and whose effective
    `refresh_token_reuse_interval` is not positive
    ([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)).

## Options considered

- **Refuse the retry without revoking the lineage.** Rejected: a client that lost the response still cannot recover,
  and the successor that the lost response carried stays valid for whoever holds it.
- **Answer the retry with the same successor.** Rejected: only the hash of a refresh token is stored, so the server
  cannot return the value of the first successor again.
- **Rotate the current head of the lineage again.** Rejected: it revokes the successor that the other tab holds, and
  that tab then trips reuse detection when it refreshes.
- **Turn the interval on by default, or allow more than 60 seconds.** Rejected for the reasons of decision 9.

## Consequences

- A client that lost the response of a rotation recovers within the interval, and two tabs that refresh at the same time
  both keep their session, when the client opts in.
- A stolen rotated token that is used within the interval receives a branch. Reuse detection still revokes the whole
  lineage when a spent token is presented outside the interval, and revoking any token of the family ends every branch.
- A retry that is answered is audited as a retry (`token.refresh.retry`, issue [#275](https://github.com/NCodeGroup/Identity/issues/275)), and a retry that is refused
  records no event.
- A client that requires a retry, as the FAPI 2.0 security profile does, and whose refresh tokens rotate has a reuse
  interval, because the coherence rule of decision 11 refuses a client without one
  ([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#272](https://github.com/NCodeGroup/Identity/issues/272)).
- **Breaking:** `IPersistedGrantService` declares `AddToRefreshTokenLineageOrDefault`, `ITokenService` declares
  `ReissueRefreshTokenAsync`, `IGrantStore` declares `GetActiveByRefreshTokenLineageAsync`, and `IAuditEventRecorder`
  declares `RecordRefreshTokenRetryAsync`, so every implementation of one of them implements the member.
- This implements issues [#270](https://github.com/NCodeGroup/Identity/issues/270), [#272](https://github.com/NCodeGroup/Identity/issues/272) and [#275](https://github.com/NCodeGroup/Identity/issues/275).

## References

- [ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md) — reuse detection and the revocation of the
  rotation family.
- [ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md) — a rotation spends the presented token with
  its successor, and a lost spend is a reuse.
- [ADR-0215](0215-a-revocation-wins-a-concurrency-race.md) — a revocation reads again and retries when it loses a race.
- [ADR-0216](0216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md) — which refresh tokens rotate.
- [ADR-0028](0028-token-revocation-refresh-tokens-only.md) — the revocation endpoint revokes refresh tokens.
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) — OAuth 2.0 Security Best Current Practice (§4.14.2).
- [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009) — OAuth 2.0 Token Revocation (§2.1).
- [FAPI 2.0 Security Profile](https://openid.net/specs/fapi-security-profile-2_0-final.html) (§5.3.2.1).
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken.DefaultRefreshTokenGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Tokens.DefaultTokenService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Handlers.DefaultRevokeTokenHandler`,
  `NCode.Identity.OpenId.Persistence.Stores.IGrantStore`.
