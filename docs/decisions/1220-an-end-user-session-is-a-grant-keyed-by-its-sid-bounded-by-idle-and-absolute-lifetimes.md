# 1220. An end-user session is a grant keyed by its sid, bounded by idle and absolute lifetimes

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

An end-user session has two bounds: `session_absolute_lifetime`, how long it lasts however it is used, and
`session_idle_timeout`, how long it lasts without a request that resolves it. A bound that a zero turns off lets a
session under the defaults never end: its grant has no expiry, the grant prune
([ADR-1218](1218-ended-grants-are-removed-after-a-retention-period-in-bounded-batches.md)) keeps it, and a user who
signed in stays signed in until the user signs out (issue [#271](https://github.com/NCodeGroup/identity/issues/271)).

An idle window that lives only in the payload of the session grant leaves the grant's row in the table until the
absolute end, and a request that finds a session past its absolute end has to terminate it through the termination seam
([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)) as it
does one past its idle window: the grants issued under the session are revoked and its relying parties receive
back-channel logout.

The active-sessions page, the management sessions API, sign-out everywhere and the concurrent-session limit all list a
subject's sessions, so listing has to read the versioned payload envelope of the stored grant correctly and report when
each session ends.

## Decision

**A session is a persisted grant keyed by an opaque `sid`; both of its bounds always apply, its grant expires when the
session ends unless it is used again, and a request that finds an ended session terminates it.**

1. **A session is a grant.** A session is a `PersistedGrant` of type `PersistedGrantTypes.Session`, keyed by an opaque
   256-bit `sid`, with no table of its own. Interactive sign-in creates it and mints the `sid` into the OP cookie
   principal and its `AuthenticationProperties`. `SessionGrant` holds the subject id, `AuthTime`, `amr`, `idp`,
   `AuthenticationContextClassReference`, `ClientIds`, `LastActivityWhen`, and the required `IdleTimeout` and
   `AbsoluteExpiresWhen`. A stored session without them is malformed and fails closed
   ([ADR-1395](1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md)).
2. **The `sid` is a claim.** It is emitted in the ID token, in every subject-bound access token (RFC 9068 section
   2.2.1) and in the `logout_token`. A token issued with no session omits it
   ([ADR-1074](1074-session-management-and-logout.md)).
3. **Both bounds always apply.** `session_absolute_lifetime` is between one second and 365 days and defaults to 30
   days. `session_idle_timeout` is between one second and 100 days and defaults to 3 days. Both apply to tenants and use
   `SettingRules.Between`
   ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)), so a zero is refused: no
   setting may weaken a security control below its safe baseline. The merge is `Replace`, because a tenant owns its
   users' session policy and the domain caps it. The defaults are Auth0's tenant defaults ("Require log in after" 30
   days, inactivity 3 days), and the longest values are Auth0's enterprise maxima. NIST SP 800-63B requires
   reauthentication at assurance level 1 at least once per 30 days.
4. **The grant expires when the session ends unless it is used again.** `CreateSessionRequest.AbsoluteLifetime` and
   `CreateSessionRequest.IdleTimeout` are required, and `ISessionService.CreateAsync` throws
   `ArgumentOutOfRangeException` for a value that is not positive. It stamps `SessionGrant.AbsoluteExpiresWhen`, the
   creation time plus the absolute lifetime, and `SessionGrant.IdleTimeout` into the payload. The grant expires at
   `min(AbsoluteExpiresWhen, LastActivityWhen + IdleTimeout)`.
5. **A request that resolves the session touches it.** `DefaultAuthenticateHandler` calls
   `ISessionService.ValidateAndTouchAsync`, which advances `LastActivityWhen` at most once a minute and slides the
   expiry with it. The touch is a read-modify-write
   ([ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md)), and a touch that loses every attempt is
   dropped.
6. **A request that finds an ended session terminates it.** A session ends at `AbsoluteExpiresWhen`, when its grant
   expires, or when its idle window lapses. `ValidateAndTouchAsync` terminates an ended session through the seam
   ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)) with
   the reason `absolute_expiry`, which it checks first, or `idle_timeout`. It logs `SessionAbsoluteExpired` (event 2056)
   or `SessionIdleExpired` (event 2028) and returns `false`, so the authorize request prompts for login. Both reasons
   revoke the grants issued under the session, notify its relying parties and are audited.
7. **The prune removes a session one retention after its termination**
   ([ADR-1218](1218-ended-grants-are-removed-after-a-retention-period-in-bounded-batches.md)). The background pass that
   terminates sessions that ended unused is
   [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)'s.
8. **A listed session reports when it ends.** `ISessionService.GetSessionsAsync` reads each stored payload through
   `IPersistedPayloadSerializer`, skips revoked sessions and sessions that have ended, and throws for a malformed one
   ([ADR-1395](1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md)).
   `SessionInfo.ExpiresWhen`, `SessionResource.ExpiresWhen` and `expiresWhen` of `/account/sessions/list` are never
   `null`.

## Options considered

- **Zero meaning "no bound".** Rejected: a setting may not weaken a security control below its safe baseline, and a
  session that never ends is the finding of issue [#271](https://github.com/NCodeGroup/identity/issues/271).
- **A grant expiry at the absolute end, with idle state only in the payload.** Rejected: the grant store does not read
  payloads, so an idle session would stay in the table until its absolute end, and every reader of a session would
  compute the idle end again.
- **A `Min` merge of the session settings.** Rejected: a tenant owns its users' session policy, and the domain caps it.
- **Translating a stored zero into the maximum.** Rejected: nothing expresses "no bound", and a value that is removed
  takes the default.
- **Both bounds always apply, with the grant expiring at the earlier end.** Chosen: it is bounded by construction, needs
  nothing specific to sessions in the grant store, and ends every session through one seam.

## Consequences

- By default a user signs in again at most 30 days after signing in, and after 3 days without a request that resolves
  the session, unless the tenant configures otherwise.
- Every ended session revokes its grants and notifies its relying parties, whether a request or the background pass
  finds it.
- A listing of sessions never reports a null end, and a malformed stored session fails closed instead of being guessed.
- A smell: a setting or request that accepts zero for a session bound, or a reader that computes an idle end of its own,
  has bypassed the grant's expiry.
- Tests cover creation with non-positive lifetimes, the touch and its throttle, termination by each reason, the
  listing and the setting domains.
- This implements issue [#271](https://github.com/NCodeGroup/identity/issues/271).

## References

- [ADR-1074](1074-session-management-and-logout.md) — end-user sessions, the `sid` claim and logout.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) — the
  termination seam that an ended session dispatches.
- [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md) — setting values are
  validated against their domain.
- [ADR-1218](1218-ended-grants-are-removed-after-a-retention-period-in-bounded-batches.md) — ended grants are removed
  after a retention period.
- [ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md) — the read-modify-write of a grant's payload.
- [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) — the background termination of
  a session that ends unused.
- [ADR-1395](1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md) — a
  malformed stored record fails closed.
- Auth0, Configure Session Lifetime Settings — the defaults and the maxima:
  <https://auth0.com/docs/manage-users/sessions/configure-session-lifetime-settings>
- [NIST SP 800-63B](https://pages.nist.gov/800-63-3/sp800-63b.html) section 4.1.3 — reauthentication at assurance
  level 1.
- [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) — the logout
  notification that a termination sends.
- Code: `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`.
