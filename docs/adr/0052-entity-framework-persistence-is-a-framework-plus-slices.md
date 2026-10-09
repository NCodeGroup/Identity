# 52. Entity Framework persistence is a reusable framework plus domain slices

- **Status:** Proposed
- **Date:** 2026-10-07
- **Deciders:** NCode Group

## Context

The Entity Framework persistence lived in a single package,
`NCode.Identity.OpenId.Persistence.EntityFramework`, that mixed two very different things: a reusable **store
framework** (the shared `OpenIdDbContext`, the `BaseStore` keyset-pagination base, the store-manager and unit-of-work
plumbing, the id generator, the value converters, the concurrency-token interceptor, and the tenant-scoping
infrastructure) and the **core OpenID schema** (the entities and stores for clients, grants, tenants, secrets, resource
servers, scopes, roles, and federated principals/identities).

A satellite that wants to persist its own entities in the _same_ `OpenIdDbContext` — so its data shares the unit of
work and can be written atomically with the core entities ([ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md),
local accounts) — needs the framework, but not the core schema. With the two fused in one package, every satellite
would depend on the core stores it never uses, and the reusable framework pieces (`BaseStore`, the store-registration
helper, the id-generator attribute) were `internal`, forcing each satellite to re-implement the store boilerplate
(`IStore` delegation, normalization, id and concurrency-token generation, the registration factory). With more than one
slice expected, that duplication and the misdirected dependency compound.

## Decision

**Entity Framework persistence is a reusable framework that domain slices plug into. The framework package owns the
extensible `OpenIdDbContext` and the store infrastructure; each schema — the core OpenID schema included — is a slice
package that contributes its entities and registers its stores against the framework. A slice depends only on the
framework, never on another slice.**

### The framework

`NCode.Identity.OpenId.Persistence.EntityFramework` keeps its name and becomes framework-only: `OpenIdDbContext`,
`BaseStore`, the public `AddStore` registration helper, the `UseIdGenerator` attribute and its convention, the
`IOpenIdModelContributor` seam, the store-manager and id-generator, the value converters, the concurrency interceptor,
and the tenant-scoping infrastructure (`ISupportTenantEntity` and the global query filter). It declares **no** domain
entities.

Because the framework owns no schema, `OpenIdDbContext` is **`DbSet`-less**: every entity — core and satellite alike —
is added to the model through an `IOpenIdModelContributor`, and stores reach their table through `context.Set<TEntity>()`
rather than a typed `DbSet` property. A slice may still restore readable table access for _its own_ entities by declaring
typed `DbSet` accessors as extension members over the context (the core slice does this), which does not re-couple the
framework to any schema. The two schema-agnostic passes in `OnModelCreating` (restrict-all-foreign-keys and
apply-the-tenant-scope-filter-to-every-`ISupportTenantEntity`) stay in the framework; schema-specific configuration
(such as the tenant's filtered-unique domain-name index) moves into the owning slice's contributor. The tenant-scope
filter is navigation-free: `ISupportTenantEntity` exposes a denormalized `NormalizedTenantId` discriminator and the
filter compares that single indexed column, so the framework interface stays decoupled from the concrete tenant entity
(which lives in a slice) and the filter avoids a join. `BaseStore`, `BaseStoreWithResourceId`, `AddStore`, and
`UseIdGeneratorAttribute` become **public**, so a slice reuses the store base, the registration helper, and opaque-id
generation instead of re-implementing them.

### The slices

Each slice is a package of entities + stores + an `IOpenIdModelContributor` + store registrations, depending only on the
framework:

- `NCode.Identity.OpenId.Persistence.EntityFramework.Core` — the core OpenID schema (the entities and stores that used
  to live in the framework package), with a `CoreModelContributor`.
- `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts` — the local-account schema
  ([ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md)), moved from
  `NCode.Identity.OpenId.Accounts.EntityFramework`.
- future adjacent slices follow the same `…Persistence.EntityFramework.<Slice>` shape.

A host registers the framework plumbing once and then each slice it wants; the slices contribute their entities to the
one `OpenIdDbContext`, so cross-slice writes remain a single unit of work.

### Naming

The EF slices are named `…Persistence.EntityFramework.<Slice>` (tech-first) rather than `…<Domain>.EntityFramework`
(domain-first). Once slices are first-class, grouping them under the persistence/EF namespace keeps the framework and
all its slices discoverable together and makes the "is-a framework slice" relationship read from the name. A non-EF
adapter is _not_ a slice and keeps domain-first naming — for example the ASP.NET Core Identity adapter stays
`NCode.Identity.OpenId.Accounts.AspNetIdentity`.

Each slice's **root namespace matches its package id** (`…Persistence.EntityFramework.<Slice>`, with `.Entities` and
`.Stores` sub-namespaces), so a type's namespace names its owning slice and cross-slice references cannot be made by
accident. The framework's own types stay under `…Persistence.EntityFramework`; its cross-slice contract
`ISupportTenantEntity` therefore lives in `…Persistence.EntityFramework.Entities`, which a slice entity imports to be
tenant-scoped.

