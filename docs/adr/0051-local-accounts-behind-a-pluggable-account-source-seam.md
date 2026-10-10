# 51. Local accounts behind a pluggable account-source seam

- **Status:** Proposed (the source/provisioner/store split and `PasswordHash` field amended by [ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md))
- **Date:** 2026-10-07
- **Deciders:** NCode Group

## Amendment (2026-10-10): one store seam and a dependency-inverted password grant

This ADR introduced the `ILocalAccountSource` (read/verify) and `ILocalAccountStore` (persistence) seams (later joined
by `ILocalAccountProvisioner` in [ADR-0057](0057-local-account-eager-provisioning-and-management-seam.md)) and a raw
`PasswordHash` on the account contract.
[ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md) collapses all three seams into a
single `ILocalAccountStore` whose credential operations are first-class (no `PasswordHash` on `PersistedLocalAccount`),
moves the resource-owner password grant out of the authentication package into the opt-in
`NCode.Identity.OpenId.Accounts.Authentication` package (so authentication no longer depends on the account package),
and relocates the metadata projection claim-type constants to `SubjectMetadataClaimTypes`. The core idea — local
accounts are a self-issued connection behind a pluggable seam — stands.

## Context

The stack authenticates an end-user by delegating to an ASP.NET Core authentication scheme at the
subject-authentication seam ([ADR-0030](0030-userinfo-endpoint-and-subject-authentication-seam.md)): the host presents a
`ClaimsPrincipal`, and the runtime resolves it to a durable `PrincipalId`
([ADR-0035](0035-federated-principals-and-identity-resolution.md)). That is sufficient when some other authority has
already authenticated the user, but it leaves three capabilities to the host with no first-class shape:

- **Credential validation.** The resource-owner password grant handler is an intentional stub returning
  `unsupported_grant_type`; there is nowhere for the server itself to verify a username and password.
- **Active status.** `DefaultValidateSubjectAuthenticationHandler` documents that the application "should also register
  an additional handler to validate the subject's active status" — the server has no persisted view of whether an
  account is enabled or disabled.
- **Profile claims.** The id_token / userinfo claims enrichers can only copy claims already present on the request-time
  `ClaimsPrincipal`; there is no server-held profile to source `email`, `name`, and the like.

The recurring question is where an end-user account — the thing that holds a credential, a profile, and a status — lives,
and how it fits the identity model already chosen without introducing a parallel one. A `Principal`
([ADR-0035](0035-federated-principals-and-identity-resolution.md)) is deliberately contentless: a server-owned opaque
join key (`PrincipalId` + concurrency token) that aggregates _one human, many connection-identities_ — the persisted
twin of `ClaimsPrincipal`. A `FederatedIdentity` is the persisted twin of a `ClaimsIdentity`: one connection,
`(issuer, subject) → principal`. Neither holds a credential or a server-owned profile today, because when that model was
written every connection was authenticated by an external authority.

What is missing is the server's own kind of connection: a **local** one, where this server — not an upstream IdP — is
the authority and therefore holds the credential, the profile, and the status it can interrogate directly. This is the
Auth0 "database connection" to a `FederatedIdentity`'s "social/enterprise connection." The further requirement is that it
must not be an opinionated model baked into the core. The stack is extensible and pluggable; end-user storage is a host
concern, and a host that already runs **ASP.NET Core Identity** (`IdentityUser`, `UserManager<TUser>`,
`IUserStore<TUser>`, `IPasswordHasher<TUser>`) must be able to plug it in without the core depending on any store.

## Decision

**A local connection reuses the existing `FederatedIdentity` linkage with its `issuer` set to this server, and carries a
`LocalAccount` — the server-owned credential, profile, and status payload — in a one-to-one companion record. The core
consumes a local account only through a pluggable `ILocalAccountSource` seam and never persists end-user accounts itself;
storage is a host concern satisfied by opt-in reference implementations.**

### `FederatedIdentity` is the universal linkage; `LocalAccount` is the payload

