/** One icon link of a web app's `index.html`: its `rel`, `href`, `sizes` and `type` attributes, `null` where absent. */
export type IconLink = readonly [
  rel: string,
  href: string,
  sizes: string | null,
  type: string | null,
];

/**
 * The brand kit's icon links that every web app's `index.html` carries, in order and relative to the index: the first
 * three tags of `brand/README.md`, and no web manifest (ADR-1492).
 */
export const FAMILY_ICON_LINKS: readonly IconLink[] = [
  ['icon', './favicon/favicon.ico', '32x32', null],
  ['icon', './favicon/favicon.svg', null, 'image/svg+xml'],
  ['apple-touch-icon', './favicon/apple-touch-icon.png', null, null],
];

/**
 * The kit's files that every web app carries in its `favicon/` folder: exactly the files {@link FAMILY_ICON_LINKS}
 * names.
 */
export const FAMILY_ICON_FILE_NAMES: readonly string[] = FAMILY_ICON_LINKS.map(([, href]) =>
  href.slice(href.lastIndexOf('/') + 1),
);