## Options considered

- **One package, expose the framework pieces as `public` (in place).** Rejected: a satellite would still depend on the
  package that contains the core stores, so the dependency direction stays wrong and the framework/schema split is only
  cosmetic. Acceptable for a single satellite, but not once slices multiply.
- **A generic, non-OpenID `NCode.Persistence.EntityFramework` framework.** Rejected: the framework is OpenID-flavored
  (the tenant-scoped `OpenIdDbContext`, `UseIdGenerator`, the OpenID model-contribution seam) and there is no goal to
  reuse it outside this family, so a context-agnostic layer would be speculative generality.
- **Per-domain EF packages that each own a `DbContext`.** Rejected: separate contexts cannot share a unit of work, so a
  cross-slice write (a local account and its self-issued federated identity) could not be atomic. One shared,
  contributor-extensible `OpenIdDbContext` is the enabler.
- **Keep typed `DbSet`s on `OpenIdDbContext`.** Rejected: a typed `DbSet<ClientEntity>` _on the context type_ would
  re-couple the framework to the core schema (the entity would have to live in the framework), defeating the split. The
  framework context stays schema-agnostic and stores use `context.Set<TEntity>()`; a slice may still offer the
  `context.Clients` convenience for its own entities via extension `DbSet` accessors, which keep the coupling inside the
  slice.
- **Framework + slices (chosen).** The framework is reused, not re-implemented; a slice depends only on the framework;
  the shared context keeps cross-slice writes atomic; new slices are additive.

## Consequences

- `BaseStore`, `AddStore`, and `UseIdGeneratorAttribute` join the public surface; a slice's store inherits `BaseStore`
  and registers with `AddStore`, dropping the hand-rolled `IStore` delegation, normalization, and factory boilerplate.
- `OpenIdDbContext` loses its typed `DbSet` properties; consumers that read them move to `context.Set<TEntity>()`, and
  every entity (core included) is registered through a contributor.
- The core schema moves from `…Persistence.EntityFramework` to `…Persistence.EntityFramework.Core`; hosts and tests that
  registered EF persistence re-point to the framework plumbing plus the core slice, and the account EF package moves to
  `…Persistence.EntityFramework.Accounts`.
- Adding a new persistence slice is additive: a package of entities, stores, a contributor, and registrations against
  the framework — no change to the framework or other slices.
- A deliberately deferred option ([`BACKLOG.md`](../../BACKLOG.md)): if the framework is ever wanted without the OpenID
  schema, a context-agnostic layer can be extracted below it; there is no current goal for that.

## References

- [ADR-0051](0051-local-accounts-behind-a-pluggable-account-source-seam.md) — the local-account slice whose shared-context
  requirement motivated making the framework reusable and the context contributor-extensible.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — the tenant-scoping infrastructure that stays
  in the framework and is applied to every slice's `ISupportTenantEntity` uniformly.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — the dependency discipline this sharpens:
  slices depend on the framework, not on each other.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — the opaque-id generation the now-public `UseIdGenerator`
  provides to every slice.
