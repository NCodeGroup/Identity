# 1057. Local-account management is an opt-in capability that provisions the principal eagerly and authorizes per tenant

- **Status:** Accepted
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

The management slice (`NCode.Identity.OpenId.Management` and its abstractions) is a general administration surface: a
host enables it to manage clients, tenants, secrets and grants. Managing local accounts (creating, listing, reading,
updating and deleting an end-user subject, resetting its credential, and managing its claims and metadata) is specific
to hosts that back their users with a local-account store. Many management hosts front a different user store, or none.

A general slice must not be coupled to one optional capability. If the local-account endpoints, their validator, their
wire contracts and their audit event lived inside the general management packages, and the management implementation
depended on the account abstractions, every management host would carry the capability whether or not it wanted it, and
the management audit abstraction would know the shape of an event that is not part of its slice.
[ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md)
establishes the keyed endpoint-group model this design relies on, and
[ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md) applies the same
dependency inversion to the authentication slice: the password grant is an opt-in package over the account abstractions.

Two facts about the account model make a plain set of CRUD endpoints insufficient:

- **Metadata and authority need a principal.** The `ProfileMetadata` and `SystemMetadata` bags live on the principal
  ([ADR-1055](1055-subject-metadata-lives-on-the-principal-and-projects-into-tokens-as-nested-claims-at-issuance.md)),
  and `SystemMetadata` is writable by the server or an administrator only. An account that had no principal until its
  first login would have no target for a metadata write.
- **Authority over an end-user subject is administration.** An end user is a tenant-scoped principal
  ([ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md)), so
  the authority to manage one belongs to the tenant's administrators, not to an owner of the account.

## Decision

**Local-account management is an opt-in capability in its own account-owned packages. The base management slice has no
dependency on the account packages, the capability authorizes every operation against the tenant scope, and creating
an account provisions its principal and self-issued identity in the same unit of work.**

### Two account-owned packages

- `NCode.Identity.OpenId.Accounts.Management.Abstractions` holds the wire contracts (namespace
  `NCode.Identity.OpenId.Accounts.Management.Contracts`), the `ILocalAccountValidator` seam, and the audit event
  `LocalAccountChangedAuditEvent`, whose resource type is `AccountManagementResourceTypes.LocalAccount`.
- `NCode.Identity.OpenId.Accounts.Management` holds `LocalAccountApiEndpointHandler`, `DefaultLocalAccountValidator`
  and `AddLocalAccountManagement(IServiceBuilder<LocalAccountLibrary>)`, an extension on
  `IServiceBuilder<OpenIdManagementLibrary>`. It depends on `NCode.Identity.OpenId.Management.Abstractions` and
  `NCode.Identity.OpenId.Accounts.Abstractions`, never on the management implementation.

`NCode.Identity.OpenId.Management` references no account package.

### The endpoint attaches to the tenant group by its key

The handler registers as a keyed `IEndpointProvider` under `ManagementEndpointGroups.Tenant`, so the capability attaches
to a group it does not own with no change to that group. It is mapped at `/api/tenants/{tenantId}/local-accounts` with
the OpenAPI tag `LocalAccounts`. The routes are:

- `POST` and `GET` on the collection;
- `GET`, `PATCH` and `DELETE` on `/{localAccountId}`;
- `POST` on `/{localAccountId}/enable`, `/disable` and `/password`;
- `GET` and `PUT` on `/{localAccountId}/claims`;
- `GET` and `PATCH` on `/{localAccountId}/metadata`.

### Shared building blocks are public abstractions

A capability package authors management endpoints against abstractions, so `NCode.Identity.OpenId.Management.Abstractions`
exposes `ManagementApiEndpointHandler` (the base carrying resource-based authorization, single-item and collection `GET`
processing, authorization-failure mapping, page-limit clamping, snapshot serialization and error mapping),
`DefaultResourceValidator` (the validator base), `Operations` (the CRUD authorization requirements),
`TenantScopeResource` and `ManagementEndpointGroups`. The management implementation's internal base handler derives
from the public one.

### Every operation authorizes against the tenant scope

Each operation authorizes against `TenantScopeResource.For(tenantId)`, so the global-administrator and tenant-administrator
handlers apply. A local account is not ownable and has no resource-node type, because managing an end-user subject is
tenant administration. The management resource server carries a `local_accounts` scope family. How a management route
orders authorization before reading, and which status it answers, is
[ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md)'s.

### The principal is provisioned eagerly

Create writes the account, its principal and its self-issued identity in one unit of work, so metadata and authority
have a target before the first login. Create may also set the metadata bags.

The metadata endpoint resolves the account's principal through its self-issued identity and reads and writes the bags
through `IFederatedPrincipalStore`. With create, it is the only write path for `SystemMetadata`, and the whole API is
administrator-gated, so the bag is never writable by an end user.

