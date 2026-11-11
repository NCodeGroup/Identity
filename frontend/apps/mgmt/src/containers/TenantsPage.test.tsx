import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { BffRuntimeConfig } from '@ncode/config';
import { TenantsPage } from './TenantsPage.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

function renderPage(onOpenTenant = vi.fn(), addressPreviewDelayMs?: number) {
  const store = createAppStore({
    config: { runtime },
  });
  render(
    <Provider store={store}>
      <TenantsPage
        teamId="acme"
        onOpenTenant={onOpenTenant}
        {...(addressPreviewDelayMs !== undefined ? { addressPreviewDelayMs } : {})}
      />
    </Provider>,
  );
  return onOpenTenant;
}

const acme = {
  tenantId: 'acme-dev',
  teamId: 'acme',
  displayName: 'Acme',
  issuer: null,
  mtlsEndpointAliasAuthority: null,
  isDisabled: false,
  concurrencyToken: 'c1',
};

const globex = {
  tenantId: 'globex-dev',
  teamId: 'acme',
  displayName: 'Globex',
  issuer: 'https://globex.auth.test',
  mtlsEndpointAliasAuthority: 'https://globex.mtls.test',
  isDisabled: true,
  concurrencyToken: 'c2',
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

describe('TenantsPage (list states)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a loading indicator, then the empty state when no tenants exist', async () => {
    installFetch(() => jsonResponse({ items: [] }));

    renderPage();

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(await screen.findByTestId('data-empty')).toHaveTextContent('No tenants exist yet.');
  });

  it('reports a failure to load the tenant list', async () => {
    installFetch(() => jsonResponse({ title: 'Internal Server Error' }, 500));

    renderPage();

    expect(await screen.findByTestId('data-error')).toHaveTextContent('Failed to load tenants.');
  });

  it('filters the tenants by environment type', async () => {
    installFetch(() =>
      jsonResponse({
        items: [
          { ...acme, environment: { type: 'production', name: null, isProduction: true } },
          { ...globex, environment: { type: 'development', name: 'Dev', isProduction: false } },
        ],
      }),
    );

    renderPage();
    expect(await screen.findByTestId('tenant-row-acme-dev')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-row-globex-dev')).toBeInTheDocument();

    fireEvent.change(screen.getByTestId('tenant-environment-filter'), {
      target: { value: 'development' },
    });

    expect(screen.queryByTestId('tenant-row-acme-dev')).not.toBeInTheDocument();
    expect(screen.getByTestId('tenant-row-globex-dev')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-environment-globex-dev')).toHaveTextContent(
      'Dev (development)',
    );
  });

  it('crosses into a tenant when its row is selected', async () => {
    installFetch(() => jsonResponse({ items: [acme] }));

    const onOpenTenant = renderPage();
    fireEvent.click(await screen.findByTestId('tenant-row-acme-dev'));

    expect(onOpenTenant).toHaveBeenCalledWith('acme-dev');
  });
});

