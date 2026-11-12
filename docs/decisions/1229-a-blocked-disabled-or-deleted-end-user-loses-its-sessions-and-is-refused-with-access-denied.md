# 1229. A blocked, disabled or deleted end user loses its sessions and is refused with access_denied

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A local account is a self-issued connection of a federated principal
([ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md)), and the sessions,
grants and consents of the end user are keyed by the principal's identifier
([ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md)).
Four forces shape what happens when an end user is blocked, disabled or deleted:

- Deleting a local account removes the account, its self-issued identity and, with its last identity, the principal and
  its role assignments ([ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md)).
  The sessions, refresh tokens and consents of the principal would remain, and disabling an account would keep all of
  them. A refresh token of a deleted or disabled account would be redeemed until it expired (issue
  [#280](https://github.com/NCodeGroup/identity/issues/280)).
- A principal whose connections are all external has no state that an administrator can set (issue
  [#284](https://github.com/NCodeGroup/identity/issues/284)). Auth0 blocks a user, not a connection.
- An administrator's password reset rotates the security stamp of the account, which nothing reads, so the sessions and
  refresh tokens of a compromised account would keep working. OWASP ASVS V3.3.3 asks that a password change or reset
  terminates the other active sessions, across the relying parties as well (issue
  [#285](https://github.com/NCodeGroup/identity/issues/285)).
- A refusal handled only by the handlers of `ValidateSubjectAuthenticationCommand` leaves the interactive sign-in
  unprotected, so a blocked principal would get a session. The authorization request would challenge a refused subject
  again, which is a login loop for a host whose sign-in is another scheme. The self-service pages would serve a cookie
  whose session has ended, a disabled account would look like a wrong password, and a consent denial would leave the
  client unanswered (issue [#289](https://github.com/NCodeGroup/identity/issues/289)).

## Decision

**An administrator blocks an end user through its principal, and blocking, disabling, deleting or resetting the password
of its account ends its sessions. The end user's standing is a command of its own that every subject validation
includes, that the interactive sign-in sends before it creates a session, and whose refusal answers the client with
`access_denied`.**

1. **The standing is its own command.** `ValidateSubjectStandingCommand(OpenIdContext, SubjectAuthentication,
   OperationDisposition<IOpenIdError>)` asks whether the end user may sign in or get tokens at all, whatever the client
   or the request. `DefaultValidateBlockedSubjectHandler` and `DefaultValidateLocalAccountSubjectHandler` handle it.
2. **Every subject validation includes the standing.** `DefaultValidateStandingOnSubjectAuthenticationHandler` forwards
   each `ValidateSubjectAuthenticationCommand` that has no error to `ValidateSubjectStandingCommand` with the same
   disposition, so every grant and every CIBA approval refuses, also a grant that another package adds
   ([ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md)).
3. **The block is principal state.** `PersistedFederatedPrincipal.IsBlocked` is `false` by default, and
   `FederatedPrincipalStore` maps it when it adds, reads and updates a principal. `DefaultValidateBlockedSubjectHandler`
   refuses a blocked principal with `ErrorFactory.AccessDenied("The end-user is blocked.")` and logs
   `SubjectValidationFailed`; a missing principal is not refused.
4. **A disabled or deleted account blocks its principal through every connection.**
   `DefaultValidateLocalAccountSubjectHandler`, in `NCode.Identity.OpenId.Accounts.Authentication`, reads the identities
   of the principal. With none, it refuses only a subject whose `principal_issuer_claim` claim is
   `AccountConstants.SelfIssuer`. For each self-issued identity it reads the account with
   `ILocalAccountStore.GetByIdOrDefaultAsync` and refuses a missing or disabled account. It leaves every other subject
   alone, including one that has only external identities, and logs nothing, because the accounts packages have no band
   of event identifiers.
5. **The interactive sign-in validates the standing before it creates a session.** It sends the command after it
   resolves the principal, and a refusal creates neither a session nor a cookie.
6. **A refusal answers the parked authorization request.** When the `ReturnUrl` names a parked authorization request,
   the request is answered at the redirect URI of the client with `access_denied` and the `state` of the request,
   through `IContinueRefusalService.RefuseAsync` and `IContinueProvider.RefuseAsync`, which spend the continuation named
   by the `state` of the URL. Otherwise the login page shows the error code `access_denied`: "This account cannot sign
   in. Contact your administrator." A consent denial answers the parked request the same way, and it redirects to `/`
   only when the `ReturnUrl` names no parked request (OpenID Connect Core 1.0 §3.1.2.6). The refusal seam is apart from
   `IContinueService`, because `DefaultContinueService` is part of the constructor graph of the authorization provider.
7. **The authorization request classifies a refusal by the standing.** For a refused subject, `DefaultAuthorizeHandler`
   sends `ValidateSubjectStandingCommand`. A refused standing is answered with its error to the client for every
   `prompt` and audited as an authorization denial. Any other refusal challenges, or answers `login_required` for
   `prompt=none`. A fresh authentication cannot cure a blocked or disabled end user (RFC 6749 §4.1.2.1).
8. **A disabled account's credential is verified.** `LocalAccountStore.VerifyCredentialAsync` returns the account of a
   valid credential whether or not it is enabled, so the standing check refuses a disabled account with its reason
   after a correct password. The ASP.NET Core Identity adapter, whose disabled state is a lockout against guessing,
   refuses a locked-out user without verifying the password. The password grant answers a disabled account
   `invalid_grant`.
9. **The self-service surface serves a cookie only while its session is active.** Every interactive endpoint
   authenticates through `AuthenticateSessionAsync`, which requires a `sid` claim and calls
   `ISessionService.ValidateAndTouchAsync`, and signs the cookie out otherwise.
10. **The management API reads, blocks and unblocks a principal.** `PrincipalApiEndpointHandler`, in
    `NCode.Identity.OpenId.Management`, serves `GET api/tenants/{tenantId}/principals/{principalId}`, which authorizes
    `Operations.Read` on `TenantScopeResource` and returns a `PrincipalResource` (`TenantId`, `PrincipalId`,
    `IsBlocked`, `ConcurrencyToken`) with an `ETag`, and `POST .../block` and `POST .../unblock`, which authorize
    `Operations.Update` and answer `204`. The OpenAPI tag is `OpenIdConstants.EndpointTags.Principals`. Each change is
    audited as a `PrincipalChangedAuditEvent` (`ManagementResourceTypes.Principal`, change type `updated`) through
    `IManagementAuditRecorder.RecordResourceChangeAsync`. No management scope family exists: the authority is role-based
    on the tenant scope, as it is for sessions.
11. **Each change of standing ends the sessions with a reason.** The reasons are an open vocabulary
    ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)),
    and the handlers and the audit event carry them unchanged:
    - Block writes `IsBlocked = true` when it changes, then ends every session of the principal with
      `SessionTerminationReasons.UserBlocked`, `user_blocked`, also when the principal was blocked already. Unblock
      writes `IsBlocked = false` when it changes, ends no session and restores no revoked grant.
    - Disabling an account (`SetEnabledAsync(isEnabled: false)` of `LocalAccountApiEndpointHandler`) ends the
      principal's sessions with `SessionTerminationReasons.AccountDisabled`, `account_disabled`, when the account has a
      self-issued identity. Enabling does nothing.
    - An administrator's password reset (`ResetPasswordAsync`) ends the sessions of the principal of the account's
      self-issued identity with `SessionTerminationReasons.PasswordReset`, `password_reset`. The security stamp is not
      consulted.
    - Deleting an account (`DeleteLocalAccountAsync`) ends the sessions with `SessionTerminationReasons.AccountDeleted`,
      `account_deleted`, before it removes anything, then removes the account, the identity, and the principal when that
      identity was its last. `FederatedPrincipalStore.RemoveAsync` erases the consents, grants and role assignments of
      the principal in its tenant
      ([ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md)), so the
      termination runs first, while the sessions can still be listed to notify their relying parties. A principal that
      keeps another identity keeps its consents.
    - Consents are kept for block, disable and reset, because each is reversible or leaves the end user's choices
      intact.
12. **Every termination is one call of the termination seam.** It is `ISessionService.RevokeAllAsync` in a context for
    the addressed tenant (`IOpenIdContextFactory.CreateForTenantAsync`), which claims each session, revokes every grant,
    sends back-channel logout and audits the termination. After a commit it runs through
    `IPostCommitSecurityEffects.TerminateSessionsAsync` on its own bounded token, and a failure is logged
    ([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)). Before a deletion it keeps
    the request's token, because it precedes the commit and a cancellation there cancels the deletion too. A deletion
    that fails after the termination leaves the account with its sessions ended.

## Options considered

- **Block only through the local account.** Rejected: a principal whose connections are all external could never be
  blocked, and the principal could keep signing in through another connection.
- **Keep the block in `SystemMetadata`.** Rejected: authorization-relevant state would live in a free-form bag, untyped
  and enforced by nothing.
- **Validate the security stamp on every request.** Rejected: nothing carries the stamp, and terminating the sessions
  already revokes every grant and notifies the relying parties.
- **Check that the principal exists in the core.** Rejected: a subject without a principal is valid for a host whose
  authentication scheme has no issuer claim, so the core cannot tell a deleted principal from one that never existed.
- **Revoke the grants outside the termination seam.** Rejected: no relying party would receive back-channel logout, and
  no termination would be audited.
- **Remove the consents when an account is disabled.** Rejected: disabling is reversible, and the end user would be
  asked for consent again.
- **Terminate after the deletion commits.** Rejected: the erased grants could not be listed to notify the relying
  parties.
- **Classify a refusal by its error code.** Rejected: every subject validation answers `access_denied`, the ones that a
  fresh authentication cures included.
- **Refuse only at the sign-in.** Rejected: a host whose sign-in is another scheme loops at the authorization request.
- **Validate the session in a cookie event.** Rejected: the OpenID context exists only inside the endpoint filter, and
  the cookie handler caches the first authentication of a request.
- **Answer a disabled account as invalid credentials.** Rejected: the end user receives no reason after a correct
  password, while a lockout keeps refusing without verifying.

## Consequences

- A blocked, disabled or deleted end user has no session and receives `access_denied` from the client's redirect URI;
  Auth0 answers a blocked user at the callback the same way. A consent denial answers the authorization request with
  `access_denied`.
- Refresh tokens and sessions stop at once. The JWT access tokens that were issued run to their expiry.
- A deleted principal leaves no consent or grant, and a deleted account leaves its sessions ended.
- Unblocking restores no revoked grant: the end user signs in again.
- Every subject validation reads the principal, its identities and their accounts once more, in one unit of work each
  time it runs. Each self-service request validates and touches its session.
- A client that disconnects after a committed change does not keep the other sessions active.
- A self-service password change keeps the current session
  ([ADR-1230](1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md)).

## References

- [ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md) — the
  federated principal and its identities.
- [ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md) — the local account.
- [ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md) — the
  interactive surface.
- [ADR-1081](1081-consent-is-a-durable-record-of-decided-and-granted-scopes-enforced-at-the-authorization-endpoint.md) — the
  consent surface, whose denial answers the authorization request.
- [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) — the
  termination seam.
- [ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md) — deleting an owner
  removes what it owns.
- [ADR-1225](1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md) — every grant validates its subject.
- [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md) — post-commit work.
- Issues [#280](https://github.com/NCodeGroup/identity/issues/280),
  [#284](https://github.com/NCodeGroup/identity/issues/284),
  [#285](https://github.com/NCodeGroup/identity/issues/285) and
  [#289](https://github.com/NCodeGroup/identity/issues/289).
- [RFC 6749 §4.1.2.1](https://www.rfc-editor.org/rfc/rfc6749#section-4.1.2.1) — the error response of the authorization
  request.
- [OpenID Connect Core 1.0 §3.1.2.6](https://openid.net/specs/openid-connect-core-1_0.html#AuthError) — the error
  response of the authentication request, which an end user's denial of consent included.
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) V3.3.3 — terminate the other
  sessions after a password change or reset.
- [Auth0: Block and Unblock Users](https://auth0.com/docs/manage-users/user-accounts/block-and-unblock-users) — Auth0
  blocks a user, not a connection, and answers a blocked user at the callback.
- Code: `NCode.Identity.OpenId.Authentication.Subject.ValidateSubjectStandingCommand`,
  `NCode.Identity.OpenId.Authentication.Subject.DefaultValidateBlockedSubjectHandler`,
  `NCode.Identity.OpenId.Accounts.Authentication.DefaultValidateLocalAccountSubjectHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers.DefaultAuthorizeHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.Principals.PrincipalApiEndpointHandler`,
  `NCode.Identity.OpenId.Accounts.Management.LocalAccountApiEndpointHandler`.
