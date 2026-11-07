# 1468. A security effect that follows a committed change runs on its own bounded token

- **Status:** Accepted (amends [ADR-1226](1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md), [ADR-1237](1237-failed-password-attempts-are-limited-per-account.md))
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md)

## Context

After a change to an end user commits, the server ends sessions or clears failed password attempts. A principal block
([ADR-1226](1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md)), an
account disable ([ADR-1224](1224-deleting-or-disabling-an-account-ends-its-sessions.md)), an administrator's password
reset (ADR-1226) and a self-service password change
([ADR-1230](1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md)) end sessions. An
administrator's account create and password reset clear the failed attempts
([ADR-1237](1237-failed-password-attempts-are-limited-per-account.md)).

Each of these must run even when the client disconnects, so none takes the request's token. Without a bound, a hung
store or back-channel logout holds the response indefinitely. An effect that throws turns the committed change into an
error response and skips the audit record that follows it
([#626](https://github.com/NCodeGroup/Identity/issues/626)).
[ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md) and
[ADR-1464](1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md) bound other
post-commit steps.

## Decision

**Every security effect that follows a committed change to an end user runs through `IPostCommitSecurityEffects`, on
its own bounded token, and a failure is logged, never thrown.**

1. **One seam serves every call site.** `IPostCommitSecurityEffects`, in
   `NCode.Identity.OpenId.Authentication.Abstractions`, has `TerminateSessionsAsync(openIdContext, subjectId, reason)`,
   `TerminateOtherSessionsAsync(openIdContext, subjectId, currentSessionId, reason)` and
   `ResetPasswordAttemptsAsync(tenantId, userName)`. Its singleton default, `DefaultPostCommitSecurityEffects` in
   `NCode.Identity.OpenId.Authentication`, serves the five call sites in the Management, Accounts.Management and
   Authentication.Interactive packages, so none repeats a bound and a catch. A host can register its own.
2. **One bounded step per effect, and no token parameter.** Each method runs its one call as one
   `PostCommitWork.RunAsync` step on a 30-second token from the injected `TimeProvider`. No method takes a
   `CancellationToken`, so no caller can pass the request's token and drop the effect. This is the exception to
   csharp-production §1's rule that every async method of ours takes a `CancellationToken`, for work that wholly follows
   a commit, as [ADR-1464](1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md)
   decision 2 states.
3. **A failure is logged, never thrown.** A call that throws, or a bound that elapses, is caught. A failed session
   termination is logged as `PostCommitSessionTerminationFailed` at `Error`, because the sessions stay active. A failed
   count reset is logged as `PostCommitPasswordAttemptResetFailed` at `Warning`, because the limit fails closed. The
   entries carry the tenant id, the subject id and the reason, and never the user name. The change answers success.
4. **The order is commit, then the security effects, then the audit record.** The five call sites and their reasons:
   - blocking a principal ends its sessions with `UserBlocked`;
   - disabling a local account ends its sessions with `AccountDisabled`;
   - an administrator's password reset ends the account's sessions with `PasswordReset` and clears its failed attempts;
   - an administrator's account create clears the failed attempts of the new account;
   - a self-service password change ends the other sessions with `PasswordChanged`.

   The management create and reset clear the count before the audit record, because no effect can throw, so nothing
   needs to run last. The local account's principal is resolved from its self-issued identity before the write, on the
   request's token, so the post-commit effect is one call.
5. **A pre-commit step and a revoke route keep the request's token.** The termination that precedes an account deletion
   precedes the commit ([ADR-1224](1224-deleting-or-disabling-an-account-ends-its-sessions.md)). The session and grant
   revoke routes revoke as the change itself. The tenant, team and platform `Notify…ChangedAsync` commands stay as
   [ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md) decides.
6. **A failed termination is repeated by repeating the request.** A block, a disable and a reset end the sessions on
   every call, so repeating the idempotent request repeats a failed termination. The response stays `204`.

## Options considered

- **An unbounded `CancellationToken.None` at each call site.** A hung store or back-channel logout holds the response
  indefinitely, and an effect that throws turns the committed change into an error and skips its audit record.
  Rejected.
- **A bound and a `catch` at each call site.** Five call sites in three packages repeat the same bound, the same catch
  and the same log event, and the next call site can omit them. A host cannot change the behavior in one place.
  Rejected.
- **An error response when an effect fails.** The change is committed, so an error tells the client to retry a change
  that has happened, and the audit record that follows is skipped. Rejected.
- **An outbox.** Each request completes the effect inline and knows its outcome before it answers
  ([project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md) decision 1).
  A block, a disable and a reset end the sessions on every call, so repeating the request repeats a failed
  termination. A queue and a worker add a store, a failure mode and a delay for no gain. Rejected.

## Consequences

- A client that disconnects after a commit skips no security effect and no audit record.
- A hung store adds at most 30 seconds per effect to the request that runs it.
- A failed termination leaves the sessions active until the request is repeated, while a blocked principal or a
  disabled account is still refused at every grant.
- A failed count reset keeps the account refused until its window ends.
- A host that registers its own `IPostCommitSecurityEffects` owns its bound and its failure handling.

## References

- [ADR-1224](1224-deleting-or-disabling-an-account-ends-its-sessions.md) — deleting or disabling an account ends its
  sessions
- [ADR-1226](1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md) — an
  administrator can block any end user, and a password reset ends the account's sessions
- [ADR-1230](1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md) — an end user changes their
  password and keeps only the current session
- [ADR-1237](1237-failed-password-attempts-are-limited-per-account.md) — failed password attempts are limited per
  account
- [ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md) — a committed change is
  applied and announced on its own bounded token
- [ADR-1464](1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md) — a management
  audit record runs on its own bounded token and takes no request token
- `IPostCommitSecurityEffects.cs`, `DefaultPostCommitSecurityEffects.cs`, `PostCommitWork.cs`
