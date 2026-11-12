# 1351. Work that follows a committed change runs on its own bounded token

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md)

## Context

A management write commits first and then does follow-up work: it evicts caches, announces the change to the other
replicas, terminates sessions, clears failed-attempt counters and records an audit event. The write has already
succeeded when that work starts, so the work must not depend on the request, and a failed announcement, security
effect or audit record must not turn the success into a failure.

- A client that disconnects after the commit cancels the request's token. Work that runs on that token skips an eviction,
  an announcement, a security effect or an audit record, and the other replicas keep serving stale state, a blocked
  principal keeps its sessions and a failed-attempt counter stays set.
- A store or Valkey timeout that surfaces as a `TaskCanceledException` while the request's token is untouched would
  fail a write that is already durable, and the caller would retry a change that happened.
- Unbounded work on `CancellationToken.None` lets a hung store hold the request open indefinitely.
- A recorder or effect method that accepts a `CancellationToken` invites its caller to pass the request's token, which
  defeats the rule.

[project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md) decision 1 keeps
such inline effects on `CancellationToken.None` with their own timeout. This record fixes that bound and the places
that use it.

## Decision

Work that wholly follows a committed change runs on its own bounded token, never on the request's.

1. **One helper holds the bound.** Each step after a commit runs through `PostCommitWork.RunAsync`, on
   `CancellationToken.None` bounded by `PostCommitWork.Timeout`, 30 seconds, through the injected `TimeProvider`.
   `PostCommitWork` is a public static class in `NCode.Identity.Persistence.Abstractions` (namespace
   `NCode.Identity.Persistence`) that every package shares. Each step takes the token it creates as its last parameter,
   and the request's token reaches none of them.
2. **Change handlers.** `TenantChangedHandler` evicts the tenant cache and the master keys and then announces the
   change; `TeamChangedHandler` evicts the team and tenant caches and then announces; `PlatformSettingsChangedHandler`
   refreshes and then announces. A failed refresh or announcement is logged (`PlatformSettingsChangeRefreshFailed`,
   `PlatformSettingsChangeBroadcastFailed`, `TeamChangeBroadcastFailed`, `TenantChangeBroadcastFailed`) and the write
   succeeds.
3. **A failed eviction still announces.** The tenant and team handlers evict (and the tenant handler seeds when the
   change carries `SettingsChanged`) inside a `try` whose `finally` announces, so the other replicas learn of the change
   even when the local eviction or seed fails. The seed keeps the request's token: its failure fails the write, and a
   tenant that lacks a key is seeded on its next materialization. A failed eviction also reaches the caller once the
   announcement has run; only a failed refresh or announcement is logged and swallowed.
4. **Audit records.** A management audit record runs in one `PostCommitWork` step that covers the tenant's
   environment-type lookup and the publish. No `IManagementAuditRecorder` method, and no helper whose only work is
   recording, takes a `CancellationToken`. A failure is logged as `AuditPublishFailed` and never thrown, so a caller
   that records several events records the rest. A custom recorder substitutes `DefaultManagementAuditRecorder` and owns
   its own bound.
5. **Security effects.** Every security effect that follows a committed change to an end user runs through
   `IPostCommitSecurityEffects`, declared in `NCode.Identity.OpenId.Authentication.Abstractions`, with the singleton
   default `DefaultPostCommitSecurityEffects` in `NCode.Identity.OpenId.Authentication`:
   - `TerminateSessionsAsync(openIdContext, subjectId, reason)`;
   - `TerminateOtherSessionsAsync(openIdContext, subjectId, currentSessionId, reason)`;
   - `ResetPasswordAttemptsAsync(tenantId, userName)`.

   Each call is one bounded step. A host can register its own implementation, which owns its bound.
6. **No token parameter.** Recorder and security-effect methods take no `CancellationToken`, so no caller can pass the
   request's token. This is the exception to csharp-production §1 for work that wholly follows a commit.
7. **Failures are logged, never thrown.** A failed termination is logged as `PostCommitSessionTerminationFailed` at
   `Error` (the sessions stay active); a failed counter reset is logged as `PostCommitPasswordAttemptResetFailed` at
   `Warning` (the limit fails closed). Entries carry the tenant id, and the subject id for terminations, and never the
   user name or the reason. The change answers success.
8. **Order and call sites.** The order is commit, then security effects, then the audit record. The call sites are:
   - blocking a principal (`UserBlocked`);
   - disabling a local account (`AccountDisabled`);
   - an administrator's password reset (`PasswordReset`, plus clearing failed attempts);
   - an administrator's account create (clearing failed attempts);
   - a self-service password change (`PasswordChanged`, other sessions).
9. **Pre-commit steps keep the request's token.** The termination before an account deletion, and the session and
   grant revoke routes, are not post-commit work and use the request's token. A failed termination is repeated by
   repeating the idempotent request, which answers `204`.
10. **The bound is fixed.** It is a backstop behind the store's and the Valkey client's own timeouts, and it equals the
    guarded client's default `TotalTimeout`
    ([ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md)). It is not an option.

## Options considered

- **The request's token.** A disconnect after the commit skips the follow-up work, and a timeout fails a durable write.
  Rejected.
- **Unbounded `CancellationToken.None`.** A hung store holds the request open indefinitely. Rejected.
- **A configured option for the bound.** The bound is a backstop behind the dependencies' own timeouts; an option adds
  a setting nobody has a reason to tune. Rejected.
- **A bound and a catch at each call site.** Every site repeats the pattern and one forgets it. Rejected.
- **A token parameter that the implementation ignores.** It invites callers to pass the request's token and misleads
  readers. Rejected.
- **Separate bounds for the lookup and the publish.** One audit record is one step; two bounds double the worst case
  without a benefit. Rejected.
- **An error response after a commit.** The change happened, so reporting failure makes the caller retry it. Rejected.
- **An outbox.** These effects are the inline effects that project-template ADR-0141 decision 1 keeps inline. Rejected.
- **Chosen:** `PostCommitWork.RunAsync` with its fixed 30-second bound for every step after a commit, with token-free
  recorder and security-effect methods.

## Consequences

- A client that disconnects after a commit skips no eviction, announcement, security effect or audit record.
- A hung store adds at most 30 seconds per step.
- A failure after a commit is visible only in the log; the entries named above are the signal to alert on.
- A new step that follows a commit goes through `PostCommitWork.RunAsync`, and a new recorder or effect method takes no
  token.

## References

- `PostCommitWork` in `NCode.Identity.Persistence.Abstractions`.
- `TenantChangedHandler`, `TeamChangedHandler`, `PlatformSettingsChangedHandler`, `IManagementAuditRecorder`,
  `DefaultManagementAuditRecorder`, `IPostCommitSecurityEffects` and `DefaultPostCommitSecurityEffects`.
- [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md).
- [project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md).
