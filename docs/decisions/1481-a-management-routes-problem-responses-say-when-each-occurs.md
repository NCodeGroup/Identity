# 1481. A management route's problem responses say when each occurs

- **Status:** Accepted (amends [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md))
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

A management route declares every problem it answers
([ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md)), and the
OpenAPI document lists those declared answers. The forces:

- **A bare reason phrase says nothing.** A declared `409` whose description is "Conflict" tells a client that the route
  can fail, never when. A client author reads the description to decide how to handle the answer, so each one has to say
  when the route gives it.
- **Most of the meaning is shared.** About 140 routes share the same meanings for `401`, `403`, `412`, `415`, `409`
  and `503`: the caller carries no valid access token, the caller lacks the authority the request needs, an `If-Match`
  token does not match, the body is not JSON, the stored settings document cannot be honored, the root key is
  unavailable. A sentence written on each route repeats 140 times and drifts.
- **A route's own answers differ.** Only the route knows which resource a `404` names, which conflicts a `409` lists,
  what its `400` validates and when it answers `422` or `502`.
- **The XML documentation reaches the document only through the project that calls `AddOpenApi`.** The source generator
  of `Microsoft.AspNetCore.OpenApi` reads the `///` of a handler method, including its `<response>` lines, in each
  project that references the package and calls `AddOpenApi`. It sets a description only for a status the route
  declares, and it reads one `<response>` per status code.
- **ApiExplorer drops a description set on endpoint metadata.** It reads a minimal API's `IProducesResponseTypeMetadata`
  without a description, so a description cannot travel on the route's declaration.

## Decision

**A route describes the problems it answers itself, the route group describes the problems every route shares once, and
tests pin both.**

1. **A route's `<response>` describes its own answers.** A handler method's `<response code="N">` says when the route
   answers `N` for a reason of its own: the `400` its handler validates, the `404` it names, its own `409` conflicts,
   `422`, `502` and any refinement of `403` or `412` beyond the shared reason. A route never repeats a shared sentence.
2. **The root group's endpoint-scoped operation transformer writes the shared answers.** An operation transformer
   registered on `ManagementRootGroup` gives `401`, `403`, `412` and `415` a default description when the route says
   nothing, appends the group's sentence to every `409` and `503`, and appends the binding sentence to the `400` of an
   operation that binds a body or a parameter that is not a string.
3. **A package that maps management routes calls `AddOpenApi`.** Each package that maps management routes references
   `Microsoft.AspNetCore.OpenApi` and calls `AddOpenApi`, so the XML documentation of its handlers reaches the document.
4. **A method carries one `<response>` per status code.** The generator reads one per code, so a method never repeats a
   code.
5. **Tests pin the descriptions.** Each handler's `Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers` theory
   asserts that every status a route declares has a description in the handler assembly's XML documentation file or in
   the shared set, and `ManagementOpenApiDocumentTests` asserts that no `/api` error response in the host's OpenAPI
   document keeps its bare reason phrase and that the shared and route-owned sentences read as written.

## Options considered

- **A `<response>` for every status on every route.** Each route would carry the shared sentences of `401`, `403`,
  `412`, `415`, `409` and `503`, so the same text repeats on about 140 routes and a change to one meaning misses
  routes. A single transformer keeps every route true to one sentence.
- **Response descriptions on endpoint metadata.** ApiExplorer reads a minimal API's `IProducesResponseTypeMetadata`
  without its description, so the description never reaches the document.

## Consequences

- A client reads from the OpenAPI document when each problem of a route occurs, in the route's own words for its own
  answers and in one shared sentence for the rest.
- A route that declares a status its handler does not describe fails its handler's theory until the handler's XML says
  when it answers it.
- A change to a shared meaning is made once, in the operation transformer.
- A project that maps management routes calls `AddOpenApi`; without it, its handlers' descriptions never reach the
  document and its theory fails.

## References

- [ADR-1046](1046-api-reference-openapi-document-and-scalar.md): the OpenAPI document and its owner.
- [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md): a route
  declares every problem it answers.
- [#706](https://github.com/NCodeGroup/Identity/issues/706)
