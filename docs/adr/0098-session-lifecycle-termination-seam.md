# 98. Session lifecycle as an observable termination seam

- **Status:** Accepted (amended by
  [ADR-0220](0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md): a request that finds an
  ended session terminates it with `absolute_expiry` or `idle_timeout`;
  [ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md): an ended session is terminated
  once, by a request or the background pass;
  [ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md): deleting or disabling an account terminates
  its principal's sessions;
  [ADR-0226](0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md): blocking
  an end user or resetting its password terminates its principal's sessions;
  [ADR-0227](0227-a-session-is-terminated-at-most-once-on-every-path.md): every path terminates a session at most once;
  [ADR-0228](0228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md):
  a replayed authorization code terminates its session with `authorization_code_replay`;
  [ADR-0230](0230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md): a self-service password
  change terminates every other session with `password_changed`)
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

The end-user session subsystem ([ADR-0074](0074-session-management-and-logout.md)) originally exposed one termination
path: the RP-Initiated Logout `end_session_endpoint`, which dispatched `OnEndSessionCommand` so default handlers could
revoke the session's grants and send back-channel logout notifications. That command was "the logout side-effect
extension point".

Session **lifecycle** then grew more termination causes ([#63](https://github.com/NCodeGroup/Identity/issues/63)): a sliding **idle timeout**, a
**concurrent-session limit** that evicts the oldest sessions, administrative **revocation**, and subject-wide **global
sign-out**. The first implementations performed _bare_ grant revocations directly inside `DefaultSessionService`
(`store.RevokeWhereAsync(...)`), bypassing the logout seam. That was wrong in three ways:

1. **Correctness:** an idle-expired, evicted, or admin-revoked session skipped back-channel logout — the relying
   parties that participated in it were never told it ended, unlike a normal logout.
2. **Observability:** there was no audit record and no structured log for any session lifecycle transition.
3. **Extensibility:** consumers had no seam to react to a termination they did not initiate.

Per the extensibility ladder in [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md), "many contributors
react to a state change" is the canonical shape for a **mediator** seam, and a service may drive the mediator at that
seam (as `DefaultTokenService` drives the claims seam).

## Decision

**Unify every session termination behind one mediator seam, and keep session creation an inline capability.**

- **Termination → `OnSessionTerminatedCommand`** (fan-out, `TryAddEnumerable` handlers ordered by priority), carrying a
  reason name and a `SessionTermination` scope (subject id, session id — `null` for
  subject-wide — and the participating relying parties). It **replaces** `OnEndSessionCommand`. Default handlers:
    - `DefaultRevokeGrantsOnSessionTerminatedHandler` (high priority) — revokes the session's grants, or the subject's
      grants for a subject-wide termination;
    - `DefaultBackChannelLogoutOnSessionTerminatedHandler` (low priority) — notifies participating relying parties;
    - `DefaultAuditSessionTerminatedHandler` — records the `session.terminated` audit event.
- **The reason is an open vocabulary** ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
  `SessionTerminationReasons` names the well-known reasons: `logout`, `idle_timeout`, `absolute_expiry`,
  `account_deleted`, `account_disabled`, `concurrent_session_limit`, `administrative_revocation`, `global_sign_out`,
  `user_initiated`, `password_reset`, `user_blocked`, `authorization_code_replay`, and `password_changed`. An extension
  that ends a session for its own cause, such as a risk signal, passes its own name, and the handlers and the audit
  event carry it unchanged. Names are compared ordinally.

    Every termination path dispatches it: the `end_session_endpoint` (reason `Logout`), which terminates a named session
    through `ISessionService.RevokeAsync`, idle and absolute expiry found on an authorize request or by the background
    pass ([ADR-0223](0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)), concurrent eviction
    inside `DefaultSessionService`, the `ISessionService.RevokeAsync` / `RevokeAllAsync` administrative and
    global-sign-out paths, the deletion and the disabling of a local account
    ([ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md)), the blocking of an end user and an
    administrator's password reset
    ([ADR-0226](0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md)), the
    replay of an authorization code
    ([ADR-0228](0228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md)),
    and a self-service password change
    ([ADR-0230](0230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md)).
    `DefaultSessionService` drives the seam through `openIdContext.Mediator`. Every path that terminates a named session
    claims it first, so a session is terminated once however many paths end it at the same time
    ([ADR-0227](0227-a-session-is-terminated-at-most-once-on-every-path.md)).

- **Creation → inline.** A session is established by exactly one owner (interactive sign-in); it is not a fan-out
  concern, so per [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) ("the mediator must be earned") it
  stays a direct capability. `DefaultSessionService.CreateAsync` records the `session.created` audit event and a
  structured log inline — no seam.

- **Concurrent-session eviction → an injected policy service.** `ISessionEvictionPolicy` (default
  `DefaultOldestFirstSessionEvictionPolicy`, which keeps the newest sessions) is a shape-1 capability consumers swap to
  evict by a different strategy (least-recently-active, preserve multi-factor sessions, reject-newest, …).

- **Auditing and structured logging** are first-class: `IAuditEventRecorder` gains `RecordSessionCreatedAsync` /
  `RecordSessionTerminatedAsync` (publishing `SessionCreatedAuditEvent` / `SessionTerminatedAuditEvent`), and the
  authentication package logs session created / idle-expired / evicted / expired-on-authorize.

## Consequences

- Terminating a session from _any_ cause now consistently revokes grants, notifies relying parties, and audits — the
  idle/evict/admin/global-sign-out correctness gap is closed.
- Consumers extend session termination exactly as they extended logout: by registering another
  `ICommandHandler<OnSessionTerminatedCommand>`.
- The server-authoritative idle check runs on the authorize path (`DefaultAuthenticateHandler` calls
  `ISessionService.ValidateAndTouchAsync`); an invalid/idle-expired session self-heals by prompting for login, exactly
  like a stale cookie, rather than dead-ending with `access_denied`.
- A tenant-addressed **admin** management surface for sessions (`api/tenants/{tenantId}/sessions`) is **delivered**
  ([ADR-0100](0100-explicit-tenant-context-materialization.md)): the management endpoints are addressed by a route
  `{tenantId}` while `ISessionService` is bound to the ambient `OpenIdContext.Tenant`, so the admin surface materializes
  a full context for the addressed tenant (`IOpenIdContextFactory.CreateForTenantAsync`) and delegates to
  `ISessionService`, running the full termination seam (grant revocation, back-channel logout, auditing) in that
  tenant's security domain. The existing grants management surface provides subject-filtered listing and bulk revoke but
  bypasses the seam (no back-channel logout, no session audit); the sessions surface is the seam-running counterpart.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service / strategy-collection / mediator
  ladder this decision applies.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — why the termination reason is a name, not an
  `enum`.
- [ADR-0074](0074-session-management-and-logout.md), [ADR-0077](0077-session-scoped-grant-revocation-on-logout.md) —
  the session model and session-scoped revocation the seam generalizes.
