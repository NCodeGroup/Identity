# 1230. An end user changes their password through an optional seam and keeps only the current session

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A self-service password change must end every other session of the principal, as OWASP ASVS V3.3.3 and NIST
SP 800-63B ask.
The interactive surface is backend-agnostic
([ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md)),
so a password change cannot assume a local account: a passwordless or external backend has no password to change.
The change must also leave the device that proved it signed in, and it must not leave a grant that outlives the
credential it was issued under.

## Decision

**The interactive surface offers a password change through an optional seam, and a change ends every session of the end
user but the current one and revokes every grant that was issued under no session.**

1. **A seam changes the password, and the surface registers one that reports no support.**
   `IInteractivePasswordChanger.ChangePasswordAsync(OpenIdContext, ClaimsPrincipal, string currentPassword, string
   newPassword, CancellationToken)` returns `ValueTask<InteractivePasswordChangeResult>`.
   `AddInteractiveAuthentication` registers `DefaultInteractivePasswordChanger`, which always reports `NotSupported`,
   with `TryAddSingleton`, and `AddLocalAccountInteractiveAuthentication` replaces it with
   `LocalAccountInteractivePasswordChanger` whatever the order of the two calls.
2. **The result is a sealed class that carries the page's error code.** `InteractivePasswordChangeResult` has
   `Changed`, `NotSupported`, `CurrentPasswordInvalid`, `Refused(error)`, `Error` and `IsChanged`
   (`[MemberNotNullWhen(false, nameof(Error))]`). `Refused` carries a code of the backend's own, such as the password
   policy's. The codes are an open vocabulary whose known values are `InteractivePasswordChangeErrors`:
   `invalid_request`, `password_mismatch`, `invalid_current_password` and `not_supported`.
3. **The local changer needs one enabled account and the current password.** It reads the principal from the cookie and
   requires exactly one self-issued identity and an enabled account, and it reports `NotSupported` otherwise. It judges
   the new password with `IPasswordPolicy` first and reports `Refused(code)` when the policy refuses it. It then
   verifies the current password through `VerifyCredentialAsync` under the limit on failed attempts
   (`PasswordAttemptGate`), requires the verified account to be the same `LocalAccountId`, and reports
   `CurrentPasswordInvalid` otherwise. It saves the new password in one unit of work. Both passwords are encoded into
   `SecureMemoryPool` buffers.
4. **`GET` and `POST` `InteractiveAuthenticationDefaults.PasswordPath` (`/account/password`) serve the change.** The
   form fields are `current_password`, `new_password` and `confirm_password`. The post redirects back to the page with
   `?error=<code>` or with `?status=changed`. Both methods require an active session and redirect to the login page
   otherwise.
5. **A change ends the other sessions and the sessionless grants.** After `Changed`, the post calls
   `IPostCommitSecurityEffects.TerminateOtherSessionsAsync` with the principal, the `sid` of the cookie and the reason
   `password_changed` (`SessionTerminationReasons.PasswordChanged`), on its own bounded token
   ([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)). That runs
   `ISessionService.RevokeAllExceptAsync`, which claims every other session of the principal, revokes every grant issued
   under no session with `GrantFilter.WithoutSession`, and then dispatches one termination for each claimed session
   ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)). A
   dispatch that fails leaves only the grants of the current session active. Consents are kept.
6. **"Sign out everywhere else" has the same effect.** The sessions page calls the same `RevokeAllExceptAsync` with the
   reason `user_initiated`, because one method has one meaning.
7. **The page guides the choice of password.** It advises a long passphrase used nowhere else, maps
   `password_too_short`, `password_too_long` and `password_blocklisted` to actionable messages, and shows a general
   message for an unknown code.
8. **Another credential backend offers the change by registering its own changer.** Until it does, the page reports
   `not_supported`.

## Options considered

- **A member of `IInteractiveCredentialVerifier`.** Rejected: a passwordless or external backend has no password to
  change, and the verifier would need a member that most implementations cannot answer.
- **End every session.** Rejected: it signs the end user out of the session that proved the change.
- **Revoke every grant outside the current session with one exclusion filter.** Rejected: it revokes a session that has
  ended and awaits the background pass without notifying its relying parties.
- **An optional seam, with a default that reports no support.** Chosen: a backend that cannot change a password says so,
  and the page reports it.

## Consequences

- The device that changes the password stays signed in, and every other device and every grant issued under no session
  is signed out.
- A host with a credential backend other than the local accounts offers the change by registering its own
  `IInteractivePasswordChanger`, and the page reports `not_supported` until it does.
- A failed termination dispatch leaves the other sessions and their grants revoked, but their relying parties receive
  no back-channel logout; only the current session's grants stay active.
- A "you chose wrong" smell: a second backend method that needs to answer "can this principal change a password" before
  the page renders would call for a capability probe on the seam.

## References

- [ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md)
  — the interactive surface and its credential verifier.
- [ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md) — an
  administrator's password reset ends every session, and the self-service pages require an active session.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) — every
  termination of a named session claims it and notifies its clients.
- [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md) — the bounded token that the
  post-commit effects run on.
- [ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) — the password policy that judges the new
  password.
- [ADR-1237](1237-failed-password-attempts-are-limited-per-account-and-a-refusal-costs-what-a-wrong-password-costs.md) — the limit on failed attempts that the
  verification of the current password observes.
- [NIST SP 800-63B](https://pages.nist.gov/800-63-3/sp800-63b.html) — a change of an authenticator ends the sessions
  that it authenticated.
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) V3.3.3 — terminate the other
  active sessions after a password change.
- Code: `NCode.Identity.OpenId.Accounts.Authentication.LocalAccountInteractivePasswordChanger`,
  `NCode.Identity.OpenId.Authentication.Interactive.InteractiveAuthenticationEndpointHandler`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`.
