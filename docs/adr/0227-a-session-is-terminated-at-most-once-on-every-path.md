# 227. A session is terminated at most once on every path

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

[ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) terminates an ended session by
claiming it with `IPersistedGrantService.RevokeSessionOnceAsync`, so a request and the background pass terminate it
once. The other paths that terminate a named session dispatched `OnSessionTerminatedCommand` without a claim:
`ISessionService.RevokeAsync`, `RevokeAllExceptAsync`, `RevokeAllAsync`, the eviction at the concurrent-session limit
and the RP-initiated logout. A session that two paths ended at once notified its relying parties twice and was audited
twice, and a subject-wide termination revoked the grants of the subject only in its dispatch, so a dispatch that failed
left them active (backlog `R95`).

## Decision

**Every path that terminates a named session claims it with `IPersistedGrantService.RevokeSessionOnceAsync` before it
dispatches `OnSessionTerminatedCommand`, and a subject-wide termination revokes every grant of the subject before it
dispatches.**

1. **Every termination of a named session claims it.** `RevokeSessionOnceAsync` revokes the session grant and every
   grant issued under its `sid` in one unit of work. `DefaultSessionService` dispatches the termination of a named
   session only when its call claimed the session, in `RevokeAsync`, `RevokeAllExceptAsync`, the eviction at the
   concurrent-session limit, the RP-initiated logout (through `RevokeAsync`) and the termination of an ended session
   ([ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)). One private helper,
   `TerminateOnceAsync`, claims a session, dispatches its termination and returns whether the call claimed it.
   `RevokeAllAsync` claims each session it lists as decision 3 describes.
2. **The service reports what it terminated.** `ISessionService.RevokeAsync` returns `ValueTask<bool>`: `true` when the
   call terminated the session, and `false` when the session does not exist or is revoked already, also by a concurrent
   caller. `RevokeAllAsync` and `RevokeAllExceptAsync` return `ValueTask<int>`, the number of sessions the call
   terminated. The sessions management API reports and audits those results instead of counting before it terminates.
3. **A subject-wide termination claims each session and revokes every grant before it dispatches.** `RevokeAllAsync`
   claims each active session of the subject. It then revokes every grant of the subject with
   `IPersistedGrantService.RevokeWhereAsync` and a `GrantFilter` that names the subject, so a dispatch that fails leaves
   no grant active. It always dispatches one subject-wide termination, whose `SessionId` is `null` and whose
   participating clients are the distinct clients of the sessions that the call claimed. It dispatches also when it
   claimed no session, so the handlers that extend the seam observe every subject-wide termination, and a concurrent
   second call notifies no relying party.
4. **The end session endpoint terminates the session through the service.** `DefaultEndSessionHandler` terminates the
   session that a logout names with `ISessionService.RevokeAsync` and `SessionTerminationReasons.Logout`. A logout that
   names no session dispatches the termination of the subject, which has no relying parties to notify.
5. **An eviction claims each session it evicts.** The eviction at the concurrent-session limit claims each evicted
   session at the creation time of the session that causes the eviction, and logs `SessionEvicted` only for a session
   that it claimed.

## Options considered

- **Dispatch one termination for each session in `RevokeAllAsync`.** Rejected: a subject-wide termination sends one
  logout token with `sub` to each relying party, which covers every session it participates in, and a termination for
  each session would send a token for each session.
- **Let the grant-revocation handler claim the session.** Rejected: it runs inside the dispatch, after the decision to
  notify the relying parties has been made.
- **Skip the subject-wide dispatch when no session was claimed.** Rejected: the handlers that extend the seam would miss
  a subject-wide termination, and its audit record.
- **Return the claimed session from `RevokeSessionOnceAsync`.** Deferred (backlog `R97`): a termination notifies the
  clients that its session listed when it was read, and `RevokeSessionOnceAsync` reports only whether the call revoked
  the session.

## Consequences

- Each relying party of a session is notified once, and each session is audited once, however many paths end it.
- A subject-wide termination always revokes the grants of the subject before its dispatch, and it is audited also when
  it terminated no session.
- The sessions management API reports the sessions that the request terminated.
- A client that an authorization request adds to a session between the read and the claim receives no back-channel
  logout, although its tokens are revoked (backlog `R97`).
- The replay of an authorization code revokes the grants of its session without the seam, so the relying parties of the
  session are not notified (backlog `R96`).
- **Breaking:** `ISessionService.RevokeAsync` returns `bool`, and `RevokeAllAsync` and `RevokeAllExceptAsync` return the
  number of sessions that the call terminated.
- This implements backlog `R95`.

## References

- [ADR-0098](0098-session-lifecycle-termination-seam.md) — the termination seam that every path dispatches.
- [ADR-0215](0215-a-revocation-wins-a-concurrency-race.md) — a revocation wins a concurrency race, so a claim that
  returns `false` found the session revoked already.
- [ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) — the claim of an ended session.
- [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) — the logout
  notification that a termination sends.
- Code: `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers.DefaultEndSessionHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.Sessions.SessionApiEndpointHandler`,
  `NCode.Identity.OpenId.Authentication.Persistence.IPersistedGrantService`.
