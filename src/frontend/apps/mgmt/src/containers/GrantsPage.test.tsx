import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { GrantsPage } from './GrantsPage.js';

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
      <GrantsPage tenantId="acme" />
    </Provider>,
  );
}

const grant = {
  grantId: 'g-1',
  grantType: 'authorization_code',
  tenantId: 'acme',
  clientId: 'web',
  subjectId: 'user-1',
  createdWhen: '2026-01-01T12:00:00Z',
  expiresWhen: '2099-01-01T12:00:00Z',
  revokedWhen: null,
  consumedWhen: null,
};

const otherGrant = { ...grant, grantId: 'g-2', subjectId: 'user-2', clientId: 'cli' };

function resourceServer(
  resourceServerId: string,
  name: string,
  identifier: string,
  isSystem: boolean,
) {
  return {
    tenantId: 'acme',
    resourceServerId,
    identifier,
    concurrencyToken: 'c-1',
    name,
    isSystem,
    isDisabled: false,
    scopes: [],
  };
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

/** The native input inside a MUI filter field (its `data-testid` lands on the root). */
function filterInput(testId: string): HTMLInputElement {
  return screen.getByTestId(testId).querySelector('input')!;
}

/** Serves the grant list, narrowed by the `subjectId`/`clientId` query parameters like the server. */
function listGrants(url: string): Response {
  const params = new URL(url).searchParams;
  const subjectId = params.get('subjectId');
  const clientId = params.get('clientId');
  return jsonResponse({
    items: [grant, otherGrant].filter(
      (g) => (!subjectId || g.subjectId === subjectId) && (!clientId || g.clientId === clientId),
    ),
  });
}

describe('GrantsPage (list states and filters)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading indicator, then the empty state when no grants match', async () => {
    installFetch(() => jsonResponse({ items: [] }));

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'No grants match the current filter.',
    );
  });

  it('reports a failure to load the grant list', async () => {
    installFetch(() => jsonResponse({ title: 'Internal Server Error' }, 500));

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent('Failed to load grants.');
  });

  it('applies the trimmed subject and client filters as list query parameters', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'GET') {
        return listGrants(url);
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-2')).toBeInTheDocument();

    fireEvent.change(filterInput('grant-filter-subject'), { target: { value: ' user-1 ' } });

    await waitFor(() => expect(screen.queryByTestId('grant-row-g-2')).not.toBeInTheDocument());
    expect(screen.getByTestId('grant-row-g-1')).toBeInTheDocument();
    const lastList = new URL(calls.at(-1)!.url);
    expect(lastList.searchParams.get('subjectId')).toBe('user-1');
    expect(lastList.searchParams.has('clientId')).toBe(false);
  });

  it('treats a whitespace-only filter as no filter and keeps bulk revoke disabled', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'GET') {
        return listGrants(url);
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-2')).toBeInTheDocument();

    fireEvent.change(filterInput('grant-filter-client'), { target: { value: '   ' } });

    expect(screen.getByTestId('revoke-matching-grants')).toBeDisabled();
    expect(calls.every((c) => new URL(c.url).search === '')).toBe(true);
  });
});

