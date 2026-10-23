# 220. An end-user session always ends, and its grant expires when it does

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

An end-user session has two bounds: `session_absolute_lifetime`, how long it lasts however it is used, and
`session_idle_timeout`, how long it lasts without a request that resolves it. Both defaulted to zero, and zero turned
the bound off. A session under the defaults therefore never ended: its grant had no expiry, so the grant prune
([ADR-0218](0218-ended-grants-are-removed-after-a-retention-period.md)) kept it, and a user who signed in stayed signed
in until the user signed out (backlog `R80`).

The idle window lived only in the payload of the session grant, so even a configured idle timeout left the grant's row
in the table until the absolute end. A request that found a session past its absolute end did not terminate it through
the termination seam ([ADR-0098](0098-session-lifecycle-termination-seam.md)), as one past its idle window was
terminated: the grants issued under the session were not revoked, and its relying parties received no back-channel
logout.

Listing a subject's sessions deserialized the versioned payload envelope of the stored grant (`$ver`, `$payload`) as the
payload, and it failed for every stored session. The active-sessions page, the management sessions API, sign-out
everywhere, and the concurrent-session limit all list a subject's sessions, so each of them failed for a subject that
had a session.

## Decision

**Both session bounds always apply, a session's grant expires when the session ends unless it is used again, and a
request that finds an ended session terminates it.**

