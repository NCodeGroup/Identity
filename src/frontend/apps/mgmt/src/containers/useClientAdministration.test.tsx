import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore } from '@ncode/store';
import type { BffRuntimeConfig } from '@ncode/config';
import type { ClientResource } from '@ncode/api';
import type { ClientFormValues } from '@ncode/ui';
import { useClientAdministration } from './useClientAdministration.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: 'https://api.test',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const bot: ClientResource = {
  clientId: 'bot',
  tenantId: 'control',
  concurrencyToken: 'c1',
  isDisabled: true,
  resourceServerId: null,
  principalId: 'principal-bot',
  owningNodeType: 'team',
  owningNodeId: 'acme',
};

const clients: readonly ClientResource[] = [bot];

type Harness = {
  readonly createClient: (values: ClientFormValues) => Promise<unknown>;
};

/** Renders the hook's state and wires each of its actions to a button. */
function HookHarness({ createClient }: Harness) {
  const admin = useClientAdministration({ clients, createClient });
  return (
    <div>
      <button onClick={admin.openCreate}>open-create</button>
      <button onClick={() => admin.openEdit('bot')}>open-edit</button>
      <button onClick={() => admin.openEdit('missing')}>open-edit-missing</button>
      <button onClick={() => admin.openDelete('bot')}>open-delete</button>
      <button onClick={() => admin.openDelete('missing')}>open-delete-missing</button>
      <button onClick={admin.close}>close</button>
      <button onClick={() => void admin.handleSubmit({ isDisabled: false })}>submit</button>
      <button onClick={() => void admin.handleDelete()}>delete</button>
      <button onClick={() => admin.select('bot')}>select</button>
      <button onClick={() => admin.select('missing')}>select-missing</button>
      <button onClick={admin.clearSelection}>clear-selection</button>
      <output data-testid="form-open">{String(admin.formOpen)}</output>
      <output data-testid="is-editing">{String(admin.isEditing)}</output>
      <output data-testid="initial">{JSON.stringify(admin.formInitial)}</output>
      <output data-testid="delete-target">{admin.deleteTarget?.clientId ?? ''}</output>
      <output data-testid="form-error">{admin.formError ?? ''}</output>
      <output data-testid="delete-error">{admin.deleteError ?? ''}</output>
      <output data-testid="selected">{admin.selected?.clientId ?? ''}</output>
      <output data-testid="submitting">{String(admin.isSubmitting)}</output>
      <output data-testid="deleting">{String(admin.isDeleting)}</output>
    </div>
  );
}

function renderHarness(createClient: Harness['createClient'] = () => Promise.resolve({})) {
  const store = createAppStore({ config: { runtime } });
  return render(
    <Provider store={store}>
      <HookHarness createClient={createClient} />
    </Provider>,
  );
}

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

function click(name: string) {
  fireEvent.click(screen.getByRole('button', { name }));
}

