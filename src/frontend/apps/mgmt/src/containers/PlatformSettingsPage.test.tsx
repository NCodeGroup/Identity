import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { BffRuntimeConfig } from '@ncode/config';
import { PlatformSettingsPage } from './PlatformSettingsPage.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

function renderPage() {
  const store = createAppStore({
    config: { runtime },
  });
  render(
    <Provider store={store}>
      <PlatformSettingsPage />
    </Provider>,
  );
}

function installFetch(handler: (url: string, method: string) => Response) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const request = input as Request;
    return handler(request.url, request.method);
  });
  vi.stubGlobal('fetch', fetchMock);
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });
}

describe('PlatformSettingsPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows the platform settings against the effective settings', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/platform/settings')) {
        return jsonResponse({ concurrencyToken: 'st1', settings: { a: 1 } });
      }
      if (url.endsWith('/settings/limits')) {
        return jsonResponse({ names: [], canChange: true });
      }
      if (url.endsWith('/api/platform/effective-settings')) {
        return jsonResponse({ settings: { a: 1, b: true } });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();

    expect(screen.getByRole('heading', { name: 'Platform settings' })).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByTestId('settings-json')).toHaveValue(JSON.stringify({ a: 1 }, null, 2));
      expect(screen.getByTestId('effective-settings-json')).toHaveValue(
        JSON.stringify({ a: 1, b: true }, null, 2),
      );
    });
  });

  it('shows the limits read-only when the caller cannot change them', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/platform/settings')) {
        return jsonResponse({ concurrencyToken: 'st1', settings: { a: 1, max_tenants: 5 } });
      }
      if (url.endsWith('/settings/limits')) {
        return jsonResponse({ names: ['max_tenants'], canChange: false });
      }
      if (url.endsWith('/api/platform/effective-settings')) {
        return jsonResponse({ settings: { a: 1, max_tenants: 5 } });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();

    await waitFor(() => {
      expect(screen.getByTestId('settings-json')).toHaveValue(JSON.stringify({ a: 1 }, null, 2));
      expect(screen.getByTestId('locked-settings-json')).toHaveValue(
        JSON.stringify({ max_tenants: 5 }, null, 2),
      );
    });
  });
});
