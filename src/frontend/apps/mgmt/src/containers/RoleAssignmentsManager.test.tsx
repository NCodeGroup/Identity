import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RoleList } from '@ncode/api';
import type { BffRuntimeConfig } from '@ncode/config';
import { RoleAssignmentsManager } from './RoleAssignmentsManager.js';
import { OwnerAccessTab } from './OwnerAccessTab.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const adminsList: RoleList = { role: 'admins', owner: { kind: 'team', teamId: 'acme' } };

function renderManager() {
  const store = createAppStore({ config: { runtime } });
  return render(
    <Provider store={store}>
      <RoleAssignmentsManager
        list={adminsList}
        title="Administrators"
        description="Administrators manage the team."
      />
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

function assignment(principalId: string) {
  return { assignmentId: `a-${principalId}`, principalId, roleName: 'TeamAdmin' };
}

describe('RoleAssignmentsManager', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('lists the principals that hold the role', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/admins') && method === 'GET') {
        return jsonResponse({ items: [assignment('alice'), assignment('bob')] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderManager();

    expect(await screen.findByTestId('admins-row-alice')).toBeInTheDocument();
    expect(screen.getByTestId('admins-row-bob')).toBeInTheDocument();
    expect(screen.getByText('Administrators manage the team.')).toBeInTheDocument();
  });

  it('grants the role to a trimmed principal id and refetches the list', async () => {
    let granted = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/admins') && method === 'POST') {
        granted = true;
        return jsonResponse(assignment('carol'), 201);
      }
      if (url.endsWith('/api/teams/acme/admins') && method === 'GET') {
        return jsonResponse({
          items: granted ? [assignment('alice'), assignment('carol')] : [assignment('alice')],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderManager();
    await screen.findByTestId('admins-row-alice');

    expect(screen.getByTestId('admins-grant')).toBeDisabled();
    fireEvent.change(screen.getByTestId('admins-principal'), { target: { value: '  carol  ' } });
    fireEvent.click(screen.getByTestId('admins-grant'));

    expect(await screen.findByTestId('admins-row-carol')).toBeInTheDocument();
    const post = calls.find((call) => call.method === 'POST');
    expect(post?.url).toMatch(/\/api\/teams\/acme\/admins$/);
    expect(JSON.parse(post?.body ?? '{}')).toEqual({ principalId: 'carol' });
    expect(screen.getByTestId('admins-principal')).toHaveValue('');
  });

  it('revokes the role after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/admins/alice') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/teams/acme/admins') && method === 'GET') {
        return jsonResponse({ items: [assignment('alice')] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderManager();
    fireEvent.click(await screen.findByTestId('admins-revoke-alice'));
    expect(screen.getByText('Revoke this role from alice?')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => {
      expect(calls.some((call) => call.method === 'DELETE')).toBe(true);
    });
    const remove = calls.find((call) => call.method === 'DELETE');
    expect(remove?.url).toMatch(/\/api\/teams\/acme\/admins\/alice$/);
  });

  it('keeps the role when the revoke is cancelled', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/admins') && method === 'GET') {
        return jsonResponse({ items: [assignment('alice')] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderManager();
    fireEvent.click(await screen.findByTestId('admins-revoke-alice'));
    expect(screen.getByText('Revoke this role from alice?')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('confirm-cancel'));

    await waitFor(() => {
      expect(screen.queryByText('Revoke this role from alice?')).not.toBeInTheDocument();
    });
    expect(calls.some((call) => call.method === 'DELETE')).toBe(false);
    expect(screen.getByTestId('admins-row-alice')).toBeInTheDocument();
  });

  it('shows the error when a revoke is refused', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/admins/alice') && method === 'DELETE') {
        return jsonResponse({ title: 'Forbidden', status: 403 }, 403);
      }
      if (url.endsWith('/api/teams/acme/admins') && method === 'GET') {
        return jsonResponse({ items: [assignment('alice')] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderManager();
    fireEvent.click(await screen.findByTestId('admins-revoke-alice'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('admins-error')).toHaveTextContent('Forbidden');
  });

  it('shows the error when a grant is refused', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/admins') && method === 'POST') {
        return jsonResponse({ title: 'Forbidden', status: 403 }, 403);
      }
      if (url.endsWith('/api/teams/acme/admins') && method === 'GET') {
        return jsonResponse({ items: [] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderManager();
    expect(await screen.findByText('No one holds this role.')).toBeInTheDocument();
    fireEvent.change(screen.getByTestId('admins-principal'), { target: { value: 'carol' } });
    fireEvent.click(screen.getByTestId('admins-grant'));

    expect(await screen.findByTestId('admins-error')).toHaveTextContent('Forbidden');
  });
});

describe('OwnerAccessTab', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  function renderTab(owner: Parameters<typeof OwnerAccessTab>[0]['owner']) {
    const store = createAppStore({ config: { runtime } });
    return render(
      <Provider store={store}>
        <OwnerAccessTab owner={owner} />
      </Provider>,
    );
  }

  it('lists the administrators and owners of a team', async () => {
    installFetch((url) => {
      if (url.endsWith('/api/teams/acme/admins')) {
        return jsonResponse({ items: [assignment('alice')] });
      }
      if (url.endsWith('/api/teams/acme/owners')) {
        return jsonResponse({ items: [{ ...assignment('bob'), roleName: 'Owner' }] });
      }
      throw new Error(`Unexpected ${url}`);
    });

    renderTab({ kind: 'team', teamId: 'acme' });

    expect(await screen.findByTestId('admins-row-alice')).toBeInTheDocument();
    expect(await screen.findByTestId('owners-row-bob')).toBeInTheDocument();
    expect(
      screen.getByText('Owners manage this team and everything beneath it.'),
    ).toBeInTheDocument();
  });

  it('lists only the owners of a client', async () => {
    installFetch((url) => {
      if (url.endsWith('/api/tenants/acme/clients/web/owners')) {
        return jsonResponse({ items: [{ ...assignment('bob'), roleName: 'Owner' }] });
      }
      throw new Error(`Unexpected ${url}`);
    });

    renderTab({ kind: 'client', tenantId: 'acme', clientId: 'web' });

    expect(await screen.findByTestId('owners-row-bob')).toBeInTheDocument();
    expect(screen.queryByTestId('admins-grant')).not.toBeInTheDocument();
  });
});
