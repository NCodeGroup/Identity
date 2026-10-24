# 193. The interactive surface is served under the tenant route, and its session is scoped to the tenant's path

- **Status:** Accepted (amended by
  [ADR-0254](0254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md):
  the bundle's hashed files are served immutable by a group that resolves no tenant)
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

The protocol endpoints are served under the tenant route
([ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md)). The interactive surface (login, consent,
logout, and the account pages) was still mounted at absolute `/account/*` routes. It had a fixed cookie `LoginPath`,
one session cookie for every tenant, and its bundle's static assets at the root.

A static tenant or a tenant selected by host does not notice. Under `DynamicByPath`, though:

- The surface's environment filter has no `{tenantId}` route value to resolve, so every interactive flow fails.
- The authorization endpoint's login challenge sends the browser to `/account/login`, which is outside the tenant.
- Tenants that share an origin would share one session cookie. A user signed in to one tenant would carry that session
  to every other tenant on the origin.

## Decision

**The interactive surface is served beneath the tenant route, every path it issues is tenant-relative, and its session
cookie is scoped to the tenant's path.**

1. **The group prefix is the tenant route.** `InteractiveAuthenticationEndpointGroup` takes its prefix from
   `ITenantResolver.GetTenantRoute()`, the same prefix as the protocol endpoints. Under `StaticSingle` with no tenant
   path the surface stays at `/account/*`; under `DynamicByPath` it is served at `/{tenantId}/account/*`.
2. **Paths come from the request.** `InteractiveSurfacePaths` computes the tenant's path as the request's `PathBase`
   followed by the tenant route, bound from the request's values for that route's own parameters. It uses the binding
   of [ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md) decision 3. Route values outlive the
   ambient tenant scope, so the path is still available when the cookie handler refreshes the session as the response
   starts. Every redirect the handler issues goes through it, as do the consent challenge and the cookie scheme's login
   challenge (`OnRedirectToLogin`), so a browser never leaves its tenant.
3. **Form actions are page-relative.** The server-rendered login and consent forms post to `login` and `consent`, so a
   form posts back to the tenant that served it without the renderer knowing the tenant's path.
4. **The session cookie is scoped by path, not by name.** `InteractiveSessionCookieManager` wraps the chunking cookie
   manager and writes and deletes every cookie with `Path` set to the tenant's path (`/` at the root). The browser then
   returns a tenant's session only to that tenant's endpoints. RFC 6265 §5.1.4 path-matching treats `/acme` as matching
   `/acme/...` but not `/acme2`. A per-tenant cookie name was rejected: `CookieBuilder.Name` is resolved without the
   request, and a name alone still sends every tenant's cookie to every tenant.
5. **The bundle is served beneath the tenant route and finds itself.**
   - The surface's own endpoints serve the `apps/auth` bundle's files from a catch-all
     `GET|HEAD {tenant}/account/{**asset}` route, ordered after the literal page and API routes. A
     `PhysicalFileProvider` rooted at the bundle resolves the file, so a path that navigates above the root or names a
     hidden file is answered `404`, as is a file type with no known content type. Files carry `ETag` and
     `Last-Modified` validators and support range requests. This replaces the root static-file middleware
     (`UseInteractiveAuthentication`), which could serve the bundle at one path only.
   - Every page the surface serves sets the document's base to the surface beneath the request's tenant:
     `<base href="{tenant path}/account/">` is written first inside `<head>`. That covers the login and consent pages,
     the pages the bundle draws entirely, and the authorization error page served at the authorize endpoint. The
     pages without the bundle carry the base too, so their relative form actions resolve the same way.
   - The bundle is built with a relative base (`./`), so its asset URLs resolve against that `<base href>`. It resolves
     its API and page paths, which are relative to the surface, against `document.baseURI`. It never assumes where it
     is mounted. `/acme/account/` (with a trailing slash) is the account page's URL beneath `acme`, which routing
     matches as `/acme/account`.

## Options considered

- **Keep the surface at the root and resolve the tenant from a query parameter or cookie.** Rejected: it gives the
  interactive surface a second tenant-resolution mechanism that the protocol endpoints do not share. It also leaves
  every tenant on an origin with one cookie.
- **Name the cookie per tenant.** Rejected for the reasons in decision 4.
- **Compute paths from the ambient tenant instead of the request.** Rejected: the cookie handler writes the refreshed
  session cookie when the response starts, after the tenant scope may have ended.

## Consequences

- Interactive flows work under `DynamicByPath`: the login challenge, the login and consent pages, logout, and the
  account pages are all reached and redirected beneath `/{tenantId}`.
- Tenants that share an origin keep separate sessions. Path scoping separates the sessions but is **not** a security
  boundary within an origin: script on one path can read another path's pages. A deployment whose tenants must be
  isolated from each other selects tenants by host.
- `StaticSingle` with no tenant path behaves as before, except that the session cookie and the form actions are now
  relative to the tenant.
- The `apps/auth` bundle works beneath any tenant path without being rebuilt, and a host no longer adds middleware to
  serve it: `UseInteractiveAuthentication()` is removed. `defineBackendServedSurfaceConfig` no longer takes a `base`.
- Requesting a bundle file under the path strategy resolves the tenant first, so an unknown tenant's files are `404`.

## References

- [ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md) — the protocol endpoints are served under
  the tenant route.
- [RFC 6265](https://www.rfc-editor.org/rfc/rfc6265) §5.1.4 — cookie path-match.