`FederatedIdentity` stays the single linkage concept — `(issuer, subject) → principal` — for _every_ connection, as the
persisted twin of `ClaimsIdentity` that [ADR-0035](0035-federated-principals-and-identity-resolution.md) intends (the
`Federated` prefix marks "this system's own type," not "external-only"). A connection whose `issuer` is an upstream IdP
is a social/enterprise connection; a connection whose `issuer` is this server is a local one. Only local connections
carry a `LocalAccount`:

| concept             | role                                    | present for                          |
| ------------------- | --------------------------------------- | ------------------------------------ |
| `Principal`         | contentless actor (`PrincipalId`)       | every human                          |
| `FederatedIdentity` | linkage `(issuer, subject)` → principal | every connection (external or local) |
| `LocalAccount`      | credential + profile + status payload   | local connections only (1:1)         |

Keeping credential/profile/status on a separate `LocalAccount` (a one-to-one companion, not extra columns on
`FederatedIdentity`) means external identities never grow password or profile columns they never use, and resolution and
the linking policy continue to operate on one uniform linkage with no local special-case. A local sign-in resolves
exactly like any other: the account's self-issued `FederatedIdentity` yields the `PrincipalId` the `sub` claim carries.

### Payload and provider are different axes

The `LocalAccount` _record_ is a DTO (`PersistedLocalAccount`), the persisted boundary shape, consistent with every
other data contract in the stack. Pluggability is not expressed by turning that record into an interface; it is expressed
by the _provider_ seam. `ILocalAccountSource` is that seam — the interface a host implements (or a reference package
supplies) to validate a credential, read a profile, and read a status. `FederatedIdentity` therefore remains a concrete
linkage DTO, not "an interface any provider implements"; the thing that varies by backend is the source, not the row.

### The seam, interrogated by the core

`ILocalAccountSource` is the single abstraction the core delegates to, all scoped to a local account. The persistence
`ILocalAccountStore` (data contract `PersistedLocalAccount`) is the storage seam a source may build on. The source is an
_optional_ dependency, absent by default, so behavior is unchanged when no source is configured. Crucially, absence is
distinct from failure: when no source is registered the password grant returns `unsupported_grant_type` (the server does
not offer the grant) and no active-status or profile-claim step runs, whereas a registered source that rejects a
credential returns `invalid_grant`. A no-op default that merely returned "no account" would conflate those two, so the
seam is injected as a nullable dependency rather than a null-returning default. Registering a real source lights the
capabilities up:

- the password-grant handler validates credentials through the source;
- a subject-validation handler enforces active status through the source;
- the claims enrichers source profile claims through the source.

Credential material crosses the seam as `ReadOnlyMemory<byte>`, never as a managed `string`, so the caller owns a
zeroable buffer and the raw password does not linger on the GC heap. This reuses the `NCode.Buffers` secure-memory
foundation that Jose and the secrets stack are already built on: the password-grant handler rents a pinned,
zeroed-on-return buffer from `SecureMemoryPool<byte>` and encodes into it with `SecureEncoding.UTF8`, and the hashing
source compares with `CryptographicOperations.FixedTimeEquals` and clears working buffers with
`CryptographicOperations.ZeroMemory`. The seam is asynchronous, so `ReadOnlySpan<byte>` cannot be used (a ref struct
cannot cross an `await`); `ReadOnlyMemory<byte>` — exactly what a rented secure buffer's `Memory` exposes — is the
async-safe equivalent. Only the implementation packages take the `NCode.Buffers` dependency; the abstraction stays on
the BCL type. A stored password **hash** remains a `string` — it is a one-way derivation held at rest, not the secret.

### Learn from ASP.NET Core Identity, diverge on the aggregate

ASP.NET Core Identity is the reference for _shape and lessons_, not a template to adopt wholesale. Its aggregate
`IdentityUser` fuses the actor, the credential, and the profile, and models external logins as `IdentityUserLogin` rows
_under_ that user — the "the user is the principal, bolt logins on" shape that double-counts a human holding both a local
account and a social login, exactly what [ADR-0035](0035-federated-principals-and-identity-resolution.md) rejects. The
adapter therefore maps _inward_: an `IdentityUser` is a `LocalAccount` plus its self-issued `FederatedIdentity`, and its
`IdentityUserLogin` rows are additional `FederatedIdentity` rows, all aggregated by one `FederatedPrincipal`.