The account's resource carries its principal's identifier (`principalId`, `null` for an account without a self-issued
identity) in the create, get and list answers, because a role assignment names a principal and the account's own
identifier is not one. Create reads it from the staged principal, get from the account's self-issued identity, and a
list resolves its page's identities in one `IFederatedIdentityStore.GetByIssuerSubjectsAsync` call, so a grant needs no
call to the metadata endpoint. A client's resource carries its `principalId` the same way.

Delete ends the principal's sessions first, then removes the account, its self-issued identity, and the principal when
that was its last identity. Disable and password reset end sessions after the commit
([ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md)). Create and reset judge
the password with `IPasswordPolicy` and answer a refusal with a `400` problem
([ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)).

### Auditing goes through one generic recorder entry point

The management audit recorder exposes `RecordResourceChangeAsync(HttpContext, ResourceChangeAuditEvent)`, which stamps
the common envelope onto a caller-supplied event and publishes it, keyed by the event's runtime type. The capability
calls it through its own `RecordLocalAccountChangedAsync` extension. Each capability owns its event subtype, so the base
auditing abstraction carries no per-capability method.

### Presence is registration

A capability is present when it is registered: the endpoints make no runtime `501` self-check. The store prerequisite is
enforced at compile time. A local-account store registration (`AddLocalAccountEntityFramework()`,
`AddAspNetIdentityLocalAccountStore<TUser>()` or a custom store) returns an `IServiceBuilder<LocalAccountLibrary>`, and
`AddLocalAccountManagement` takes it as a required parameter, so a host cannot write the capability without a store.

## Options considered

- **Local-account management inside the management packages.** It couples the general slice to one optional capability,
  forces every management host to carry it, and makes the management implementation depend on the account abstractions.
- **A capability-specific method on the management audit recorder.** It puts a per-capability shape on a general
  abstraction; one generic resource-change entry point serves every capability.
- **`501` feature detection.** A library that checks whether it is itself registered is meaningless once the endpoints
  are an opt-in package: the registration answers the question, and the compile-time store parameter covers the
  prerequisite.
- **Lazy creation of the principal on the first metadata write.** It scatters principal provisioning across read-ish
  paths, makes metadata writes materialize identity rows, and leaves a window where an account exists without a
  principal.
- **Ownable local accounts with an owners sub-resource.** Authority over an end-user subject is tenant administration,
  so an owner model adds a resource-node type and a sub-resource for no authority a tenant administrator lacks.
- **Folding metadata into the account update.** Metadata belongs to the principal, which the self-issued identity
  resolves, not to the account, and it carries the owner-versus-server trust boundary, so it has its own endpoint.
- **A provisioner seam between the handler and the store.** The create handler composes the account, principal and
  identity writes in one unit of work against the one store seam; a separate write seam is the split
  [ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md) rejects.
- **Duplicating the endpoint base in the capability package.** It leaves the other handlers untouched but copies
  endpoint plumbing that drifts.
- **Account-owned opt-in packages that build on a public management abstraction, with eager provisioning and
  tenant-scope authorization (chosen).** The base slice stays free of account concerns, the capability composes, and
  metadata and authority have a target from the moment the account exists.

## Consequences

- A host enables the capability with `AddLocalAccountManagement(...)` beside `AddOpenIdManagementLibrary()` and a
  local-account store registration; a host without it carries no account package.
- `TenantManagementGroup` maps only its own families, and the local-account endpoint attaches from the capability
  package by keyed registration.
- Every local account has a resolvable principal from the moment it exists, so metadata projection and authority work
  before the first login.
- The only write paths for `SystemMetadata` are create and the administrator-gated metadata endpoint.
- The management audit abstraction carries no per-capability method, and the local-account audit event and its
  resource-type constant belong to the accounts-management abstractions.
- A missing store is a compile error, not a startup or resolution failure.

## References

- Code: `src/NCode.Identity.OpenId.Accounts.Management.Abstractions`, `src/NCode.Identity.OpenId.Accounts.Management`,
  `src/NCode.Identity.OpenId.Management.Abstractions`.
- [ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md): the store seam and the
  self-issued identity.
- [ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md): the
  principal an account resolves to.
- [ADR-1055](1055-subject-metadata-lives-on-the-principal-and-projects-into-tokens-as-nested-claims-at-issuance.md): the
  metadata bags and the `SystemMetadata` write invariant.
- [ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md):
  keyed endpoint providers and the group tree.
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md):
  how a management route authorizes.
- [ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md): ending sessions after a
  refusal.
- [ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md): the password policy.
- [project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md): why this
  reasoning lives in a record.
