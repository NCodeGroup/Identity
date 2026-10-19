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
    auth: {
      accessToken: 'token',
      subject: 'admin',
      expiresAt: null,
      refreshToken: null,
      idToken: null,
    },
  });
  return render(
    <Provider store={store}>
      <ServersPage />
    </Provider>,
  );
}

type Call = {
  readonly url: string;
  readonly method: string;
  readonly body: string;
  readonly ifMatch: string | null;
};

function installFetch(handler: (url: string, method: string) => Response) {
  const calls: Call[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    calls.push({
      url: request.url,
      method: request.method,
      body: request.method === 'GET' ? '' : await request.clone().text(),
      ifMatch: request.headers.get('if-match'),
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

/** Serves only the server list; any write is unexpected. */
function listOnly(items: readonly unknown[]) {
  return (url: string, method: string) => {
    if (url.endsWith('/api/servers') && method === 'GET') {
      return jsonResponse({ items });
    }
    throw new Error(`Unexpected ${method} ${url}`);
  };
}

describe('ServersPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading indicator, then the empty state when no servers exist', async () => {
    installFetch(listOnly([]));

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent('No servers exist yet.');
  });

  it('reports a failure to load the server list', async () => {
    installFetch(() => jsonResponse({ title: 'Internal Server Error' }, 500));

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent('Failed to load servers.');
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

  it('returns from a server drill-in to the list', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/api/servers/root/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.endsWith('/api/servers') && method === 'GET') {
        return jsonResponse({ items: [server] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('server-row-root'));
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This server has no secrets yet.',
    );

    fireEvent.click(screen.getByTestId('server-detail-back'));

    expect(screen.getByTestId('create-server')).toBeInTheDocument();
    expect(screen.getByTestId('server-row-root')).toBeInTheDocument();
  });
});

describe('ServersPage (CRUD wiring)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates a server through the form dialog', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/servers') && method === 'POST') {
        return jsonResponse({ ...server, serverId: 'new', displayName: 'New server' }, 201);
      }
      if (url.endsWith('/api/servers')) {
        return jsonResponse({ items: [server] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Root server')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-server'));
    fireEvent.change(screen.getByTestId('server-display-name'), {
      target: { value: 'New server' },
    });
    fireEvent.click(screen.getByTestId('server-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('server-form-submit')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/servers');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({
      displayName: 'New server',
      settings: {},
    });
  });

  it('edits a server through the form dialog with an If-Match patch', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/servers/root') && method === 'PATCH') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/servers')) {
        return jsonResponse({ items: [server] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Root server')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('server-edit-root'));
    expect(screen.getByRole('dialog', { name: 'Edit server' })).toBeInTheDocument();
    expect(screen.getByTestId('server-display-name')).toHaveValue('Root server');
    fireEvent.change(await screen.findByTestId('server-display-name'), {
      target: { value: 'Renamed server' },
    });
    fireEvent.click(screen.getByTestId('server-form-submit'));

    await waitFor(() => {
      const patch = calls.find((c) => c.method === 'PATCH');
      expect(patch?.url).toBe('https://api.test/api/servers/root');
      expect(patch?.ifMatch).toBe('sc1');
      expect(JSON.parse(patch?.body ?? '[]')).toEqual([
        { op: 'replace', path: '/displayName', value: 'Renamed server' },
      ]);
    });
    await waitFor(() => expect(screen.queryByTestId('server-form-submit')).not.toBeInTheDocument());
  });

  it('discards the edit form on cancel', async () => {
    const calls = installFetch(listOnly([server]));

    renderPage();
    fireEvent.click(await screen.findByTestId('server-edit-root'));
    fireEvent.click(screen.getByTestId('server-form-cancel'));

    await waitFor(() => expect(screen.queryByTestId('server-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('ignores a submit fired while the form dialog is closing', async () => {
    const calls = installFetch(listOnly([server]));

    renderPage();
    fireEvent.click(await screen.findByTestId('create-server'));
    fireEvent.change(screen.getByTestId('server-display-name'), { target: { value: 'Late' } });
    fireEvent.click(screen.getByTestId('server-form-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not create anything.
    fireEvent.click(screen.getByTestId('server-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('server-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('deletes a server after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/servers/root') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/servers')) {
        return jsonResponse({ items: [server] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Root server')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('server-delete-root'));
    expect(await screen.findByTestId('confirm-accept')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() =>
      expect(calls.some((c) => c.method === 'DELETE' && c.url.endsWith('/api/servers/root'))).toBe(
        true,
      ),
    );
  });

  it('keeps the delete dialog open with the server problem detail when the delete is rejected', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/servers/root') && method === 'DELETE') {
        return jsonResponse({ detail: 'The root server cannot be deleted.' }, 409);
      }
      if (url.endsWith('/api/servers')) {
        return jsonResponse({ items: [server] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('server-delete-root'));
    expect(screen.getByRole('dialog', { name: 'Delete server' })).toHaveTextContent(
      'Delete “Root server”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The root server cannot be deleted.',
    );

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a confirm fired while the delete dialog is closing', async () => {
    const calls = installFetch(listOnly([server]));

    renderPage();
    fireEvent.click(await screen.findByTestId('server-delete-root'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('surfaces a server error in the form and keeps it open', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/servers') && method === 'POST') {
        return jsonResponse({ detail: 'A server with that identifier already exists.' }, 409);
      }
      if (url.endsWith('/api/servers')) {
        return jsonResponse({ items: [server] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Root server')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-server'));
    fireEvent.change(screen.getByTestId('server-display-name'), { target: { value: 'Dup' } });
    fireEvent.click(screen.getByTestId('server-form-submit'));

    expect(await screen.findByTestId('server-form-error')).toHaveTextContent(
      'A server with that identifier already exists.',
    );
    expect(screen.getByTestId('server-form-submit')).toBeInTheDocument();
  });
});
