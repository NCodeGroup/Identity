# 60. Local accounts: one store seam, dependency-inverted authentication capability, no owners

- **Status:** Accepted
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

[ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md) introduced three local-account seams —
`ILocalAccountSource` (read/verify), `ILocalAccountProvisioner` (write), and `ILocalAccountStore` (persistence) — plus a
runtime `LocalAccount` payload distinct from the `PersistedLocalAccount` data contract.
[ADR-0057](0057-local-account-eager-provisioning-and-management-seam.md) then expanded the provisioner into the full
write seam and added the management family. Exercising that model surfaced four problems:

- **Three seams for one concept.** `ILocalAccountSource`, `ILocalAccountProvisioner`, and `ILocalAccountStore` overlapped:
  every operation ultimately read or wrote the same account row, and the split forced a backend author (for example, an
  ASP.NET Core Identity adapter) to implement and register multiple interfaces that differed only by verb. The
  credential also leaked onto the public `PersistedLocalAccount` as a raw `PasswordHash` string, so hashing semantics
  were neither encapsulated nor uniform across backends.
- **The authentication stack depended on the account stack.** `NCode.Identity.OpenId.Authentication` referenced
  `NCode.Identity.OpenId.Accounts.Abstractions` so its resource-owner password grant (ROPC) handlers could consume a
  local account. That inverts the intended layering: a capability (the password grant) that is _specific to local
  accounts_ lived in the general authentication package, which every host pays for even when it fronts a different user
  store or disables the grant entirely.
- **Local accounts were modeled as ownable resources.** ADR-0057 gave the management family an owners sub-resource and a
  `local_account` resource-node type, mirroring clients. But a local account is an **end-user subject**, not a
  delegated-administration resource: the authority to manage one is tenant/global administration, not per-account
  ownership. The owner model added surface and an authorization axis that never had a real meaning here.
- **The metadata projection claim-type constants lived on `AccountConstants`.**
  [ADR-0055](0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) established that
  `ProfileMetadata` / `SystemMetadata` are a **principal-level** concept projected uniformly for every connection kind,
  yet the claim-type names under which they project (`profile_metadata` / `system_metadata`) still lived on
  `AccountConstants` in the accounts package — the one tie that kept the authentication token/UserInfo handlers
  depending on the accounts package.

This family is pre-release ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)), so the response is a clean
cutover rather than compatibility shims.

## Decision

**A local account has exactly one pluggable seam — `ILocalAccountStore` — the authentication package no longer depends
on the account package, local accounts are not ownable, and the metadata projection claim types live with the
principal.**

### One store seam, with first-class credential operations

`ILocalAccountSource` and `ILocalAccountProvisioner` are removed. `ILocalAccountStore` is the single, backing-store-
agnostic seam. Because credential handling differs by backend, it is expressed as first-class operations rather than a
raw hash field on the data contract:

- `AddAsync(account, ReadOnlyMemory<byte>? password, ct)` — the store hashes the credential and generates the security
  stamp.
- `SetPasswordAsync(localAccountId, password, ct)` — rehash and rotate the security stamp.
- `VerifyCredentialAsync(userName, password, ct) -> PersistedLocalAccount?` — the store performs the comparison (and may
  transparently upgrade a stored hash on success).
- `UpdateAsync`, `GetByIdOrDefaultAsync`, `GetByUserNameOrDefaultAsync`, `GetPageAsync`, `RemoveAsync`, and
  `ReplaceClaimsAsync` round out profile, lookup, paging, and claim management.

`PersistedLocalAccount` no longer exposes `PasswordHash`; the hash is a store-internal detail. `SecurityStamp` is
store-managed (read-only on the contract). The default PBKDF2 `IPasswordHasher` stays public so the generic Entity
Framework store can use it, while a backend that owns hashing (ASP.NET Core Identity) ignores it. The runtime
`LocalAccount` payload and the `LocalAccountCreationRequest` / `LocalAccountUpdateRequest` /
`LocalAccountPasswordResetRequest` DTOs are removed.

### Dependency inversion: the account stack provides the password-grant capability