describe('useClientAdministration', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('opens the create form with empty values and creates through the page-supplied call', async () => {
    const createClient = vi.fn(() => Promise.resolve({}));
    renderHarness(createClient);

    click('open-create');

    expect(screen.getByTestId('form-open')).toHaveTextContent('true');
    expect(screen.getByTestId('is-editing')).toHaveTextContent('false');
    expect(screen.getByTestId('initial')).toHaveTextContent('{"isDisabled":false}');

    click('submit');

    await waitFor(() => expect(screen.getByTestId('form-open')).toHaveTextContent('false'));
    expect(createClient).toHaveBeenCalledWith({ isDisabled: false });
  });

  it('keeps the create form open and reports a refused create', async () => {
    renderHarness(() => Promise.reject({ status: 409, data: { title: 'Conflict' } }));

    click('open-create');
    click('submit');

    await waitFor(() => expect(screen.getByTestId('form-error')).not.toBeEmptyDOMElement());
    expect(screen.getByTestId('form-open')).toHaveTextContent('true');
    expect(screen.getByTestId('submitting')).toHaveTextContent('false');

    click('close');
    expect(screen.getByTestId('form-open')).toHaveTextContent('false');

    click('open-create');
    expect(screen.getByTestId('form-error')).toBeEmptyDOMElement();
  });

  it('edits a client through its own tenant with its concurrency token', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/control/clients/bot') && method === 'PATCH') {
        return jsonResponse({ ...bot, isDisabled: false, concurrencyToken: 'c2' });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });
    renderHarness();

    click('open-edit');

    expect(screen.getByTestId('is-editing')).toHaveTextContent('true');
    expect(screen.getByTestId('initial')).toHaveTextContent('{"isDisabled":true}');

    click('submit');

    await waitFor(() => expect(screen.getByTestId('form-open')).toHaveTextContent('false'));
    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({
      url: 'https://api.test/api/tenants/control/clients/bot',
      method: 'PATCH',
      ifMatch: 'c1',
    });
    expect(JSON.parse(calls[0]!.body)).toEqual([
      { op: 'replace', path: '/isDisabled', value: false },
    ]);
  });

  it('reports a refused edit and keeps the form open', async () => {
    installFetch(() => jsonResponse({ title: 'Precondition failed' }, 412));
    renderHarness();

    click('open-edit');
    click('submit');

    await waitFor(() => expect(screen.getByTestId('form-error')).not.toBeEmptyDOMElement());
    expect(screen.getByTestId('form-open')).toHaveTextContent('true');
  });

  it('ignores an edit or delete of a client that is not listed', () => {
    renderHarness();

    click('open-edit-missing');
    click('open-delete-missing');

    expect(screen.getByTestId('form-open')).toHaveTextContent('false');
    expect(screen.getByTestId('delete-target')).toBeEmptyDOMElement();
  });

  it('deletes a client through its own tenant', async () => {
    const calls = installFetch((url, method) => {
      if (url.endsWith('/api/tenants/control/clients/bot') && method === 'DELETE') {
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected ${method} ${url}`);
    });
    renderHarness();

    click('open-delete');

    expect(screen.getByTestId('delete-target')).toHaveTextContent('bot');

    click('delete');

    await waitFor(() => expect(screen.getByTestId('delete-target')).toBeEmptyDOMElement());
    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({
      url: 'https://api.test/api/tenants/control/clients/bot',
      method: 'DELETE',
    });
  });

  it('reports a refused delete, keeps the target and clears the error when it reopens', async () => {
    installFetch(() => jsonResponse({ title: 'Forbidden' }, 403));
    renderHarness();

    click('open-delete');
    click('delete');

    await waitFor(() => expect(screen.getByTestId('delete-error')).not.toBeEmptyDOMElement());
    expect(screen.getByTestId('delete-target')).toHaveTextContent('bot');

    click('close');
    expect(screen.getByTestId('delete-target')).toBeEmptyDOMElement();

    click('open-delete');
    expect(screen.getByTestId('delete-error')).toBeEmptyDOMElement();
  });

  it('does nothing when asked to delete with no delete confirmation open', () => {
    const calls = installFetch(() => {
      throw new Error('Unexpected request');
    });
    renderHarness();

    click('delete');

    expect(calls).toHaveLength(0);
  });

  it('submits nothing when no form is open', async () => {
    const createClient = vi.fn(() => Promise.resolve({}));
    const calls = installFetch(() => {
      throw new Error('Unexpected request');
    });
    renderHarness(createClient);

    click('submit');

    await waitFor(() => expect(screen.getByTestId('submitting')).toHaveTextContent('false'));
    expect(createClient).not.toHaveBeenCalled();
    expect(calls).toHaveLength(0);
  });

  it('selects a listed client for drill-in and clears the selection', () => {
    renderHarness();

    click('select');
    expect(screen.getByTestId('selected')).toHaveTextContent('bot');

    click('clear-selection');
    expect(screen.getByTestId('selected')).toBeEmptyDOMElement();

    click('select-missing');
    expect(screen.getByTestId('selected')).toBeEmptyDOMElement();
  });
});