describe('GrantsPage (tenant-scoped revocation wiring)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('revokes a single grant after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants/g-1') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [grant] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-1')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('grant-revoke-g-1'));
    expect(screen.getByRole('dialog', { name: 'Revoke grant' })).toHaveTextContent(
      'Revoke grant “g-1”? The client must re-authorize.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() =>
      expect(
        calls.find((c) => c.method === 'DELETE' && c.url.includes('/grants/g-1')),
      ).toBeDefined(),
    );
  });

  it('keeps the revoke dialog open with the server problem detail when revocation fails', async () => {
    installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants/g-1') && method === 'DELETE') {
        return jsonResponse({ detail: 'The grant was already consumed.' }, 409);
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [grant] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('grant-revoke-g-1'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The grant was already consumed.',
    );

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a revoke confirm fired while the dialog is closing', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'GET') {
        return jsonResponse({ items: [grant] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('grant-revoke-g-1'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not revoke anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('bulk-revokes with the active filters once a filter is set', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'DELETE') {
        return jsonResponse({ revoked: 2 });
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [grant] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-1')).toBeInTheDocument();

    expect(screen.getByTestId('revoke-matching-grants')).toBeDisabled();
    fireEvent.change(screen.getByTestId('grant-filter-client').querySelector('input')!, {
      target: { value: 'web' },
    });
    fireEvent.click(screen.getByTestId('revoke-matching-grants'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => {
      const del = calls.find((c) => c.method === 'DELETE');
      expect(del?.url).toContain('clientId=web');
    });
  });

  it('bulk-revokes by subject and client together, naming both filters', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'DELETE') {
        return jsonResponse({ revoked: 1 });
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return listGrants(url);
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-2')).toBeInTheDocument();
    fireEvent.change(filterInput('grant-filter-subject'), { target: { value: 'user-1' } });
    fireEvent.change(filterInput('grant-filter-client'), { target: { value: 'web' } });
    await waitFor(() => expect(screen.queryByTestId('grant-row-g-2')).not.toBeInTheDocument());

    fireEvent.click(screen.getByTestId('revoke-matching-grants'));
    expect(screen.getByRole('dialog', { name: 'Revoke matching grants' })).toHaveTextContent(
      'Revoke all active grants for subject “user-1” and client “web”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    const del = new URL(calls.find((c) => c.method === 'DELETE')!.url);
    expect(del.pathname).toBe('/api/tenants/acme/grants');
    expect(del.searchParams.get('subjectId')).toBe('user-1');
    expect(del.searchParams.get('clientId')).toBe('web');
  });

  it('keeps the bulk-revoke dialog open with the server problem detail when it fails', async () => {
    installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'DELETE') {
        return jsonResponse({ title: 'At least one filter is required.' }, 400);
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return listGrants(url);
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-2')).toBeInTheDocument();
    fireEvent.change(filterInput('grant-filter-subject'), { target: { value: 'user-2' } });
    await waitFor(() => expect(screen.queryByTestId('grant-row-g-1')).not.toBeInTheDocument());

    fireEvent.click(screen.getByTestId('revoke-matching-grants'));
    expect(screen.getByRole('dialog', { name: 'Revoke matching grants' })).toHaveTextContent(
      'Revoke all active grants for subject “user-2”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'At least one filter is required.',
    );
  });

  it('ignores a bulk-revoke confirm fired while the dialog is closing', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'GET') {
        return listGrants(url);
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('grant-row-g-2')).toBeInTheDocument();
    fireEvent.change(filterInput('grant-filter-client'), { target: { value: 'cli' } });
    await waitFor(() => expect(screen.queryByTestId('grant-row-g-1')).not.toBeInTheDocument());

    fireEvent.click(screen.getByTestId('revoke-matching-grants'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not revoke anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });
});

describe('GrantsPage (initial access tokens)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('mints an initial access token and reveals its one-time value', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants/initial-access-tokens') && method === 'POST') {
        return jsonResponse(
          {
            initialAccessToken: 'iat-one-time',
            grantId: 'g-iat',
            tenantId: 'acme',
            description: 'Partner X',
            createdWhen: '2026-01-01T00:00:00Z',
            expiresWhen: null,
          },
          201,
        );
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    await screen.findByTestId('mint-initial-access-token');

    fireEvent.click(screen.getByTestId('mint-initial-access-token'));
    fireEvent.change(screen.getByTestId('iat-description'), { target: { value: 'Partner X' } });
    fireEvent.click(screen.getByTestId('iat-form-submit'));

    expect(await screen.findByTestId('reveal-iat-token')).toHaveTextContent('iat-one-time');
    expect(screen.getByTestId('reveal-iat-no-expiry')).toBeInTheDocument();
    const post = calls.find(
      (c) => c.method === 'POST' && c.url.includes('/grants/initial-access-tokens'),
    );
    expect(JSON.parse(post?.body ?? '{}')).toEqual({
      description: 'Partner X',
      lifetimeSeconds: null,
    });
  });

  it('mints an expiring token without a description and closes the reveal', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants/initial-access-tokens') && method === 'POST') {
        return jsonResponse(
          {
            initialAccessToken: 'iat-expiring',
            grantId: 'g-iat',
            tenantId: 'acme',
            description: null,
            createdWhen: '2026-01-01T00:00:00Z',
            expiresWhen: '2026-01-01T01:00:00Z',
          },
          201,
        );
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('mint-initial-access-token'));
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: '3600' } });
    fireEvent.click(screen.getByTestId('iat-form-submit'));

    expect(await screen.findByTestId('reveal-iat-expires')).toHaveTextContent(
      '2026-01-01T01:00:00Z',
    );
    expect(screen.getByTestId('reveal-iat-grant-id')).toHaveTextContent('g-iat');
    const post = calls.find((c) => c.method === 'POST');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({ description: null, lifetimeSeconds: 3600 });

    fireEvent.click(screen.getByTestId('reveal-iat-done'));
    await waitFor(() => expect(screen.queryByTestId('reveal-iat-token')).not.toBeInTheDocument());
  });

  it('surfaces a mint failure in the form and keeps it open', async () => {
    installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants/initial-access-tokens') && method === 'POST') {
        return jsonResponse({ detail: 'Dynamic registration is disabled for this tenant.' }, 403);
      }
      if (url.includes('/api/tenants/acme/grants')) {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('mint-initial-access-token'));
    fireEvent.click(screen.getByTestId('iat-form-submit'));

    expect(await screen.findByTestId('iat-form-error')).toHaveTextContent(
      'Dynamic registration is disabled for this tenant.',
    );
    expect(screen.queryByTestId('reveal-iat-token')).not.toBeInTheDocument();
  });

  it('discards the mint form on cancel', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/grants') && method === 'GET') {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('mint-initial-access-token'));
    fireEvent.click(screen.getByTestId('iat-form-cancel'));

    await waitFor(() => expect(screen.queryByTestId('iat-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('loads resource servers only while minting and scopes the token to the chosen one', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/tenants/acme/resource-servers') && method === 'GET') {
        return jsonResponse({
          items: [
            resourceServer('rs-system', 'Management API', 'urn:ncode:management', true),
            resourceServer('rs-orders', 'Orders', 'https://orders.example', false),
          ],
        });
      }
      if (url.includes('/api/tenants/acme/grants/initial-access-tokens') && method === 'POST') {
        return jsonResponse(
          {
            initialAccessToken: 'iat-scoped',
            grantId: 'g-iat',
            tenantId: 'acme',
            description: null,
            resourceServerId: 'rs-orders',
            createdWhen: '2026-01-01T00:00:00Z',
            expiresWhen: null,
          },
          201,
        );
      }
      if (url.includes('/api/tenants/acme/grants') && method === 'GET') {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByTestId('data-empty')).toBeInTheDocument();
    expect(calls.some((c) => c.url.includes('/resource-servers'))).toBe(false);

    fireEvent.click(screen.getByTestId('mint-initial-access-token'));
    fireEvent.mouseDown(await screen.findByRole('combobox', { name: /Resource server/ }));
    expect(
      within(screen.getByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['None', 'Orders (https://orders.example)']);
    fireEvent.click(screen.getByRole('option', { name: 'Orders (https://orders.example)' }));
    fireEvent.click(screen.getByTestId('iat-form-submit'));

    expect(await screen.findByTestId('reveal-iat-resource-server')).toHaveTextContent('Orders');
    const post = calls.find((c) => c.method === 'POST');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({
      description: null,
      lifetimeSeconds: null,
      resourceServerId: 'rs-orders',
    });
  });
});
