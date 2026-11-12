# 1193. The interactive surface uses the tenant route, host-only cookies and one shared single-page bundle helper

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)

## Context

The protocol endpoints are served under the tenant route
([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)). The
interactive surface (login, consent, logout and the account pages) is served under the same route, because under
`DynamicByPath` three things otherwise fail:

- The surface's environment filter has no `{tenantId}` route value to resolve, so every interactive flow fails.
- The authorization endpoint's login challenge would send the browser outside its tenant.
- Tenants that share an origin would share one session, so a user signed in to one tenant would carry that session to
  every other.

The surface also has cookies, and [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)
decision 4 gives a browser-facing cookie the `__Host-` prefix: `Secure`, `Path=/` and no `Domain`. A prefix of that kind
forbids scoping a cookie by path, so tenants that share an origin have to be told apart by cookie name.

The `apps/auth` bundle is built into content-hashed files. Four forces bear on how the host serves them:

- A hashed file never changes under its name, so it can be cached for a year, but validators alone give a browser no
  freshness lifetime and it revalidates on every use.
- A public static file needs no tenant. Resolving one for each request makes an unknown tenant's files answer `404` and
  adds work to a path that serves bytes.
- The .NET gate installs no Node and never builds the bundle, yet the host's split of the bundle's `index.html` around
  its mount point must match what the build writes.
- `apps/auth` and `apps/mgmt` are both Vite bundles whose host reads an index once per version and serves the `assets`
  directory immutable. Two copies of that code drift.

## Decision

**The interactive surface is served beneath the tenant route with tenant-relative paths, every cookie is host-only and
named for its tenant, and one general helper reads a single-page bundle and serves its assets.**

### The route and its paths

1. **The group prefix is the tenant route.** `InteractiveAuthenticationEndpointGroup` takes its prefix from
   `ITenantResolver.GetTenantRoute()`, the prefix the protocol endpoints use. Under `StaticSingle` with no tenant path
   the surface is at `/account/*`; under `DynamicByPath` it is at `/{tenantId}/account/*`.
2. **Paths come from the request.** `InteractiveSurfacePaths` computes the tenant path as the request's `PathBase`
   followed by the tenant route, bound from the request's route values by the binding of
   [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md) decision 3,
   not from the ambient tenant, because the cookie handler writes the refreshed session cookie when the response starts,
   after the tenant scope may have ended. Every redirect the surface issues, the consent challenge and the cookie
   scheme's `OnRedirectToLogin` use it, so a browser never leaves its tenant.
3. **Paths in a page are relative.** Server form actions are page-relative (`login`, `consent`). Every page the surface
   writes, the authorization error page included, writes `<base href="{tenant path}/account/">` first inside `<head>`.
   The bundle is built with base `./` and resolves its API and page paths against `document.baseURI`, so one build
   mounts under any tenant path and never assumes where it is mounted.

### Cookies

4. **Every cookie is host-only**: `Path=/`, no `Domain` and a `__Host-` name.
   - `__Host-NCode.Identity.Interactive`: the interactive session. `Secure` always, `HttpOnly`, `SameSite=Lax`.
   - `__Host-NCode.Identity.Antiforgery`: the antiforgery cookie token. `HttpOnly`, `SecurePolicy=SameAsRequest`, because
     `Always` makes the antiforgery services refuse every request that is not HTTPS, and the `__Host-` prefix already
     makes a browser refuse the cookie without `Secure`.
   - `__Host-XSRF-TOKEN`: the antiforgery request token that the pages echo back as a hidden field. Script-readable by
     design, `SameSite=Strict`.
   - `__Host-opbs`: the Session Management browser state. Script-readable, `Secure`, `SameSite=None`;
     `check_session_iframe` reads it from the same constant.
5. **The name separates tenants.** For a tenant served under a path, `InteractiveSessionCookieManager` appends `.` and
   `Base64Url(SHA-256(tenant path))` to the session cookie's name. A tenant at the root keeps the base name. Every read,
   write and delete maps the name the same way, and the chunks of a large session keep the tenant's name.
6. **Neither a cookie name nor a path is a security boundary within an origin.** The browser sends every tenant's
   session cookie to every tenant, and each tenant reads only its own. Tenants that must be isolated from each other are
   selected by host.

### The bundle

7. **`SinglePageBundle<TIndex>` and `SinglePageBundleDefaults` in `NCode.Identity.AspNetCore`** read a Vite bundle's
   index and serve its assets. The defaults name the `assets` directory and the `index.html` file. A bundle takes the
   absolute path of its root and a parser from the index's text to `TIndex`. Each surface registers one instance with
   its own index type and parser: `SinglePageBundle<InteractiveBundleShell>` here and
   `SinglePageBundle<ManagementConsolePage>` in the management console, each rooted at its configured path resolved
   against the content root.
8. **The parsed index is cached per version of the file**, identified by its last-write time and length. A request
   revalidates that metadata; no file watcher runs.
9. **Assets are served by their own group.** `InteractiveBundleAssetEndpointGroup` serves
   `GET|HEAD {tenant route}/account/assets/{**asset}` with the surface's prefix and no `OpenIdEnvironmentEndpointFilter`,
   so no tenant is resolved: any tenant segment, known or not, gets the same public files. It keeps the front-channel
   marker (`WithFrontChannel()`, [ADR-1388](1388-the-html-page-security-model-and-the-front-channel-marker-are-general-aspnet-core-helpers.md)).
