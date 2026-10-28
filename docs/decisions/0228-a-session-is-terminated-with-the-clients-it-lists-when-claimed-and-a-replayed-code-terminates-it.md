# 228. A session is terminated with the clients it lists when claimed, and a replayed code terminates it

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

[ADR-0227](0227-a-session-is-terminated-at-most-once-on-every-path.md) claims every named session with
`IPersistedGrantService.RevokeSessionOnceAsync` before it dispatches `OnSessionTerminatedCommand`. The claim reported
only whether the call revoked the session, so a termination notified the clients that an earlier read listed. A client
that an ID token issued under the session added to it between that read and the claim received no back-channel logout,
although its tokens were revoked (issue [#288](https://github.com/NCodeGroup/Identity/issues/288)). A subject-wide termination listed only the active sessions and then
revoked every grant of the subject, so a session that had ended and awaited the background pass
([ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)) was revoked without its relying
parties being notified. The replay of an authorization code revoked the grants of its session without the seam
([ADR-0112](0112-authorization-code-replay-revokes-session.md)), so its relying parties were not notified and nothing
was audited (issue [#287](https://github.com/NCodeGroup/Identity/issues/287)).

## Decision

**A termination of a named session takes its subject and clients from the session grant that its claim returns, a
subject-wide termination claims every unrevoked session of the subject, and a replayed authorization code terminates its
session through `ISessionService.RevokeAsync`.**

1. **The claim returns the session as it revoked it.** `IPersistedGrantService.RevokeSessionOnceAsync<TPayload>` returns
   `PersistedGrant<TPayload>?`: the session grant with `Status` `Revoked` and the payload as stored when the call
   revoked it, or `null` when the grant is unknown, belongs to another tenant or is revoked already. The payload is
   deserialized from the envelope that the committing attempt read, after the save, so a payload that cannot be read
   never keeps a session active. The unit of work of the claim is the only read of the session that no `AddClientAsync`
   write can follow: a concurrent client addition changes the session grant, which makes the claim retry, so the client
   list of the returned payload is complete.
2. **A termination names the subject and the clients of the claimed grant.** `DefaultSessionService` builds the
   `SessionTermination` of a named session from the claimed grant: its subject and the `ClientIds` of its payload.
   `ISessionService.RevokeAsync` claims without reading the session first, and `RevokeAllAsync` unions the clients of
   the grants that it claimed.
3. **A subject-wide termination claims every unrevoked session.** `RevokeAllAsync` and `RevokeAllExceptAsync` claim
   each session grant of the subject that no caller revoked, a session that has ended included. They read a page of the
   session grants of the subject and skip the revoked ones without reading a payload. A session that has ended and
   awaits the background pass is terminated with the reason of the subject-wide termination, and its relying parties
   are notified, instead of being revoked silently. The eviction at the concurrent-session limit claims the active
   sessions that it selects with `GetSessionsAsync`.
4. **A replayed code terminates its session through the seam.** A replay is a code whose grant was redeemed
   (`ConsumedWhen` is set). `DefaultAuthorizationCodeGrantHandler` terminates the session of the code
   (`PersistedGrant.SessionId`) with `ISessionService.RevokeAsync` and the reason `authorization_code_replay`
   (`SessionTerminationReasons.AuthorizationCodeReplay`), so the claim revokes the grants issued under the session, its
   relying parties receive back-channel logout and the termination is audited
   ([ADR-0098](0098-session-lifecycle-termination-seam.md)). A code that expired unused, was revoked, or names no
   session terminates nothing: it is no compromise signal, and a termination notifies every relying party of the
   session.

## Options considered

- **Read the session again after the claim.** Rejected: a second unit of work, and the claim's own read is already
  complete because a concurrent client addition makes the claim retry.
- **Keep the replay outside the seam.** Rejected: the relying parties keep sessions whose tokens were revoked, and
  nothing is audited.
- **Treat every refused code as a replay.** Rejected: a code that expired unused is no compromise signal, and a
  termination would sign the end user out of every relying party.

## Consequences

- Each client that a session lists when it is claimed is notified once, a client that an ID token issued under the
  session added after an earlier read included.
- A session that has ended and awaits the background pass is terminated by a subject-wide termination with the reason of
  that termination, and its relying parties are notified.
- The replay of an authorization code is audited, and it notifies the relying parties of the session.
- **Breaking:** `RevokeSessionOnceAsync<TPayload>` returns `PersistedGrant<TPayload>?`, and
  `SessionTerminationReasons.AuthorizationCodeReplay` is added.
- This implements issues [#287](https://github.com/NCodeGroup/Identity/issues/287) and [#288](https://github.com/NCodeGroup/Identity/issues/288).

## References

- [ADR-0098](0098-session-lifecycle-termination-seam.md) — the termination seam that every termination dispatches.
- [ADR-0112](0112-authorization-code-replay-revokes-session.md) — the replay of an authorization code revokes its
  session.
- [ADR-0215](0215-a-revocation-wins-a-concurrency-race.md) — a concurrent write makes the claim read the grants again.
- [ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) — the claim of an ended session.
- [ADR-0227](0227-a-session-is-terminated-at-most-once-on-every-path.md) — every termination of a named session claims
  it.
- [RFC 6749 §4.1.2](https://www.rfc-editor.org/rfc/rfc6749#section-4.1.2) — the replay of an authorization code
  revokes the tokens that it issued.
- [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) — the logout
  notification that a termination sends.
- Code: `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.AuthorizationCode.DefaultAuthorizationCodeGrantHandler`.
