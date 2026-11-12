# 1214. A management write that loses a concurrency race is answered 412

- **Status:** Accepted (amended by [ADR-1215](1215-a-revocation-wins-a-concurrency-race.md): a revocation retries a lost
  race)
- **Date:** 2026-10-22
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md)

## Context

[project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md) answers an API write that
loses a concurrency race with `412 Precondition Failed`, from one endpoint filter on the API's root route group. This
record applies it to the management API.

A store reports a lost concurrency race as a `StoreConcurrencyException`
([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)). The management endpoints let the
exception reach the client as `500`, although the caller can recover by reading the resource again (issue [#261](https://github.com/NCodeGroup/identity/issues/261)).

## Decision

**One endpoint filter answers a lost race with `412 Precondition Failed`.** `StoreConcurrencyEndpointFilter` is
installed once, on the management root route group (`/api`), so every management endpoint inherits it, including the
endpoints that a package adds under the tenant group. It answers a `StoreConcurrencyException` with a `412` problem
("The resource was changed by another request after this request read it. Read it again and retry.") and logs it at
Debug (event 3005). No endpoint catches the exception itself.

The filter covers the management API only. A protocol endpoint answers a lost race as its specification requires where a
spend or rotation handles it
([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md),
[ADR-1211](1211-a-rotated-refresh-token-is-spent-with-its-successor.md)); a revocation reads again and retries
([ADR-1215](1215-a-revocation-wins-a-concurrency-race.md)).

## Options considered

- **A catch in each handler.** Rejected: every handler, including the handlers of a package, would repeat it, and a
  handler that forgets it answers `500`.
- **A retry in the server.** Rejected: the write was computed from a stale read, and the caller decides whether it still
  applies ([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)).

## Consequences

- Work that a write starts after its own save, such as the key seed of a tenant, is answered `412` as well when it loses
  a race, and the re-read of the caller shows the change that was committed.
- This implements issue [#261](https://github.com/NCodeGroup/identity/issues/261).

## References

- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) — a lost race is a
  `StoreConcurrencyException`.
- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) — the concurrency tokens and the `If-Match` check at the
  edge.
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md) — the management validators that answer a stale
  `If-Match` with `412`.
- Code: `NCode.Identity.OpenId.Management.Endpoints.StoreConcurrencyEndpointFilter`,
  `NCode.Identity.OpenId.Management.Endpoints.ManagementRootGroup`.
