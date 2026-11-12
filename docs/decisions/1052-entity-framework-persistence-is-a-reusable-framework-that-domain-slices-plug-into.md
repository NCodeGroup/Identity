# 1052. Entity Framework persistence is a reusable framework that domain slices plug into

- **Status:** Accepted
- **Date:** 2026-10-07
- **Deciders:** NCode Group

## Context

Entity Framework persistence has two very different parts. One is a reusable **store framework**: the shared
`OpenIdDbContext`, the `BaseStore` keyset-pagination base, the store-manager and unit-of-work plumbing, the id
generator, the value converters, the concurrency-token interceptor and the tenant-scoping infrastructure. The other is
the **core OpenID schema**: the entities and stores for clients, grants, tenants, secrets, resource servers, scopes,
roles and federated principals and identities.

A satellite that persists its own entities in the _same_ `OpenIdDbContext` shares the unit of work and can write
atomically with the core entities, as the local accounts of
[ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md) do. It needs the framework, not the core
schema. If the two were fused in one package, every satellite would depend on the core stores it never uses, and the
reusable pieces (`BaseStore`, the store-registration helper, the id-generator attribute) would have to stay internal,
which forces each satellite to re-implement the store boilerplate: `IStore` delegation, normalization, id and
concurrency-token generation, and the registration factory. With more than one slice, that duplication and the
misdirected dependency compound.

## Decision

**Entity Framework persistence is a reusable framework that domain slices plug into. The framework package owns the
extensible `OpenIdDbContext` and the store infrastructure and declares no domain entity; each schema, the core OpenID
schema included, is a slice package that contributes its entities and registers its stores against the framework. A
slice depends only on the framework, never on another slice.**

### The framework

`NCode.Identity.OpenId.Persistence.EntityFramework` is the framework. It owns:

- `OpenIdDbContext`;
- the public `BaseStore` and `BaseStoreWithResourceId`, the public `AddStore` registration helper and the public
  `UseIdGeneratorAttribute`, so a slice reuses the store base, the registration helper and opaque-id generation instead
  of re-implementing them;
- the `IOpenIdModelContributor` seam;
- the store manager and the id generator;
- the value converters;
- the concurrency interceptor;
- the tenant-scoping infrastructure.

Because the framework owns no schema, `OpenIdDbContext` has no `DbSet` properties. Every entity, core and satellite
alike, enters the model through an `IOpenIdModelContributor`, and stores reach their table through
`context.Set<TEntity>()`. A slice may offer readable table access for its own entities by declaring typed `DbSet`
accessors as extension members over the context, as `CoreDbContextExtensions` does; this keeps the coupling inside the
slice and never ties the framework to a schema.

The framework's `OnModelCreating` keeps only the two schema-agnostic passes: it restricts the delete behavior of every
foreign key, and it applies the tenant filter to every `ISupportTenantEntity`. Schema-specific configuration, such as
the tenant's filtered-unique domain-name index, lives in the owning slice's contributor.

The tenant filter is navigation-free. `ISupportTenantEntity`, in `NCode.Identity.OpenId.Persistence.EntityFramework.Entities`,
exposes a denormalized `NormalizedTenantId`, and the filter compares that one indexed column. The framework therefore
never references the concrete tenant entity, which lives in a slice, and the filter needs no join. A slice entity
imports the interface to be tenant-scoped.

#### Normalized companion columns and verbatim-indexed keys

The framework's `Normalize` helper is `value?.ToLowerInvariant()`. A `Normalized<Name>` companion column plus index
exists only to make a wanted case-insensitive lookup index-usable on database engines without case-insensitive
collations. Whether a string column gets a companion is therefore decided by whether case-insensitive matching is
wanted, not by whether the column is an identifier.

- A column gets a `Normalized<Name>` companion and an index when its value is an externally supplied or
  caller-addressable identifier whose match should be case-insensitive: `TenantId`, `ClientId`, `SubjectId` (an
  upstream `sub` whose case semantics vary by identity provider) and the management-addressable `GrantId`. `GrantId` is
  a Base64Url resource id, yet it is the public handle an operator may paste into a URL, so forgiving case-insensitive
  addressing is chosen over its small entropy cost.
