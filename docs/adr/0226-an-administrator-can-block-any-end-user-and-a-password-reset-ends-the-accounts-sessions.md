# 226. An administrator can block any end user, and a password reset ends the account's sessions

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A disabled local account refuses its principal through every connection
([ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md)), but a principal whose connections are all
external has no state that an administrator can set (backlog `R93`). Auth0 blocks a user, not a connection.

An administrator's password reset rotates the security stamp of the account, which nothing reads, so the sessions and
the refresh tokens of a compromised account keep working until they expire (backlog `R94`). OWASP ASVS V3.3.3 asks that
a password change or a password reset terminates the other active sessions, across the relying parties as well.

## Decision

**An administrator blocks an end user through its principal, which ends the principal's sessions and refuses every token
request for it, and an administrator's password reset ends the account's sessions.**

1. **The block state lives on the principal.** `PersistedFederatedPrincipal.IsBlocked` is
   `public bool IsBlocked { get; set; }`, `false` by default and not `required`, and
   `FederatedPrincipalEntity.IsBlocked` is `public required bool IsBlocked { get; set; }`. `FederatedPrincipalStore`
   maps it when it adds, reads and updates a principal. The migration `AddFederatedPrincipalIsBlocked` adds a non-null
   boolean column with the default `false` to the federated principals table, and expands the schema only.
2. **A blocked principal is refused wherever a subject is validated.** `DefaultValidateBlockedSubjectHandler`, in
   `NCode.Identity.OpenId.Authentication`, handles `ValidateSubjectAuthenticationCommand` at the default priority and
   returns at once when the operation already has an error. It reads the principal of
   `subjectAuthentication.PrincipalId` with `IFederatedPrincipalStore.GetOrDefaultAsync`. A blocked principal sets
   `ErrorFactory.AccessDenied("The end-user is blocked.")` and logs `SubjectValidationFailed`; a missing principal is
   not refused. The handler is registered with `TryAddEnumerable` after the default handler, so it runs at the
   authorization request and at every grant that sends the command
   ([ADR-0225](0225-every-grant-that-issues-tokens-for-a-subject-validates-it.md)).
3. **The management API reads, blocks and unblocks a principal.** `PrincipalApiEndpointHandler`, in
   `NCode.Identity.OpenId.Management`, serves `GET api/tenants/{tenantId}/principals/{principalId}`, which authorizes
   `Operations.Read` on `TenantScopeResource.For(tenantId)` and returns a `PrincipalResource` with an `ETag`, and
   `POST .../block` and `POST .../unblock`, which authorize `Operations.Update` and answer `204`. The contract
   `PrincipalResource` has `TenantId`, `PrincipalId`, `IsBlocked` and `ConcurrencyToken`. The OpenAPI tag is
   `OpenIdConstants.EndpointTags.Principals`, `"Principals"`. Each change is audited as a `PrincipalChangedAuditEvent`
   (`ManagementResourceTypes.Principal`, change type `updated`) through
   `IManagementAuditRecorder.RecordResourceChangeAsync`. No management scope family is added: the authority is
   role-based on the tenant scope, as it is for sessions.
4. **Blocking ends the sessions, and unblocking ends nothing.** Block writes `IsBlocked = true` only when it changes.
   Then, also when the principal was blocked already, it terminates every session of the principal with
   `SessionTerminationReasons.UserBlocked`, `"user_blocked"`, through `ISessionService.RevokeAllAsync` in a context for
   the addressed tenant, so every grant is revoked, the relying parties receive back-channel logout and each
   termination is audited. Consents are kept. Unblock writes `IsBlocked = false` when it changes and terminates no
   session.
5. **A disabled account and a blocked principal are different states.** A disabled local account stays a
   credential-level state that also refuses its principal
   ([ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md)). The block is the principal-level state,
   and it works for every principal whatever its connections.
6. **A password reset ends the account's sessions.** `SessionTerminationReasons.PasswordReset` is `"password_reset"`.
   After `ResetPasswordAsync` of `LocalAccountApiEndpointHandler` commits, the principal of the self-issued identity of
   the account has its sessions terminated through `ISessionService.RevokeAllAsync`, which claims each session, revokes
   every grant, sends back-channel logout and audits the termination
   ([ADR-0227](0227-a-session-is-terminated-at-most-once-on-every-path.md)). Consents are kept, and the security stamp
   is not consulted.
7. **A self-service password change keeps the current session.** When the server offers one, a change terminates every
   other session of the principal with `password_changed`, revokes every grant that was not issued under the current
   session, and keeps the current session, as OWASP ASVS V3.3.3 and NIST SP 800-63B ask. The server offers no change
   yet (backlog `R99`), so no constant for the reason is added until a caller exists.

## Options considered

- **Block only through the local account.** Rejected: a principal whose connections are all external could never be
  blocked.
- **Keep the block in `SystemMetadata`.** Rejected: authorization-relevant state would live in a free-form bag, untyped
  and enforced by nothing.
- **Validate the security stamp on every request.** Rejected: nothing carries the stamp, and terminating the sessions
  already revokes every grant and notifies the relying parties.
- **End every session on a self-service change.** Rejected: it signs the end user out of the session that proved the
  change.

## Consequences

- Every subject validation reads the principal once more.
- A blocked end user can still sign in at the login page and receives no token: every grant answers `access_denied`,
  and the authorization request validates the subject as the grants do but answers a refused subject with a login
  challenge (`login_required` for `prompt=none`), so the end user is sent back to the login page (backlog `R98`).
- Unblocking restores no revoked grant: the end user signs in again.
- A self-service password change is not offered (backlog `R99`).
- **Breaking:** `PersistedFederatedPrincipal.IsBlocked`, `FederatedPrincipalEntity.IsBlocked`,
  `SessionTerminationReasons.UserBlocked`, `SessionTerminationReasons.PasswordReset`,
  `OpenIdConstants.EndpointTags.Principals`, `ManagementResourceTypes.Principal`, `PrincipalChangedAuditEvent` and
  `PrincipalResource` are added, and the `AddFederatedPrincipalIsBlocked` migration adds a column to the federated
  principals table.
- This implements backlog `R93` and `R94`.

## References

- [ADR-0035](0035-federated-principals-and-identity-resolution.md) — the federated principal that carries the block.
- [ADR-0098](0098-session-lifecycle-termination-seam.md) — the termination seam that the reasons name.
- [ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md) — the disabled account and the termination of
  its sessions.
- [ADR-0225](0225-every-grant-that-issues-tokens-for-a-subject-validates-it.md) — every grant validates its subject.
- Auth0, Block and Unblock Users — Auth0 blocks a user, not a connection:
  <https://auth0.com/docs/manage-users/user-accounts/block-and-unblock-users>
- [OWASP ASVS](https://owasp.org/www-project-application-security-verification-standard/) V3.3.3 — terminate the other
  sessions after a password change or reset.
- OWASP Session Management Cheat Sheet:
  <https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html>
- [NIST SP 800-63B](https://pages.nist.gov/800-63-3/sp800-63b.html) — digital identity guidelines, authentication and
  lifecycle management.
- Code: `NCode.Identity.OpenId.Authentication.Subject.DefaultValidateBlockedSubjectHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.Principals.PrincipalApiEndpointHandler`,
  `NCode.Identity.OpenId.Accounts.Management.LocalAccountApiEndpointHandler`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core.Stores.FederatedPrincipalStore`.
