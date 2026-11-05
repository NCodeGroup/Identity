import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { TeamsPage } from './TeamsPage.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

function renderPage(onOpenTeam = vi.fn()) {
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
  render(
    <Provider store={store}>
      <TeamsPage onOpenTeam={onOpenTeam} />
    </Provider>,
  );
  return onOpenTeam;
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

const team = {
  teamId: 'support',
  concurrencyToken: 'tc1',
  displayName: 'Support',
  isDisabled: false,
};

/** Serves only the team list; any write is unexpected. */
function listOnly(items: readonly unknown[]) {
  return (url: string, method: string) => {
    if (url.endsWith('/api/teams') && method === 'GET') {
      return jsonResponse({ items });
    }
    throw new Error(`Unexpected ${method} ${url}`);
  };
}

describe('TeamsPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading indicator, then the empty state when no teams exist', async () => {
    installFetch(listOnly([]));

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent('No teams exist yet.');
  });

  it('reports a failure to load the team list', async () => {
    installFetch(() => jsonResponse({ title: 'Internal Server Error' }, 500));

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent('Failed to load teams.');
  });

  it('lists teams and opens the selected team', async () => {
    installFetch(listOnly([team]));

    const onOpenTeam = renderPage();
    fireEvent.click(await screen.findByTestId('team-row-support'));

    expect(onOpenTeam).toHaveBeenCalledWith('support');
  });
});

describe('TeamsPage (CRUD wiring)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates a team through the form dialog', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams') && method === 'POST') {
        return jsonResponse({ ...team, teamId: 'sales', displayName: 'Sales' }, 201);
      }
      if (url.endsWith('/api/teams')) {
        return jsonResponse({ items: [team] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Support')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-team'));
    fireEvent.change(screen.getByTestId('team-id'), { target: { value: 'sales' } });
    fireEvent.change(screen.getByTestId('team-display-name'), { target: { value: 'Sales' } });
    fireEvent.click(screen.getByTestId('team-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('team-form-submit')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/teams');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({
      teamId: 'sales',
      displayName: 'Sales',
      isDisabled: false,
      settings: {},
    });
  });

  it('edits a team through the form dialog with an If-Match patch', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/support') && method === 'PATCH') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/teams')) {
        return jsonResponse({ items: [team] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Support')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('team-edit-support'));
    expect(screen.getByRole('dialog', { name: 'Edit team' })).toBeInTheDocument();
    expect(screen.getByTestId('team-id')).toBeDisabled();
    expect(screen.getByTestId('team-display-name')).toHaveValue('Support');
    fireEvent.change(screen.getByTestId('team-display-name'), {
      target: { value: 'Renamed team' },
    });
    fireEvent.click(screen.getByTestId('team-disabled'));
    fireEvent.click(screen.getByTestId('team-form-submit'));

    await waitFor(() => {
      const patch = calls.find((c) => c.method === 'PATCH');
      expect(patch?.url).toBe('https://api.test/api/teams/support');
      expect(patch?.ifMatch).toBe('tc1');
      expect(JSON.parse(patch?.body ?? '[]')).toEqual([
        { op: 'replace', path: '/displayName', value: 'Renamed team' },
        { op: 'replace', path: '/isDisabled', value: true },
      ]);
    });
    await waitFor(() => expect(screen.queryByTestId('team-form-submit')).not.toBeInTheDocument());
  });

  it('discards the edit form on cancel', async () => {
    const calls = installFetch(listOnly([team]));

    renderPage();
    fireEvent.click(await screen.findByTestId('team-edit-support'));
    fireEvent.click(screen.getByTestId('team-form-cancel'));

    await waitFor(() => expect(screen.queryByTestId('team-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('ignores a submit fired while the form dialog is closing', async () => {
    const calls = installFetch(listOnly([team]));

    renderPage();
    fireEvent.click(await screen.findByTestId('create-team'));
    fireEvent.change(screen.getByTestId('team-id'), { target: { value: 'late' } });
    fireEvent.change(screen.getByTestId('team-display-name'), { target: { value: 'Late' } });
    fireEvent.click(screen.getByTestId('team-form-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not create anything.
    fireEvent.click(screen.getByTestId('team-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('team-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('deletes a team after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/support') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/teams')) {
        return jsonResponse({ items: [team] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Support')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('team-delete-support'));
    expect(await screen.findByTestId('confirm-accept')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() =>
      expect(calls.some((c) => c.method === 'DELETE' && c.url.endsWith('/api/teams/support'))).toBe(
        true,
      ),
    );
  });

  it('keeps the delete dialog open with the problem detail when the delete is rejected', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/support') && method === 'DELETE') {
        return jsonResponse({ detail: 'The team still has tenants.' }, 409);
      }
      if (url.endsWith('/api/teams')) {
        return jsonResponse({ items: [team] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('team-delete-support'));
    expect(screen.getByRole('dialog', { name: 'Delete team' })).toHaveTextContent(
      'Delete “Support”? Its tenants must be deleted first.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The team still has tenants.',
    );

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a confirm fired while the delete dialog is closing', async () => {
    const calls = installFetch(listOnly([team]));

    renderPage();
    fireEvent.click(await screen.findByTestId('team-delete-support'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('surfaces a server error in the form and keeps it open', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams') && method === 'POST') {
        return jsonResponse({ detail: 'A team with that identifier already exists.' }, 409);
      }
      if (url.endsWith('/api/teams')) {
        return jsonResponse({ items: [team] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Support')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-team'));
    fireEvent.change(screen.getByTestId('team-id'), { target: { value: 'dup' } });
    fireEvent.change(screen.getByTestId('team-display-name'), { target: { value: 'Dup' } });
    fireEvent.click(screen.getByTestId('team-form-submit'));

    expect(await screen.findByTestId('team-form-error')).toHaveTextContent(
      'A team with that identifier already exists.',
    );
    expect(screen.getByTestId('team-form-submit')).toBeInTheDocument();
  });
});
