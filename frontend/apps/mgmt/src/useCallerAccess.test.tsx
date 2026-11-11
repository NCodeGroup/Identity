import type { ReactNode } from 'react';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { BffRuntimeConfig } from '@ncode/config';
import { useCallerAccess } from './useCallerAccess.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

function renderAccess(isSignedIn: boolean) {
  const store = createAppStore({
    config: { runtime },
  });
  const wrapper = ({ children }: { readonly children: ReactNode }) => (
    <Provider store={store}>{children}</Provider>
  );
  return renderHook(() => useCallerAccess(isSignedIn), { wrapper });
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

/** Serves the caller's resource, its teams and its tenants; any other request is unexpected. */
function installFetch() {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    if (request.method === 'GET' && request.url.endsWith('/api/me')) {
      return jsonResponse({
        principalId: 'principal-1',
        isPlatformAdministrator: false,
        assignments: [],
      });
    }
    if (request.method === 'GET' && request.url.endsWith('/api/me/teams')) {
      return jsonResponse({
        items: [
          { teamId: 'support', concurrencyToken: 'tc1', displayName: 'Support', isDisabled: false },
        ],
      });
    }
    if (request.method === 'GET' && request.url.endsWith('/api/me/tenants')) {
      return jsonResponse({ items: [] });
    }
    throw new Error(`Unexpected ${request.method} ${request.url}`);
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('useCallerAccess', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('is unknown and sends no request while signed out', () => {
    const fetchMock = installFetch();

    const { result } = renderAccess(false);

    expect(result.current).toBeUndefined();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('is unknown while the queries are loading', async () => {
    installFetch();

    const { result } = renderAccess(true);

    expect(result.current).toBeUndefined();
    await waitFor(() => expect(result.current).toBeDefined());
  });

  it('combines the caller, its teams and its tenants once they answer', async () => {
    installFetch();

    const { result } = renderAccess(true);

    await waitFor(() =>
      expect(result.current).toEqual({
        isPlatformAdministrator: false,
        teamIds: ['support'],
        tenantIds: [],
      }),
    );
  });
});
