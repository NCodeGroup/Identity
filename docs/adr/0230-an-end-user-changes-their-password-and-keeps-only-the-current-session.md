# 230. An end user changes their password and keeps only the current session

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

[ADR-0226](0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md) decision 7
decides what a self-service password change does: it terminates every other session of the principal and keeps the
current one. The server offered no such change (backlog `R99`). The interactive surface is backend-agnostic
([ADR-0070](0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md)), so a password change cannot
assume a local account.

## Decision

**The interactive surface offers a password change through an optional seam, and a change ends every session of the end
user but the current one and revokes every grant that was issued under no session.**

1. **A seam changes the password, and the surface registers one that reports no support.**
   `IInteractivePasswordChanger.ChangePasswordAsync(OpenIdContext, ClaimsPrincipal, string currentPassword, string
   newPassword, CancellationToken)` returns `ValueTask<InteractivePasswordChangeResult>`. The enum is the closed set of
   outcomes of one operation: `NotSupported` (`0`), `CurrentPasswordInvalid` (`1`) and `Changed` (`2`).
   `AddInteractiveAuthentication` registers `DefaultInteractivePasswordChanger`, which always reports `NotSupported`,
   with `TryAddSingleton`, and `AddLocalAccountInteractiveAuthentication` replaces it with
   `LocalAccountInteractivePasswordChanger`.
2. **The local changer needs one enabled account and the current password.** It reads the principal id from the cookie
   principal and requires exactly one self-issued identity of the principal and an enabled account, and it reports
   `NotSupported` otherwise. It verifies the current password with `VerifyCredentialAsync(account.UserName, ...)` and
   requires the verified account to be the same `LocalAccountId`, and it reports `CurrentPasswordInvalid` otherwise.
   It replaces the password and saves in one unit of work. Both passwords are encoded into `SecureMemoryPool` buffers.
3. **`GET` and `POST /account/password` serve the change.** `InteractiveAuthenticationDefaults.PasswordPath` is the
   path. The form fields are `current_password`, `new_password` and `confirm_password`. The post redirects back to the
   page with `?error=` and one of `invalid_request`, `password_mismatch`, `invalid_current_password` or
   `not_supported`, or with `?status=changed`. Both methods require an active session
   ([ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md)) and redirect to the
   login page otherwise.
4. **A change ends the other sessions and the sessionless grants.** After `Changed`, the post calls
   `ISessionService.RevokeAllExceptAsync` with the principal, the `sid` of the cookie and the reason `password_changed`
   (`SessionTerminationReasons.PasswordChanged`). It claims every other unrevoked session of the principal as
   [ADR-0228](0228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md)
   describes, revokes every grant of the principal that was issued under no session with `GrantFilter.WithoutSession`,
   and then dispatches one termination for each session it claimed, so a dispatch that fails leaves only the grants of
   the current session active. Consents are kept. The "sign out everywhere else" action has the same effect, because
   one method has one meaning.
5. **There is no password policy and no limit on failed attempts.** The change accepts any non-empty password that the
   confirmation repeats, and the verification of the current password is not rate limited (backlog `R101` and `R102`).

## Options considered

- **A member of `IInteractiveCredentialVerifier`.** Rejected: a passwordless or external backend has no password to
  change, and the verifier would need a member that most implementations cannot answer.
- **End every session.** Rejected by
  [ADR-0226](0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md): it signs
  the end user out of the session that proved the change.
- **Revoke every grant outside the current session with one exclusion filter.** Rejected: it revokes a session that has
  ended and awaits the background pass without notifying its relying parties.

## Consequences

- The device that changes the password stays signed in, and every other device and every grant issued under no session
  is signed out.
- A host with a credential backend other than the local accounts offers the change by registering its own
  `IInteractivePasswordChanger`, and the page reports `not_supported` until it does.
- **Breaking:** `IInteractivePasswordChanger`, `InteractivePasswordChangeResult`,
  `InteractiveAuthenticationDefaults.PasswordPath`, `SessionTerminationReasons.PasswordChanged` and
  `GrantFilter.WithoutSession` are added, and `RevokeAllExceptAsync` revokes the grants issued under no session.
- This implements backlog `R99`.

## References

- [ADR-0070](0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md) — the interactive surface and
  its credential verifier.
- [ADR-0226](0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md) — a
  self-service change keeps the current session.
- [ADR-0227](0227-a-session-is-terminated-at-most-once-on-every-path.md) — every termination of a named session claims
  it.
- [ADR-0228](0228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md) —
  the claim returns the clients that the termination notifies.
- [NIST SP 800-63B](https://pages.nist.gov/800-63-3/sp800-63b.html) — a change of an authenticator ends the sessions
  that it authenticated.
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) V3.3.3 — terminate the other
  active sessions after a password change.
- Code: `NCode.Identity.OpenId.Accounts.Authentication.LocalAccountInteractivePasswordChanger`,
  `NCode.Identity.OpenId.Authentication.Interactive.InteractiveAuthenticationEndpointHandler`,
  `NCode.Identity.OpenId.Authentication.Sessions.DefaultSessionService`.