1. **Both bounds always apply.** `session_absolute_lifetime` is between one second and 365 days and defaults to 30
   days. `session_idle_timeout` is between one second and 100 days and defaults to 3 days. Both use
   `SettingRules.Between` ([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)), so a zero is
   refused: no setting may weaken a security control below its safe baseline. The merge stays `Replace`, because a
   tenant owns its users' session policy and the domain caps it. The defaults are Auth0's tenant defaults ("Require log
   in after" 30 days, inactivity 3 days), and the longest values are Auth0's enterprise maxima. NIST SP 800-63B
   requires reauthentication at assurance level 1 at least once per 30 days.
2. **The grant expires when the session ends unless it is used again.** The expiry of a session's grant is
   `min(absolute end, LastActivityWhen + IdleTimeout)`. `CreateSessionRequest.AbsoluteLifetime` and
   `CreateSessionRequest.IdleTimeout` are required, and `ISessionService.CreateAsync` throws
   `ArgumentOutOfRangeException` for a value that is not positive. It stamps `SessionGrant.AbsoluteExpiresWhen`, the
   creation time plus the absolute lifetime, and `SessionGrant.IdleTimeout` into the payload, and it adds the grant
   with the shorter of the two lifetimes. A request that resolves the session advances `LastActivityWhen` at most once
   a minute and slides the expiry with it, through `IPersistedGrantService.UpdatePayloadAndExpirationAsync`, which
   replaces the payload and the expiration of an active grant in one unit of work. The call returns `false` and drops
   the touch when the grant changed between the service's read of it and its save, because that change stands. The
   payload that a touch writes is the one that the session service read before, so a write that landed between the two
   reads, such as a client that a concurrent authorize request added to the session, is overwritten (backlog `R91`).
   `SessionGrant.AbsoluteExpiresWhen` and `SessionGrant.IdleTimeout` are nullable, because a session can be stored
   without them: such a session takes the expiry of its grant as its absolute end, and its first touch stamps it.
3. **A request that finds an ended session terminates it.** A session ends at its absolute end, which is
   `AbsoluteExpiresWhen` or, for a session stored without one, the expiry of its grant, or when its grant has expired
   or its idle window has lapsed. A session with neither an absolute end nor an expiry has ended.
   `ISessionService.ValidateAndTouchAsync` dispatches `OnSessionTerminatedCommand`
   ([ADR-0098](0098-session-lifecycle-termination-seam.md)) with the reason `absolute_expiry`, which it checks first,
   or `idle_timeout`, and returns `false`. Both reasons therefore revoke the grants issued under the session, notify its
   relying parties, and are audited. The service logs `SessionAbsoluteExpired` (event 2056) or `SessionIdleExpired`
   (event 2028).
4. **The prune removes every ended session after the retention.** An ended session is a grant whose expiry has passed,
   so `GrantPruneService` removes it after the retention period of
   [ADR-0218](0218-ended-grants-are-removed-after-a-retention-period.md), whether a request terminated it or not. The
   prune and the grant store read only the expiry of a grant, so they need nothing that is specific to sessions.
5. **A listed session reports when it ends.** `ISessionService.GetSessionsAsync` reads each stored payload through
   `IPersistedPayloadSerializer`, skips revoked sessions and sessions that have ended by the rule of decision 3, and
   reports the time the session ends unless it is used again: the end of its idle window or, if earlier, its absolute
   end. `SessionInfo.ExpiresWhen`, `SessionResource.ExpiresWhen`, and `expiresWhen` of `/account/sessions/list` are
   therefore never `null`.
6. **The `BoundEndUserSessions` migration bounds the stored data.** It is a data migration in PostgreSQL SQL and makes
   two rewrites. In the settings document of every server and tenant, it removes a `session_absolute_lifetime` or
   `session_idle_timeout` value that is a string in TimeSpan form and that its domain refuses, so the default applies;
   it gives each rewritten row a regenerated `SettingsConcurrencyToken`, and it leaves a document that is not a JSON
   object and a value that is not in TimeSpan form as they are. It then gives every session grant that has no
   `ExpiresWhen` and is not revoked an `ExpiresWhen` of its `CreatedWhen` plus 30 days, the default absolute lifetime,
   and a regenerated `ConcurrencyToken`. `Down` restores nothing.

## Options considered

- **Keep zero as "no bound".** Rejected: a setting may not weaken a security control below its safe baseline, and a
  session that never ends is the finding of backlog `R80`.
- **Keep the grant's expiry at the absolute end and prune idle sessions from their payload.** Rejected: the grant store
  does not read payloads, and every reader of a session would compute the idle end again.
- **Terminate ended sessions in a background pass.** Deferred: it is backlog `R90`.
- **A `Min` merge of the session settings.** Rejected: a tenant owns its users' session policy, and the domain caps it.
- **Translate a stored zero into the maximum.** Rejected: nothing expresses "no bound", and removing the value applies
  the default.

## Consequences

- A user signs in again at most 30 days after signing in, and after 3 days without an authorize request, unless the
  tenant configures otherwise.
- A session that ends while no request uses it is not terminated: its grants are not revoked, its relying parties
  receive no back-channel logout, and the check-session iframe reports no change until the next authorization request.
  The prune removes it after the retention (backlog `R90`).
- A touch can overwrite a concurrent change to the payload of the session, and so can the addition of a client to the
  session. A client that an overwritten addition names receives no back-channel logout when the session is terminated
  (backlog `R91`).
- A session that the previous release stores without an expiry during the rollout is terminated when a request finds
  it and is otherwise kept (backlog `R90`), because the previous release keeps serving while a migration is applied
  ([ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)).
- A value outside a domain that the previous release stores during the rollout fails closed
  ([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)) until it is corrected.
- **Breaking:** `CreateSessionRequest.Lifetime` is replaced by the required `CreateSessionRequest.AbsoluteLifetime`, and
  `CreateSessionRequest.IdleTimeout` is required and not nullable. `SessionGrant.AbsoluteExpiresWhen` is added.
  `IPersistedGrantService.UpdatePayloadAndExpirationAsync` is added, so every implementation of the service implements
  it. `SessionInfo.ExpiresWhen`, `SessionResource.ExpiresWhen`, and `expiresWhen` of `/account/sessions/list` are not
  nullable.
- This implements backlog `R80`.

## References

- [ADR-0074](0074-session-management-and-logout.md) — end-user sessions, the `sid` claim, and logout.
- [ADR-0077](0077-session-scoped-grant-revocation-on-logout.md) — a termination revokes the terminating session's
  grants.
- [ADR-0098](0098-session-lifecycle-termination-seam.md) — the termination seam that an ended session dispatches.
- [ADR-0140](0140-setting-values-are-validated-against-their-domain.md) — setting values are validated against their
  domain.
- [ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md) — a migration keeps the
  previous release working.
- [ADR-0218](0218-ended-grants-are-removed-after-a-retention-period.md) — ended grants are removed after a retention
  period.
- Auth0, Configure Session Lifetime Settings — the defaults and the maxima:
  <https://auth0.com/docs/manage-users/sessions/configure-session-lifetime-settings>
- [NIST SP 800-63B](https://pages.nist.gov/800-63-3/sp800-63b.html) §4.1.3 — reauthentication at assurance level 1.
- [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) — the logout
  notification that a termination sends.
- Code: `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`.
