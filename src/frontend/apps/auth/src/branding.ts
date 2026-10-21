import { parseBrandingConfig, type BrandingConfig } from '@ncode/config';
import { BRANDING_ENDPOINT, resolveSurfacePath } from './params.js';

/**
 * Fetches the branding of the tenant that serves the surface. Branding only themes the surface, so any failure
 * resolves to `undefined` and the default presentation applies rather than blocking sign-in.
 */
export async function loadBranding(
  fetchFn: typeof fetch = fetch,
  baseUri: string = document.baseURI,
): Promise<BrandingConfig | undefined> {
  try {
    const response = await fetchFn(resolveSurfacePath(BRANDING_ENDPOINT, baseUri), {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
    });
    return response.ok ? parseBrandingConfig(await response.json()) : undefined;
  } catch {
    return undefined;
  }
}
