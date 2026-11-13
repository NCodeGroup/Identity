/**
 * Reads the setup token from the `token` parameter of the URL fragment, which never reaches the server or a referrer,
 * and removes it from the URL so it does not stay in the address bar or the history entry. The path, the query and any
 * other fragment parameters stay. Yields `undefined` when the fragment carries no token.
 */
export function takeSetupToken(win: Window = window): string | undefined {
  const { hash, pathname, search } = win.location;
  const params = new URLSearchParams(hash.startsWith('#') ? hash.slice(1) : hash);
  const token = params.get('token');
  if (token === null) {
    return undefined;
  }
  params.delete('token');
  const rest = params.toString();
  win.history.replaceState(win.history.state, '', `${pathname}${search}${rest ? `#${rest}` : ''}`);
  return token;
}
