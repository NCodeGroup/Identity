# 254. The interactive bundle's hashed files are served immutable without resolving the tenant

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The `apps/auth` bundle is built into content-hashed files: a file's name carries a hash of its content, so a name never
names two versions, and every tenant loads the same files. The host serves them beneath the tenant route, where the
page's `<base href>` resolves the bundle's relative URLs
([ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md)). Three forces bear on how:

- A request served by the surface's own group passes the environment filter, which materializes the tenant and opens
  its scope. A file that is the same for every tenant does not need either, and an unknown tenant's request for it
  fails for no reason (issue [#221](https://github.com/NCodeGroup/Identity/issues/221)).
- Validators alone, `ETag` and `Last-Modified`, give a browser no freshness lifetime, so it sends a conditional request
  for every file of every page load.
- The .NET gate installs no Node and never builds the bundle. The host's split of the bundle's `index.html` around its
  mount point is tested only against hand-written HTML, so a change to the build output that breaks the split passes
  that gate (issue [#227](https://github.com/NCodeGroup/Identity/issues/227)).

## Decision

**The host serves the bundle's content-hashed files from a route group that resolves no tenant, marks them immutable,
and serves nothing else of the bundle. A test runs the host's split against the real build output.**

1. **A group with the surface's prefix and no environment filter maps the route.** The route is
   `GET|HEAD {tenant route}/account/assets/{**asset}`, and `InteractiveBundleAssetEndpointGroup` maps it. Its prefix is
   the tenant route, as the surface's group has, and it adds no `OpenIdEnvironmentEndpointFilter`; it keeps
   `WithOpenIdFrontChannel()`. The URL stays beneath the surface, so the bundle's relative asset URLs resolve against
   the page's `<base href>` unchanged. A request for any tenant segment, known or not, is served the file: the files are
   public and identical for every tenant.
2. **Only the assets directory is servable.** `InteractiveBundleReader`'s file provider is rooted at
   `InteractiveBundleReader.AssetsDirectory`, which is `assets`, beneath the bundle's root. A path that leaves the
   directory, a hidden or system file, and a file of an unknown content type are answered `404`. The bundle's
   `index.html`, which the host reads to render its pages, lies outside the directory and is not served.
3. **A served file is immutable.** It is sent `Cache-Control: public, max-age=31536000, immutable`, and it keeps its
   `ETag` and `Last-Modified` validators and range requests. A `404` carries no cache lifetime.
4. **The Vite preset pins the directory.** `@ncode/vite-config` sets `build.assetsDir` to `assets`, so the host's route
   and the build agree on where the hashed files are
   ([ADR-0157](0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md)).
5. **A fact reads the built bundle.** `InteractiveBundleShellTests.TryCreate_WhenGivenTheBuiltBundlesIndex_…` splits
   and renders the real built `index.html`. It carries `[BuiltAuthBundleFact]`, which skips unless the
   `NCODE_AUTH_BUNDLE_INDEX` environment variable names the file. The Frontend CI workflow builds the bundle, then runs
   the fact with the variable set to the absolute path of `apps/auth/dist/index.html`, and its path filters include the
   interactive projects. The .NET Definition of Done installs no Node, so it reports the fact skipped.

## Options considered

- **A root-level asset path.** Rejected: the bundle is built with a relative base so that it mounts anywhere
  ([ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md)). A path at the root needs absolute
  URLs, which tie the bundle to one mount point.
- **Keep resolving the tenant.** Rejected: a request for a public file materializes a tenant for nothing, and the
  answer would depend on the tenant while the file does not.
- **Static-file middleware.** Rejected: it serves a bundle at one request path, while the tenant route is a pattern, and
  it makes every host add middleware that the endpoint group registers for itself.
- **A checked-in copy of the built `index.html`.** Rejected: the copy drifts from the build, so the test would assert
  against a file that no build produced.

## Consequences

- A returning browser loads the bundle with no request until a release changes a file's name.
- Any first path segment serves the public files. Nothing in them is tenant data: the tenant's branding travels in the
  page ([ADR-0253](0253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md)).
- `index.html` is not served, and neither is any other file outside the assets directory.
- `assets` is a contract between the host and `@ncode/vite-config`. A bundle built with another directory has no file
  served.
- The Definition of Done reports the built-bundle fact skipped. Frontend CI is the gate that runs it, and a local run
  builds the bundle with `pnpm exec vite build` in `src/frontend/apps/auth` and sets `NCODE_AUTH_BUNDLE_INDEX` to the
  absolute path of `dist/index.html`.

## References

- [ADR-0157](0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md): every frontend workspace builds
  and tests from one Vite preset.
- [ADR-0175](0175-bundles-split-into-vendor-and-surface-chunks-and-an-oversized-chunk-fails-the-build.md): bundles split
  into vendor and surface chunks, and an oversized chunk fails the build.
- [ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md): the interactive surface is served under
  the tenant route.
- Code: `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveBundleAssetEndpointGroup.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveBundleAssetEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveBundleReader.cs`,
  `src/frontend/packages/vite-config/src/preset.ts`,
  `tests/NCode.Identity.OpenId.Authentication.Interactive.Tests/BuiltAuthBundleFactAttribute.cs`,
  `tests/NCode.Identity.OpenId.Authentication.Interactive.Tests/InteractiveBundleShellTests.cs` and
  `.github/workflows/frontend.yml`.
