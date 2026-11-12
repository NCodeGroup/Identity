# 1481. The composition layer serves the API reference, and each management problem says when it occurs

- **Status:** Accepted
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The OpenID server publishes an OpenAPI document and renders it with a UI (Scalar), so an operator can explore the
management API, authenticate against it and read, for every route, when each problem occurs. The forces:

- **The document describes the server's own endpoints.** Its tags, security schemes and request and response shapes are
  a property of the server, not of each host. A host that re-declares them drifts.
- **A host still owns its pipeline.** It decides what is mapped and which middleware runs, and the renderer is a
  specific package choice.
- **The management endpoints need a bearer token.** The natural way to get one is the `client_credentials` grant,
  including the cold-start bootstrap administrator of
  [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md).
  An operator has to run that login from the UI in production as well as development.
- **A pre-filled secret is a served secret.** A renderer pre-fills credentials from configuration for a one-click
  login, which bakes them into the OpenAPI and Scalar configuration where anyone who reaches the reference page reads
  them. That violates the rule of [ADR-1011](1011-secret-management-api.md) that no secret material is on the public
  surface.
- **A renderer builds a form from schema properties.** Scalar renders a `application/x-www-form-urlencoded` body from
  the schema: optional properties render unchecked and are omitted from the request, and a named `example` is not
  applied to the form fields.
- **A bare reason phrase says nothing.** A declared `409` whose description is "Conflict" tells a client that the route
  can fail, never when. A client author reads the description to decide how to handle the answer.
- **Most of the meaning is shared.** Every management route shares the meanings of `401`, `403`, `412`, `415`, `409`
  and `503`: the caller carries no valid access token, the caller lacks the authority the request needs, an `If-Match`
  token does not match, the body is not JSON, the stored settings document cannot be honored, the root key is
  unavailable. A sentence written on each route repeats on every one and drifts.
- **A route's own answers differ.** Only the route knows which resource a `404` names, which conflicts a `409` lists,
  what its `400` validates and when it answers `422` or `502`.
- **The XML documentation reaches the document through one generator.** The source generator of
  `Microsoft.AspNetCore.OpenApi` reads the `///` of a handler method, its `<response>` lines included, in each project
  that references the package and calls `AddOpenApi`. It sets a description only for a status the route declares, and
  it reads one `<response>` per status code.
- **ApiExplorer drops a description set on endpoint metadata.** It reads a minimal API's `IProducesResponseTypeMetadata`
  without a description, so a description cannot travel on the route's declaration.

## Decision

**The composition layer owns the API reference end to end, a route describes the problems it answers itself, the route
group describes the shared problems once, and tests pin both.**

### The API reference

1. **`NCode.Identity.Server` owns the document contributions.** `AddIdentityServer()` registers renderer-agnostic
   `AddOpenApi` document transformers: `TagGroupsDocumentTransformer` with `ManagementOpenApiTags`,
   `SecuritySchemeDocumentTransformer`, `TokenEndpointExamplesDocumentTransformer` and the Development-only
   `BootstrapTokenExampleDocumentTransformer`. A host never re-declares them.
2. **The security scheme is a bearer scheme** with the OAuth2 `client_credentials` flow against the token endpoint,
   which the management operations reference.
3. **`MapIdentityServerApiReference()` maps the document and the UI.** It maps the OpenAPI document and the Scalar UI
   (`Scalar.AspNetCore`) on the independent `IdentityServerApiReferenceOptions` toggles `MapOpenApiDocument` and
   `MapApiReferenceUi`, so a host can serve either, both or neither.
4. **The client-credentials login is wired in every environment.** The token URL, the audience-binding scope and the
   credentials location `Header` are set everywhere, because the bootstrap client registers `client_secret_basic`
   ([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method.md)). The bootstrap client id and secret are
   pre-filled only in Development. Elsewhere the operator types them into the Authorize dialog, so the served
   configuration holds no secret.
5. **Form bodies are shaped in the document, not the renderer.** A form-urlencoded field that must be pre-filled is
   marked `required` with a schema `Default`; the document never relies on a renderer applying a named example.

### The problem descriptions

