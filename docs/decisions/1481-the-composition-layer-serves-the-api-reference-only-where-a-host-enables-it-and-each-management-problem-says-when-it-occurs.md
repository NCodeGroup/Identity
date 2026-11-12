# 1481. The composition layer serves the API reference only where a host enables it, and each management problem says when it occurs

- **Status:** Accepted
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

The OpenID server publishes an OpenAPI document and renders it with a UI (Scalar), so an operator can explore the
management API, authenticate against it and read, for every route, when each problem occurs. The forces:

- **The document describes the server's own endpoints.** Its tags, security schemes and request and response shapes are
  a property of the server, not of each host. A host that re-declares them drifts.
- **A host still owns its pipeline.** It decides what is mapped and which middleware runs, and the renderer is a
  specific package choice.
- **The document maps the management API for an anonymous visitor.** Served on the issuer's origin, it lists every
  management route and scope, and the reference page is an interactive client beside the sign-in pages.
- **Publishing a description is the operator's choice.** A public developer-facing deployment wants the reference; a
  deployment that does not want it must get neither the document nor the page from the defaults.
- **The environment is decided in one place.** [ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md)
  keeps the environment decision in `AddProductionSafety`, and a convenience is inert unless configured.
- **The page is a page the server writes.** It falls under the strict Content-Security-Policy of
  [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md).
- **Scalar's defaults call its own services.** Its telemetry, AI agent, MCP integration and CDN fonts reach a third
  party from the visitor's browser.
- **The management endpoints need a bearer token.** The natural way to get one is the `client_credentials` grant,
  including the cold-start bootstrap administrator of
  [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md).
  Where the operator enables the page, the operator has to run that login from it in production as well as
  development.
- **A pre-filled secret is a served secret.** A renderer pre-fills credentials from configuration for a one-click
  login, which bakes them into the OpenAPI and Scalar configuration where anyone who reaches the reference page reads
  them. That violates the rule of [ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md) that no secret material is on the public
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
3. **The `ApiReference` section turns each surface on, and both are off by default.**
   `IdentityServerApiReferenceOptions` binds the top-level `ApiReference` section with `DocumentEnabled` (the OpenAPI
   document at `/openapi/v1.json`) and `PageEnabled` (the Scalar page at `/scalar/v1`), both `false` in every
   environment. The Development configuration of the reference host sets both to `true`, and the Helm chart exposes
   them as `apiReference.documentEnabled` and `apiReference.pageEnabled`. The page renders the document, so
   `IdentityServerApiReferenceOptionsValidator` fails `PageEnabled` without `DocumentEnabled` when the host starts, and
   the chart refuses to render it. `MapIdentityServerApiReference()` takes no callback and reads
   `IOptions<IdentityServerApiReferenceOptions>`; a host that decides in code calls
   `services.Configure<IdentityServerApiReferenceOptions>(...)`. The library holds no environment logic.
4. **Enabled means anonymous.** There is no token-gated document mode. The document describes the product: it is the
   same for every deployment of a version and derives from the open-source code, so a management-token gate hides
   nothing the product does not publish, and the Scalar page's browser fetch of the document carries no token and would
   break. Turning the document off removes the exposure.
5. **The page is written under a strict policy.** It goes through `SetHtmlPageSecurity` with a fresh nonce per
   response: `script-src 'nonce-N' 'strict-dynamic'`, `style-src 'nonce-N'`, `img-src 'self' data:`,
   `connect-src 'self'`, the strict defaults `base-uri 'none'`, `form-action 'none'` and `frame-ancestors 'none'`, and
   `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: same-origin` and `no-store`. Scalar emits the nonce on its
   scripts (`WithNonce(nonce)`), and its bundled stylesheet reads it from a `<meta property="csp-nonce" content="N" />`
   element the page adds to its head.
6. **The page loads nothing from a third party.** It disables Scalar's telemetry, AI agent, MCP integration and CDN
   fonts, and keeps authentication state in memory, never `EnablePersistentAuthentication`. `connect-src 'self'`
   enforces this in the browser.
7. **The client-credentials login exists only with the page.** The token URL, the audience-binding scope and the
   credentials location `Header` are part of the page's configuration and are built only when the page is mapped,
   because the bootstrap client registers `client_secret_basic`
   ([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md)). The bootstrap client id and secret are
   pre-filled only in Development. Elsewhere the operator types them into the Authorize dialog, so the served
   configuration holds no secret. The document's security scheme describes the API and is present whenever the
   document is served.
8. **Form bodies are shaped in the document, not the renderer.** A form-urlencoded field that must be pre-filled is
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

