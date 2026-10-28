# 13. Management preconditions are a mediator validation pipeline

- **Status:** Superseded by [ADR-0015](0015-management-core-validation-via-validators.md)
- **Date:** 2026-09-29
- **Deciders:** NCode Group

> **Superseded by [ADR-0015](0015-management-core-validation-via-validators.md).** This ADR records a mediator
> fan-out pipeline for management preconditions — a `Validate…Command` per operation with a separate handler per
> check. [ADR-0015](0015-management-core-validation-via-validators.md) replaces it with a single replaceable
> `I{Entity}Validator` service per entity: the preconditions our entities enforce are a **fixed, intrinsic** set,
> so the fan-out's per-precondition extensibility does not justify its ceremony (a class per check, each opening
> its own store manager). The "inferring a failure from a caught exception is unsound" analysis below still holds
> and is preserved by the validators, which decide on queried facts.

## Context

The OpenID **management** endpoints (server / tenant / client + their settings and secrets) each guard a
mutating operation with a sequence of **stateful preconditions**: the resource exists, the caller is
authorized for the operation, an `If-Match` token matches, a parent exists, an id is unique, no dependent
children remain, and so on. The first cut expressed these inline in each endpoint handler and — worse —
inferred failure causes by catching broad framework exceptions (`catch (InvalidOperationException)`) and
_assuming_ the exception meant a specific domain condition.

That inference is unsound. `InvalidOperationException` is thrown by LINQ, EF, and our own stores for several
different reasons; catching it and labelling it "duplicate" or "missing parent" is a guess. You can only be
**sure** a precondition failed by **checking the symptom directly** (querying for the dependent, the parent,
the token) and deciding on the fact — not by catching an exception you _hope_ corresponds to it.

The preconditions are also a natural **extension seam**: a deployment may want to add its own business rules
(e.g. tenant-specific policies) to an operation. [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)'s
litmus test — _"a deliberate extension/override/observation seam on the request pipeline → mediator"_ —
places this squarely in the mediator shape, and the authentication stack already implements exactly this
pattern with its `Validate…Command` fan-outs.

## Decision

Model each management operation's preconditions as a **mediator fan-out validation pipeline**, mirroring the
authentication `Validate…Command` pattern:

- A public `Validate{Operation}Command` (a `readonly record struct : ICommand`) carries the **already-loaded
  state**, the request, the caller, and a shared `OperationDisposition<ManagementError>`.
- Each precondition is a separate `ICommandHandler<T>` implementing `ISupportMediatorPriority`. Handlers run
  in priority order; each **short-circuits** when `Disposition.HasError`, otherwise **checks its symptom
  directly** (queries the store / compares the token) and, on failure, sets `Disposition.Error ??= …`. No
  handler throws for an expected outcome.
- `ManagementError` carries the HTTP status and a **fixed, safe** detail string (never `exception.Message`).
  One mapper turns a populated disposition into the `IResult` (404 / 403 / 409 / 412 / 400).
- The endpoint loads the resource, dispatches the command, and either returns the mapped error or performs
  the mutation. The **decision is made on facts the handlers queried**, not on caught exceptions.

Exceptions shrink to the two things that genuinely cannot be pre-checked — **races** — and those surface as
the database's own **specific, contractual** exceptions: a unique-index or FK-`RESTRICT` violation
(`DbUpdateException`) and a concurrency-token mismatch (`DbUpdateConcurrencyException`). Anything else is an
**unexpected fault** and is left to bubble to the 500 handler, never swallowed.

Stores therefore expose the **facts** the pipeline needs (existence, "has dependents") as explicit query
methods, and their remove methods no longer overload a broad exception to signal an expected outcome.

## Consequences

- Every failure mode is a small, individually testable rule, and the answer to "how do we know condition B
  holds?" is "a handler queried for B" — not exception inference.
- Deployments can add, reorder, or override precondition handlers without touching the endpoints.
- There is a per-operation command + a handler per rule (more types), and the Management library takes a
  dependency on `NCode.Mediator` (`AddMediator()`), consistent with the authentication library.
- The broad `catch (InvalidOperationException)` guesswork is removed; only the specific EF race exceptions are
  caught, and only at the mutation boundary.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — seam-vs-capability litmus test that
  places these preconditions in the mediator.
- [ADR-0011](0011-secret-management-api.md) — the secret/owner management endpoints these preconditions guard.
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the concurrency tokens the `If-Match` and race
  handling rely on.
