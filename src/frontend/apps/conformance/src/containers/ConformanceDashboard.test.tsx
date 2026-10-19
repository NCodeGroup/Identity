import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import type { RuntimeConfig } from '@ncode/config';
import { ConformanceDashboard, type ConformanceDashboardProps } from './ConformanceDashboard.js';
import type { RawDiscovery } from '@ncode/conformance-core';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const secureDiscovery: RawDiscovery = {
  issuer: 'https://op.test',
  authorization_endpoint: 'https://op.test/authorize',
  token_endpoint: 'https://op.test/token',
  jwks_uri: 'https://op.test/jwks',
  response_types_supported: ['code'],
  grant_types_supported: ['authorization_code'],
  code_challenge_methods_supported: ['S256'],
  id_token_signing_alg_values_supported: ['PS256'],
  subject_types_supported: ['public'],
  scopes_supported: ['openid'],
};

// Advertises the implicit grant, so the Basic OP profile is blocked — distinguishable from the secure audit.
const insecureDiscovery: RawDiscovery = {
  ...secureDiscovery,
  response_types_supported: ['code', 'token'],
  grant_types_supported: ['authorization_code', 'implicit'],
};

function renderDashboard(
  loadDiscovery: Loader,
  props: Partial<Omit<ConformanceDashboardProps, 'loadDiscovery'>> = {},
) {
  return render(
    <AppThemeProvider>
      <ConformanceDashboard runtime={runtime} loadDiscovery={loadDiscovery} {...props} />
    </AppThemeProvider>,
  );
}

type Loader = (issuer: string, signal?: AbortSignal) => Promise<RawDiscovery>;

