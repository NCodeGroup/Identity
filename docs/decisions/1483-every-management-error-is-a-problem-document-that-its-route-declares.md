# 1483. Every management error is a problem document that its route declares

- **Status:** Accepted
- **Date:** 2026-11-09
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md)

## Context

A management route answers `404 Not Found`, `409 Conflict` and `412 Precondition Failed`, and the OpenAPI document tells
a client which answers a route gives.
[project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md) answers an API write
that loses a concurrency race with `412`, from one endpoint filter on the API's root route group. The forces:

- **A store reports every failure alike.** A store reports a duplicate, a missing row and a failure of its backing
  database alike as `InvalidOperationException`, so a route that maps that exception answers a conflict for a store
  failure. A lost optimistic-concurrency race is a different exception, `StoreConcurrencyException`
  ([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)),
  and the caller can recover from it by reading the resource again.
- **The rows a route read decide the answer.** A route that loads its owner, or calls a service that does, knows before
  it writes whether the resource is missing and whether an identifier, a `kid` or a scope value is taken.
- **The OpenAPI document lists only declared answers.** A route that answers a status it does not declare lies by
  omission, and a client that reads the document never handles it.
- **A bare `404` has no body.** An error that is not a problem document breaks the contract that every management error
  is one ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)), and a declared `404` problem describes a body the route
  does not send.
- **A JSON body is read before endpoint filters run.** A body that is not JSON, or is not valid JSON for the operation,
  fails while the request delegate binds its parameters, so no endpoint filter sees the request and the answer is a bare
  `400`.
- **A thrown binding failure meets the host's exception handling.** With `ThrowOnBadRequest`, which is on in the
  Development environment, binding throws `BadHttpRequestException` instead of setting a status, and an exception
  handler of the host, such as the developer exception page, renders the failure.
- **The router answers some errors with no route endpoint at all.** A body with the wrong content type is answered
  `415`, a wrong method `405` and an unknown path `404`, each with no body and no endpoint that a convention could wrap.
- **The root group's filters answer for state any route may read.** A stored settings document a route cannot honor
  answers `409` ([ADR-1245](1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md)),
  and an unavailable root key answers `503`
  ([ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)); both arise from reads through
  shared services whose reach no route owns.
- **A browser fetch layer drops headers.** Three problems ask the caller to wait: a rekey too soon after the last
  rotation, a root key change too soon, and an owner whose root key is unavailable. Each carries the wait in
  `Retry-After`, but RTK Query's `fetchBaseQuery` hands a failed response's body to the caller and drops its headers, so
  the console cannot show the wait. RFC 9457 allows a problem to carry extension members.

## Decision

**Every management error is an RFC 9457 problem document that the management library produces in every host, a route
answers `404` and `409` from the rows it read, and each route declares every problem it answers.**