- A column is indexed verbatim, with no companion, when its value is an opaque, case-significant key matched only
  against the bytes the server stored: `GrantType` (a fixed lowercase protocol literal), `HashedKey` (a Base64Url
  SHA-256 hash) and the session `sid` (an opaque 256-bit Base64Url key that round-trips verbatim through the OP cookie
  and `id_token_hint`). Base64Url is case-significant, so lowercasing would let two distinct values collide and a
  lowercased lookup would never match the stored mixed-case key. For a bearer-grade secret such as `sid`, verbatim
  matching is also the safer choice.

### The slices

A slice is a package of entities, stores, an `IOpenIdModelContributor` and store registrations. Among the persistence
packages it depends only on the framework.

- `NCode.Identity.OpenId.Persistence.EntityFramework.Core` holds the core OpenID schema, with `CoreModelContributor`.
- `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts` holds the local-account schema of
  [ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md), with `LocalAccountModelContributor`.
- A further slice follows the same `NCode.Identity.OpenId.Persistence.EntityFramework.<Slice>` shape.

A slice's store inherits `BaseStore` and registers with `AddStore`, so it reuses `IStore` delegation, normalization and
opaque-id and concurrency-token generation. A host registers the framework plumbing once and then each slice it wants.
All slices share one `OpenIdDbContext`, so a cross-slice write, such as a local account and its self-issued federated
identity, is one unit of work.

### Naming

The Entity Framework slices are named `NCode.Identity.OpenId.Persistence.EntityFramework.<Slice>` (tech-first), not
`…<Domain>.EntityFramework` (domain-first). Grouping them under the persistence namespace keeps the framework and its
slices discoverable together and makes the relationship read from the name. Each slice's root namespace equals its
package id, with `.Entities` and `.Stores` sub-namespaces, so a type's namespace names its owning slice and a cross-slice
reference cannot be made by accident. The framework's own types stay under `NCode.Identity.OpenId.Persistence.EntityFramework`.

A non-Entity-Framework adapter is not a slice and keeps domain-first naming, as the ASP.NET Core Identity adapter
`NCode.Identity.OpenId.Accounts.AspNetIdentity` does.

## Options considered

- **One package that exposes the framework pieces publicly.** Rejected: a satellite would still depend on the package
  that holds the core stores, so the dependency direction stays wrong and the split between framework and schema is
  cosmetic. It suffices for a single satellite, not once slices multiply.
- **A generic, non-OpenID Entity Framework framework.** Rejected: the framework is OpenID-flavored, with the
  tenant-scoped `OpenIdDbContext`, `UseIdGeneratorAttribute` and the OpenID model-contribution seam, and the family has
  no goal to reuse it elsewhere, so a context-agnostic layer would be speculative generality.
- **Per-domain `DbContext`s, one in each package.** Rejected: separate contexts cannot share a unit of work, so a
  cross-slice write, such as a local account and its self-issued federated identity, could not be atomic.
- **Typed `DbSet`s on `OpenIdDbContext`.** Rejected: a typed `DbSet<ClientEntity>` on the context type would re-couple
  the framework to the core schema, because the entity would have to live in the framework. The framework context stays
  schema-agnostic and stores use `context.Set<TEntity>()`.
- **A framework plus slices (chosen).** The framework is reused rather than re-implemented, a slice depends only on the
  framework, the shared context keeps cross-slice writes atomic and a new slice is additive.

## Consequences

- A slice's store inherits `BaseStore` and registers with `AddStore`, so it carries no hand-rolled `IStore` delegation,
  normalization or factory boilerplate.
- Every entity, core included, reaches the model through a contributor, and `OpenIdDbContext` stays schema-agnostic.
- Adding a persistence slice is additive: a package of entities, stores, a contributor and registrations against the
  framework, with no change to the framework or to other slices.
- Case-insensitive lookups on caller-addressable identifiers use an indexed `Normalized<Name>` column, and opaque keys
  such as `sid` and `HashedKey` match verbatim, so Base64Url values never collide.

## References

- `NCode.Identity.OpenId.Persistence.EntityFramework`, `NCode.Identity.OpenId.Persistence.EntityFramework.Core` and
  `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts`
- [ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md): the local-account slice whose shared-context
  requirement the framework serves.
- [ADR-1018](1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md): the tenant scoping that the framework applies
  to every slice's `ISupportTenantEntity`.
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md): the dependency discipline that slices follow
  by depending on the framework and not on each other.
- [ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md): the opaque-id generation that `UseIdGeneratorAttribute`
  provides to every slice.
