import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { BffRuntimeConfig } from '@ncode/config';
import type { MachineClientOwner } from '@ncode/api';
import { MachineClientsPage } from './MachineClientsPage.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const teamOwner: MachineClientOwner = { kind: 'team', teamId: 'acme' };

function renderPage(owner: MachineClientOwner = teamOwner) {
  const store = createAppStore({
    config: { runtime },
  });
  return render(
    <Provider store={store}>
      <MachineClientsPage owner={owner} />
    </Provider>,
  );
}

const bot = {
  clientId: 'bot',
  tenantId: 'control',
  concurrencyToken: 'c1',
  isDisabled: false,
  resourceServerId: null,
  principalId: 'principal-bot',
  owningNodeType: 'team',
  owningNodeId: 'acme',
};

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
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

/** Serves only the team's machine-client list; any write is unexpected. */
function listOnly(items: readonly unknown[]) {
  return (url: string, method: string) => {
    if (url.endsWith('/api/teams/acme/clients') && method === 'GET') {
      return jsonResponse({ items });
    }
    throw new Error(`Unexpected ${method} ${url}`);
  };
}

describe('MachineClientsPage (list states and scope)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading indicator, then the empty state when the team has no machine clients', async () => {
    installFetch(listOnly([]));

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This team has no machine clients yet.',
    );
    expect(screen.getByTestId('machine-clients-description')).toHaveTextContent(
      'holds roles only within this team.',
    );
  });

  it('lists the platform machine clients and describes the platform scope', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/platform/clients') && method === 'GET') {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage({ kind: 'platform' });

    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'The platform has no machine clients yet.',
    );
    expect(screen.getByTestId('machine-clients-description')).toHaveTextContent(
      'holds roles only within the platform.',
    );
    expect(calls[0]?.url).toBe('https://api.test/api/platform/clients');
  });

  it('reports a failure to load the machine client list', async () => {
    installFetch(() => jsonResponse({ title: 'Forbidden' }, 403));

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent(
      'Failed to load machine clients.',
    );
  });

  it('drills into a machine client through the control-plane tenant and returns to the list', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/capabilities')) {
        return jsonResponse({ secretTypes: [] });
      }
      if (url.endsWith('/api/tenants/control/clients/bot/secrets')) {
        return jsonResponse({ concurrencyToken: 'c1', secrets: [] });
      }
      if (url.endsWith('/api/teams/acme/clients') && method === 'GET') {
        return jsonResponse({ items: [bot] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('client-row-bot'));

    expect(screen.getByRole('heading', { name: 'bot' })).toBeInTheDocument();
    expect(screen.getByText('Client · Active · Machine client of team acme')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'This client has no secrets yet.',
    );

    fireEvent.click(screen.getByTestId('client-detail-back'));

    expect(screen.getByTestId('create-machine-client')).toBeInTheDocument();
    expect(screen.getByTestId('client-row-bot')).toBeInTheDocument();
  });
});

describe('MachineClientsPage (owner-scoped create and tenant-scoped changes)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates a machine client under the team path', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/clients') && method === 'POST') {
        return jsonResponse({ ...bot, clientId: 'new' }, 201);
      }
      if (url.endsWith('/api/teams/acme/clients')) {
        return jsonResponse({ items: [bot] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('bot')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-machine-client'));
    expect(screen.getByRole('dialog', { name: 'Create machine client' })).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('client-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('client-form-submit')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/teams/acme/clients');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({ isDisabled: false, settings: {} });
  });

  it('keeps the form open with the server problem detail when the create is rejected', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/clients') && method === 'POST') {
        return jsonResponse({ detail: 'Client quota exceeded.' }, 409);
      }
      if (url.endsWith('/api/teams/acme/clients')) {
        return jsonResponse({ items: [bot] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('bot')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-machine-client'));
    fireEvent.click(screen.getByTestId('client-form-submit'));

    expect(await screen.findByTestId('client-form-error')).toHaveTextContent(
      'Client quota exceeded.',
    );
    expect(screen.getByTestId('client-form-submit')).toBeInTheDocument();
  });

  it('edits a machine client through the control-plane tenant with an If-Match JSON Patch', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/control/clients/bot') && method === 'PATCH') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/teams/acme/clients')) {
        return jsonResponse({ items: [bot] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('client-edit-bot'));

    expect(screen.getByRole('dialog', { name: 'Edit machine client' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('switch', { name: 'Disabled' }));
    fireEvent.click(screen.getByTestId('client-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('client-form-submit')).not.toBeInTheDocument());
    const patch = calls.find((c) => c.method === 'PATCH');
    expect(patch?.url).toBe('https://api.test/api/tenants/control/clients/bot');
    expect(patch?.ifMatch).toBe('c1');
    expect(JSON.parse(patch?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('discards the edit form on cancel without sending a write', async () => {
    const calls = installFetch(listOnly([bot]));

    renderPage();
    fireEvent.click(await screen.findByTestId('client-edit-bot'));
    fireEvent.click(screen.getByTestId('client-form-cancel'));

    await waitFor(() => expect(screen.queryByTestId('client-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('ignores a submit fired while the form dialog is closing', async () => {
    const calls = installFetch(listOnly([bot]));

    renderPage();
    fireEvent.click(await screen.findByTestId('create-machine-client'));
    fireEvent.click(screen.getByTestId('client-form-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not create anything.
    fireEvent.click(screen.getByTestId('client-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('client-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('deletes a machine client through the control-plane tenant after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/control/clients/bot') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/teams/acme/clients')) {
        return jsonResponse({ items: [bot] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('bot')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('client-delete-bot'));
    fireEvent.click(await screen.findByTestId('confirm-accept'));

    await waitFor(() =>
      expect(
        calls.some(
          (c) => c.method === 'DELETE' && c.url.endsWith('/api/tenants/control/clients/bot'),
        ),
      ).toBe(true),
    );
  });

  it('keeps the delete dialog open with the server problem detail when the delete is rejected', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants/control/clients/bot') && method === 'DELETE') {
        return jsonResponse({ detail: 'The client still has active grants.' }, 409);
      }
      if (url.endsWith('/api/teams/acme/clients')) {
        return jsonResponse({ items: [bot] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('client-delete-bot'));
    expect(screen.getByRole('dialog', { name: 'Delete machine client' })).toHaveTextContent(
      'Delete machine client “bot”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The client still has active grants.',
    );

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a confirm fired while the delete dialog is closing', async () => {
    const calls = installFetch(listOnly([bot]));

    renderPage();
    fireEvent.click(await screen.findByTestId('client-delete-bot'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });
});
