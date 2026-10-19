import { StrictMode, type ReactNode } from 'react';
import { webcrypto } from 'node:crypto';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi, type Mock } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import type { FetchLike } from '@ncode/auth';
import type { RuntimeConfig } from '@ncode/config';
import { FlowExplorer, type FlowExplorerProps } from './FlowExplorer.js';
import { AuthError, TRANSACTION_KEY, type FlowDeps, type FlowTransaction } from '../flow.js';

/** The OP coordinates every runtime shares; the optional endpoints and client id are layered on per test. */
const minimalRuntime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid', 'profile'],
  features: {},
};

const runtime: RuntimeConfig = {
  ...minimalRuntime,
  endSessionEndpoint: 'https://op.test/end-session',
  userInfoEndpoint: 'https://op.test/userinfo',
  clientId: 'playground-spa',
};

// The transaction's client differs from the prefilled one, proving refresh/logout use the client that completed the flow.
const transaction: FlowTransaction = {
  clientId: 'tx-client',
  redirectUri: 'https://ui.test/callback',
  scopes: ['openid'],
  state: 'state-7',
  nonce: 'nonce-7',
  codeVerifier: 'verifier-7',
  codeChallenge: 'challenge-7',
};

const CALLBACK_SEARCH = '?code=the-code&state=state-7';

const nodeSubtle = webcrypto.subtle as unknown as SubtleCrypto;

type FetchResult = Awaited<ReturnType<FetchLike>>;

function jsonResponse(body: unknown, status = 200): FetchResult {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  };
}

function encodeSegment(value: object): string {
  return Buffer.from(JSON.stringify(value)).toString('base64url');
}

function jwt(payload: object): string {
  return `${encodeSegment({ alg: 'PS256', typ: 'JWT' })}.${encodeSegment(payload)}.signature`;
}

function createStorage() {
  const map = new Map<string, string>();
  return {
    getItem: (key: string) => map.get(key) ?? null,
    setItem: (key: string, value: string) => void map.set(key, value),
    removeItem: (key: string) => void map.delete(key),
  };
}

function createDeps(fetchFn: FetchLike, overrides: Partial<FlowDeps> = {}): FlowDeps {
  return {
    fetchFn,
    storage: createStorage(),
    randomBytes: (length: number) => new Uint8Array(length).fill(7),
    subtle: nodeSubtle,
    ...overrides,
  };
}

function seedTransaction(deps: FlowDeps): void {
  deps.storage.setItem(TRANSACTION_KEY, JSON.stringify(transaction));
}

function renderExplorer(
  props: Partial<FlowExplorerProps> = {},
  wrap: (node: ReactNode) => ReactNode = (node) => node,
) {
  const navigate = vi.fn<(url: string) => void>();
  const onCleanUrl = vi.fn<() => void>();
  const deps = props.deps ?? createDeps(vi.fn<FetchLike>());
  render(
    wrap(
      <AppThemeProvider>
        <FlowExplorer
          runtime={runtime}
          deps={deps}
          origin="https://ui.test"
          initialSearch=""
          navigate={navigate}
          onCleanUrl={onCleanUrl}
          {...props}
        />
      </AppThemeProvider>,
    ),
  );
  return { deps, navigate, onCleanUrl };
}

/** Renders the explorer on a successful callback and waits until the issued tokens are displayed. */
async function renderWithTokens(
  tokenBody: Record<string, unknown>,
  props: Partial<FlowExplorerProps> = {},
) {
  const fetchFn = vi.fn<FetchLike>().mockResolvedValueOnce(jsonResponse(tokenBody));
  const deps = createDeps(fetchFn);
  seedTransaction(deps);
  const view = renderExplorer({ deps, initialSearch: CALLBACK_SEARCH, ...props });
  await screen.findByText(/token_type/);
  return { ...view, fetchFn };
}

function stepStatus(index: number): string | null {
  return screen.getByTestId(`step-${index}`).getAttribute('data-status');
}

