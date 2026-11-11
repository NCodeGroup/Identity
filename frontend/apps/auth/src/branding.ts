import { parseBrandingConfig, type BrandingConfig } from '@ncode/config';

/** The id of the `<script type="application/json">` element in which the host embeds the tenant's branding. */
export const BRANDING_ELEMENT_ID = 'branding';

/**
 * Reads the branding of the tenant that serves the surface from the page. The host embeds the tenant's branding in
 * every page it serves with the bundle, so nothing is requested. Branding only themes the surface, so a missing or
 * malformed block resolves to `undefined` and the default presentation applies rather than blocking sign-in.
 */
export function readBranding(doc: Document = document): BrandingConfig | undefined {
  const text = doc.getElementById(BRANDING_ELEMENT_ID)?.textContent;
  if (!text) {
    return undefined;
  }
  try {
    return parseBrandingConfig(JSON.parse(text));
  } catch {
    return undefined;
  }
}