describe('TenantsPage (CRUD wiring)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  function addressHandler(address: unknown) {
    return (url: string, method: string) => {
      if (url.includes('/address')) {
        return jsonResponse(address);
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    };
  }

  it('previews the issuer and mTLS endpoint alias a new tenant would be given', async () => {
    const calls = installFetch(
      addressHandler({
        tenantId: 'new-co',
        issuer: 'https://new-co.auth.test',
        isIssuerDerived: true,
        mtlsEndpointAliasAuthority: 'https://new-co.mtls.test',
        isMtlsEndpointAliasAuthorityDerived: true,
        exists: false,
        failures: [],
      }),
    );

    renderPage(vi.fn(), 0);
    expect(await screen.findByText('Acme')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('create-tenant'));
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: 'new-co' } });

    expect(
      await screen.findByText('Leave empty to use https://new-co.auth.test.'),
    ).toBeInTheDocument();
    expect(screen.getByTestId('tenant-issuer')).toHaveAttribute(
      'placeholder',
      'https://new-co.auth.test',
    );
    expect(screen.getByText('Leave empty to use https://new-co.mtls.test.')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-mtls-endpoint-alias')).toHaveAttribute(
      'placeholder',
      'https://new-co.mtls.test',
    );
    expect(
      calls.some((c) => c.url === 'https://api.test/api/teams/acme/tenants/new-co/address'),
    ).toBe(true);
  });

  it('blocks creating a tenant whose ID is already taken', async () => {
    installFetch(
      addressHandler({
        tenantId: 'acme-dev',
        issuer: 'https://acme.auth.test',
        isIssuerDerived: true,
        mtlsEndpointAliasAuthority: null,
        isMtlsEndpointAliasAuthorityDerived: false,
        exists: true,
        failures: [],
      }),
    );

    renderPage(vi.fn(), 0);
    expect(await screen.findByText('Acme')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('create-tenant'));
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: 'acme-dev' } });
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'Acme 2' } });

    expect(await screen.findByText('A tenant with this ID already exists.')).toBeInTheDocument();
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
  });

  it('reports why a proposed issuer cannot be used', async () => {
    const calls = installFetch(
      addressHandler({
        tenantId: 'new-co',
        issuer: 'https://taken.test',
        isIssuerDerived: false,
        mtlsEndpointAliasAuthority: null,
        isMtlsEndpointAliasAuthorityDerived: false,
        exists: false,
        failures: [
          {
            detail: "Another tenant already has the issuer 'https://taken.test'.",
            isConflict: true,
          },
        ],
      }),
    );

    renderPage(vi.fn(), 0);
    expect(await screen.findByText('Acme')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('create-tenant'));
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: 'new-co' } });
    fireEvent.change(screen.getByTestId('tenant-issuer'), {
      target: { value: 'https://taken.test' },
    });

    expect(await screen.findByTestId('tenant-address-failures')).toHaveTextContent(
      "Another tenant already has the issuer 'https://taken.test'.",
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    expect(
      calls.some(
        (c) =>
          c.url ===
          'https://api.test/api/teams/acme/tenants/new-co/address?issuer=https%3A%2F%2Ftaken.test',
      ),
    ).toBe(true);
  });

  it('creates a tenant through the form dialog', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/tenants') && method === 'POST') {
        return jsonResponse({ ...acme, tenantId: 'new-co', displayName: 'New Co' }, 201);
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-tenant'));
    expect(screen.getByTestId('tenant-id')).toBeEnabled();
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: 'new-co' } });
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'New Co' } });
    fireEvent.click(screen.getByTestId('tenant-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('tenant-form-submit')).not.toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/teams/acme/tenants');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({
      tenantId: 'new-co',
      issuer: null,
      mtlsEndpointAliasAuthority: null,
      displayName: 'New Co',
      isDisabled: false,
      settings: {},
    });
  });

  it('edits a tenant through the form dialog with an If-Match JSON Patch', async () => {
    let renamed = false;
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme-dev') && method === 'PATCH') {
        renamed = true;
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({
          items: [renamed ? { ...acme, displayName: 'Acme Corp' } : acme, globex],
        });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('tenant-edit-acme-dev'));

    expect(screen.getByRole('dialog', { name: 'Edit tenant' })).toBeInTheDocument();
    expect(screen.getByTestId('tenant-id')).toHaveValue('acme-dev');
    expect(screen.getByTestId('tenant-id')).toBeDisabled();
    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Acme');
    expect(screen.getByTestId('tenant-issuer')).toHaveValue('');
    expect(screen.getByTestId('tenant-mtls-endpoint-alias')).toHaveValue('');
    expect(screen.getByTestId('tenant-form-submit')).toHaveTextContent('Save');

    fireEvent.change(screen.getByTestId('tenant-display-name'), {
      target: { value: 'Acme Corp' },
    });
    fireEvent.change(screen.getByTestId('tenant-issuer'), {
      target: { value: 'https://acme.auth.test' },
    });
    fireEvent.change(screen.getByTestId('tenant-mtls-endpoint-alias'), {
      target: { value: 'https://acme.mtls.test' },
    });
    fireEvent.click(screen.getByRole('switch', { name: 'Disabled' }));
    fireEvent.click(screen.getByTestId('tenant-form-submit'));

    expect(await screen.findByText('Acme Corp')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTestId('tenant-form-submit')).not.toBeInTheDocument());
    const patch = calls.find((c) => c.method === 'PATCH');
    expect(patch?.url).toBe('https://api.test/api/tenants/acme-dev');
    expect(patch?.ifMatch).toBe('c1');
    expect(JSON.parse(patch?.body ?? '[]')).toEqual([
      { op: 'replace', path: '/issuer', value: 'https://acme.auth.test' },
      { op: 'replace', path: '/mtlsEndpointAliasAuthority', value: 'https://acme.mtls.test' },
      { op: 'replace', path: '/displayName', value: 'Acme Corp' },
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('prefills the edit form from a tenant with an issuer and discards it on cancel', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/tenants') && method === 'GET') {
        return jsonResponse({ items: [acme, globex] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('tenant-edit-globex-dev'));

    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Globex');
    expect(screen.getByTestId('tenant-issuer')).toHaveValue('https://globex.auth.test');
    expect(screen.getByTestId('tenant-mtls-endpoint-alias')).toHaveValue(
      'https://globex.mtls.test',
    );
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();

    fireEvent.click(screen.getByTestId('tenant-form-cancel'));

    await waitFor(() => expect(screen.queryByTestId('tenant-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it("re-judges an existing tenant's address when only its mTLS endpoint alias changes", async () => {
    const calls = installFetch((url, method) => {
      if (url.includes('/address')) {
        return jsonResponse({
          tenantId: 'globex-dev',
          issuer: 'https://globex.auth.test',
          isIssuerDerived: false,
          mtlsEndpointAliasAuthority: 'https://globex.auth.test',
          isMtlsEndpointAliasAuthorityDerived: false,
          exists: true,
          failures: [
            {
              detail:
                'The mTLS endpoint alias must be served on a host other than the issuer host.',
              isConflict: false,
            },
          ],
        });
      }
      if (url.endsWith('/api/teams/acme/tenants') && method === 'GET') {
        return jsonResponse({ items: [acme, globex] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage(vi.fn(), 0);
    fireEvent.click(await screen.findByTestId('tenant-edit-globex-dev'));
    fireEvent.change(screen.getByTestId('tenant-mtls-endpoint-alias'), {
      target: { value: 'https://globex.auth.test' },
    });

    expect(await screen.findByTestId('tenant-address-failures')).toHaveTextContent(
      'The mTLS endpoint alias must be served on a host other than the issuer host.',
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    expect(
      calls.some(
        (c) =>
          c.url ===
          'https://api.test/api/tenants/globex-dev/address?issuer=https%3A%2F%2Fglobex.auth.test&mtlsEndpointAliasAuthority=https%3A%2F%2Fglobex.auth.test',
      ),
    ).toBe(true);
  });

  it('ignores a submit fired while the form dialog is closing', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/tenants') && method === 'GET') {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-tenant'));
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: 'late-co' } });
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'Late Co' } });
    fireEvent.click(screen.getByTestId('tenant-form-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not create anything.
    fireEvent.click(screen.getByTestId('tenant-form-submit'));

    await waitFor(() => expect(screen.queryByTestId('tenant-form-submit')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('deletes a tenant after confirmation', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme-dev') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('tenant-delete-acme-dev'));
    expect(await screen.findByTestId('confirm-accept')).toBeInTheDocument();
    expect(screen.getByRole('dialog', { name: 'Delete tenant' })).toHaveTextContent(
      'Delete “Acme”? This cannot be undone.',
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() =>
      expect(
        calls.some((c) => c.method === 'DELETE' && c.url.endsWith('/api/tenants/acme-dev')),
      ).toBe(true),
    );
  });

  it('keeps the delete dialog open with the server problem detail when the delete is rejected', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/tenants/acme-dev') && method === 'DELETE') {
        return jsonResponse({ detail: 'The tenant still has clients.' }, 409);
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('tenant-delete-acme-dev'));
    fireEvent.click(screen.getByTestId('confirm-accept'));

    expect(await screen.findByTestId('confirm-error')).toHaveTextContent(
      'The tenant still has clients.',
    );
    expect(screen.getByTestId('confirm-accept')).toBeEnabled();

    fireEvent.click(screen.getByTestId('confirm-cancel'));
    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
  });

  it('ignores a confirm fired while the delete dialog is closing', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/tenants') && method === 'GET') {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    fireEvent.click(await screen.findByTestId('tenant-delete-acme-dev'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    // The dialog stays mounted for its exit transition; a stray click on it must not delete anything.
    fireEvent.click(screen.getByTestId('confirm-accept'));

    await waitFor(() => expect(screen.queryByTestId('confirm-accept')).not.toBeInTheDocument());
    expect(calls.every((c) => c.method === 'GET')).toBe(true);
  });

  it('surfaces a server error in the form and keeps it open', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/tenants') && method === 'POST') {
        return jsonResponse(
          { detail: 'A tenant with the specified identifier already exists.' },
          409,
        );
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('create-tenant'));
    fireEvent.change(screen.getByTestId('tenant-id'), { target: { value: 'acme-dev' } });
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'Dup' } });
    fireEvent.click(screen.getByTestId('tenant-form-submit'));

    expect(await screen.findByTestId('tenant-form-error')).toHaveTextContent(
      'A tenant with the specified identifier already exists.',
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeInTheDocument();
  });
});

