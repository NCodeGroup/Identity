# 1491. The family's logo is the {id} mark between the NCode braces, and one script draws every file of it

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

## Decision

**The family's logo is `{id}`: the letters i and d between the braces of the NCode Group's mark, in the group's
palette. `brand/build.py` draws every file of it, and `brand/` holds them.**

1. **The mark is `{id}`.** `id` says identity, and the braces say NCode. The braces are the group mark's, point for
   point, in the master and in each small drawing, and the letters have the braces' stroke. The palette is the group's:
   NCode Blue `#0033CC`, NCode Blue Light `#809FFF` for dark backgrounds, NCode Navy `#0A1F5C`, and white.
2. **The wordmark writes the name as two words, "NCode Identity".** The package and namespace names keep their dot,
   `NCode.Identity`. A lockup sets the wordmark beside the mark, or under the mark at twice that size.
3. **One script draws every file from one geometry.** The mark is straight lines and circular arcs on a 64-unit grid.
   `brand/build.py` writes the SVG masters from them and rasterizes the same outlines for every PNG and ICO, so no file
   is exported by hand. Each size from 16 to 48 pixels has a drawing of its own with every stem on whole pixels. The
   script reads every PNG and ICO entry back, and stops when a file breaks its platform's limit or holds a transparent
   pixel where the platform takes none. Its output is deterministic.
4. **Text is outlines.** `brand/outlines.py` converts the wordmark, and the descriptor "OpenID Connect and OAuth 2.x for
   .NET" that the link previews carry, from a pinned release of [Inter](https://github.com/rsms/inter), after it
   verifies the SHA-256 of the archive and of each font file it reads. It writes `brand/outlines.json`, which
   `brand/build.py` reads, so no file of the kit needs a font. Inter is under the SIL Open Font License 1.1, and
   `THIRD-PARTY-NOTICES.md` records its copyright and license, as it does for the bundled password list
   ([ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)).
5. **The repository takes the logo from the kit wherever it shows it.** `Directory.Build.targets` packs
   `brand/nuget/ncode-identity-nuget-tile-blue.png` into every package as its icon. The host chart's `icon` is the
   address of `brand/tile/ncode-identity-tile-blue-256.png` on `main`. The README opens with the lockup, in blue for
   GitHub's light theme and in light blue for its dark one.
6. **The blue tile is the default.** Wherever the background is not the family's to choose, the logo is the white mark
   on a blue tile, which reads on a white page and in a dark IDE. The bare blue mark has a contrast of 2 to 1 on a dark
   page, so it is for light backgrounds only, and the light blue mark for dark ones.

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

## Consequences

- A change to the mark, a color or a line of text is an edit to `brand/build.py` or `brand/outlines.py` and a run of the
  scripts, and every file changes together.
- The kit is about 1 MB of generated files in the repository.
- The host chart and any README outside the repository read the files by their address on `main`, so a file under
  `brand/` that such an address names keeps its path.
- A build file that packs or embeds a file from `brand/` makes every project depend on it. The host images are
  published without packing, so their build context does not hold `brand/`.

## References

- [`brand/README.md`](../../brand/README.md) — which file is for what, the variants, the colors and the drawing's
  measures.
- [`brand/build.py`](../../brand/build.py) and [`brand/outlines.py`](../../brand/outlines.py) — the two scripts.
- [`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md) — Inter's copyright and license.
- [ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) — the bundled list that set the practice for
  third-party material: a pinned source, a script that verifies its SHA-256, and a notice.
- [ADR-1178](1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md) — the host image
  and its allow-listed build context.
