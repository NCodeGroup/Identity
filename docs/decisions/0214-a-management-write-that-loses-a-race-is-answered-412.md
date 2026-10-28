# 214. A management write that loses a concurrency race is answered 412

- **Status:** Accepted (amended by [ADR-0215](0215-a-revocation-wins-a-concurrency-race.md): a revocation retries a lost
  race)
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

A store reports a lost concurrency race as a `StoreConcurrencyException`
([ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md)). The management endpoints let the
exception reach the client as `500`, although the caller can recover by reading the resource again (issue [#261](https://github.com/NCodeGroup/Identity/issues/261)).

## Decision

**One endpoint filter answers a lost race with `412 Precondition Failed`.** `StoreConcurrencyEndpointFilter` is
installed once, on the management root route group (`/api`), so every management endpoint inherits it, including the
endpoints that a package adds under the tenant group. It answers a `StoreConcurrencyException` with a `412` problem
("The resource was changed by another request after this request read it. Read it again and retry.") and logs it at
Debug (event 3005). No endpoint catches the exception itself.

`412` is the answer because a lost race is the condition that a stale `If-Match` reports: the resource changed after the
view of the caller, and the caller recovers the same way, by reading it again and retrying.

The filter covers the management API only. A protocol endpoint answers a lost race as its specification requires where a
spend or rotation handles it
([ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md),
[ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md)); a revocation reads again and retries
([ADR-0215](0215-a-revocation-wins-a-concurrency-race.md)).

## Options considered

- **`409 Conflict`.** Rejected: it reports a state that the caller must change, such as dependents that block a delete,
  whereas a lost race resolves on a retry.
- **A catch in each handler.** Rejected: every handler, including the handlers of a package, would repeat it, and a
  handler that forgets it answers `500`.
- **A retry in the server.** Rejected: the write was computed from a stale read, and the caller decides whether it still
  applies ([ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md)).

## Consequences

- No management write reaches its client as `500` for a lost race.
- Work that a write starts after its own save, such as the key seed of a tenant, is answered `412` as well when it loses
  a race, and the re-read of the caller shows the change that was committed.
- This implements issue [#261](https://github.com/NCodeGroup/Identity/issues/261).

## References

- [ADR-0208](0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md) — a lost race is a
  `StoreConcurrencyException`.
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the concurrency tokens and the `If-Match` check at the
  edge.
- [ADR-0015](0015-management-core-validation-via-validators.md) — the management validators that answer a stale
  `If-Match` with `412`.
- Code: `NCode.Identity.OpenId.Management.Endpoints.StoreConcurrencyEndpointFilter`,
  `NCode.Identity.OpenId.Management.Endpoints.ManagementRootGroup`.
