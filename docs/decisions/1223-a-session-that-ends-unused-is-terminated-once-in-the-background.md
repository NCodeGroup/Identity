# 1223. A session that ends unused is terminated once, in the background

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

[ADR-1220](1220-an-end-user-session-is-a-grant-keyed-by-its-sid-bounded-by-idle-and-absolute-lifetimes.md) terminates an ended session
when a request finds it, through the termination seam
([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)). A session that ends while no
request uses it would only be pruned, which leaves these gaps:

- the grants issued under it, its refresh tokens among them, stay active, and its relying parties receive no
  back-channel logout, which Auth0 sends with the `session-expired` initiator;
- the grant prune ([ADR-1218](1218-ended-grants-are-removed-after-a-retention-period-in-bounded-batches.md)) removes an ended session after
  the retention whether it was terminated or not, so a session could vanish without ever being terminated;
- an `OpenIdContext` can be built only from an `HttpContext`, and the termination seam needs one, so no background work
  can run it;
- a request and a pass that find one ended session would both terminate it, and its relying parties would be notified
  twice (issue [#281](https://github.com/NCodeGroup/identity/issues/281)).

## Decision

**A session that ends while no request uses it is terminated once, by a background pass that one replica runs in each
interval, and the grant prune keeps a session until it is terminated.**

1. **An ended session is terminated by claiming it.** `RevokeSessionOnceAsync` revokes the session grant and every
   other grant issued under its `sid` in one unit of work, and returns the session grant as this call revoked it, or
   `null` when the grant is unknown, belongs to another tenant or is already revoked. Only the claiming call dispatches
   `OnSessionTerminatedCommand`, with the reason `absolute_expiry` or
   `idle_timeout`, and logs the termination. A dispatch that fails or is cancelled after the claim leaves no grant of
   the session active. `ISessionService.ValidateAndTouchAsync` and `ISessionService.TerminateIfEndedAsync`, which
   returns whether this call terminated the session, share one helper, so a request and the pass that find one ended
   session terminate it once.
2. **A hosted service runs the pass.** `EndedSessionTerminationService` awaits
   `IMaterializedPlatformAccessor.GetPlatformAsync`
   ([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)), so it never reads the store
   while the host starts. It then ticks a `PeriodicTimer` on `EndedSessionTerminationOptions.Interval` with the
   registered `TimeProvider` and runs one pass per tick. It never throws on stop, and it logs a failed pass and runs
   again at the next tick.
3. **One replica runs each interval.** A pass takes the lease `ended-session-termination:{window}` with
   `IExpiringKeySet.TryAddAsync`
   ([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)). The `window` is the
   UTC ticks of the pass's time divided by the ticks of the interval, and the key expires at the end of the window. A
   replica that loses the lease skips the pass. The claim of decision 1 makes a pass that two replicas run anyway
   harmless.
4. **The pass reads the ended sessions that are not revoked.**
   `IGrantStore.GetUnrevokedExpiredPageAsync(grantType, expiredAt, page, cancellationToken)` selects the unrevoked
   grants of the type whose expiry is at or before `expiredAt`, in tenants that are not disabled, ordered by `Id` with a
   keyset cursor. The index `(GrantType, RevokedWhen, ExpiresWhen)` of the grants table serves the query. The pass reads
   pages of 100 session grants until `NextCursor` is `null`.
5. **A background context has no request.**
   `IOpenIdContextFactory.CreateForBackgroundAsync(serviceProvider, tenantId, cancellationToken)` builds a
   `DefaultHttpContext` that carries the services of `serviceProvider` and creates the context through
   `CreateForTenantAsync`: the tenant is loaded uncached, and an unknown or disabled tenant throws. The context has no
   endpoint, connection or user, and its audit `Source` is empty.
6. **The pass groups each page by tenant.** For each tenant it creates an `AsyncServiceScope` and a context, and
   terminates each session on its own, so one session that fails does not stop the others. It logs:
   - `EndedSessionTerminationFailedForSession` (event 2071) for a session it cannot terminate, such as a malformed
     one, which it skips;
   - `EndedSessionTerminationFailedForTenant` (event 2058) for a tenant whose context cannot be created;
   - `EndedSessionTerminationFailed` (event 2057) for a failed pass;
   - `EndedSessionsTerminated` (event 2059) with the count, when it is above zero.
7. **The interval is configured and validated.** `EndedSessionTerminationOptions` is `public sealed`, with
   `SectionName` and `Interval`, which defaults to one minute. `EndedSessionTerminationOptionsValidator` validates it
   when the host starts, and `AddConfiguration` binds it strictly from `OpenId:EndedSessionTermination`
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
8. **The grant prune keeps a session until it is terminated.** A session ends only by revocation, so
   `GrantPruneService` removes it one retention after its termination, never before. The ended sessions of a disabled
   tenant wait until it is enabled, or are erased when it is deleted.

## Options considered

- **Clamp the lifetime of a refresh token to the end of its session.** Rejected: the idle end of a session slides with
  its use, so a clamp would end a refresh token of a session that continues. The termination of a session revokes its
  refresh tokens.
- **Terminate inside the grant prune.** Rejected: the prune runs every hour by default, so a session would be
  terminated up to an hour late.
- **A dispatched job for each session.** Rejected: the in-process dispatcher loses its jobs when the process restarts,
  and a session that no job reaches would never be terminated.
- **A pass on every replica without a lease.** Rejected: every replica would read the same pages and claim the same
  sessions, and all but one claim of each would find the session already revoked.
- **Revoke the session's grants in the dispatch rather than in the claim.** Rejected: a failure or a cancellation
  between the claim and the dispatch leaves the refresh tokens of an ended session active, and no later request or pass
  finds the session, because it is revoked.
- **A "terminated" marker column.** Rejected: it needs a new column on the grants table and a retry of the sessions that
  are revoked but not marked, and revoking the grants in the unit of work of the claim already closes the gap that
  matters, the tokens.
- **Chosen:** a claim that revokes the session and its grants in one unit of work, a background pass under a
  per-interval lease, and a prune that waits for the termination.

## Consequences

- A session that ends while no request uses it is terminated within about one interval of its end, on one replica: its
  grants are revoked, its relying parties receive back-channel logout with `absolute_expiry` or `idle_timeout`, and the
  termination is audited.
- A pass runs once the replica has materialized the platform, so it never reads the store while the host starts.
- The `check_session_iframe` cannot observe a termination in the background, because its state is a browser cookie;
  back-channel logout is the notification.
- A session is removed one retention after its termination, and the prune never removes a session that was not
  terminated.
- The ended sessions of a disabled tenant wait for the tenant.
- A malformed session is logged and skipped, so the tenant's other sessions are terminated.
- A context of a background pass has no request: no endpoint, connection or user, and an empty audit `Source`.
- An operator sets the interval with `OpenId:EndedSessionTermination:Interval`, and a misspelled key stops the host.
- A dispatch failure after the claim loses only that session's back-channel logout and audit, which the failure reports;
  the grants stay revoked.

## References

- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) — the termination seam that the pass
  dispatches.
- [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md) — the expiring key set that
  holds the lease of an interval.
- [ADR-1218](1218-ended-grants-are-removed-after-a-retention-period-in-bounded-batches.md) — the grant prune that keeps a session until it
  is terminated.
- [ADR-1220](1220-an-end-user-session-is-a-grant-keyed-by-its-sid-bounded-by-idle-and-absolute-lifetimes.md) — the session bounds and the
  termination by a request.
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) — the materialized platform that
  background work waits for.
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md) — the options that
  are validated when the host starts.
- [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) — the logout
  notification that a termination sends.
- Issue [#281](https://github.com/NCodeGroup/identity/issues/281).
- Code: `NCode.Identity.OpenId.Authentication.Sessions.EndedSessionTerminationService`,
  `NCode.Identity.OpenId.Authentication.Sessions.EndedSessionTerminationOptions`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Authentication.Persistence.GrantPruneService`,
  `NCode.Identity.OpenId.Persistence.Stores.IGrantStore`,
  `NCode.Identity.OpenId.Contexts.IOpenIdContextFactory`.