Three of its choices are borrowed because they are battle-tested and directly serve this design:

- **Store interface segregation.** `UserManager` feature-detects granular capability stores (`IUserPasswordStore`,
  `IUserEmailStore`, `IUserLockoutStore`, `IUserClaimStore`) rather than one wide interface. `ILocalAccountSource` and
  `ILocalAccountStore` carry only the core (validate credential, read profile, read status); lockout, two-factor, and
  password-reset arrive later as _optional capability interfaces_ the core probes for, so deferring them costs no rework
  to the shipped surface.
- **`SecurityStamp`.** A `LocalAccount` carries a security stamp so a credential change can invalidate outstanding
  sessions and tokens; it is a single column now and the hook a future session-revocation flow needs.
- **Password hashing behind a facade.** Credential hashing is abstracted by an in-house `IPasswordHasher` facade
  (span-based: it takes the password as `ReadOnlySpan<byte>` and returns a self-describing hash string with
  rehash-on-verify), not a direct dependency on any one hasher. The default implementation is a modern span-aware
  PBKDF2-HMAC-SHA256 built on `Rfc2898DeriveBytes.Pbkdf2`, `RandomNumberGenerator`, and
  `CryptographicOperations.FixedTimeEquals`; ASP.NET Core Identity's `PasswordHasher<TUser>` is one swappable backend a
  host may register instead, and the ASP.NET Core Identity source bypasses the facade entirely (its `UserManager` owns
  hashing). The lesson borrowed from the framework hasher is the _shape_ (versioned parameters, rehash-on-verify), not a
  coupling to it.

### Two reference implementations prove the seam

Both are opt-in satellite packages the core does not depend on:

- **Generic EF reference** — implements `ILocalAccountSource` over this system's own store abstractions
  (`ILocalAccountStore` → `BaseStore<PersistedLocalAccount, LocalAccountEntity>` on the shared `OpenIdDbContext`), the
  faithful analog of the `FederatedIdentity` slice, with hashed credentials, profile claims, and status, writing the
  `LocalAccount` and its self-issued `FederatedIdentity` in one unit of work.
- **ASP.NET Core Identity adapter** — implements the _same_ seam over `UserManager<TUser>`, so a host brings Microsoft's
  EF-backed user store (or any `IUserStore<TUser>`) with no core change.

Any other store (LDAP, a bespoke table, an external API) is a third implementation of the same seam.

## Options considered

- **Credential/profile/status on the `Principal` (chosen against).** Rejected: it recontents the deliberately-contentless
  join key, and because a human has one principal but may hold several credentials across connections, "the principal's
  password" is ill-defined. Credentials belong to a connection, not the actor.
- **A `LocalAccount` that owns its own `PrincipalId` and a second resolver path (chosen against).** Rejected: giving the
  account a direct principal link and teaching `IPrincipalResolver` a local branch forks resolution. Hanging the payload
  off a self-issued `FederatedIdentity` keeps one uniform linkage and one resolution path.
- **Extending `FederatedIdentity` with sparse credential/profile/status columns, one table (chosen against).** Rejected
  on interface segregation: external, lookup-only identities would carry password and profile columns that are always
  null. A one-to-one `LocalAccount` companion keeps the linkage lean.
- **A standalone end-user/account model independent of `FederatedPrincipal`/`FederatedIdentity` (chosen against).**
  Rejected: it reintroduces the duplicate-human hazard [ADR-0035](0035-federated-principals-and-identity-resolution.md)
  exists to prevent — a human with a local account and a social login becomes two actors that account-linking patches over.