The authentication package no longer references the accounts package. The ROPC handlers
(`DefaultAuthenticatePasswordGrantHandler`, `DefaultCreatePasswordGrantSubjectHandler`) and the public
`CreatePasswordGrantSubjectCommand` move into a new opt-in package, **`NCode.Identity.OpenId.Accounts.Authentication`**,
which depends on both the authentication and account abstractions and whose registration (`AddLocalAccountAuthentication`)
is what enables the grant over the configured `ILocalAccountStore`. The base `NCode.Identity.OpenId.Authentication`
package ships a default handler that reports the password grant as `unsupported_grant_type`; the capability package
replaces it. The grant's support therefore follows the **presence of the capability handler**, not a feature flag: a
configured store that rejects the credentials still yields a failed (not unsupported) result, keeping the two cases
distinct.

### Local accounts are not ownable; management authorizes against the tenant scope

The owners sub-resource and the `local_account` resource-node type are removed. The local-account management family
authorizes every operation against the owning tenant (a `TenantScopeResource`, so the global- and tenant-admin handlers
apply and the ownership handler does not), because the authority to manage an end-user subject is tenant/global
administration. The eager principal + self-issued identity provisioning of ADR-0057 is retained, but it is orchestrated
by the management **create handler** (which composes the account write with the principal and identity writes) rather
than by a provisioner seam.

### The metadata projection claim types live with the principal

`AccountConstants.ProfileMetadataClaimType` / `SystemMetadataClaimType` move to a new
`NCode.Identity.OpenId.Principals.SubjectMetadataClaimTypes` (`ProfileMetadata` / `SystemMetadata`) in
`NCode.Identity.OpenId.Abstractions`, the home of `PrincipalMetadata`. The authentication token/UserInfo metadata
handlers read them from there, which removes the last compile dependency from the authentication package to the account
package.

### ASP.NET Core Identity implements the one seam

The `NCode.Identity.OpenId.Accounts.AspNetIdentity` adapter implements `ILocalAccountStore` over
`UserManager<TUser>` (`where TUser : IdentityUser`), mapping lockout to enabled state and keeping accounts global
(`TenantId` empty; the grant authenticates against the request's tenant). Because such a store does not participate in
the Entity Framework `OpenIdDbContext` unit of work, the `EntityStoreManager` resolves it through a DbContext-less
store-factory fallback, so the adapter need not reference the concrete `DbContext`.

## Options considered

- **Collapse to one `ILocalAccountStore` with first-class credential ops (chosen).** One seam to implement and register,
  the hash stays encapsulated, and every backend expresses credentials the same way.
- **Keep the source/provisioner/store split (chosen against).** Familiar, but it multiplies the backend author's work
  and leaks the hash onto the public contract.
- **Invert the password grant into a new account-authentication package (chosen).** Keeps the authentication package
  free of account concerns and makes the grant a composable, opt-in capability.
- **Fold the ROPC handlers into the account package, or leave them in authentication (chosen against).** The former
  makes the account package depend on authentication internals; the latter keeps the unwanted dependency and ships the
  grant to hosts that do not want it.
- **Relocate the metadata claim types to the principal abstractions (chosen).** They are principal-level per ADR-0055;
  this is their correct home and it severs the last authentication-to-accounts tie.
- **Keep local accounts ownable (chosen against).** Ownership is meaningless for an end-user subject and adds an
  authorization axis and surface that never applied.

## Consequences

- `ILocalAccountSource`, `ILocalAccountProvisioner`, the runtime `LocalAccount`, and the `LocalAccount*Request` DTOs are
  removed; `ILocalAccountStore` is the single seam, with credential operations first-class and no `PasswordHash` on
  `PersistedLocalAccount`.
- A new opt-in package `NCode.Identity.OpenId.Accounts.Authentication` provides the resource-owner password grant;
  `NCode.Identity.OpenId.Authentication(.Abstractions)` no longer reference the account abstractions, and the base
  package answers an unconfigured password grant with `unsupported_grant_type`. `CreatePasswordGrantSubjectCommand`
  moves to the new package's namespace.
- The local-account management family drops its owners sub-resource and the `local_account` resource-node type and
  authorizes against the tenant scope; `TenantScopeResource` becomes a public authorization resource alongside
  `ResourceNode`.
- `SubjectMetadataClaimTypes` joins `NCode.Identity.OpenId.Abstractions`; the two metadata claim-type constants leave
  `AccountConstants`.
- The ASP.NET Core Identity adapter now implements `ILocalAccountStore` (registered via
  `AddAspNetIdentityLocalAccountStore<TUser>()`); the `EntityStoreManager` gains a DbContext-less store-factory fallback
  so non-EF stores resolve without referencing `OpenIdDbContext`.