10. **Only the `assets` directory is servable.** The bundle's file provider is rooted at `assets`, so a path that leaves
    it, a hidden file and a file of an unknown content type answer `404`, and `index.html` is never served.
11. **`ServeAsset` sends `Cache-Control: public, max-age=31536000, immutable`** with `ETag`, `Last-Modified` and range
    support. A `404` carries no cache lifetime. `@ncode/vite-config` pins `build.assetsDir` to `assets`
    ([ADR-1157](1157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md)).
12. **`[BuiltAuthBundleFact]` tests the real build.** `InteractiveBundleShellTests` runs the host's split against the
    real built `index.html` named by `NCODE_AUTH_BUNDLE_INDEX`. The Frontend CI workflow builds the bundle, sets the
    variable to the absolute path of `apps/auth/dist/index.html` and runs the fact; the Definition of Done reports it
    skipped.

## Options considered

- **A root surface that resolves the tenant from a query parameter or a cookie.** Rejected: it gives the interactive
  surface a second tenant-resolution mechanism that the protocol endpoints do not share, and it leaves every tenant on
  an origin with one cookie.
- **Path-scoped cookies.** Rejected: the `__Host-` prefix requires `Path=/`, and a path is not a boundary within an
  origin.
- **A `__Secure-` prefix with a path.** Rejected: it allows a `Domain` attribute and keeps the cookie readable by a
  sibling subdomain, which the `__Host-` prefix refuses.
- **A readable tenant suffix in the cookie name.** Rejected: the hash separates tenants without putting the tenant's
  path in every request's headers.
- **Paths computed from the ambient tenant.** Rejected: the cookie handler writes at response start, after the tenant
  scope may have ended.
- **Root-level asset URLs.** Rejected: a path at the root needs absolute URLs and cannot mount under a tenant.
- **Resolving the tenant for public assets.** Rejected: the files are the same for every tenant, and resolution makes an
  unknown tenant's files answer `404` and costs work on every request.
- **Static-file middleware.** Rejected: it serves the bundle at one path only and cannot take the tenant route.
- **A checked-in copy of the built `index.html`.** Rejected: the copy drifts from the build, so the test would assert
  against a file the product never serves.
- **A bundle reader copied into each surface.** Rejected: the copies drift. One general helper, tested once, serves both.
- **Parsing the index on every request.** Rejected: it repeats work for a file that changes once per build.
- **A file watcher.** Rejected: revalidating two metadata values per request is cheaper than a watcher's lifecycle and
  needs no teardown.
- **The chosen design** serves the surface under the tenant route with relative paths, names host-only cookies for their
  tenant and serves every bundle through one helper.

## Consequences

- Interactive flows work under `DynamicByPath`: the login challenge, the login and consent pages, logout and the account
  pages are reached and redirected beneath `/{tenantId}`.
- The `apps/auth` bundle works beneath any tenant path without a rebuild, and the host adds no static-file middleware
  to serve it.
- Tenants that share an origin keep separate sessions by name. Script on one tenant's path can still read another's
  pages, so a deployment whose tenants must be isolated selects them by host.
- A hashed file is cached for a year and revalidated by the validators once it expires; a browser sends no request for a
  fresh one.
- An asset request resolves no tenant, so it answers the same public files for every tenant segment.
- `assets` is a contract between the host and `@ncode/vite-config`: a bundle built with another directory has no file
  the host serves.
- One implementation, tested in `NCode.Identity.AspNetCore.Tests`, serves both bundles.
- The host's split of `index.html` is tested against the real build in the Frontend CI workflow, so the .NET gate needs
  no Node.

## References

- [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md): the
  protocol endpoints are served under the tenant route.
- [ADR-1157](1157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md): every frontend workspace builds
  from one Vite preset.
- [ADR-1175](1175-bundles-split-into-vendor-and-surface-chunks-and-an-oversized-chunk-fails-the-build.md): bundles split
  into vendor and surface chunks.
- [ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md): the bundle mounts over the
  page the host rendered.
- [ADR-1388](1388-the-html-page-security-model-and-the-front-channel-marker-are-general-aspnet-core-helpers.md): the
  front-channel marker.
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md):
  every package names one capability.
- [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md): browser-facing
  requests are safe by construction.
- [RFC 6265](https://www.rfc-editor.org/rfc/rfc6265): HTTP state management, cookie prefixes.
- Code: `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointGroup.cs`,
  `InteractiveSurfacePaths.cs`, `InteractiveSessionCookieManager.cs`, `InteractiveBundleAssetEndpointGroup.cs`,
  `InteractiveBundleAssetEndpointHandler.cs` and `DefaultRegistration.cs` in the same folder;
  `src/NCode.Identity.AspNetCore/SinglePageBundle.cs` and `SinglePageBundleDefaults.cs`;
  `src/NCode.Identity.OpenId.Authentication/Sessions/DefaultSessionStateService.cs`;
  `frontend/packages/vite-config/src/preset.ts`;
  `tests/NCode.Identity.OpenId.Authentication.Interactive.Tests/BuiltAuthBundleFactAttribute.cs` and
  `InteractiveBundleShellTests.cs`; `.github/workflows/frontend.yml`.
- Issue [#612](https://github.com/NCodeGroup/identity/issues/612).
