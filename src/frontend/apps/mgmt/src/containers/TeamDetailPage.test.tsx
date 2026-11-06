import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { BffRuntimeConfig } from '@ncode/config';
import { TeamDetailPage } from './TeamDetailPage.js';
import type { TeamSurface } from '../routes.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

function renderPage(surface: TeamSurface = 'tenants', onOpenTeam = vi.fn()) {
  const store = createAppStore({
    config: { runtime },
  });
  return render(
    <Provider store={store}>
      <TeamDetailPage
        teamId="acme"
        surface={surface}
        onOpenTenant={vi.fn()}
        onOpenTeam={onOpenTeam}
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

describe('TeamDetailPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("lists the team's tenants on the tenants surface", async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({
          items: [
            {
              tenantId: 'globex',
              displayName: 'Globex',
              issuer: null,
              mtlsEndpointAliasAuthority: null,
              isDisabled: false,
              concurrencyToken: 'c1',
            },
          ],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();

    expect(screen.getByRole('heading', { name: 'Team' })).toBeInTheDocument();
    expect(screen.getByText('acme')).toBeInTheDocument();
    expect(await screen.findByText('Globex')).toBeInTheDocument();
  });

  it("lists the team's child teams on the teams surface and opens one", async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/teams')) {
        return jsonResponse({
          items: [
            {
              teamId: 'acme-web',
              parentTeamId: 'acme',
              displayName: 'Acme Web',
              isDisabled: false,
              concurrencyToken: 'c1',
            },
          ],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    const onOpenTeam = vi.fn();
    renderPage('teams', onOpenTeam);

    expect(await screen.findByText('Acme Web')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('team-row-acme-web'));

    expect(onOpenTeam).toHaveBeenCalledWith('acme-web');
  });

  it('saves edited team settings as a JSON Patch diff on the settings tab', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/settings') && method === 'PATCH') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/teams/acme/settings')) {
        return jsonResponse({ concurrencyToken: 'sc9', settings: { a: 1 } });
      }
      if (url.endsWith('/settings/limits')) {
        return jsonResponse({ names: [], canChange: true });
      }
      if (url.endsWith('/api/teams/acme/effective-settings')) {
        return jsonResponse({ settings: { a: 1, issuer: 'https://op.test' } });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage('settings');

    const editor = await screen.findByTestId('settings-json');
    fireEvent.change(editor, { target: { value: '{"a": 2}' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    await waitFor(() => {
      const patch = calls.find((c) => c.method === 'PATCH');
      expect(patch?.url).toBe('https://api.test/api/teams/acme/settings');
      expect(JSON.parse(patch?.body ?? '[]')).toEqual([{ op: 'replace', path: '/a', value: 2 }]);
    });
  });

  it('shows the team usage ledger on the usage tab and narrows it to a chosen range', async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/api/teams/acme/usage?from=2026-10-21&to=2026-10-21')) {
        return jsonResponse({
          teamId: 'acme',
          from: '2026-10-21',
          to: '2026-10-21',
          entries: [{ tenantId: 'globex', day: '2026-10-21', meter: 'requests', count: 3 }],
        });
      }
      if (url.includes('/api/teams/acme/usage?from=2026-10-21')) {
        return jsonResponse({
          teamId: 'acme',
          from: '2026-10-21',
          to: '2026-10-21',
          entries: [],
        });
      }
      if (url.endsWith('/api/teams/acme/usage')) {
        return jsonResponse({
          teamId: 'acme',
          from: '2026-09-21',
          to: '2026-10-21',
          entries: [
            { tenantId: 'globex', day: '2026-10-21', meter: 'requests', count: 42 },
            { tenantId: 'globex', day: '2026-10-21', meter: 'token_requests', count: 7 },
          ],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage('usage');

    expect(await screen.findByTestId('usage-row-globex-2026-10-21')).toHaveTextContent(
      'globex2026-10-21427',
    );
    expect(screen.getByTestId('usage-range')).toHaveTextContent('2026-09-21 to 2026-10-21');

    fireEvent.change(screen.getByTestId('usage-from'), { target: { value: '2026-10-21' } });

    expect(await screen.findByTestId('data-empty')).toHaveTextContent(
      'No usage recorded in this range.',
    );
    expect(
      calls.some((c) => c.url === 'https://api.test/api/teams/acme/usage?from=2026-10-21'),
    ).toBe(true);

    fireEvent.change(screen.getByTestId('usage-to'), { target: { value: '2026-10-21' } });

    expect(await screen.findByTestId('usage-row-globex-2026-10-21')).toHaveTextContent(
      'globex2026-10-213',
    );
    expect(
      calls.some(
        (c) => c.url === 'https://api.test/api/teams/acme/usage?from=2026-10-21&to=2026-10-21',
      ),
    ).toBe(true);
  });

  it('shows the server refusal of an invalid usage range', async () => {
    installFetch((url, method) => {
      if (url.includes('/api/teams/acme/usage')) {
        return jsonResponse(
          {
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: {
              from: ["The range must run forward from 'from' to 'to' and span at most 366 days."],
            },
          },
          400,
        );
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage('usage');

    expect(await screen.findByTestId('data-error')).toBeInTheDocument();
  });
});
