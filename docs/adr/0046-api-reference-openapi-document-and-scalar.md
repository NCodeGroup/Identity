# 46. The API reference (OpenAPI document + Scalar) is owned by the composition layer and turnkey for every environment

- **Status:** Accepted (extends the GlobalAdmin bootstrap of
  [ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md); reinforces
  [ADR-0011](0011-secret-management-api.md) — no secret material on the surface —, the opaque-id rule of
  [ADR-0014](0014-server-generated-opaque-resource-ids.md), and the composition boundary of
  [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md))
- **Date:** 2026-10-04
- **Deciders:** NCode Group

## Context

The OpenID server publishes an OpenAPI document and renders it with a UI (Scalar) so an operator can explore the API
and authenticate the management surface. Several forces pull on where that wiring lives and how it behaves:

- The document _describes this server's own endpoints_ — its tags, security schemes, and request/response shapes are a
  property of the server, not of each host. Leaving every host to re-declare them invites drift.
- A host still owns pipeline concerns (which middleware runs, what is mapped), and the renderer is a specific package
  choice.
- The management endpoints require a bearer token; the natural way to obtain one is the `client_credentials` grant,
  including the cold-start **bootstrap administrator** (ADR-0044). An operator must be able to run that login from the
  UI — **in production as well as development**, because production is exactly where the first tenant/server is
  bootstrapped.
- A renderer pre-fills credentials from configuration for a one-click login. Pre-filling a **secret** bakes it into the
  served OpenAPI/Scalar configuration, where anyone who can reach the reference page can read it — a direct violation of
  "never put secret material on the public surface" (ADR-0011).
- A renderer such as Scalar builds a `application/x-www-form-urlencoded` request body from the OpenAPI **schema
  properties**: optional properties render unchecked and are omitted from the request, and a named `example` is not
  auto-applied to the form fields. A form whose fields all default to empty is easy to submit with nothing filled in.

## Decision

The **composition layer** (`NCode.Identity.Server`) owns the API reference end to end:

1. **Renderer-agnostic document contributions live in the composition root**, registered as `AddOpenApi` document
   transformers by `AddIdentityServer()` (tag groups, security schemes, token examples, and the bootstrap
   example/pre-fill). A host never re-declares them.
2. **A turnkey app-side mapping** — `MapIdentityServerApiReference()` — maps the OpenAPI document and the Scalar UI, on
   independent toggles (`MapOpenApiDocument`, `MapApiReferenceUi`) so a host may expose the machine-readable spec
   without the UI, serve the UI against a document hosted elsewhere, or disable either.
3. **The client-credentials login is wired in every environment** (token URL, body credentials, audience-binding
   scope) so the production bootstrap path works from the UI. **Only the secret is pre-filled, and only in
   Development**; outside Development the operator supplies it in the Authorize dialog, so no secret is embedded in the
   served configuration.
4. **Form bodies are shaped in the document, not the renderer.** To make a renderer pre-check and pre-fill a
   form-urlencoded field, the transformer marks it `required` and sets its schema `Default`; it never relies on a named
   example being applied to form fields. Credentials go in the request **body** (`client_secret_post`), matching the
   token endpoint, so the renderer's credentials location is `Body`.

The rule: _the document is a property of the server (composition layer); the host only decides whether, and in which
form, to expose it; and a secret is never pre-filled into a served document outside Development._

## Options considered

- **Leave the OpenAPI/Scalar wiring in each host's `Program.cs`** (the starting point). Rejected: the document
  describes the server, so hosts drift, and each host re-implements the security-scheme and login wiring.
- **Keep the composition layer renderer-agnostic and put only Scalar config in the host.** Workable, but it splits one
  concern across two places and leaves every host to re-wire the login correctly (credentials location, token URL,
  scope). Once a Scalar dependency in the composition package is acceptable, a single turnkey extension is simpler and
  harder to get wrong.
- **Pre-fill the bootstrap secret in all environments for a one-click production login.** Rejected: it embeds a secret
  in the served OpenAPI/Scalar configuration (ADR-0011). The login still works in production — the operator types the
  secret once — so the convenience does not justify the exposure.
- **Gate the whole login on Development.** Rejected: it breaks the production bootstrap path, which is the whole point
  of the bootstrap administrator (ADR-0044). Only the secret pre-fill is environment-gated; the flow is not.

## Consequences

- `NCode.Identity.Server` takes a dependency on `Scalar.AspNetCore`; hosts get the reference UI by calling one
  extension and opt out per surface via the toggles.
- The security-scheme, example, and form-shaping knowledge lives once, next to the endpoints it describes.
- Production operators bootstrap from the UI by entering the bootstrap credentials in the Authorize dialog; nothing
  secret is served. After provisioning durable administrators, the bootstrap client can be disabled (ADR-0044).
- A new renderer (or none) is a change in one place — the map extension — not across every host.

## References

- [ADR-0011](0011-secret-management-api.md) — no secret material on the surface.
- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — server-generated opaque identifiers.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — composition wires implementations.
- [ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md) — management auth and the GlobalAdmin bootstrap.
- `src/NCode.Identity.Server/IdentityServerApiReferenceEndpointRouteBuilderExtensions.cs`,
  `src/NCode.Identity.Server/OpenApi/` (document transformers), `src/NCode.Identity.Server/IdentityServerBuilder.cs`.