The rule: the document is a property of the server; the host serves it and its page only where the operator enables
them; no secret is pre-filled into a served document outside Development; and a client reads from the document when each
problem occurs.

## Options considered

- **OpenAPI and Scalar wiring in each host's `Program.cs`.** The document describes the server, so hosts drift and each
  re-implements the security-scheme and login wiring. Rejected.
- **Scalar configuration split into the hosts, with the composition layer renderer-agnostic.** It splits one concern
  across two places and leaves every host to wire the login correctly: credentials location, token URL, scope. One
  turnkey extension is simpler and harder to get wrong. Rejected.
- **Pre-filling the secret in every environment.** It embeds a secret in the served OpenAPI and Scalar configuration
  ([ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md)). The login works in production anyway, because the operator types the
  secret once, so the convenience does not justify the exposure. Rejected.
- **Gating the whole login on Development.** It breaks the production bootstrap path, which is the purpose of the
  bootstrap administrator. Only the pre-fill is environment-gated. Rejected.
- **A `<response>` for every status on every route.** The shared sentences repeat on every route, and a change to one
  meaning misses routes. Rejected.
- **Response descriptions on endpoint metadata.** ApiExplorer drops them, so they never reach the document. Rejected.
- **A management-token gate on the document.** The document is the same for every deployment of a version and derives
  from the open-source code, so a gate hides nothing the product does not publish, and the Scalar page's browser fetch
  of the document carries no token and would break. Rejected.
- **A tenant setting that enables the document and the page.** The document and the page describe the server, not a
  tenant, and are served at host-level routes (`/openapi/v1.json`, `/scalar/v1`) outside any tenant's route, so their
  exposure is a host setting. Rejected.
- **A host-name restriction switch.** An operator who wants the reference on an internal host only routes `/openapi/`
  and `/scalar/` there at the ingress, which already routes by host and path; a second, host-level copy of that rule
  would drift from it. Rejected.
- **An environment-based library default.** [ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md)
  makes `AddProductionSafety` the one place that decides the environment, and a convenience is inert unless
  configured. Rejected.
- **Refusing an enabled reference in Production.** Publishing an API description is the operator's choice, not a
  development convenience, so `AddProductionSafety` stays out of it. Rejected.
- **Chosen: the composition layer serves the reference where configuration enables it, routes describe their own
  answers, and one operation transformer writes the shared ones.** One place per concern keeps every route true to one
  sentence.

## Consequences

- `NCode.Identity.Server` depends on `Scalar.AspNetCore`; a host gets the reference UI by calling one extension and
  turns each surface on through the `ApiReference` configuration switches.
- A deployment with the defaults serves neither the document nor the page, so an anonymous visitor gets no map of the
  management API and no interactive client on the issuer's origin.
- The security-scheme, example and form-shaping knowledge lives once, next to the endpoints it describes, and a new
  renderer is a change in the map extension alone.
- Where the operator enables the page in Production, the operator bootstraps from it by entering the bootstrap
  credentials in the Authorize dialog; nothing secret is served.
- A client reads from the OpenAPI document when each problem of a route occurs, in the route's own words for its own
  answers and in one shared sentence for the rest.
- A route that declares a status its handler does not describe fails its handler's theory until the handler's XML says
  when it answers it.
- A change to a shared meaning is made once, in the operation transformer.
- A project that maps management routes calls `AddOpenApi`; without it, its handlers' descriptions never reach the
  document and its theory fails.
- The Publish artifacts workflow publishes the same document as an OCI artifact whatever these switches say, so client
  generators read it without a deployment serving it
  ([ADR-1505](1505-every-merge-to-main-publishes-the-host-image-the-console-image-the-chart-and-the-openapi-document-at-one-prerelease-version.md)).

## References

- [ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md): no secret material on the surface.
- [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md):
  management authentication and the bootstrap administrator.
- [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md): the bootstrap client registers
  `client_secret_basic`.
- [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md): a route
  declares every problem it answers.
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md):
  the composition layer wires implementations.
- `src/NCode.Identity.Server/IdentityServerApiReferenceEndpointRouteBuilderExtensions.cs`,
  `src/NCode.Identity.Server/OpenApi/` (document transformers), `src/NCode.Identity.Server/IdentityServerBuilder.cs`.
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md):
  the page's Content-Security-Policy.
- [ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md):
  environment decisions stay in `AddProductionSafety`.
- [#706](https://github.com/NCodeGroup/identity/issues/706)
- [#779](https://github.com/NCodeGroup/identity/issues/779)
