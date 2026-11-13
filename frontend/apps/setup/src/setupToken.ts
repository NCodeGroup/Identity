/** What the setup link's URL fragment carries. A property is absent when its parameter is missing or empty. */
export interface SetupFragment {
  readonly token?: string;
  readonly consoleUrl?: string;
  readonly consoleClientId?: string;
}

const FRAGMENT_PARAMETERS = ['token', 'consoleUrl', 'consoleClientId'] as const;

/**
 * Reads the setup token, the management console's URL and the console's client ID from the `token`, `consoleUrl` and
 * `consoleClientId` parameters of the URL fragment, which never reaches the server or a referrer, and removes them
 * from the URL so they do not stay in the address bar or the history entry. The path, the query and any other
 * fragment parameters stay. A parameter that is missing or empty is absent from the result.
 */
export function takeSetupFragment(win: Window = window): SetupFragment {
  const { hash, pathname, search } = win.location;
  const params = new URLSearchParams(hash.startsWith('#') ? hash.slice(1) : hash);
  const taken: { token?: string; consoleUrl?: string; consoleClientId?: string } = {};
  let removed = false;
  for (const name of FRAGMENT_PARAMETERS) {
    if (!params.has(name)) {
      continue;
    }
    const value = params.get(name);
    if (value) {
      taken[name] = value;
    }
    params.delete(name);
    removed = true;
  }
  if (removed) {
    const rest = params.toString();
    win.history.replaceState(
      win.history.state,
      '',
      `${pathname}${search}${rest ? `#${rest}` : ''}`,
    );
  }
  return taken;
}