1. **`ManagementProblems` creates the shared problems.** The public static class `ManagementProblems` in
   `NCode.Identity.OpenId.Management.Abstractions` creates `AuthenticationRequired()` (`401`), `ResourceNotFound()`
   (`404`, "The resource does not exist.") and `PreconditionFailed(resource)` (`412`, "The supplied If-Match token does
   not match the current <resource>."), which answers an `If-Match` token that does not match the stored settings or
   secret.
2. **One middleware answers every bodyless error under the management prefix.** `ManagementProblemResponseMiddleware`,
   added by `ManagementProblemResponseStartupFilter` (an `IStartupFilter`) as the outermost middleware, answers every
   response under `ManagementRootGroup.RoutePrefix`, the one source of the prefix, that ends with a status of `400` to
   `599` and no body (not started, no `Content-Length`, no `Content-Type`: the rule the status-code pages middleware
   uses) with `TypedResults.Problem(statusCode: …)`. A handler's result, a failed binding's `400`, the router's `404`,
   `405` and `415`, and any endpoint a host maps under the prefix are therefore problem documents, in every host, and a
   host adds nothing.
3. **The root group converts a thrown binding failure.** `BindingExceptionEndpointConvention` answers a
   `BadHttpRequestException` that `ThrowOnBadRequest` throws with its status and no body, before a host exception
   handler renders it, and the middleware writes the problem. `ManagementEnvironmentEndpointFilter` returns an
   `HttpResultException`'s result and stops a request whose binding failed, so a binding failure is answered before
   authentication.
4. **A route answers `404` and `409` from what it read.** A route answers them from the rows it, or a service it calls
   such as `IResourceOwnershipService`, read before the write. It never catches a store exception to answer them and
   never answers them from the return value of a store write. The store's `InvalidOperationException` propagates as
   `500`.
5. **A lost race is answered `412` once.** `StoreConcurrencyEndpointFilter` is installed once, on the management root
   route group (`/api`), so every management endpoint inherits it, including the endpoints that a package adds under the
   tenant group. It answers a `StoreConcurrencyException` with a `412` problem ("The resource was changed by another
   request after this request read it. Read it again and retry.") and logs it at Debug (`LostConcurrencyRace`). No
   endpoint catches the exception itself. A grant or session revocation retries a lost race
   ([ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md)) and declares no `412`; when its last attempt
   loses, `StoreConcurrencyEndpointFilter` still answers `412`. The filter covers the management API only: a protocol
   endpoint answers a lost race as its specification requires.
6. **The scope routes check the loaded scopes.** The resource-server scope routes check the loaded
   `resourceServer.Scopes`, comparing a value `OrdinalIgnoreCase`. They write the stored scope's own `Value` (its
   casing) and `IsSystem`, not the route value. Removing a system scope answers `409` with the fixed detail "A reserved
   system scope cannot be removed.", never an exception message.
7. **A route declares what its handler answers.** A route declares every problem its handler answers with
   `ProducesProblem`:
   - `412` when it saves a unit of work it does not retry, an insert included because whether a save can lose a race
     depends on which rows the store touches, or when it checks `If-Match`;
   - `ProducesValidationProblem()` for a validation `400`, whose plain `400` problems fit that schema;
   - `415` for a route with a `[FromBody]` parameter;
   - `400`, once, for a route with a `[FromBody]` parameter, a `[FromQuery]`, `[FromHeader]` or `[FromRoute]` parameter
     whose type is not `string`, or a required `string` query or header parameter, unless it already declares `400`.
8. **The route group declares the answers of its state filters.** `ManagementRootGroup` declares once, for every route,
   the `409` of `UnreadableSettingsEndpointFilter` ([ADR-1245](1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md))
   and the `503` of `OwnerRootKeyUnavailableEndpointFilter`
   ([ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)). A route declares its own `409`s
   and no `503`.
9. **A problem that asks the caller to wait carries the wait in `retryAfterSeconds`.**
   `RetryAfterProblem.Create(httpContext, wait, problem)` writes each such problem (a rekey too soon, a root key change
   too soon, an unavailable root key): it sets `Retry-After` and the problem's `retryAfterSeconds` member to the wait in
   whole seconds, rounded up and at least 1. The rekey-too-soon detail is "The last rotation of this master key has not
   reached every replica yet."; the wait is in the member, not in the prose.
10. **`ManagementProblemMembers` names the extension members.** The public constants `retryAfterSeconds`, `ownerType`,
    `ownerId`, `provider`, `keyReference`, `settingsScope`, `documentError` and `accessIdentity` are the names of the
    extension members the management API writes, and every writer and test uses them. `ManagementProblem` in
    `@ncode/api` mirrors the shape, and `apiErrorMessage` in `@ncode/store` appends " Try again in N seconds." when the
    problem carries a positive whole `retryAfterSeconds`: in seconds below a minute, otherwise in whole minutes or hours
    rounded up, and singular for one.
11. **Tests read the declarations from the routes.** `EndpointProblemMetadata` in
    `tests/NCode.Identity.OpenId.Management.TestSupport` reads a handler's declarations. Each handler's
    `Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers` theory pins the exact problems of its routes.
    `ManagementRouteProblemDeclarationTests` walks every `/api` route and asserts the group's `409` and `503` and, for
    every route except the four authenticated-caller routes
    ([ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md)),
    `401` and `403`. `ManagementRequestBindingTests` asserts that a malformed body answers `400`, a wrong content type
    `415`, a wrong method `405` and an unknown path under the prefix `404`, each as `application/problem+json`.

## Options considered

- **Mapping store exceptions to `404` or `409`.** A duplicate, a missing row and a store failure are one exception, so a
  failure reads as a conflict and the route hides a fault that a `500` shows. The rows the route read answer the same
  question without a catch.
- **Declaring `409` and `503` per route.** The filters answer for state read through shared services, whose reach grows
  without a route changing, so a per-route list drifts silently. One declaration on the route group stays true for every
  route.
- **A bare `404`.** The status declares no body, and a declared `404` problem describes a body the route does not send.
- **A catch in each handler, or a retry in the server.** Every handler, including a package's, would repeat the catch,
  and a handler that forgets it answers `500`. The write was computed from a stale read, so the caller decides whether
  it still applies.
- **An endpoint convention, an endpoint filter or a `MatcherPolicy` alone.** The router's `404`, `405` and `415` have no
  route endpoint to wrap, a filter never runs for a body that fails to read, and a policy sees only a request that
  reaches routing with candidates, so each misses some answers.
- **`AddProblemDetails` and `UseStatusCodePages` in the host.** Each host would have to opt in, so the answer differs by
  host, while the library answers the same in every host.
- **The middleware catching `BadHttpRequestException`.** A host's exception handler sits inside the outermost
  middleware and renders the exception first, so the middleware never sees it.
- **A base-query wrapper that reads the `Retry-After` header.** Only the console gains the wait, and any other client
  that cannot read headers still lacks it.
- **The wait in the detail prose.** It is not machine-readable, and every change of wording breaks the clients that
  parse it.
- **One middleware, a convention for thrown failures and per-route declarations.** Chosen: it answers the same in every
  host and keeps each declaration true to its route.

## Consequences

- A client of any host that registers the management library reads a problem document for every error under the
  management prefix, whether a handler, the binding or the router produced it.
- The management prefix belongs to the management API and is matched at the application's root, where
  `MapEndpointGroups` maps the root group; a host maps its own routes outside it.
- A store failure answers `500`, never a conflict or a missing resource, and a lost race answers `412`. Work that a
  write starts after its own save, such as the key seed of a tenant, is answered `412` as well when it loses a race.
- A client reads from the OpenAPI document every problem a route answers, and a route that answers a problem it does not
  declare fails its handler's theory and the host walk.
- Any client reads the wait from the body, whether or not its fetch layer exposes headers. A new problem that asks the
  caller to wait uses `RetryAfterProblem.Create`, so the header and the member agree, and a member name is a constant of
  `ManagementProblemMembers`, not a string literal.

## References

- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md):
  a lost race is a `StoreConcurrencyException`.
- [ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md): a revocation retries a lost race.
- [ADR-1245](1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md):
  the route group's `409`.
- [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md): the route group's `503`.
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md):
  the management validators that answer a stale `If-Match` with `412`.
- [ADR-1481](1481-the-composition-layer-serves-the-api-reference-and-each-management-problem-says-when-it-occurs.md):
  the problems a route declares say when each occurs.
- [project-template ADR-0125](project-template/0125-a-write-that-loses-a-race-is-answered-412.md): a write that loses a
  race is answered `412`.
- [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) (problem details), RFC 9110 §10.2.3 (`Retry-After`).
- Code: `NCode.Identity.OpenId.Management.Endpoints.StoreConcurrencyEndpointFilter`, `ManagementRootGroup`,
  `ManagementProblemResponseMiddleware`, `RetryAfterProblem`, `frontend/packages/store/src/apiError.ts`.
- Issues [#261](https://github.com/NCodeGroup/identity/issues/261),
  [#571](https://github.com/NCodeGroup/identity/issues/571),
  [#700](https://github.com/NCodeGroup/identity/issues/700),
  [#701](https://github.com/NCodeGroup/identity/issues/701),
  [#708](https://github.com/NCodeGroup/identity/issues/708)
