# 1480. A management route answers from what it read and declares every problem it answers

- **Status:** Accepted
- **Date:** 2026-11-08
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md)

## Context

A management route answers `404 Not Found` and `409 Conflict`, and the OpenAPI document tells a client which answers a
route gives. The forces:

- **A store reports every failure alike.** A store reports a duplicate, a missing row and a failure of its backing
  database alike as `InvalidOperationException`, so a route that maps that exception answers a conflict for a store
  failure. A lost optimistic-concurrency race is a different exception, `StoreConcurrencyException`, which the route
  group's `StoreConcurrencyEndpointFilter` answers `412`
  ([ADR-1214](1214-a-management-write-that-loses-a-race-is-answered-412.md)).
- **The rows a route read decide the answer.** A route that loads its owner, or calls a service that does, knows before
  it writes whether the resource is missing and whether an identifier, a `kid` or a scope value is taken.
- **The OpenAPI document lists only declared answers.** A route that answers a status it does not declare lies by
  omission, and a client that reads the document never handles it.
- **A bare `404` has no body.** An error that is not a problem document breaks the contract that every management error
  is one ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)), and a declared `404` problem describes a body the
  route does not send.
- **The root group's filters answer for state any route may read.** A stored settings document a route cannot honor
  answers `409` ([ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)),
  and an unavailable root key answers `503`
  ([ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)); both arise from reads
  through shared services whose reach no route owns.

## Decision

**A management route answers `404` and `409` from the rows it read before it writes, answers every error as a problem
document, and declares every problem it answers on its mapping, while the route group declares the answers of its own
filters on every route.**

1. **A route answers `404` and `409` from what it read.** A route answers them from the rows it, or a service it calls
   such as `IResourceOwnershipService`, read before the write. It never catches a store exception to answer them and
   never answers them from the return value of a store write. The store's `InvalidOperationException` propagates
   (`500`), and a lost race is `StoreConcurrencyException`, answered `412` by `StoreConcurrencyEndpointFilter`
   ([ADR-1186](1186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md)).
2. **The scope routes check the loaded scopes.** The resource-server scope routes check the loaded
   `resourceServer.Scopes`, comparing a value `OrdinalIgnoreCase` as `GetScopeAsync` does. They write the stored
   scope's own `Value` (its casing) and `IsSystem`, not the route value. Removing a system scope answers `409` with the
   fixed detail "A reserved system scope cannot be removed.", never an exception message.
3. **Every management error is a problem document.** `ManagementProblems.ResourceNotFound()`, a public static class
   in `NCode.Identity.OpenId.Management.Abstractions` with the detail "The resource does not exist.", answers every
   missing resource in place of a bare `TypedResults.NotFound()`. `ManagementEnvironmentEndpointFilter` answers an
   `HttpResultException` whose result has a status of `400` or more and no body (an `IStatusCodeHttpResult` that is not
   an `IValueHttpResult`) with `TypedResults.Problem(statusCode: …)`, because a tenant `OpenIdContextFactory` reports a
   missing tenant as a bare `404`. A declared `ProducesProblem(404)` is therefore truthful.
4. **A route declares what its handler answers, and `412` when it writes.** A route declares the problems its handler
   answers with `ProducesProblem`, and `412` when it saves a unit of work it does not retry or checks `If-Match`. An
   insert counts, because whether a save can lose a race depends on which rows the store touches. A revocation (a grant
   or a session) retries and declares no `412`
   ([ADR-1215](1215-a-revocation-wins-a-concurrency-race.md)).
5. **The route group declares the answers of its state filters.** `ManagementRootGroup` declares `409` for a stored
   settings document it cannot honor (`UnreadableSettingsEndpointFilter`) and `503` for an unavailable root key
   (`OwnerRootKeyUnavailableEndpointFilter`) once, for every route. A route keeps declaring its own `409`s and
   declares no `503`.
6. **A validation problem is declared with its own schema.** A route whose `400` is a validation problem declares it
   with `ProducesValidationProblem()`, whose plain `400` problems fit that schema; any other `400` is
   `ProducesProblem(StatusCodes.Status400BadRequest)`. A route declares `400` once.
7. **The tests read the declarations from the routes.** `EndpointProblemMetadata` lives in the support project
   `tests/NCode.Identity.OpenId.Management.TestSupport` (namespace `NCode.Identity.OpenId.Management.Endpoints`, not
   packable), counts `HttpValidationProblemDetails` as a problem, and matches a pattern with its trailing `/` trimmed,
   so every management test project reads a handler's declarations with it.
8. **A theory pins each handler and a walk pins the host.** Each handler's
   `Map_Always_DeclaresEveryProblemARouteAnswers` theory pins the exact problems of each of its routes.
   `ManagementRouteProblemDeclarationTests` walks every `/api` route of the real host and asserts the group's `409`
   and `503` and, for every route except the four any-caller routes (endpoint names `api/me/get`,
   `api/me/teams/list`, `api/me/tenants/list` and `api/capabilities/get`), `401` and `403`.

## Options considered

- **Mapping store exceptions to `404` and `409`.** The store reports a duplicate, a missing row and a failure alike as
  `InvalidOperationException`, so a failure reads as a conflict and the route hides a fault that a `500` shows. The
  rows the route read answer the same question without a catch.
- **Declaring `409` and `503` per route from a trace of each handler.** The filters answer for state read through
  shared services, whose reach grows without a route changing, so a per-route list drifts silently. One declaration on
  the route group stays true for every route.
- **A bare `404`.** The status declares no body, and a declared `404` problem describes a body the route does not
  send, so the declared schema lies.

## Consequences

- A store failure answers `500`, never a conflict or a missing resource, and a lost race answers `412`.
- A client reads from the OpenAPI document every problem a route answers.
- A route that answers a problem declares it, and its handler's theory fails until it does.
- A route that authorizes its caller declares `401` and `403`; the four any-caller routes are the only exceptions the
  host-wide test names.

## References

- [ADR-1186](1186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md): a secret's identifier
  and an imported key's `kid` are checked against the owner's rows.
- [ADR-1214](1214-a-management-write-that-loses-a-race-is-answered-412.md): a lost race answers `412`.
- [ADR-1215](1215-a-revocation-wins-a-concurrency-race.md): a revocation retries and declares no `412`.
- [ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md): the route
  group's `409`.
- [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md): the route group's `503`.
- [ADR-1422](1422-a-management-problem-that-asks-the-caller-to-wait-carries-the-wait-in-retry-after-seconds.md): the
  management problem members.
- [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md):
  the secret routes that declare their problems.
- [ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md):
  the remote secret routes.
- [project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md): a write that loses
  a race is answered `412`.
- [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457): problem details for HTTP APIs.
- [#700](https://github.com/NCodeGroup/Identity/issues/700),
  [#701](https://github.com/NCodeGroup/Identity/issues/701)
