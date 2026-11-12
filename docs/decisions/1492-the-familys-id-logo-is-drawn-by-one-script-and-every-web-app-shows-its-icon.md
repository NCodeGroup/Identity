# 1492. The family's {id} logo is drawn by one script, and every web app shows its icon

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

The family is shown under a picture in many places, and each place asks for a file of its own. NuGet recommends a
package icon of 128 by 128 pixels and scales it down to as little as 32, on a white page and in a dark IDE alike. A
favicon is an ICO with a drawing per size, an SVG, an icon for iOS and the icons a web manifest names. GitHub's social
preview is 1280 by 640 pixels. GitLab takes an avatar of at most 200 KiB. Microsoft Entra's My Apps takes exactly 215 by
215 pixels, at most 100 KB, with a solid background. Artifact Hub and cluster dashboards show the icon a Helm chart
names.

The family belongs to the NCode Group, whose mark is `{N}`: an N between curly braces in one blue, `#0033CC`, drawn by
`brand/build.py` of [`NCodeGroup/project-template`](https://github.com/NCodeGroup/project-template).

Pictures exported by hand for each place drift apart in proportion and color. A small icon scaled down from a large
drawing has its strokes between pixels and looks soft. A wordmark set in a font makes every file that shows it depend
on that font, and an SVG shown as an image cannot load one.

The four web apps under `frontend/apps` are served in two ways. `auth` is the sign-in and account bundle: the OP host
serves it beneath each tenant's route, with a relative base
([ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)).
`mgmt` is the management console: its BFF host serves it at its origin's root, and writes each response's nonce into the
page
([ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)).
`conformance` and `playground` are developer surfaces that run on their own Vite servers.

The kit's `brand/README.md` describes a site that serves the favicon files at its root, but neither host serves a file at
the root of a bundle. Each serves only the bundle's `assets/` directory, with an immutable cache. Every page the server
writes carries a content security policy with `img-src 'self'` and no `manifest-src`
([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)).
The image builds' context holds only `src/`, `hosts/`, `analyzers/`, `frontend/` and the root build files
([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)), so
a frontend build cannot read `brand/`.

A web manifest makes a page installable as an app named "NCode Identity". A tenant has a `logoUrl`, a header picture of
any shape, which a browser would squash into a 16-pixel square if it were a tab icon.

## Decision

**The family's logo is `{id}`: the letters i and d between the braces of the NCode Group's mark, in the group's
palette. `brand/build.py` draws every file of it, and `brand/` holds them. Each web app carries copies of the kit's
browser icons, links them from its `index.html`, and its build content-hashes them into the `assets/` its host already
serves.**

### The logo and its kit

1. **The mark is `{id}`.** `id` says identity, and the braces say NCode. The braces are the group mark's, point for
   point, in the master and in each small drawing, and the letters have the braces' stroke. The palette is the group's:
   NCode Blue `#0033CC`, NCode Blue Light `#809FFF` for dark backgrounds, NCode Navy `#0A1F5C`, and white.
2. **The wordmark writes the name as two words, "NCode Identity".** The package and namespace names keep their dot,
   `NCode.Identity`. A lockup sets the wordmark beside the mark, or under the mark at twice that size.
3. **One script draws every file from one geometry.** The mark is straight lines and circular arcs on a 64-unit grid.
   `brand/build.py` writes the SVG masters from them and rasterizes the same outlines for every PNG and ICO, so no file
   is exported by hand: a change is an edit to a script and a run, never to a file it writes. Each size from 16 to 48
   pixels has a pixel-snapped drawing of its own with every stem on whole pixels. The script reads every PNG and ICO
   entry back, and stops when a file breaks its platform's limit or holds a transparent pixel where the platform takes
   none. Its output is deterministic.
4. **Text is outlines.** `brand/outlines.py` converts the wordmark, and the descriptor "OpenID Connect and OAuth 2.x for
   .NET" that the link previews carry, from a pinned release of [Inter](https://github.com/rsms/inter), after it
   verifies the SHA-256 of the archive and of each font file it reads. It writes `brand/outlines.json`, which
   `brand/build.py` reads, so no file of the kit needs a font. Inter is under the SIL Open Font License 1.1, and
   `THIRD-PARTY-NOTICES.md` records its copyright and license, as it does for the bundled password list
   ([ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)).
5. **The repository takes the logo from the kit wherever it shows it.** `Directory.Build.targets` packs
   `brand/nuget/ncode-identity-nuget-tile-blue.png` into every package as its icon. The host chart's `icon` is the
   address of `brand/tile/ncode-identity-tile-blue-256.png` on `main`, so that path is kept. The README opens with the
   blue lockup, or the light one in dark theme.
6. **The blue tile is the default.** Wherever the background is not the family's to choose, the logo is the white mark
   on a blue tile, which reads on a white page and in a dark IDE. The bare blue mark has a contrast of 2 to 1 on a dark
   page, so it is for light backgrounds only, and the light-blue mark for dark ones.

### The web apps' icons

7. **The copies.** Each app holds `favicon.ico`, `favicon.svg` and `apple-touch-icon.png` in
   `frontend/apps/<app>/favicon/`, byte for byte the files of `brand/favicon/`.
8. **The links.** Each `index.html` has the first three tags of `brand/README.md`, relative to itself:

   ```html
   <link rel="icon" href="./favicon/favicon.ico" sizes="32x32" />
   <link rel="icon" href="./favicon/favicon.svg" type="image/svg+xml" />
   <link rel="apple-touch-icon" href="./favicon/apple-touch-icon.png" />
   ```

   The Vite build emits each file as `assets/<name>-<hash><ext>` and rewrites the `href` to it: `./assets/…` for `auth`,
   whose base is `./`, and `/assets/…` for the other apps, whose base is `/`. The build never inlines an icon.
9. **The hosts serve them.** The OP host (`{tenant}/account/assets/…`) and the console host (`/assets/…`) serve the
   hashed files through their assets routes, immutable, as `image/x-icon`, `image/svg+xml` and `image/png`, under the
   pages' `img-src 'self'`. Only the assets directory is servable
   ([ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)).
10. **No web manifest.** No app carries `site.webmanifest` or the 192 and 512 pixel icons, and the fourth tag of the
    kit's README is not used. A manifest makes a page installable as an app named "NCode Identity": every tenant's
    sign-in page would offer that install, and the sign-in surface is a step of a client's authorization request, never
    an app. The console and the developer surfaces are opened by address and are not offered as installed apps either.
    Linking a manifest would also need its icons rewritten to their hashed names and a `manifest-src` source in both
    hosts' policies.
11. **The tile.** The apps carry the kit's tile, not `favicon/transparent/`, because the tile reads on light and dark
    tab strips alike.
12. **The family's icon, never `logoUrl`.** A tenant's `logoUrl` is a header picture of any shape that a browser would
    squash into a 16-pixel square, and the console's branding `logoUrl` is treated the same way, so neither is a tab
    icon. When a tenant or the console's branding sets its own favicon, the host removes the bundle's icon links and
    links that icon in their place (`HtmlIconLinks`,
    [ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md)).
13. **The checks.** `build/favicon-copies.ps1`, run by the `favicon copies` stage of `./build/dod.ps1` through its
    fixture tests, fails on an app copy that differs from, or has no counterpart in, `brand/favicon/`. Each app's
    `src/favicon.test.ts` checks its links and its three files against the family icon set that `@ncode/test-support`
    holds once. For each host and each icon, one case reads the built `index.html`, finds the icon's link and serves the
    linked asset through `SinglePageBundle.ServeAsset` with its image type, and one integration test requests an icon
    through the host's assets route and gets its image type. The pages' own policy tests hold their `img-src 'self'`.

## Options considered

- **The group's `{N}` for the family too.** One picture for everything of NCode's. Rejected: in a package list or an
  app launcher it does not tell the family from the group's other work.
- **A pictogram between the braces, such as a key, a keyhole or a person, or a shield around an N.** Each says security
  at a glance. Rejected: at 16 pixels a pictogram is a few ambiguous pixels, and two letters stay legible; a shield
  also gives up the braces, which tie the family to the group.
- **A color of its own.** Rejected: the one hue is what makes the logo NCode's, and the letters tell the family apart.
- **Files exported from a drawing program.** Rejected: nothing keeps a hundred files in step, and the small sizes need
  drawings of their own either way.
- **The wordmark as text in the SVG files.** Smaller files. Rejected: an SVG shown as an image falls back to a font of
  the reader's system.
- **`public/` and root URLs, as the kit's README describes for a site.** Rejected: neither host serves a file at the
  root of a bundle, so serving `/favicon.ico` needs new routes with a revalidated cache, outside the assets directory
  that [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)
  limits serving to.
- **One shared copy and a preset plugin.** Rejected: a link into another package resolves in a build but not on a dev
  server ([ADR-1151](1151-every-frontend-workspace-builds-and-tests-from-one-preset-that-fails-on-warnings-and-oversized-chunks.md)).
- **Reading `brand/` directly from the frontend build.** Rejected: the image builds' context holds no `brand/`
  ([ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)).
- **Linking the web manifest.** Rejected for the reasons of decision 10.
- **The chosen design.** One script draws the `{id}` kit, and each app carries hashed copies of its three icons that its
  host already serves, so nothing is added to a host and no file drifts from the script's output.

## Consequences

- A change to the mark, a color or a line of text is an edit to `brand/build.py` or `brand/outlines.py` and a run of the
  scripts, and every file changes together.
- The host chart and any README outside the repository read the files by their address on `main`, so a file under
  `brand/` that such an address names keeps its path.
- A build file that packs or embeds a file from `brand/` makes every project depend on it. The host images are
  published without packing, so their build context does not hold `brand/`.
- Every tab shows the tile.
- A returning browser fetches no icon again until a kit change renames them, because the hashed files are immutable.
- A change to `brand/favicon/` fails the `favicon copies` stage until each app's copy is replaced.
- No app is installable.

## References

- [`brand/README.md`](../../brand/README.md) — which file is for what, the variants, the colors and the drawing's
  measures.
- [`brand/build.py`](../../brand/build.py) and [`brand/outlines.py`](../../brand/outlines.py) — the two scripts.
- [`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md) — Inter's copyright and license.
- Issue [#737](https://github.com/NCodeGroup/identity/issues/737)
- [ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) — the bundled list that set the practice for
  third-party material: a pinned source, a script that verifies its SHA-256, and a notice.
- [ADR-1248](1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md) — the
  host image and its allow-listed build context.
- [ADR-1151](1151-every-frontend-workspace-builds-and-tests-from-one-preset-that-fails-on-warnings-and-oversized-chunks.md)
- [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)
- [ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md)
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)
- [ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)
