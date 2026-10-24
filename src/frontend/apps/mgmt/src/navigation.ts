import { useEffect, useSyncExternalStore } from 'react';
import { resolveRoute, routePath, type ConsoleRoute } from './routes.js';

// Binds the route grammar (routes.ts) to the browser history: the address bar is the single source of the console's
// navigation state, so back/forward, reload, and a shared link all land on the same scope and surface (ADR-0174).

const listeners = new Set<() => void>();

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  window.addEventListener('popstate', listener);
  return () => {
    listeners.delete(listener);
    window.removeEventListener('popstate', listener);
  };
}

function currentPathname(): string {
  return window.location.pathname;
}

/**
 * Navigates to a route: pushes a history entry (or replaces the current one), then notifies the subscribed views.
 * Navigating to the current path is a no-op, so it adds no duplicate history entry.
 */
export function navigateTo(
  route: ConsoleRoute,
  options: { readonly replace?: boolean } = {},
): void {
  const path = routePath(route);
  if (path === window.location.pathname) {
    return;
  }
  if (options.replace) {
    window.history.replaceState(null, '', path);
  } else {
    window.history.pushState(null, '', path);
  }
  for (const listener of listeners) {
    listener();
  }
}

/**
 * The route the address bar names. A path that is not its route's canonical path — the bare origin, a trailing slash,
 * a tenant without a surface, or an unknown path — is rewritten in place to the canonical one, without a new history
 * entry.
 */
export function useConsoleRoute(): ConsoleRoute {
  const pathname = useSyncExternalStore(subscribe, currentPathname);
  const { route, path } = resolveRoute(pathname);

  useEffect(() => {
    if (path !== pathname) {
      navigateTo(route, { replace: true });
    }
  }, [route, path, pathname]);

  return route;
}
