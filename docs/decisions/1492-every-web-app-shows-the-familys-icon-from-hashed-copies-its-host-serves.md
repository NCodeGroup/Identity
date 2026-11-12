# 1492. Every web app shows the family's icon from hashed copies its host serves

- **Status:** Accepted (decision 6 amended by [ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md): a tenant's tab icon is its branding setting `branding_favicon_uri`)
- **Refines:** [ADR-1491](1491-the-familys-logo-is-the-id-mark-between-the-ncode-braces-and-one-script-draws-every-file-of-it.md)
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

The four web apps under `frontend/apps` are served in two ways. `auth` is the sign-in and account bundle: the OP
host serves it beneath each tenant's route, with a relative base
([ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)). `mgmt` is the management console: its
BFF host serves it at its origin's root, and writes each response's nonce into the page
([ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)). `conformance` and
`playground` are developer surfaces that run on their own Vite servers.

The brand kit in `brand/favicon/` holds the family's browser icons: an ICO, an SVG and an icon for iOS, with the tile
and the files a web manifest names
([ADR-1491](1491-the-familys-logo-is-the-id-mark-between-the-ncode-braces-and-one-script-draws-every-file-of-it.md)).
The kit's README describes a site that serves the files at its root.

Neither host serves a file at the root of a bundle. Each serves only the bundle's `assets/` directory, with an immutable
cache
([ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)). Every page the server writes
carries a content security policy with `img-src 'self'` and no `manifest-src`
([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)).
The image builds' context holds only `src/`, `hosts/`, `analyzers/`, `frontend/` and the root build files
([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)), so a frontend build
cannot read `brand/`.

A web manifest makes a page installable as an app named "NCode Identity". A tenant has a `logoUrl`, a header picture of
any shape, which a browser would squash into a 16-pixel square if it were a tab icon.

## Decision

**Each web app carries copies of the kit's icons, links them from its `index.html`, and its build content-hashes them
into the `assets/` its host already serves.**

1. **The copies.** Each app holds `favicon.ico`, `favicon.svg` and `apple-touch-icon.png` in
   `frontend/apps/<app>/favicon/`, byte for byte the files of `brand/favicon/`.
2. **The links.** Each `index.html` has the first three tags of `brand/README.md`, relative to itself:

   ```html
   <link rel="icon" href="./favicon/favicon.ico" sizes="32x32" />
   <link rel="icon" href="./favicon/favicon.svg" type="image/svg+xml" />
   <link rel="apple-touch-icon" href="./favicon/apple-touch-icon.png" />
   ```

   The Vite build emits each file as `assets/<name>-<hash><ext>` and rewrites the `href` to it: `./assets/…` for `auth`,
   whose base is `./`, and `/assets/…` for the other apps, whose base is `/`. The build never inlines an icon.
3. **The hosts serve them.** The OP host (`{tenant}/account/assets/…`) and the console host (`/assets/…`) serve the
   hashed files through their assets routes, immutable, as `image/x-icon`, `image/svg+xml` and `image/png`, under the
   pages' existing `img-src 'self'`. No host route, policy or code changes, and
   [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)'s rule that
   only the assets directory is servable holds.
4. **No web manifest.** The fourth tag of the kit's README is not used and no app carries `site.webmanifest` or the
   192 and 512 pixel icons. A manifest makes a page installable as an app named "NCode Identity": every tenant's sign-in
   page would offer that install, and the sign-in surface is a step of a client's authorization request, never an app.
   The console and the developer surfaces are opened by address and are not offered as installed apps either. Linking a
   manifest would also need its icons rewritten to their hashed names and a `manifest-src` source in both hosts'
   policies.
5. **The tile.** The apps carry the kit's tile, not `favicon/transparent/`, because the tile reads on light and dark tab
   strips alike.
6. **The family's icon, never `logoUrl`.** A tenant's sign-in tab and the console show the family's icon. A tenant's
   `logoUrl` is a header picture of any shape that a browser would squash into a 16-pixel square, and the console's
   branding `logoUrl` is treated the same way. A tenant's own tab icon would be a branding setting of its own, decided
   separately.
7. **The checks.** `build/favicon-copies.ps1`, run by the `favicon copies` stage of `./build/dod.ps1` through its
   fixture tests, reports every app copy that differs from, or has no counterpart in, `brand/favicon/`. Each app's
   `src/favicon.test.ts` checks its links and its three files against the family icon set that `@ncode/test-support`
   holds once. For each host and each icon, one case reads the built `index.html`, finds the icon's link and serves
   the linked asset through `SinglePageBundle.ServeAsset` with its image type, and one integration test requests an
   icon through the host's assets route and gets its image type. The pages' own policy tests hold their
   `img-src 'self'`.

## Options considered

- **`public/` and root URLs, as the kit's README describes for a site.** Rejected: neither host serves a file at the
  root of a bundle, so serving `/favicon.ico` needs new routes with a revalidated cache, outside the assets directory
  that [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)
  limits serving to.
- **One shared copy and a preset plugin.** Rejected: a link into another package resolves in a build but not on a dev
  server ([ADR-1151](1151-every-frontend-workspace-builds-and-tests-from-one-preset-that-fails-on-warnings-and-oversized-chunks.md)).
- **Reading `brand/` directly.** Rejected: the image builds' context holds no `brand/`
  ([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)).
- **Linking the web manifest.** Rejected for the reasons of decision 4.

## Consequences

- Every tab shows the tile.
- A returning browser fetches no icon again until a kit change renames them, because the hashed files are immutable.
- A change to `brand/favicon/` fails the `favicon copies` stage until each app's copy is replaced.
- No app is installable.
- A tenant tab icon is a branding setting of its own, decided separately.

## References

- Issue [#737](https://github.com/NCodeGroup/identity/issues/737)
- `brand/README.md`
- [ADR-1151](1151-every-frontend-workspace-builds-and-tests-from-one-preset-that-fails-on-warnings-and-oversized-chunks.md)
- [ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)
- [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)
- [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)
- [ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)
- [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)
- [ADR-1491](1491-the-familys-logo-is-the-id-mark-between-the-ncode-braces-and-one-script-draws-every-file-of-it.md)
