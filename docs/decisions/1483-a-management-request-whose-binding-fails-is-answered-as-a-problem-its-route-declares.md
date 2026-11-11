# 1483. A management request whose binding fails is answered as a problem its route declares

- **Status:** Accepted (amends [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md))
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

Every management error is a problem document, and every management route declares each problem it answers
([ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md)). That holds for
what a handler returns. It does not hold for a request that fails before a handler runs. The forces:

- **A JSON body is read before endpoint filters run.** A body that is not JSON, or is not valid JSON for the operation,
  fails while the request delegate binds its parameters, so no endpoint filter sees the request and the answer is a bare
  `400` with no body.
- **A thrown binding failure meets the host's exception handling.** With `ThrowOnBadRequest`, which is on in the
  Development environment, binding throws `BadHttpRequestException` instead of setting a status. The route's request
  delegate is left, and an exception handler of the host, such as the developer exception page, renders the failure.
- **The router answers some errors with no route endpoint at all.** A body with the wrong content type is answered
  `415`, a wrong method `405` and an unknown path `404`, each with no body and no endpoint that a convention could wrap.
- **A route declares only what it answers.** A route that binds a JSON body or a parameter that can fail to parse
  answers `400`, and a route that binds a JSON body answers `415`, so each has to declare them.

## Decision

**One middleware the management library owns answers every bodyless management error as a problem, and each route
declares the binding problems it can answer.**

1. **`ManagementProblemResponseMiddleware` answers every bodyless error under the management prefix.** The management
   library adds the middleware through an `IStartupFilter` as the outermost middleware, so no host changes. It answers
   every response under the management prefix (`ManagementRootGroup.RoutePrefix`, the one source of the prefix) that
   ends with a status of `400` to `599` and no body (not started, no `Content-Length`, no `Content-Type`: the rule the
   status-code pages middleware uses) as `TypedResults.Problem(statusCode: …)`. A route's answer, a failed binding's
   `400` and the router's `404`, `405` and `415` are therefore problem documents. The middleware, not
   `ManagementEnvironmentEndpointFilter`, converts a bodiless error to a problem.
2. **The root group keeps `BindingExceptionEndpointConvention`.** A thrown binding failure leaves the route's request
   delegate and meets a host's exception handler, such as the developer exception page, inside the outermost
   middleware, which renders it first. The convention answers the failure with its status and no body, so the middleware
   writes the problem.
3. **The environment filter returns an `HttpResultException`'s result and stops a request whose binding failed.** A
   binding failure is answered before authentication.
4. **A route declares the binding problems it can answer.** A route whose handler has a `[FromBody]` parameter declares
   `ProducesProblem(StatusCodes.Status415UnsupportedMediaType)`. A route whose handler has a `[FromBody]` parameter, a
   `[FromQuery]`, `[FromHeader]` or `[FromRoute]` parameter whose type is not `string`, or a required `[FromQuery]` or
   `[FromHeader]` `string` parameter, declares `ProducesProblem(StatusCodes.Status400BadRequest)` unless it already
   declares `400`. A route declares `400` once.
5. **Tests pin the answers and the declarations.** Each handler's
   `Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers` theory asserts the declared statuses,
   `ManagementRouteProblemDeclarationTests` walks every route in the host, and `ManagementRequestBindingTests` asserts
   that a malformed body answers `400`, a wrong content type `415`, a wrong method `405` and an unknown path under the
   management prefix `404`, each as `application/problem+json`.

## Options considered

- **An endpoint convention alone.** The router's `404`, `405` and `415` have no route endpoint to wrap, so a convention
  never sees them.
- **An endpoint filter.** A filter never runs for a body that fails to read, because the body binds before filters run.
- **A `MatcherPolicy`.** A policy sees only a request that reaches routing with candidates, so it misses the unmatched
  `404`.
- **`AddProblemDetails` and `UseStatusCodePages` in the host.** Each host would have to opt in, so the answer differs by
  host, while the library answers the same in every host.
- **The middleware catching `BadHttpRequestException`.** A host's exception handler sits inside the outermost
  middleware and renders the exception first, so the middleware never sees it.
- **The group declaring from parameters.** A route declares what it answers, so each mapping declares its own binding
  problems and the group does not infer them.

## Consequences

- A client of any host that registers the management library reads a problem document for every error under the
  management prefix, whether a handler, the binding or the router produced it.
- The management prefix belongs to the management API. An endpoint a host maps under it is answered the same way, so a
  client reads one error shape under the prefix, and a host maps its own routes outside it.
- A host adds nothing to get the problem documents, and a host that renders its own error pages still shows a binding
  failure as a problem under the management prefix.
- The management prefix is matched at the application's root, where `MapEndpointGroups` maps the root group.
- A route that binds a JSON body or a parameter that can fail to parse declares `400`, and one that binds a JSON body
  declares `415`, so a missing declaration fails its handler's theory and the host walk.

## References

- [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md): a route
  declares every problem it answers.
- [ADR-1481](1481-a-management-routes-problem-responses-say-when-each-occurs.md): the problems a route declares say when
  each occurs.
- [#708](https://github.com/NCodeGroup/identity/issues/708)
