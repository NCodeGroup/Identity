# 223. A session that ends unused is terminated once, in the background

- **Status:** Accepted (amended by
  [ADR-0227](0227-a-session-is-terminated-at-most-once-on-every-path.md): every other path that terminates a session
  claims it the same way;
  [ADR-0228](0228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md):
  a claim returns the session as it was revoked)
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

[ADR-0220](0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) terminates an ended session
only when a request finds it, through the termination seam
([ADR-0098](0098-session-lifecycle-termination-seam.md)). A session that ends while no request uses it was therefore
only pruned:

- the grants issued under it, its refresh tokens among them, stayed active, and its relying parties received no
  back-channel logout, which Auth0 sends with the `session-expired` initiator;
- a session that the release before the session bounds stored without an expiry was kept until a request found it;
- the grant prune ([ADR-0218](0218-ended-grants-are-removed-after-a-retention-period.md)) removed an ended session
  after the retention whether it was terminated or not, so a session could vanish without ever being terminated;
- an `OpenIdContext` could be built only from an `HttpContext`, and the termination seam needs one, so no background
  work could run it;
- a request and a pass that find one ended session would both terminate it, and its relying parties would be
  notified twice (issue [#281](https://github.com/NCodeGroup/Identity/issues/281)).

## Decision

**A session that ends while no request uses it is terminated once, by a background pass that one replica runs in each
interval, and the grant prune keeps a session until it is terminated.**

1. **An ended session is terminated by claiming it.** `IPersistedGrantService.SetRevokedOnceAsync` returns `true` only
   when this call set `RevokedWhen`. It revokes the grant whether or not the grant expired, and returns `false` when
   the grant is unknown, belongs to another tenant, or is already revoked, also after a lost race whose second read
   finds it revoked ([ADR-0215](0215-a-revocation-wins-a-concurrency-race.md)). An ended session is claimed first,
   with `IPersistedGrantService.RevokeSessionOnceAsync(sessionGrantId, now)`, which revokes the session grant and every
   other grant issued under its `sid` in one unit of work and returns `true` only when this call revoked the session
   grant. Only on `true` is `OnSessionTerminatedCommand` sent, with the reason `absolute_expiry` or `idle_timeout`, and
   `SessionAbsoluteExpired` or `SessionIdleExpired` logged. A dispatch that fails or is cancelled after the claim
   therefore leaves no grant of the session active. `ISessionService.ValidateAndTouchAsync` and
   `ISessionService.TerminateIfEndedAsync`, which returns `true` only when this call terminated the session, share one
   private helper, so a request and a pass that find one ended session terminate it once.
2. **A hosted service runs the pass.** `EndedSessionTerminationService` awaits
   `IMaterializedServerAccessor.GetServerAsync`, which completes at the replica's first request, so it never reads the
   store while the host starts. It then ticks a `PeriodicTimer` on `EndedSessionTerminationOptions.Interval` with the
   registered `TimeProvider` and runs one pass per tick. It never throws on stop, and a pass that fails is logged and
   run again at the next tick.
3. **One replica runs each interval.** A pass takes a lease with `IExpiringKeySet.TryAddAsync`
   ([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)). The key is
   `ended-session-termination:{window}`, where the window is the UTC ticks of the pass's time divided by the ticks of
   the interval, and the key expires at the end of the window. A replica whose add returns `false` skips the pass. The
   claim of decision 1 makes a pass that two replicas run anyway harmless.
4. **The pass reads the ended sessions that are not revoked.**
   `IGrantStore.GetUnrevokedExpiredPageAsync(grantType, expiredAt, page, cancellationToken)` selects the grants of the
   type that are not revoked and have no expiration or one at or before `expiredAt`, of a tenant that is not
   disabled, ordered by `Id` with a keyset cursor. The pass reads pages of 100 grants of the type
   `OpenIdConstants.PersistedGrantTypes.Session` at its time until `NextCursor` is `null`. The index
   `(GrantType, RevokedWhen, ExpiresWhen)` of the grants table, which the `AddGrantTypeRevokedWhenExpiresWhenIndex`
   migration adds, serves the query. A session grant without an expiration, which the release before the session
   bounds wrote, has ended
   ([ADR-0220](0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) decision 3) and is
   terminated with `absolute_expiry`.
5. **A background context has no request.**
   `IOpenIdContextFactory.CreateForBackgroundAsync(serviceProvider, tenantId, cancellationToken)` builds a
   `DefaultHttpContext` that carries the services of `serviceProvider`, resolves `IMediator` from them, and creates
   the context with `CreateForTenantAsync`: the tenant is loaded by its identifier, not cached and not seeded, and an
   unknown or disabled tenant throws. The context has no endpoint, so a client bound to a resource server is not
   resolved, and no connection or user; the audit `Source` is empty. The pass groups the sessions of a page by tenant.
   For each tenant it creates an `AsyncServiceScope` and a context, calls `TerminateIfEndedAsync` for each of the
   sessions, and disposes the context before the scope. A tenant whose context or termination fails is logged and
   skipped, so the sessions of the other tenants are terminated. The pass logs `EndedSessionTerminationFailed` (event
   2057, Warning) for a pass that failed, `EndedSessionTerminationFailedForTenant` (event 2058, Warning) for a tenant
   it skipped, and `EndedSessionsTerminated` (event 2059, Information) with the count, only when it is above zero.
6. **The interval is configured and validated.** `EndedSessionTerminationOptions` is `public sealed`, with
   `SectionName` and `Interval`, which defaults to one minute. `EndedSessionTerminationOptionsValidator` checks when
   the host starts that `Interval` is a period that a timer accepts, and `AddConfiguration` binds the options strictly
   from `OpenId:EndedSessionTermination`
   ([ADR-0221](0221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md)). The service reads
   the options on use, never in its constructor.
7. **Background work outside the core waits for the server through a public accessor.** `IMaterializedServerAccessor`
   in `NCode.Identity.OpenId.Servers` has `GetServerAsync(cancellationToken)`. The internal
   `IMaterializedServerRegistry` extends it, and `NCode.Identity.OpenId.Core` registers the accessor as the registry
   ([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)).
8. **The grant prune keeps a session until it is terminated.**
   `IGrantStore.RemoveEndedBeforeAsync(endedBefore, grantTypesEndedByRevocation, limit, cancellationToken)` removes a
   grant whose type is listed only when `RevokedWhen < endedBefore`, because such a grant ends only when it is
   revoked, and removes the other grants as before. `GrantPruneService` passes the session grant type, so a session is
   removed one retention after its termination. The ended sessions of a disabled tenant are neither terminated nor
   removed until the tenant is enabled, when they are terminated, or deleted, when they are erased with it.

## Options considered

- **Clamp the lifetime of a refresh token to the end of its session.** Rejected: the idle end of a session slides with
  its use, so a clamp would end a refresh token of a session that continues. The termination of a session revokes its
  refresh tokens.
- **Terminate inside the grant prune.** Rejected: the prune runs every hour by default, so a session would be
  terminated up to an hour late.
- **A dispatched job for each session.** Rejected: the in-process dispatcher loses its jobs when the process restarts
  ([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)), and a session that no
  job reaches would never be terminated.
- **A pass on every replica without a lease.** Rejected: every replica would read the same pages and claim the same
  sessions, and all but one claim of each would find the session already revoked.
- **Claim the session with `SetRevokedOnceAsync` and revoke the grants issued under it in the dispatch that follows.**
  Rejected: a failure or a cancellation between the two leaves the refresh tokens of an ended session active, and no
  later request or pass finds the session, because it is revoked.
- **Mark a session as terminated and have the pass retry the sessions that are revoked but not marked.** Rejected: it
  needs a new column on the grants table, and revoking the grants in the unit of work of the claim already closes the
  gap that matters, the tokens.

## Consequences

- A session that ends while no request uses it is terminated within about one interval of its end, on one replica:
  its grants are revoked, its relying parties receive back-channel logout with `absolute_expiry` or `idle_timeout`,
  and the termination is audited.
- A pass runs only once the replica has served its first request, because it waits for the materialized server.
- The `check_session_iframe` cannot observe a termination in the background, because its state is a browser cookie;
  back-channel logout is the notification.
- A session is removed one retention after its termination, and the prune never removes a session that was not
  terminated. A session grant without an expiration is terminated, not kept.
- The ended sessions of a disabled tenant wait for the tenant.
- A context of a background pass has no request: no endpoint, connection, or user, and an empty audit `Source`.
- An operator sets the interval with `OpenId:EndedSessionTermination:Interval`, and a misspelled key stops the host.
- Every other path that terminates a named session claims it the same way
  ([ADR-0227](0227-a-session-is-terminated-at-most-once-on-every-path.md)).
- A failure or a cancellation of the dispatch after the claim loses the back-channel logout and the audit of that
  session, which the failure reports (a request fails, and a pass logs the tenant); the grants of the session are
  revoked.
- **Breaking:** `IPersistedGrantService.SetRevokedOnceAsync` returns `ValueTask<bool>`, and
  `IGrantStore.RemoveEndedBeforeAsync` takes the grant types that end by revocation.
  `IGrantStore.GetUnrevokedExpiredPageAsync`, `IPersistedGrantService.RevokeSessionOnceAsync`,
  `ISessionService.TerminateIfEndedAsync`, `IOpenIdContextFactory.CreateForBackgroundAsync`, and
  `IMaterializedServerAccessor` are added, so every implementation of those interfaces implements them. The
  `AddGrantTypeRevokedWhenExpiresWhenIndex` migration adds an index to the grants table.
- This implements issue [#281](https://github.com/NCodeGroup/Identity/issues/281).

## References

- [ADR-0098](0098-session-lifecycle-termination-seam.md) — the termination seam that the pass dispatches.
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — the expiring key set that
  holds the lease of an interval.
- [ADR-0215](0215-a-revocation-wins-a-concurrency-race.md) — `SetRevokedOnceAsync` and its retry.
- [ADR-0218](0218-ended-grants-are-removed-after-a-retention-period.md) — the grant prune that keeps a session until it
  is terminated.
- [ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — the materialized server
  that background work waits for.
- [ADR-0220](0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) — the session bounds and the
  termination by a request.
- [ADR-0221](0221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md) — the options that
  are validated when the host starts.
- [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) — the logout
  notification that a termination sends.
- Code: `NCode.Identity.OpenId.Authentication.Sessions.EndedSessionTerminationService`,
  `NCode.Identity.OpenId.Authentication.Sessions.EndedSessionTerminationOptions`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Authentication.Persistence.GrantPruneService`,
  `NCode.Identity.OpenId.Persistence.Stores.IGrantStore`,
  `NCode.Identity.OpenId.Contexts.IOpenIdContextFactory`,
  `NCode.Identity.OpenId.Servers.IMaterializedServerAccessor`.
