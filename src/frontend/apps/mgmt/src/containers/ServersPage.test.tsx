import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { ServersPage } from './ServersPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function renderPage() {
  const store = createAppStore({
    config: { runtime },
    auth: { accessToken: 'token', subject: 'admin', expiresAt: null, refreshToken: null },
  });
  return render(
    <Provider store={store}>
      <ServersPage />
    </Provider>,
  );
}

type Call = { readonly url: string; readonly method: string; readonly body: string };

function installFetch(handler: (url: string, method: string) => Response) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    calls.push({
      url: request.url,
      method: request.method,
      body: request.method === 'GET' ? '' : await request.clone().text(),
    });
    return handler(request.url, request.method);
  });
  vi.stubGlobal('fetch', fetchMock);
  return calls;
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

const server = { serverId: 'root', concurrencyToken: 'sc1', displayName: 'Root server' };

describe('ServersPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('lists servers and drills into a server to save its settings diff', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/servers')) {
        return jsonResponse({ items: [server] });
      }
      if (url.endsWith('/api/servers/root/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.endsWith('/api/servers/root/settings') && method === 'PATCH') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/servers/root/settings')) {
        return jsonResponse({ concurrencyToken: 'sc9', settings: { a: 1 } });
      }
      if (url.endsWith('/api/servers/root/effective-settings')) {
        return jsonResponse({ settings: { a: 1 } });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('server-row-root'));

    fireEvent.click(await screen.findByTestId('server-tab-settings'));
    const editor = await screen.findByTestId('settings-json');
    fireEvent.change(editor, { target: { value: '{"a": 2}' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    await waitFor(() => {
      const patch = calls.find((c) => c.method === 'PATCH');
      expect(patch?.url).toBe('https://api.test/api/servers/root/settings');
      expect(JSON.parse(patch?.body ?? '[]')).toEqual([{ op: 'replace', path: '/a', value: 2 }]);
    });
  });
});
