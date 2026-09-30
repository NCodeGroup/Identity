# 15. Management core validation via replaceable validators

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** NCode Group

## Context

The OpenID **management** endpoints (server / tenant / client, plus their settings and secrets) guard each
mutating operation with a sequence of **preconditions**: the resource exists, the caller is authorized, an
`If-Match` token matches, a parent exists, an id (or domain) is unique, no dependent children remain, a
required field is present. [ADR-0013](0013-management-precondition-mediator-pipeline.md) modelled these as a
**mediator fan-out pipeline** — a `Validate…Command` per operation with a separate `ICommandHandler` per
individual check, ordered by priority and short-circuiting on the first error.

Shipping that revealed three costs that outweighed its benefit:

- **The extensibility it optimized for is speculative.** These preconditions are the **intrinsic, fixed**
  requirements of the entities themselves — they do not change per deployment. The fan-out's value is letting
  an integrator inject or replace a *single* precondition without touching the others; we have no such
  requirement today (YAGNI).
- **Ceremony.** Each check is a ~50-line class with its own command deconstruction and priority. One operation
  is spread across many files whose execution order is implied by `ISupportMediatorPriority` rather than read
  top-to-bottom.
- **A concrete inefficiency.** Each fan-out handler opened its **own** `IStoreManager`; a single create-tenant
  ran authorization + id-uniqueness + domain-uniqueness across *multiple* store managers instead of one shared
  unit of work.

The unsound part of the prior design — inferring a failure cause from a caught `InvalidOperationException` —
was correctly identified in ADR-0013 and must be preserved: a precondition is only known to have failed by
**querying the symptom directly** and deciding on the fact.

## Decision

Replace the mediator pipeline with a single **replaceable validator service per entity**:
`ITenantValidator`, `IClientValidator`, `IServerValidator` — public interfaces with a `ValidateCreateAsync` /
`ValidateUpdateAsync` / `ValidateDeleteAsync` method (server has no update), each returning a
`ManagementError?` (`null` = valid). This is the **service** shape of
[ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md), not the strategy-collection/mediator shape.

- **Fast, ordered, cohesive.** The `Default{Entity}Validator` runs the intrinsic checks in order, short-circuiting
  on the first `ManagementError`. The whole "what must hold to update a tenant" reads in one place. Shared
  authorization→`ManagementError` (401/403) logic lives on a small `DefaultResourceValidator` base.
- **One unit of work.** The endpoint passes its already-open `IStoreManager` to the validator, so every check
  shares one store/transaction view instead of opening its own.
- **Decides on queried facts.** Uniqueness, parent-exists, and no-dependents are store queries that return a
  deterministic `409`/`400`; nothing is inferred from a caught exception.
- **Hydrated model for updates.** The endpoint applies the JSON Patch to a throwaway model first, then hands the
  **proposed** state to `ValidateUpdateAsync`, so post-patch checks (required fields, domain uniqueness) are
  ordinary ordered validations rather than inline specials in the endpoint.
- **Extensibility is a DI replacement.** Each validator is registered `TryAddSingleton<I{Entity}Validator,
  Default{Entity}Validator>()`, so an integrator that needs custom rules registers **their own implementation**
  of the interface — replacing (or wrapping) the default wholesale. If per-check composition is ever genuinely
  needed, a pipeline can be layered *inside* a custom validator, after the fast core checks.

The `Validate…Command` records, the per-check `Default…Handler` classes, the `AddMediator()` registration, the
`[FromServices] IMediator` endpoint parameters, and the `NCode.Mediator` package reference are all removed from
the management library. Because the validators are singletons, the endpoint handlers inject them by constructor
(the earlier `[FromServices]` workaround for the scoped `IMediator` is no longer needed).

## Options considered

- **Keep the mediator fan-out (ADR-0013).** Rejected: it pays per-check class/UoW ceremony for an extension
  seam we do not use. Reversed before release rather than shipped and carried.
- **One handler/method per `Entity-Operation` on the endpoint handler.** Viable and simple, but a separate
  interface makes the core validation independently testable and — crucially — **replaceable via DI**, which is
  the extensibility story we do want to keep open.
- **Inline validation in each endpoint.** Rejected: it scatters the same authz/If-Match logic across every
  endpoint and cannot be replaced by an integrator.

## Consequences

- The management validation surface is uniform and reads top-to-bottom; one operation is one method.
- A single store manager backs all of an operation's checks.
- Public API: three new `I{Entity}Validator` interfaces; removal of the `Validate…Command` records and the
  `NCode.Mediator` dependency from the management library.
- Extensibility is coarser than the fan-out (replace the whole validator, not one check) — an accepted trade
  for simplicity, and reversible by adding an in-validator pipeline later if a real need appears.
- The `Default{Entity}Validator` implementations are `internal`; integrators replace a validator by registering
  their own implementation of the public interface **before** `AddOpenIdManagementLibrary()` (the library uses
  `TryAdd`).

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service vs strategy-collection vs
  mediator litmus test this decision re-applies.
- [ADR-0013](0013-management-precondition-mediator-pipeline.md) — the superseded mediator pipeline, whose
  "decide on queried facts, not caught exceptions" analysis is retained.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — server-generated ids; its uniqueness checks are the
  ones the validators assert.