1. **A route's `<response>` describes its own answers.** A handler method's `<response code="N">` says when the route
   answers `N` for a reason of its own: the `400` its handler validates, the `404` it names, its own `409`s, `422`,
   `502` and any refinement of `403` or `412` beyond the shared reason. A route never repeats a shared sentence.
2. **The root group's operation transformer writes the shared answers.** `ManagementProblemResponseOperationTransformer`,
   registered on `ManagementRootGroup`, gives `401`, `403`, `412` and `415` a default description when the route says
   nothing, appends the group's sentence to every `409` and `503`, and appends the binding sentence to the `400` of an
   operation that binds a body or a parameter that is not a string.
3. **A package that maps management routes calls `AddOpenApi`.** Each such package references
   `Microsoft.AspNetCore.OpenApi` and calls `AddOpenApi`, so the XML documentation of its handlers reaches the document.
4. **A method carries one `<response>` per status code.** The generator reads one per code, so a method never repeats a
   code.
5. **Tests pin the descriptions.** `Map_Always_DeclaresAndDescribesEveryProblemARouteAnswers` asserts that every status
   a route declares has a description in the handler assembly's XML documentation or in the shared set, and
   `ManagementOpenApiDocumentTests` asserts that no `/api` error response keeps its bare reason phrase and that the
   shared and route-owned sentences read as written.

The rule: the document is a property of the server; the host decides only whether, and in which form, to expose it; no
secret is pre-filled into a served document outside Development; and a client reads from the document when each problem
occurs.

## Options considered

- **OpenAPI and Scalar wiring in each host's `Program.cs`.** The document describes the server, so hosts drift and each
  re-implements the security-scheme and login wiring. Rejected.
- **Scalar configuration split into the hosts, with the composition layer renderer-agnostic.** It splits one concern
  across two places and leaves every host to wire the login correctly: credentials location, token URL, scope. One
  turnkey extension is simpler and harder to get wrong. Rejected.
- **Pre-filling the secret in every environment.** It embeds a secret in the served OpenAPI and Scalar configuration
  ([ADR-1011](1011-secret-management-api.md)). The login works in production anyway, because the operator types the
  secret once, so the convenience does not justify the exposure. Rejected.
- **Gating the whole login on Development.** It breaks the production bootstrap path, which is the purpose of the
  bootstrap administrator. Only the pre-fill is environment-gated. Rejected.
- **A `<response>` for every status on every route.** The shared sentences repeat on every route, and a change to one
  meaning misses routes. Rejected.
- **Response descriptions on endpoint metadata.** ApiExplorer drops them, so they never reach the document. Rejected.
- **Chosen: the composition layer serves the reference, routes describe their own answers, and one operation
  transformer writes the shared ones.** One place per concern keeps every route true to one sentence.

## Consequences

- `NCode.Identity.Server` depends on `Scalar.AspNetCore`; a host gets the reference UI by calling one extension and opts
  out per surface through the toggles.
- The security-scheme, example and form-shaping knowledge lives once, next to the endpoints it describes, and a new
  renderer is a change in the map extension alone.
- Production operators bootstrap from the UI by entering the bootstrap credentials in the Authorize dialog; nothing
  secret is served.
- A client reads from the OpenAPI document when each problem of a route occurs, in the route's own words for its own
  answers and in one shared sentence for the rest.
- A route that declares a status its handler does not describe fails its handler's theory until the handler's XML says
  when it answers it.
- A change to a shared meaning is made once, in the operation transformer.
- A project that maps management routes calls `AddOpenApi`; without it, its handlers' descriptions never reach the
  document and its theory fails.

## References

- [ADR-1011](1011-secret-management-api.md): no secret material on the surface.
- [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md):
  management authentication and the bootstrap administrator.
- [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method.md): the bootstrap client registers
  `client_secret_basic`.
- [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md): a route
  declares every problem it answers.
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md):
  the composition layer wires implementations.
- `src/NCode.Identity.Server/IdentityServerApiReferenceEndpointRouteBuilderExtensions.cs`,
  `src/NCode.Identity.Server/OpenApi/` (document transformers), `src/NCode.Identity.Server/IdentityServerBuilder.cs`.
- [#706](https://github.com/NCodeGroup/identity/issues/706)
