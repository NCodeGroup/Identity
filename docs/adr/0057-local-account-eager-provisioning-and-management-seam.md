# 57. Local accounts are provisioned eagerly with their principal, and the provisioner is the account write seam

- **Status:** Accepted (write seam and owner model amended by [ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md))
- **Date:** 2026-10-09
- **Deciders:** NCode Group

## Amendment (2026-10-10): one store seam, no owners, orchestration in the management handler

This ADR expanded `ILocalAccountProvisioner` into the account write seam and gave the management family an owners
sub-resource and a `local_account` resource-node type.
[ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md) supersedes both: the
`ILocalAccountSource` / `ILocalAccountProvisioner` seams collapse into the single `ILocalAccountStore` (with first-class
credential operations), the eager principal + self-issued identity provisioning moves into the management **create
handler**, and local accounts are no longer ownable — the management family authorizes against the tenant scope because
the authority to manage an end-user subject is tenant/global administration. The eager-provisioning decision and the
management surface (minus owners) otherwise stand.

## Context

[ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md) introduced the `LocalAccount` payload and
the `ILocalAccountSource` (read) / `ILocalAccountProvisioner` (write) seams, but only the read seam had callers: the
resource-owner password grant. `ILocalAccountProvisioner` exposed a single `CreateAsync` that persisted the account row
and nothing else, and it had **no caller at all** — an operator could only _seed_ accounts directly through the store.
There was no management surface to create, update, disable, reset the credential of, or set the metadata of a local
account. This gap is tracked as item `A1` in [`BACKLOG.md`](../../BACKLOG.md).

Two facts about the existing model made a straightforward "add CRUD endpoints" increment insufficient:

- **A local account had no principal until its first login.** A `LocalAccount`'s owning `FederatedPrincipal` and its
  one-to-one self-issued `FederatedIdentity` (issuer = `AccountConstants.SelfIssuer`, subject = the account id) were
  created **lazily**, by `DefaultPrincipalResolver`, on the first authentication. But
  [ADR-0055](0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) moved the `ProfileMetadata` /
  `SystemMetadata` bags onto the **principal**. A freshly-created account that had never logged in therefore had **no
  principal to carry metadata** — so a `set-metadata` operation (an explicit `A1` requirement, which must also enforce
  the ADR-0054 invariant that `SystemMetadata` is server/admin-only) had no target. Resource ownership (the management
  authorization model) is likewise keyed to a principal.
- **`CreateAsync` owned its own unit of work and only wrote the account row.** It opened a store manager, added the
  account, and saved. A management handler could not compose the account write with recording the creator as owner in
  one atomic unit of work, and the credential-hashing / security-stamp / id-generation logic it encapsulated was not
  reachable for update or password-reset.

## Decision

**A local account is provisioned eagerly as a connected human actor, and `ILocalAccountProvisioner` is the full
account write seam that the management API drives.**

### Eager principal and self-issued identity at account creation

`ILocalAccountProvisioner.CreateAsync` now stages the `PersistedLocalAccount`, its owning `PersistedFederatedPrincipal`,
and their one-to-one self-issued `PersistedFederatedIdentity` (issuer = `AccountConstants.SelfIssuer`, subject = the
generated account id) **together, in one unit of work**. This realizes ADR-0051's own statement that "a local account
is written together with its one-to-one self-issued federated identity in a single unit of work," and it means
ownership and principal-level metadata have a stable target from the moment the account exists — no "no principal before
first login" special case. The lazy principal resolution in `DefaultPrincipalResolver` still stands for external-IdP
connections and remains the fallback for any local account seeded before this change; eager creation is simply the
honest default for the one connection kind the server itself issues.

### The provisioner is the account write seam, participating in the caller's unit of work

`ILocalAccountProvisioner` grows `UpdateAsync` (profile fields), `SetEnabledAsync` (enable/disable), and
`ResetPasswordAsync` (rehash + rotate the security stamp, invalidating outstanding sessions and tokens). Every method
now **takes the caller's `IStoreManager` and stages its changes without saving**, so the management handler controls the
transaction boundary and composes the account write with recording the creator as owner atomically — exactly as the
client-management handler already does. Credential hashing, id generation, and security-stamp rotation stay behind the
seam, so an alternative provisioner (for example, one over ASP.NET Core Identity) can own them.