type Deferred<T> = {
  readonly promise: Promise<T>;
  readonly resolve: (value: T) => void;
  readonly reject: (reason: unknown) => void;
};

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('ConformanceDashboard', () => {
  it('audits the fetched discovery and shows profile readiness', async () => {
    const loadDiscovery = vi.fn(() => Promise.resolve(secureDiscovery));
    const { getByTestId, findByTestId } = renderDashboard(loadDiscovery);

    await findByTestId('summary');
    expect(getByTestId('profile-basic-op')).toHaveAttribute('data-status', 'pass');
    expect(getByTestId('profile-config-op')).toHaveAttribute('data-status', 'pass');
  });

  it('flags an insecure provider as blocked', async () => {
    const loadDiscovery = vi.fn(() => Promise.resolve(insecureDiscovery));
    const { findByTestId } = renderDashboard(loadDiscovery);

    const profile = await findByTestId('profile-basic-op');
    expect(profile).toHaveAttribute('data-status', 'fail');
  });

  it('derives the suite RP callback url from the alias', async () => {
    const loadDiscovery = vi.fn(() => Promise.resolve(secureDiscovery));
    const { getByTestId, findByTestId } = renderDashboard(loadDiscovery);

    await findByTestId('summary');
    expect(getByTestId('callback-url').textContent).toContain('/test/a/ncode/callback');
    expect(getByTestId('issuer').textContent).toBe('https://op.test');
  });

  it('renders the spec-coverage matrix with missing gaps surfaced', async () => {
    const loadDiscovery = vi.fn(() => Promise.resolve(secureDiscovery));
    const { getByTestId, getAllByTestId, findByTestId } = renderDashboard(loadDiscovery);

    await findByTestId('coverage-matrix');
    expect(getByTestId('coverage-summary')).toBeInTheDocument();
    // PAR is not advertised by the baseline, so its tracking backlog id surfaces as a gap.
    expect(getAllByTestId('backlog-F8').length).toBeGreaterThan(0);
  });

  it('organizes the panels into tabs and switches on click', async () => {
    const loadDiscovery = vi.fn(() => Promise.resolve(secureDiscovery));
    const { getByTestId, findByTestId } = renderDashboard(loadDiscovery);

    await findByTestId('summary');
    expect(getByTestId('tab-overview')).toHaveAttribute('aria-selected', 'true');

    const coverageTab = getByTestId('tab-coverage');
    fireEvent.click(coverageTab);
    expect(coverageTab).toHaveAttribute('aria-selected', 'true');
    expect(getByTestId('tab-overview')).toHaveAttribute('aria-selected', 'false');
  });

  it('shows an error when discovery cannot be loaded', async () => {
    const loadDiscovery = vi.fn(() => Promise.reject(new Error('boom')));
    const { findByText } = renderDashboard(loadDiscovery);
    expect(await findByText(/boom/)).toBeInTheDocument();
  });

  it('shows a generic error when the load rejects with a non-Error value', async () => {
    renderDashboard(vi.fn<Loader>().mockRejectedValue('offline'));

    expect(await screen.findByTestId('data-error')).toHaveTextContent(
      'Failed to load discovery metadata.',
    );
  });

  it('recovers from a failed load when a new loader is supplied', async () => {
    const failing = vi.fn<Loader>().mockRejectedValue(new Error('boom'));
    const { rerender } = renderDashboard(failing);
    await screen.findByText('boom');

    const working = vi.fn<Loader>().mockResolvedValue(secureDiscovery);
    rerender(
      <AppThemeProvider>
        <ConformanceDashboard runtime={runtime} loadDiscovery={working} />
      </AppThemeProvider>,
    );

    expect(await screen.findByTestId('summary')).toBeInTheDocument();
    expect(screen.queryByText('boom')).not.toBeInTheDocument();
    expect(working).toHaveBeenCalledWith('https://op.test', expect.any(AbortSignal));
  });

  it('reloads for a new issuer and ignores the superseded load', async () => {
    const superseded = deferred<RawDiscovery>();
    const loadDiscovery = vi
      .fn<Loader>()
      .mockReturnValueOnce(superseded.promise)
      .mockResolvedValueOnce({ ...secureDiscovery, issuer: 'https://op2.test' });
    const { rerender } = renderDashboard(loadDiscovery);
    const supersededSignal = loadDiscovery.mock.calls[0]![1]!;

    rerender(
      <AppThemeProvider>
        <ConformanceDashboard
          runtime={{ ...runtime, issuer: 'https://op2.test' }}
          loadDiscovery={loadDiscovery}
        />
      </AppThemeProvider>,
    );

    expect(supersededSignal.aborted).toBe(true);
    expect(await screen.findByTestId('summary')).toBeInTheDocument();
    expect(loadDiscovery).toHaveBeenLastCalledWith('https://op2.test', expect.any(AbortSignal));

    // A superseded load that fails late must neither surface its error nor mark the new issuer as loading again.
    // No UI change is expected, so there is nothing to await with findBy; act settles the rejection deterministically.
    await act(async () => superseded.reject(new Error('stale failure')));

    expect(screen.getByTestId('summary')).toBeInTheDocument();
    expect(screen.queryByText('stale failure')).not.toBeInTheDocument();
  });

  it('never displays a superseded load that succeeds after the current one', async () => {
    const loadA = deferred<RawDiscovery>();
    const loadB = deferred<RawDiscovery>();
    // The injected loader ignores its abort signal, so the superseded load for issuer A still resolves.
    const loadDiscovery = vi
      .fn<Loader>()
      .mockReturnValueOnce(loadA.promise)
      .mockReturnValueOnce(loadB.promise);
    const { rerender } = renderDashboard(loadDiscovery);

    rerender(
      <AppThemeProvider>
        <ConformanceDashboard
          runtime={{ ...runtime, issuer: 'https://op-b.test' }}
          loadDiscovery={loadDiscovery}
        />
      </AppThemeProvider>,
    );
    expect(loadDiscovery.mock.calls[0]![1]!.aborted).toBe(true);
    expect(loadDiscovery).toHaveBeenLastCalledWith('https://op-b.test', expect.any(AbortSignal));

    // B resolves first and A late. Both settle before React renders, so A's handler runs last: without the abort guard
    // its insecure audit would be the final write and be displayed under issuer B.
    loadB.resolve({ ...secureDiscovery, issuer: 'https://op-b.test' });
    loadA.resolve(insecureDiscovery);

    expect(await screen.findByTestId('profile-basic-op')).toHaveAttribute('data-status', 'pass');
    expect(screen.getByTestId('profile-config-op')).toHaveAttribute('data-status', 'pass');
    expect(screen.getByTestId('issuer')).toHaveTextContent('https://op-b.test');
  });

  it('derives the callback url from the edited suite base url and alias', async () => {
    renderDashboard(
      vi.fn(() => Promise.resolve(secureDiscovery)),
      {
        defaultSuiteBaseUrl: 'https://suite.test',
      },
    );
    await screen.findByTestId('summary');
    fireEvent.click(screen.getByTestId('tab-suite'));
    expect(screen.getByTestId('callback-url')).toHaveTextContent(
      'https://suite.test/test/a/ncode/callback',
    );

    fireEvent.change(screen.getByTestId('suite-base'), {
      target: { value: 'https://other-suite.test/' },
    });
    expect(screen.getByTestId('callback-url')).toHaveTextContent(
      'https://other-suite.test/test/a/ncode/callback',
    );

    fireEvent.change(screen.getByTestId('alias'), { target: { value: '' } });
    expect(screen.getByTestId('callback-url')).toHaveTextContent(
      'https://other-suite.test/test/a/<alias>/callback',
    );
  });

  it.each([
    { name: 'the configured client id', clientId: 'conformance-client' },
    { name: 'no client id when none is configured', clientId: undefined },
  ])('runs the live probes with $name', async ({ clientId }) => {
    const fetchStub = vi.fn<typeof fetch>((input) =>
      Promise.resolve(
        String(input) === 'https://op.test/jwks'
          ? Response.json({ keys: [{ kty: 'EC' }] })
          : Response.json({ error: 'invalid_grant' }, { status: 400 }),
      ),
    );
    vi.stubGlobal('fetch', fetchStub);
    renderDashboard(
      vi.fn(() => Promise.resolve(secureDiscovery)),
      {
        runtime: clientId ? { ...runtime, clientId } : runtime,
      },
    );
    await screen.findByTestId('summary');
    fireEvent.click(screen.getByTestId('tab-probes'));

    fireEvent.click(screen.getByTestId('run-probes'));

    expect(await screen.findByTestId('probe-summary')).toHaveTextContent('Verified 2');
    const tokenInit = fetchStub.mock.calls.find(([url]) => url === 'https://op.test/token')![1]!;
    expect(new URLSearchParams(String(tokenInit.body)).get('client_id')).toBe(clientId ?? null);
  });
});
