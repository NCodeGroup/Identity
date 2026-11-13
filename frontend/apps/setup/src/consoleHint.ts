import type { ManagementConsoleHint } from './setupApi.js';

/** The console the deployment or the page's URL fragment names, which the console step prefills. */
export type ConsoleHint = { readonly baseUrl?: string; readonly clientId?: string };

/**
 * Merges the console the server hints with the one the page's URL fragment names. The server's hint wins on each
 * field, and the fragment fills only what the hint leaves empty.
 */
export function mergeConsoleHint(
  hint: ManagementConsoleHint | undefined,
  fragment: { readonly consoleUrl?: string; readonly consoleClientId?: string },
): ConsoleHint {
  const baseUrl = hint?.baseUrl || fragment.consoleUrl;
  const clientId = hint?.clientId || fragment.consoleClientId;
  return {
    ...(baseUrl ? { baseUrl } : {}),
    ...(clientId ? { clientId } : {}),
  };
}