### A local-account management family mirroring the existing Management package

A `local-accounts` resource family is added under `/api/tenants/{tenantId}/local-accounts`, mirroring the clients family
(CRUD + owners, a validator, audit events, a `local_accounts` scope family, and a `local_account` resource-node type).
It adds explicit `disable` / `enable` and `password` (reset) operations, and `metadata` (get / set) that targets the
account's **principal**: the self-issued identity resolves the account id to its principal, whose metadata bags are read
and written through `IFederatedPrincipalStore`. Because the whole management API is administrator-gated, this metadata
endpoint is the server/admin write path for `SystemMetadata`, upholding the ADR-0054 invariant that it is never
end-user writable. Deleting a local account removes the account, its self-issued identity, and — when the account was
the principal's sole connection — the principal. The endpoints require the opt-in local-account persistence package;
without a registered `ILocalAccountProvisioner` they respond `501 Not Implemented`.

## Options considered

- **Eager principal creation at account creation (chosen).** Cohesive with ADR-0051; gives metadata and ownership a
  stable target immediately; removes the before-first-login special case.
- **Keep lazy principal creation; have `set-metadata` get-or-create the principal on demand (chosen against).** Smaller
  change to `CreateAsync`, but it scatters principal provisioning across read-ish paths, forces metadata writes to also
  materialize identity graph rows, and leaves a window where a created account has ownership but no principal.
- **Expand `ILocalAccountProvisioner` into the full write seam (chosen).** Keeps hashing, stamping, and the
  account/principal/identity shape behind one replaceable seam.
- **Keep the provisioner create-only and mutate `ILocalAccountStore` + `IPasswordHasher` directly from the management
  handler (chosen against).** Fewer abstraction changes, but it leaks credential-hashing and security-stamp logic into
  the management layer and prevents an alternative account backend from owning them.
- **Fold `set-metadata` into the account update (chosen against).** Metadata is a principal-level concept
  ([ADR-0055](0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md)); it targets a different
  entity (the principal, via the self-issued identity) and carries the server-vs-owner trust boundary, so it is its own
  endpoint.

## Consequences

- `ILocalAccountProvisioner.CreateAsync` changes signature (takes an `IStoreManager`, stages without saving) and gains
  `UpdateAsync`, `SetEnabledAsync`, and `ResetPasswordAsync`; two request DTOs join the surface
  (`LocalAccountUpdateRequest`, `LocalAccountPasswordResetRequest`). `ILocalAccountStore` gains `GetPageAsync` and
  `RemoveAsync`; `IFederatedPrincipalStore` gains `UpdateAsync` and `RemoveAsync`; `IFederatedIdentityStore` gains
  `RemoveAsync`.
- Creating a local account now writes three rows (account, principal, self-issued identity) in one unit of work; every
  local account has a resolvable principal immediately, so ownership and metadata projection work before first login.
- A `local-accounts` management family is available (create / list / get / update / delete / disable / enable /
  password / get-metadata / set-metadata / owners), gated by `*:local_accounts` scopes and the `local_account`
  resource-node type, and audited via `LocalAccountChangedAuditEvent`. It requires the opt-in local-account persistence
  package and otherwise responds `501 Not Implemented`.
- The `SystemMetadata` write-path invariant (never end-user-writable) is upheld: the only write path is the
  administrator-gated management metadata endpoint.

## References

- [ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md) — the `LocalAccount` payload and the
  read/write seams this ADR completes; its "written together with its self-issued identity in a single unit of work"
  statement is realized here.
- [ADR-0054](0054-local-account-metadata-projection.md) — the metadata projection and the `SystemMetadata`
  server-only write invariant the management metadata endpoint upholds.
- [ADR-0055](0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) — why metadata is
  principal-level, which makes eager principal creation the clean target for `set-metadata`.
- [ADR-0035](0035-federated-principals-and-identity-resolution.md) — the `FederatedPrincipal` / `FederatedIdentity`
  model the self-issued connection participates in.
- [ADR-0011](0011-secret-management-api.md) — the management-API shape (validators, auditing, ownership) this family
  mirrors.
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — why this reasoning lives in an ADR rather than
  chat or agent memory.
