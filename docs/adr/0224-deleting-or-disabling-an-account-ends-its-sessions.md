# 224. Deleting or disabling an account ends its sessions, and every token request checks the account

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A local account is a self-issued connection of a federated principal
([ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md),
[ADR-0035](0035-federated-principals-and-identity-resolution.md)), and the sessions, grants and consents of the end user
are keyed by the principal's identifier ([ADR-0094](0094-resolve-principal-at-every-authentication-entry-point.md)).
Deleting a local account removed the account, its self-issued identity and, with its last identity, the principal and
its role assignments ([ADR-0212](0212-deleting-an-owner-removes-what-it-owns.md)), but the sessions, the refresh
tokens and the consents of the principal remained. Disabling an account kept all of them as well. No handler of
`ValidateSubjectAuthenticationCommand` checked the account, so a refresh token of a deleted or disabled account was
redeemed until it expired (backlog `R89`).

## Decision

**Deleting or disabling a local account terminates its principal's sessions through the termination seam, a deleted
principal is erased with its consents and grants, and every token request refuses a subject whose local account is
disabled or gone.**

1. **Two reasons name the cause.** `SessionTerminationReasons.AccountDeleted` is `account_deleted`, and
   `SessionTerminationReasons.AccountDisabled` is `account_disabled`. The reasons are an open vocabulary
   ([ADR-0098](0098-session-lifecycle-termination-seam.md)), and the handlers and the audit event carry them
   unchanged.
2. **Disabling an account ends its sessions.** When `SetEnabledAsync(isEnabled: false)` of
   `LocalAccountApiEndpointHandler` has committed and the account has a self-issued identity, it creates the context of
   the addressed tenant with `IOpenIdContextFactory.CreateForTenantAsync`, as the sessions management endpoint does,
   and calls `ISessionService.RevokeAllAsync(context, principalId, SessionTerminationReasons.AccountDisabled, …)`. Every
   grant of the principal is revoked, its relying parties receive back-channel logout, and each termination is audited.
   Consents are kept, because disabling is reversible. Enabling an account does nothing.
3. **Deleting an account ends its sessions before it removes anything.** After it validates the deletion,
   `DeleteLocalAccountAsync` looks up the self-issued identity and terminates the principal's sessions with
   `AccountDeleted`. It then removes the account, the identity, and the principal when that identity was its last, and
   saves. `FederatedPrincipalStore.RemoveAsync` erases the consents and the grants of the principal in its tenant,
   besides its role assignments ([ADR-0212](0212-deleting-an-owner-removes-what-it-owns.md)), so the termination runs
   first, while the sessions can still be listed to notify their relying parties. A principal that keeps another
   identity keeps its consents, and the termination revoked its grants. A deletion that fails after the termination
   leaves the account with its sessions ended.
4. **Every token request checks the account.** `DefaultValidateLocalAccountSubjectHandler`, in
   `NCode.Identity.OpenId.Accounts.Authentication`, handles `ValidateSubjectAuthenticationCommand` at the default
   priority, after the default handler, and returns at once when the operation already has an error. It reads the
   identities of `subjectAuthentication.PrincipalId` from `IFederatedIdentityStore`:
   - when the principal has no identity, it refuses only a subject whose claim of the type that the tenant setting
     `PrincipalIssuerClaim` names equals `AccountConstants.SelfIssuer`, with "The end-user's account no longer
     exists.";
   - for each identity whose issuer is `AccountConstants.SelfIssuer`, it reads the account with
     `ILocalAccountStore.GetByIdOrDefaultAsync` and refuses when the account is missing or not enabled, with "The
     end-user's account is disabled or no longer exists.".

   A refusal sets `access_denied` through `ErrorFactory.AccessDenied`. `AddLocalAccountAuthentication` and
   `AddLocalAccountInteractiveAuthentication` register the handler. The command is sent by the authorization request
   and by the authorization-code, refresh-token and password grants, so the handler runs at each of them. It logs
   nothing, because the accounts packages have no band of event identifiers.
5. **A disabled account blocks its principal through every connection.** The local account is the server's record of
   the end user, and Auth0 blocks a user, not a connection. A subject that the local accounts cannot judge, which has no
   identity and no self-issuer claim or only external identities, is not refused.

## Options considered

- **Check that the principal exists in the core.** Rejected: a subject without a principal is valid for a host whose
  authentication scheme has no issuer claim, so the core cannot tell a deleted principal from one that never existed.
- **Revoke the grants without the termination seam.** Rejected: no relying party would receive back-channel logout, and
  no termination would be audited ([ADR-0098](0098-session-lifecycle-termination-seam.md)).
- **Remove the consents when an account is disabled.** Rejected: disabling is reversible, and the end user would be
  asked for consent again.
- **Terminate after the deletion commits.** Rejected: the erased grants could not be listed to notify the
  relying parties.
- **Block only the local connection.** Rejected: the principal could keep signing in through another connection.

## Consequences

- The refresh tokens of a disabled or deleted account stop at once, and so do its sessions. The JWT access tokens that
  were issued run to their expiry.
- A deleted principal leaves no consent or grant, and a deleted account leaves its sessions ended.
- The device-code, CIBA and token-exchange grants do not send `ValidateSubjectAuthenticationCommand`, so a disabled
  account is not refused there yet (backlog `R92`).
- A principal without a local account cannot be blocked (backlog `R93`).
- An administrator's reset of a password does not end the sessions of the account (backlog `R94`).
- The handler reads the identities of the principal and, for each self-issued identity, its account, in one unit of
  work each time it runs.
- **Breaking:** `SessionTerminationReasons.AccountDeleted` and `SessionTerminationReasons.AccountDisabled` are added,
  `IFederatedPrincipalStore.RemoveAsync` erases the consents and the grants of the principal, and
  `NCode.Identity.OpenId.Accounts.Management` references `NCode.Identity.OpenId.Authentication.Abstractions`.
- This implements backlog `R89`.

## References

- [ADR-0035](0035-federated-principals-and-identity-resolution.md) — the federated principal and its identities.
- [ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md) — the local account as a self-issued
  connection.
- [ADR-0094](0094-resolve-principal-at-every-authentication-entry-point.md) — the principal identifier is the subject
  that grants, consents and sessions are keyed by.
- [ADR-0098](0098-session-lifecycle-termination-seam.md) — the termination seam that the reasons name.
- [ADR-0212](0212-deleting-an-owner-removes-what-it-owns.md) — deleting an owner removes what it owns.
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) V3.3 — session logout and
  timeout.
- Auth0, Block and Unblock Users — Auth0 blocks a user, not a connection:
  <https://auth0.com/docs/manage-users/user-accounts/block-and-unblock-users>
- Code: `NCode.Identity.OpenId.Accounts.Management.LocalAccountApiEndpointHandler`,
  `NCode.Identity.OpenId.Accounts.Authentication.DefaultValidateLocalAccountSubjectHandler`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores.FederatedPrincipalStore`,
  `NCode.Identity.OpenId.Authentication.Sessions.SessionTerminationReasons`.
