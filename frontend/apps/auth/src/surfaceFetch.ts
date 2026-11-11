import { LOGIN_ACTION, resolveSurfacePath } from './params.js';

/**
 * Fetches a JSON endpoint of the surface for a page the signed-in end-user is on. A `401` means the cookie session is
 * gone, so it sends the browser to the login page with the given surface path as the return URL and yields `null`; any
 * other non-OK response also yields `null`. It is a no-op when `fetch` is unavailable (e.g. a DOM-free unit test).
 */
export async function fetchSurfaceJson<T>(endpoint: string, returnPath: string): Promise<T | null> {
  if (typeof fetch !== 'function') {
    return null;
  }
  const response = await fetch(resolveSurfacePath(endpoint), {
    headers: { accept: 'application/json' },
  });
  if (response.status === 401) {
    window.location.assign(
      `${resolveSurfacePath(LOGIN_ACTION)}?ReturnUrl=${encodeURIComponent(resolveSurfacePath(returnPath))}`,
    );
    return null;
  }
  if (!response.ok) {
    return null;
  }
  return (await response.json()) as T;
}