/** The monospace block rendered under the given caption. */
function codeBlockFor(label: string): HTMLElement {
  return within(screen.getByText(label).parentElement!).getByTestId('code-block');
}

function lastRequestBody(fetchFn: Mock<FetchLike>): URLSearchParams {
  return new URLSearchParams(fetchFn.mock.lastCall![1].body);
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('FlowExplorer', () => {
  describe('authorization request', () => {
    it('prefills the client id, scopes, and redirect uri from the discovered config', () => {
      renderExplorer();

      expect(screen.getByTestId('client-id')).toHaveValue('playground-spa');
      expect(screen.getByTestId('scopes')).toHaveValue('openid profile');
      expect(codeBlockFor('Redirect URI (registered)')).toHaveTextContent(
        'https://ui.test/callback',
      );
      expect(screen.getByText(/No tokens yet/)).toBeInTheDocument();
      expect([stepStatus(1), stepStatus(2), stepStatus(3)]).toEqual(['active', 'idle', 'idle']);
      expect(screen.getByTestId('begin')).toBeEnabled();
      expect(screen.getByTestId('userinfo')).toBeDisabled();
      expect(screen.getByTestId('refresh')).toBeDisabled();
      expect(screen.getByTestId('logout')).toBeDisabled();
    });

    it('persists the transaction and navigates to the PKCE authorize url', async () => {
      const { deps, navigate } = renderExplorer();

      fireEvent.click(screen.getByTestId('begin'));

      await waitFor(() => expect(navigate).toHaveBeenCalledOnce());
      const url = new URL(navigate.mock.calls[0]![0]);
      expect(url.origin + url.pathname).toBe('https://op.test/authorize');
      expect(url.searchParams.get('client_id')).toBe('playground-spa');
      expect(url.searchParams.get('scope')).toBe('openid profile');
      expect(url.searchParams.get('code_challenge_method')).toBe('S256');
      expect(url.searchParams.get('redirect_uri')).toBe('https://ui.test/callback');
      // The consent toggle is off by default, so no prompt is forced.
      expect(url.searchParams.has('prompt')).toBe(false);
      const persisted = JSON.parse(deps.storage.getItem(TRANSACTION_KEY)!) as FlowTransaction;
      expect(persisted.state).toBe(url.searchParams.get('state'));
    });

    it('forces prompt=consent when the consent toggle is checked', async () => {
      const { navigate } = renderExplorer();
      const consent = screen.getByTestId('prompt-consent');

      fireEvent.click(consent);
      expect(consent).toBeChecked();
      fireEvent.click(screen.getByTestId('begin'));

      await waitFor(() => expect(navigate).toHaveBeenCalledOnce());
      const url = new URL(navigate.mock.calls[0]![0]);
      expect(url.searchParams.get('prompt')).toBe('consent');
    });

    it('requests the edited client id and the normalized scope list', async () => {
      const { navigate } = renderExplorer();

      fireEvent.change(screen.getByTestId('client-id'), { target: { value: 'custom-client' } });
      fireEvent.change(screen.getByTestId('scopes'), { target: { value: '  openid   email  ' } });
      fireEvent.click(screen.getByTestId('begin'));

      await waitFor(() => expect(navigate).toHaveBeenCalledOnce());
      const url = new URL(navigate.mock.calls[0]![0]);
      expect(url.searchParams.get('client_id')).toBe('custom-client');
      expect(url.searchParams.get('scope')).toBe('openid email');
    });

    it('disables the request until a client id is entered', () => {
      renderExplorer({ runtime: minimalRuntime });
      const begin = screen.getByTestId('begin');
      expect(screen.getByTestId('client-id')).toHaveValue('');
      expect(begin).toBeDisabled();

      fireEvent.change(screen.getByTestId('client-id'), { target: { value: 'my-client' } });

      expect(begin).toBeEnabled();
    });

    it.each([
      {
        name: 'an Error message',
        cause: new Error('SubtleCrypto is unavailable.'),
        message: 'SubtleCrypto is unavailable.',
      },
      {
        name: 'a coded protocol error as "code: message"',
        cause: new AuthError('PKCE is not supported.', 'invalid_request'),
        message: 'invalid_request: PKCE is not supported.',
      },
      { name: 'a generic message for a non-Error', cause: 'opaque', message: 'Unexpected error.' },
    ])('shows $name when the request cannot be built', async ({ cause, message }) => {
      const subtle = { digest: vi.fn().mockRejectedValue(cause) } as unknown as SubtleCrypto;
      const { navigate } = renderExplorer({ deps: createDeps(vi.fn<FetchLike>(), { subtle }) });

      fireEvent.click(screen.getByTestId('begin'));

      expect(await screen.findByText(message)).toBeInTheDocument();
      expect(stepStatus(1)).toBe('error');
      expect(navigate).not.toHaveBeenCalled();
    });

    it('clears a request error when a retry succeeds', async () => {
      const digest = vi
        .fn<SubtleCrypto['digest']>()
        .mockRejectedValueOnce(new Error('SubtleCrypto is unavailable.'))
        .mockImplementation((algorithm, data) => nodeSubtle.digest(algorithm, data));
      const subtle = { digest } as unknown as SubtleCrypto;
      const { navigate } = renderExplorer({ deps: createDeps(vi.fn<FetchLike>(), { subtle }) });
      fireEvent.click(screen.getByTestId('begin'));
      await screen.findByText('SubtleCrypto is unavailable.');

      fireEvent.click(screen.getByTestId('begin'));

      await waitFor(() => expect(navigate).toHaveBeenCalledOnce());
      expect(screen.queryByText('SubtleCrypto is unavailable.')).not.toBeInTheDocument();
      expect(stepStatus(1)).toBe('active');
    });
  });

  describe('callback completion', () => {
    it('exchanges the code for tokens, clears the transaction, and cleans the url', async () => {
      const { deps, fetchFn, onCleanUrl } = await renderWithTokens({
        access_token: 'opaque-at',
        token_type: 'Bearer',
      });

      expect(fetchFn).toHaveBeenCalledOnce();
      expect(fetchFn.mock.calls[0]![0]).toBe('https://op.test/token');
      const body = lastRequestBody(fetchFn);
      expect(body.get('grant_type')).toBe('authorization_code');
      expect(body.get('code')).toBe('the-code');
      expect(body.get('code_verifier')).toBe('verifier-7');
      expect(body.get('client_id')).toBe('tx-client');
      expect(deps.storage.getItem(TRANSACTION_KEY)).toBeNull();
      expect(onCleanUrl).toHaveBeenCalledOnce();
      expect([stepStatus(1), stepStatus(2), stepStatus(3)]).toEqual(['done', 'done', 'active']);
    });

    it('shows an opaque access token without the optional token fields', async () => {
      await renderWithTokens({ access_token: 'opaque-at', token_type: 'Bearer' });

      expect(screen.getByText(/token_type/)).toHaveTextContent(/^token_type Bearer$/);
      expect(codeBlockFor('access_token (opaque)')).toHaveTextContent('opaque-at');
      expect(screen.queryByText(/^id_token/)).not.toBeInTheDocument();
      expect(screen.queryByText('refresh_token')).not.toBeInTheDocument();
      expect(screen.queryByText(/No tokens yet/)).not.toBeInTheDocument();
      expect(screen.getByTestId('userinfo')).toBeEnabled();
      expect(screen.getByTestId('refresh')).toBeDisabled();
      expect(screen.getByTestId('logout')).toBeEnabled();
    });

    it('decodes JWT tokens and shows every optional token field', async () => {
      await renderWithTokens({
        access_token: jwt({ sub: 'user-1', scope: 'openid profile' }),
        token_type: 'Bearer',
        expires_in: 300,
        scope: 'openid profile',
        id_token: jwt({ sub: 'user-1', nonce: 'nonce-7' }),
        refresh_token: 'rt-1',
      });

      expect(screen.getByText(/token_type/)).toHaveTextContent(
        'token_type Bearer · expires_in 300s · scope openid profile',
      );
      expect(codeBlockFor('access_token — header')).toHaveTextContent('"alg": "PS256"');
      expect(codeBlockFor('access_token — payload')).toHaveTextContent('"sub": "user-1"');
      expect(codeBlockFor('id_token — header')).toHaveTextContent('"typ": "JWT"');
      expect(codeBlockFor('id_token — payload')).toHaveTextContent('"nonce": "nonce-7"');
      expect(codeBlockFor('refresh_token')).toHaveTextContent('rt-1');
      expect(screen.getByTestId('refresh')).toBeEnabled();
    });

    it('completes the callback exactly once under StrictMode', async () => {
      const fetchFn = vi
        .fn<FetchLike>()
        .mockResolvedValue(jsonResponse({ access_token: 'opaque-at', token_type: 'Bearer' }));
      const deps = createDeps(fetchFn);
      seedTransaction(deps);

      const { onCleanUrl } = renderExplorer({ deps, initialSearch: CALLBACK_SEARCH }, (node) => (
        <StrictMode>{node}</StrictMode>
      ));

      expect(await screen.findByText(/token_type/)).toBeInTheDocument();
      expect(fetchFn).toHaveBeenCalledOnce();
      expect(onCleanUrl).toHaveBeenCalledOnce();
    });

    it('does not complete anything when the url carries no authorization response', () => {
      const fetchFn = vi.fn<FetchLike>();
      const deps = createDeps(fetchFn);
      seedTransaction(deps);

      const { onCleanUrl } = renderExplorer({ deps, initialSearch: '?tab=tokens' });

      expect(fetchFn).not.toHaveBeenCalled();
      expect(onCleanUrl).not.toHaveBeenCalled();
      expect(deps.storage.getItem(TRANSACTION_KEY)).not.toBeNull();
      expect(stepStatus(2)).toBe('idle');
    });

    it.each([
      {
        name: 'the server error response',
        search: '?error=access_denied&error_description=The%20user%20denied%20access.',
        arrange: (_fetchFn: Mock<FetchLike>) => {},
        message: 'access_denied: The user denied access.',
      },
      {
        name: 'a state mismatch',
        search: '?code=the-code&state=wrong',
        arrange: (_fetchFn: Mock<FetchLike>) => {},
        message: 'Authorization state mismatch (possible CSRF).',
      },
      {
        name: 'a token endpoint error',
        search: CALLBACK_SEARCH,
        arrange: (fetchFn: Mock<FetchLike>) =>
          fetchFn.mockResolvedValue(
            jsonResponse(
              { error: 'invalid_grant', error_description: 'The code has expired.' },
              400,
            ),
          ),
        message: 'invalid_grant: The code has expired.',
      },
      {
        name: 'a network failure',
        search: CALLBACK_SEARCH,
        arrange: (fetchFn: Mock<FetchLike>) =>
          fetchFn.mockRejectedValue(new TypeError('Failed to fetch')),
        message: 'Failed to fetch',
      },
      {
        name: 'a non-Error failure',
        search: CALLBACK_SEARCH,
        arrange: (fetchFn: Mock<FetchLike>) => fetchFn.mockRejectedValue('offline'),
        message: 'Unexpected error.',
      },
    ])('shows $name as the callback error', async ({ search, arrange, message }) => {
      const fetchFn = vi.fn<FetchLike>();
      arrange(fetchFn);
      const deps = createDeps(fetchFn);
      seedTransaction(deps);

      const { onCleanUrl } = renderExplorer({ deps, initialSearch: search });

      expect(await screen.findByText(message)).toBeInTheDocument();
      expect(stepStatus(1)).toBe('active');
      expect(stepStatus(2)).toBe('error');
      expect(screen.queryByText(/No tokens yet/)).not.toBeInTheDocument();
      expect(onCleanUrl).toHaveBeenCalledOnce();
    });
  });

  describe('token actions', () => {
    it('calls UserInfo with the access token and shows the claims', async () => {
      const { fetchFn } = await renderWithTokens({ access_token: 'opaque-at' });
      fetchFn.mockResolvedValueOnce(jsonResponse({ sub: 'user-1', email: 'user@test' }));

      fireEvent.click(screen.getByTestId('userinfo'));

      expect(await screen.findByText('UserInfo response')).toBeInTheDocument();
      expect(codeBlockFor('UserInfo response')).toHaveTextContent('"email": "user@test"');
      const [url, init] = fetchFn.mock.lastCall!;
      expect(url).toBe('https://op.test/userinfo');
      expect(init.method).toBe('GET');
      expect(init.headers.authorization).toBe('Bearer opaque-at');
    });

    it('surfaces a UserInfo failure and clears it when a retry succeeds', async () => {
      const { fetchFn } = await renderWithTokens({ access_token: 'opaque-at' });
      fetchFn.mockResolvedValueOnce(jsonResponse({}, 401));

      fireEvent.click(screen.getByTestId('userinfo'));
      expect(await screen.findByText('UserInfo request failed (HTTP 401).')).toBeInTheDocument();

      fetchFn.mockResolvedValueOnce(jsonResponse({ sub: 'user-1' }));
      fireEvent.click(screen.getByTestId('userinfo'));

      expect(await screen.findByText('UserInfo response')).toBeInTheDocument();
      expect(screen.queryByText('UserInfo request failed (HTTP 401).')).not.toBeInTheDocument();
    });

    it('disables UserInfo and sign-out when the provider advertises neither endpoint', async () => {
      await renderWithTokens(
        { access_token: 'opaque-at' },
        { runtime: { ...minimalRuntime, clientId: 'playground-spa' } },
      );

      expect(screen.getByTestId('userinfo')).toBeDisabled();
      expect(screen.getByTestId('logout')).toBeDisabled();
      expect(screen.getByTestId('reset')).toBeEnabled();
    });

    it('refreshes the tokens for the client that completed the callback', async () => {
      const { fetchFn } = await renderWithTokens({ access_token: 'at-1', refresh_token: 'rt-1' });
      fetchFn.mockResolvedValueOnce(jsonResponse({ access_token: 'at-2', refresh_token: 'rt-2' }));

      fireEvent.click(screen.getByTestId('refresh'));

      expect(await screen.findByText('at-2')).toBeInTheDocument();
      expect(codeBlockFor('refresh_token')).toHaveTextContent('rt-2');
      const body = lastRequestBody(fetchFn);
      expect(body.get('grant_type')).toBe('refresh_token');
      expect(body.get('refresh_token')).toBe('rt-1');
      expect(body.get('client_id')).toBe('tx-client');
    });

    it('surfaces a refresh failure and clears it when a retry succeeds', async () => {
      const { fetchFn } = await renderWithTokens({ access_token: 'at-1', refresh_token: 'rt-1' });
      fetchFn.mockResolvedValueOnce(
        jsonResponse({ error: 'invalid_grant', error_description: 'The token was revoked.' }, 400),
      );

      fireEvent.click(screen.getByTestId('refresh'));
      const failure = 'invalid_grant: The token was revoked.';
      expect(await screen.findByText(failure)).toBeInTheDocument();

      fetchFn.mockResolvedValueOnce(jsonResponse({ access_token: 'at-2', refresh_token: 'rt-2' }));
      fireEvent.click(screen.getByTestId('refresh'));

      expect(await screen.findByText('at-2')).toBeInTheDocument();
      expect(screen.queryByText(failure)).not.toBeInTheDocument();
    });

    it('signs out through RP-initiated logout with the id token hint', async () => {
      const idToken = jwt({ sub: 'user-1' });
      const { navigate } = await renderWithTokens({ access_token: 'opaque-at', id_token: idToken });

      fireEvent.click(screen.getByTestId('logout'));

      expect(navigate).toHaveBeenCalledOnce();
      const url = new URL(navigate.mock.calls[0]![0]);
      expect(url.origin + url.pathname).toBe('https://op.test/end-session');
      expect(url.searchParams.get('id_token_hint')).toBe(idToken);
      expect(url.searchParams.get('client_id')).toBe('tx-client');
      expect(url.searchParams.get('post_logout_redirect_uri')).toBe('https://ui.test/');
    });

    it('omits the id token hint when no id token was issued', async () => {
      const { navigate } = await renderWithTokens({ access_token: 'opaque-at' });

      fireEvent.click(screen.getByTestId('logout'));

      const url = new URL(navigate.mock.calls[0]![0]);
      expect(url.searchParams.has('id_token_hint')).toBe(false);
      expect(url.searchParams.get('client_id')).toBe('tx-client');
    });

    it('resets the explorer to its initial state', async () => {
      const { fetchFn } = await renderWithTokens({ access_token: 'opaque-at' });
      fetchFn.mockResolvedValueOnce(jsonResponse({ sub: 'user-1' }));
      fireEvent.click(screen.getByTestId('userinfo'));
      await screen.findByText('UserInfo response');

      fireEvent.click(screen.getByTestId('reset'));

      expect(screen.getByText(/No tokens yet/)).toBeInTheDocument();
      expect(screen.queryByText(/token_type/)).not.toBeInTheDocument();
      expect(screen.queryByText('UserInfo response')).not.toBeInTheDocument();
      expect([stepStatus(1), stepStatus(2), stepStatus(3)]).toEqual(['active', 'idle', 'idle']);
      expect(screen.getByTestId('userinfo')).toBeDisabled();
    });

    it('clears a callback error on reset', async () => {
      const deps = createDeps(vi.fn<FetchLike>());
      seedTransaction(deps);
      renderExplorer({ deps, initialSearch: '?code=the-code&state=wrong' });
      await screen.findByText(/possible CSRF/);

      fireEvent.click(screen.getByTestId('reset'));

      expect(screen.queryByText(/possible CSRF/)).not.toBeInTheDocument();
      expect(screen.getByText(/No tokens yet/)).toBeInTheDocument();
      expect(stepStatus(2)).toBe('idle');
    });
  });

  describe('live environment defaults', () => {
    function renderWithLiveDefaults(deps: FlowDeps) {
      render(
        <AppThemeProvider>
          <FlowExplorer runtime={runtime} deps={deps} />
        </AppThemeProvider>,
      );
    }

    it('completes a callback from the live url, then cleans it and signs out via the location', async () => {
      const assign = vi.fn<(url: string) => void>();
      const replaceState = vi.fn<History['replaceState']>();
      vi.stubGlobal('location', { origin: 'https://live.test', search: CALLBACK_SEARCH, assign });
      vi.stubGlobal('history', { replaceState });
      const deps = createDeps(
        vi.fn<FetchLike>().mockResolvedValue(jsonResponse({ access_token: 'opaque-at' })),
      );
      seedTransaction(deps);

      renderWithLiveDefaults(deps);

      expect(await screen.findByText(/token_type/)).toBeInTheDocument();
      expect(replaceState).toHaveBeenCalledWith({}, document.title, 'https://live.test/');
      expect(codeBlockFor('Redirect URI (registered)')).toHaveTextContent(
        'https://live.test/callback',
      );

      fireEvent.click(screen.getByTestId('logout'));

      expect(assign).toHaveBeenCalledOnce();
      const url = new URL(assign.mock.calls[0]![0]);
      expect(url.searchParams.get('post_logout_redirect_uri')).toBe('https://live.test/');
    });

    it('begins the flow by assigning the live location', async () => {
      const assign = vi.fn<(url: string) => void>();
      vi.stubGlobal('location', { origin: 'https://live.test', search: '', assign });

      renderWithLiveDefaults(createDeps(vi.fn<FetchLike>()));
      fireEvent.click(screen.getByTestId('begin'));

      await waitFor(() => expect(assign).toHaveBeenCalledOnce());
      const url = new URL(assign.mock.calls[0]![0]);
      expect(url.origin + url.pathname).toBe('https://op.test/authorize');
      expect(url.searchParams.get('redirect_uri')).toBe('https://live.test/callback');
    });
  });
});
