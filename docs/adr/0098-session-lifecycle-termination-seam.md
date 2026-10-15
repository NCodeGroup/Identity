# 98. Session lifecycle as an observable termination seam

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

The end-user session subsystem ([ADR-0074](0074-session-management-and-logout.md)) originally exposed one termination
path: the RP-Initiated Logout `end_session_endpoint`, which dispatched `OnEndSessionCommand` so default handlers could
revoke the session's grants and send back-channel logout notifications. That command was "the logout side-effect
extension point".

Session **lifecycle** then grew more termination causes ([F25](../../BACKLOG.md)): a sliding **idle timeout**, a
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
  `SessionTerminationReason` (`Logout`, `IdleTimeout`, `AbsoluteExpiry`, `ConcurrentSessionLimit`,
  `AdministrativeRevocation`, `GlobalSignOut`) and a `SessionTermination` scope (subject id, session id — `null` for
  subject-wide — and the participating relying parties). It **replaces** `OnEndSessionCommand`. Default handlers:
    - `DefaultRevokeGrantsOnSessionTerminatedHandler` (high priority) — revokes the session's grants, or the subject's
      grants for a subject-wide termination;
    - `DefaultBackChannelLogoutOnSessionTerminatedHandler` (low priority) — notifies participating relying parties;
    - `DefaultAuditSessionTerminatedHandler` — records the `session.terminated` audit event.

    Every termination path dispatches it: the `end_session_endpoint` (reason `Logout`), idle expiry and concurrent
    eviction inside `DefaultSessionService`, and the `ISessionService.RevokeAsync` / `RevokeAllAsync` administrative and
    global-sign-out paths. `DefaultSessionService` drives the seam through `openIdContext.Mediator`.

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
- A tenant-addressed **admin** management surface for sessions (`api/tenants/{tenantId}/sessions`) is deferred: the
  management endpoints are addressed by a route `{tenantId}` while `ISessionService` is bound to the ambient
  `OpenIdContext.Tenant`, so an admin surface must first resolve a per-tenant context (or gain a tenant-parameterized
  overload). The existing grants management surface already provides subject-filtered listing and bulk revoke in the
  interim. Tracked in [`BACKLOG.md`](../../BACKLOG.md).

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service / strategy-collection / mediator
  ladder this decision applies.
- [ADR-0074](0074-session-management-and-logout.md), [ADR-0077](0077-session-scoped-grant-revocation-on-logout.md) —
  the session model and session-scoped revocation the seam generalizes.