- **Renaming `FederatedIdentity` to a neutral `ConnectionIdentity` (chosen against).** Considered for clarity now that a
  connection may be local, but it splits the `Federated*` family into mixed vocabulary (`FederatedPrincipal` +
  `ConnectionIdentity`) with no clean consistent pair, since `Identity` collides with `ClaimsIdentity` and the product
  name and `Principal` is reserved as the abstract supertype. The `Federated` prefix already means "this system's
  persisted identity type," so a self-issued connection fits the existing name; the definition is clarified instead.
- **Turning `FederatedIdentity` or `LocalAccount` into a provider interface (chosen against).** Rejected: the record is a
  persistence DTO like every other data contract; backend variance lives on the `ILocalAccountSource` provider seam, not
  on the row.
- **Reusing the word "subject" for the account (chosen against).** Rejected for the same reason
  [ADR-0035](0035-federated-principals-and-identity-resolution.md) reserves it: "subject" is the upstream value and the
  emitted `sub` claim (whose value is the `PrincipalId`), not an entity name.
- **Adopting ASP.NET Core Identity's `IdentityUser` model wholesale (chosen against).** Rejected: `IdentityUser` fuses
  the actor, credential, and profile and nests external logins beneath it, reproducing the duplicate-human conflation
  this system splits apart. Its _store interface segregation_, `SecurityStamp`, and `IPasswordHasher<T>` are borrowed;
  its aggregate shape is not.

## Consequences

- The password grant, active-status validation, and profile-claim enrichment gain a first-class home without the core
  persisting any end-user data; an unconfigured host behaves exactly as today.
- New `ILocalAccountSource` (consumption) and `ILocalAccountStore` / `PersistedLocalAccount` (storage) abstractions join
  the surface; the core consumes the source as an optional, nullable dependency that is absent by default, so an
  unconfigured host keeps today's behavior and a configured one returns `invalid_grant` (not `unsupported_grant_type`)
  on a bad credential.
- `FederatedIdentity`'s definition is clarified ([ADR-0035](0035-federated-principals-and-identity-resolution.md)): its
  `issuer` is an external IdP for social/enterprise connections, or this server for a local one; resolution and linking
  are unchanged and uniform across both.
- A local account is written as a `LocalAccount` plus a self-issued `FederatedIdentity` in one unit of work; the `sub`
  emitted for a local login is the resolved `PrincipalId`, so relying parties see one durable subject per human.
- A new EF satellite package carries `LocalAccount` entities, stores, and registration; a new adapter package maps the
  seam onto ASP.NET Core Identity and is the first place the family references
  `Microsoft.AspNetCore.Identity.EntityFrameworkCore`.
- Lockout, two-factor, and password-reset are pre-shaped as optional capability interfaces but not shipped; adding them
  later extends the surface additively without touching the core seam.
- Known follow-ups deferred to [`BACKLOG.md`](../../BACKLOG.md): lockout/throttling policy, password-reset and
  verification flows, and whether the no-op default or the EF reference becomes the Playground's wired default.

## References

- [ADR-0035](0035-federated-principals-and-identity-resolution.md) — federated principals and identities; this ADR adds
  the local kind of connection (a `LocalAccount` payload on a self-issued `FederatedIdentity`) and clarifies that a
  connection's `issuer` may be this server.
- [ADR-0030](0030-userinfo-endpoint-and-subject-authentication-seam.md) — the subject-authentication seam and claims
  enrichers the account source feeds.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — opaque server-generated ids the `LocalAccount` follows.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — why the EF reference and the ASP.NET Core
  Identity adapter are opt-in satellites depending only on abstractions.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service-vs-mediator model under which the
  default handlers delegate to the `ILocalAccountSource` service.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the pre-release, Auth0-parity-plus posture: a database
  connection to `FederatedIdentity`'s social/enterprise connection.
- ASP.NET Core Identity (`Microsoft.AspNetCore.Identity`) — prior art for store interface segregation, `SecurityStamp`,
  and password-hasher shape (versioned parameters, rehash-on-verify), borrowed in shape while its aggregate
  `IdentityUser` model is deliberately not adopted and credential hashing is abstracted behind an in-house
  `IPasswordHasher` facade (default: span-aware PBKDF2) rather than coupled to `PasswordHasher<TUser>`.