describe('TenantsPage (promotion)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  const report = {
    teamId: 'acme',
    sourceTenantId: 'acme-dev',
    targetTenantId: 'globex-dev',
    isApplied: false,
    changes: [{ resourceType: 'client', resourceId: 'web', action: 'created' }],
    notes: [],
    errors: [],
  };

  function chooseTenants() {
    fireEvent.change(screen.getByTestId('promotion-source'), { target: { value: 'acme-dev' } });
    fireEvent.change(screen.getByTestId('promotion-target'), { target: { value: 'globex-dev' } });
  }

  it('enables Promote only when the team has two tenants', async () => {
    installFetch(() => jsonResponse({ items: [acme] }));

    renderPage();
    expect(await screen.findByText('Acme')).toBeInTheDocument();
    expect(screen.getByTestId('promote-tenant')).toBeDisabled();
  });

  it('previews a promotion between two tenants of the team', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/promotions/preview') && method === 'POST') {
        return jsonResponse(report);
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme, globex] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Globex')).toBeInTheDocument();
    expect(screen.getByTestId('promote-tenant')).toBeEnabled();

    fireEvent.click(screen.getByTestId('promote-tenant'));
    chooseTenants();
    fireEvent.click(screen.getByTestId('promotion-preview'));

    expect(await screen.findByTestId('promotion-changes')).toHaveTextContent('web');
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.url).toBe('https://api.test/api/teams/acme/promotions/preview');
    expect(JSON.parse(post?.body ?? '{}')).toEqual({
      sourceTenantId: 'acme-dev',
      targetTenantId: 'globex-dev',
      environmentSettings: [],
    });
  });

  it('promotes after a preview and reports the promotion as applied', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/promotions/preview') && method === 'POST') {
        return jsonResponse(report);
      }
      if (url.endsWith('/api/teams/acme/promotions') && method === 'POST') {
        return jsonResponse({ ...report, isApplied: true });
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme, globex] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Globex')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('promote-tenant'));
    chooseTenants();
    fireEvent.click(screen.getByTestId('promotion-preview'));
    await waitFor(() => expect(screen.getByTestId('promotion-promote')).toBeEnabled());
    fireEvent.click(screen.getByTestId('promotion-promote'));

    expect(await screen.findByTestId('promotion-applied')).toHaveTextContent('Promoted.');
    expect(
      calls.some(
        (c) => c.method === 'POST' && c.url === 'https://api.test/api/teams/acme/promotions',
      ),
    ).toBe(true);
  });

  it('shows the detail of a refusal naming a limit', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/promotions/preview') && method === 'POST') {
        return jsonResponse({ detail: 'Changing the limit max_clients needs authority.' }, 403);
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme, globex] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Globex')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('promote-tenant'));
    chooseTenants();
    fireEvent.click(screen.getByTestId('promotion-preview'));

    expect(await screen.findByTestId('promotion-error')).toHaveTextContent(
      'Changing the limit max_clients needs authority.',
    );
  });

  it('lists the errors of a promotion that cannot be applied', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/promotions/preview') && method === 'POST') {
        return jsonResponse(report);
      }
      if (url.endsWith('/api/teams/acme/promotions') && method === 'POST') {
        return jsonResponse(
          {
            detail: 'The promotion cannot be applied; preview it to see why.',
            errors: [{ resourceType: 'client', resourceId: 'web', detail: 'It is host managed.' }],
          },
          409,
        );
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme, globex] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Globex')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('promote-tenant'));
    chooseTenants();
    fireEvent.click(screen.getByTestId('promotion-preview'));
    await waitFor(() => expect(screen.getByTestId('promotion-promote')).toBeEnabled());
    fireEvent.click(screen.getByTestId('promotion-promote'));

    expect(await screen.findByTestId('promotion-error')).toHaveTextContent(
      'The promotion cannot be applied; preview it to see why. client web: It is host managed.',
    );
  });

  it('closes the promotion dialog on cancel', async () => {
    installFetch(() => jsonResponse({ items: [acme, globex] }));

    renderPage();
    expect(await screen.findByText('Globex')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('promote-tenant'));
    expect(screen.getByTestId('promotion-cancel')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('promotion-cancel'));

    await waitFor(() => expect(screen.queryByTestId('promotion-cancel')).not.toBeInTheDocument());
  });

  it('lists only the promotion errors that carry a detail', async () => {
    installFetch((url, method) => {
      if (url.endsWith('/api/teams/acme/promotions/preview') && method === 'POST') {
        return jsonResponse(report);
      }
      if (url.endsWith('/api/teams/acme/promotions') && method === 'POST') {
        return jsonResponse(
          {
            detail: 'The promotion cannot be applied; preview it to see why.',
            errors: [
              null,
              { resourceType: 'client', resourceId: 'web' },
              { resourceType: 'client', resourceId: 'api', detail: 'It is host managed.' },
            ],
          },
          409,
        );
      }
      if (url.endsWith('/api/teams/acme/tenants')) {
        return jsonResponse({ items: [acme, globex] });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });

    renderPage();
    expect(await screen.findByText('Globex')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('promote-tenant'));
    chooseTenants();
    fireEvent.click(screen.getByTestId('promotion-preview'));
    await waitFor(() => expect(screen.getByTestId('promotion-promote')).toBeEnabled());
    fireEvent.click(screen.getByTestId('promotion-promote'));

    expect(await screen.findByTestId('promotion-error')).toHaveTextContent(
      /^The promotion cannot be applied; preview it to see why\. client api: It is host managed\.$/,
    );
  });
});
