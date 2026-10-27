# 61. Local-account management is an opt-in capability that plugs into the Management slice

- **Status:** Accepted
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

The OpenID management slice (`NCode.Identity.OpenId.Management` and its abstractions) is a general administration
surface: a host enables it to manage clients, tenants, secrets, and grants. Local-account management — creating, listing,
reading, updating, deleting an end-user subject, resetting its credential, and managing its claims and metadata — is a
capability specific to hosts that back their users with a local-account store. Many management hosts front a different
user store, or none.

A general slice must not be coupled to one optional capability. When the local-account management endpoints, their
validator and wire contracts, and their audit event live inside the general management packages — and the management
implementation depends on the account abstractions — every management host carries the local-accounts capability whether
or not it wants it, and the management audit abstraction knows the shape of an event that is not part of its slice.

[ADR-0042](0042-endpoint-groups.md) establishes the keyed endpoint-group model this design relies on, and
[ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md) establishes the same
dependency-inversion principle for the authentication slice: the local-account-specific resource-owner password grant is
an opt-in package that depends on the account abstractions, so the base authentication slice does not. The family is
pre-release ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)), which favors the cohesive model over
compatibility.

## Decision

**Local-account management is an opt-in capability in its own account-owned packages that depend on the management
_abstractions_. The base management slice has no dependency on the account packages, and the shared endpoint
infrastructure it owns is a public abstraction that any capability package builds on.**

### Two account-owned packages

- `NCode.Identity.OpenId.Accounts.Management.Abstractions` owns the wire contracts (namespace
  `NCode.Identity.OpenId.Accounts.Management.Contracts`), the `ILocalAccountValidator` seam, and the local-account audit
  event (`LocalAccountChangedAuditEvent`, whose resource type is `AccountManagementResourceTypes.LocalAccount`).
- `NCode.Identity.OpenId.Accounts.Management` owns the endpoint handler, the default validator, and the
  `AddLocalAccountManagement()` registration. It depends on `NCode.Identity.OpenId.Management.Abstractions` and
  `NCode.Identity.OpenId.Accounts.Abstractions`, never on the management implementation package.

`NCode.Identity.OpenId.Management` depends on the management, OpenID, and persistence abstractions only — not on the
account abstractions.

### Endpoints attach to a route group by its well-known key

Endpoints register as **keyed** `IEndpointProvider` services under the owning route group's name
([ADR-0042](0042-endpoint-groups.md)); the route tree resolves the keyed providers for a group when it maps that group.
A capability package therefore attaches an endpoint to a group it does not own by registering its provider keyed under
that group's published name, with no change to the group. `ManagementEndpointGroups.Tenant` publishes the tenant-plane
group's name, and `AddLocalAccountManagement()` registers the local-account endpoint handler keyed under it, nesting it
beneath `/api/tenants/{tenantId}`.

### Shared endpoint infrastructure is a public abstraction

A capability package authors management endpoints against abstractions, not the management implementation, so
`NCode.Identity.OpenId.Management.Abstractions` exposes the reusable building blocks:

- `ManagementApiEndpointHandler` is the public base carrying the generic, backing-store-agnostic endpoint helpers:
  resource-based authorization, single-item and collection `GET` processing, authorization-failure mapping, page-limit
  clamping, resource-snapshot serialization, and error mapping. The management implementation's internal
  `BaseApiEndpointHandler` derives from it and adds the secret- and settings-projection helpers specific to its own
  client, tenant, and server handlers.
- `DefaultResourceValidator`, `Operations`, and `TenantScopeResource` are the validator base, the CRUD authorization
  requirements, and the tenant-scope authorization resource.

### Audit recording is a generic, capability-agnostic seam

The management audit recorder exposes one generic entry point,
`RecordResourceChangeAsync(HttpContext, ResourceChangeAuditEvent, ct)`: it stamps the common audit envelope (event id,
timestamp, correlation id, acting administrator, source) onto a caller-supplied resource-change event and publishes it,
keying dispatch off the event's runtime type. Each capability defines and owns its own `ResourceChangeAuditEvent`
subtype and records through this seam, so the base auditing abstraction carries no per-capability method and does not
know the shape of any one capability's events.

### Capability presence is registration, not a runtime flag

A capability is available when its package is registered; the handler performs no runtime check for whether the
capability is configured. A per-request guard that returns `501 Not Implemented` when a store is absent — a library
testing whether the library itself is wired up — is rejected: the endpoints exist only when `AddLocalAccountManagement()`
is called, which already answers that question. The store prerequisite, however, is enforced at **compile time**, not
deferred to resolution: a local-account store registration (`AddLocalAccountEntityFramework()`,
`AddAspNetIdentityLocalAccountStore<TUser>()`, or a custom store) **returns** an `IServiceBuilder<LocalAccountLibrary>`,
and `AddLocalAccountManagement(IServiceBuilder<LocalAccountLibrary>)` (like the authentication capabilities) takes it as a
**required parameter** (its receiver stays `IServiceBuilder<OpenIdManagementLibrary>`) — so a capability cannot even be
written without a store. This is a **compile-time** requirement
(distinct from the rejected **per-request** `501` feature-detection) and requires the
store to be registered before the capability — the natural order a host already follows.

## Options considered

- **Account-owned opt-in packages (chosen).** Symmetric with [ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md):
  the base management slice carries no account concerns, and the capability is composable and opt-in.
- **Local-account management inside the management packages (rejected).** Couples the general slice to one optional
  capability and forces every management host to carry it, with the management implementation depending on the account
  abstractions.
- **A capability-specific audit method on the management recorder (rejected).** Puts a per-capability shape on a general
  abstraction; a single generic resource-change seam serves every capability without that coupling.
- **A runtime capability marker the endpoints feature-detect, returning `501` when a store is absent (rejected).** A
  library that checks whether it is itself registered is meaningless once the endpoints are an opt-in package: the
  package's presence is the signal, so the marker and its guards are dead weight.
- **Duplicate the thin endpoint base the capability needs instead of promoting the shared one (rejected).** It leaves
  the other handlers untouched but duplicates endpoint plumbing that drifts; promoting one shared base is the cohesive
  model.

## Consequences

- The capability lives in `NCode.Identity.OpenId.Accounts.Management(.Abstractions)` and is enabled by
  `AddLocalAccountManagement()`, called alongside `AddOpenIdManagementLibrary()` and a local-account store registration.
- `NCode.Identity.OpenId.Management` depends on no account package; `TenantManagementGroup` maps only its own families,
  and the local-account endpoint attaches from the capability package by keyed registration.
- `ManagementApiEndpointHandler`, `DefaultResourceValidator`, `Operations`, `TenantScopeResource`, and
  `ManagementEndpointGroups` are part of the public management abstractions; the management implementation's
  `BaseApiEndpointHandler` derives from the public base.
- The management audit abstraction exposes the generic `RecordResourceChangeAsync` seam and carries no per-capability
  method; the local-account audit event and its resource-type constant belong to the accounts-management abstractions.
- The local-account wire contracts live in `NCode.Identity.OpenId.Accounts.Management.Contracts` and the validator seam
  in `NCode.Identity.OpenId.Accounts.Management`.
- Enabling the capability requires a registered `ILocalAccountStore`, enforced at compile time: a store registration
  returns an `IServiceBuilder<LocalAccountLibrary>` and `AddLocalAccountManagement(IServiceBuilder<LocalAccountLibrary>)`
  takes it as a required parameter, so a missing store is a compile error rather than a startup or resolution-time
  failure. The
  handler still performs no runtime per-request capability self-check.
