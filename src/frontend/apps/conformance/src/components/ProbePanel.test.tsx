import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import type { ProbeFetch, ProbeReport, RawDiscovery } from '@ncode/conformance-core';
import { ProbePanel, type ProbePanelProps } from './ProbePanel.js';

const discovery: RawDiscovery = { issuer: 'https://op.test', jwks_uri: 'https://op.test/jwks' };

const report: ProbeReport = {
  results: [
    {
      id: 'probe-jwks',
      feature: 'JWKS endpoint serves a valid key set',
      outcome: 'verified',
      detail: 'JWKS advertises 1 key(s).',
      reference: 'RFC 7517',
    },
    {
      id: 'probe-par',
      feature: 'PAR endpoint accepts a pushed request',
      outcome: 'skipped',
      detail: 'Not advertised (gap: #46).',
      reference: 'RFC 9126',
    },
  ],
  totals: { verified: 1, failed: 0, skipped: 1 },
};

type RunProbes = NonNullable<ProbePanelProps['runProbes']>;

function renderPanel(runProbes: () => Promise<ProbeReport>) {
  return render(
    <AppThemeProvider>
      <ProbePanel raw={discovery} runProbes={runProbes} probeFetch={vi.fn()} />
    </AppThemeProvider>,
  );
}

function renderPanelWith(props: Omit<ProbePanelProps, 'raw'> & { readonly raw?: RawDiscovery }) {
  return render(
    <AppThemeProvider>
      <ProbePanel raw={discovery} {...props} />
    </AppThemeProvider>,
  );
}

type Deferred<T> = {
  readonly promise: Promise<T>;
  readonly resolve: (value: T) => void;
};

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((res) => {
    resolve = res;
  });
  return { promise, resolve };
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('ProbePanel', () => {
  it('runs probes on demand and renders the outcomes', async () => {
    const runProbes = vi.fn(() => Promise.resolve(report));
    const { getByTestId, findByTestId } = renderPanel(runProbes);

    fireEvent.click(getByTestId('run-probes'));

    await findByTestId('probe-results');
    expect(runProbes).toHaveBeenCalledOnce();
    expect(getByTestId('probe-verified')).toBeInTheDocument();
    expect(getByTestId('probe-skipped')).toBeInTheDocument();
  });

  it('shows an error when the probe run throws', async () => {
    const runProbes = vi.fn(() => Promise.reject(new Error('network down')));
    const { getByTestId, findByTestId } = renderPanel(runProbes);

    fireEvent.click(getByTestId('run-probes'));

    const error = await findByTestId('probe-error');
    expect(error.textContent).toContain('network down');
  });

  it('does not run probes until the button is clicked', () => {
    const runProbes = vi.fn(() => Promise.resolve(report));
    renderPanel(runProbes);
    expect(runProbes).not.toHaveBeenCalled();
  });

  it('waits for completion', async () => {
    const runProbes = vi.fn(() => Promise.resolve(report));
    const { getByTestId } = renderPanel(runProbes);
    fireEvent.click(getByTestId('run-probes'));
    await waitFor(() => expect(getByTestId('probe-summary')).toBeInTheDocument());
  });

  it('disables the button and shows progress while the probes run', async () => {
    const run = deferred<ProbeReport>();
    renderPanel(() => run.promise);
    const button = screen.getByTestId('run-probes');

    fireEvent.click(button);

    expect(button).toBeDisabled();
    expect(button).toHaveTextContent('Running…');

    run.resolve(report);

    expect(await screen.findByTestId('probe-results')).toBeInTheDocument();
    expect(button).toBeEnabled();
    expect(button).toHaveTextContent('Run live probes');
  });

  it('renders every outcome with its detail, reference, and the summary totals', async () => {
    const mixed: ProbeReport = {
      results: [
        ...report.results,
        {
          id: 'probe-userinfo-401',
          feature: 'UserInfo rejects an unauthenticated request',
          outcome: 'failed',
          detail: 'Expected 401, got HTTP 200.',
          reference: 'RFC 6750 §3.1',
        },
      ],
      totals: { verified: 1, failed: 1, skipped: 1 },
    };
    renderPanel(() => Promise.resolve(mixed));

    fireEvent.click(screen.getByTestId('run-probes'));

    const summary = await screen.findByTestId('probe-summary');
    expect(summary).toHaveTextContent('Verified 1');
    expect(summary).toHaveTextContent('Failed 1');
    expect(summary).toHaveTextContent('Skipped 1');
    expect(screen.getByTestId('probe-failed')).toHaveTextContent('Failed');
    expect(screen.getByText('Expected 401, got HTTP 200.')).toBeInTheDocument();
    expect(screen.getByText('RFC 6750 §3.1')).toBeInTheDocument();
  });

  it('shows a generic message when the run rejects with a non-Error value', async () => {
    renderPanel(vi.fn<() => Promise<ProbeReport>>().mockRejectedValue('offline'));

    fireEvent.click(screen.getByTestId('run-probes'));

    expect(await screen.findByTestId('probe-error')).toHaveTextContent('Probe run failed.');
  });

  it('clears a previous error when a rerun succeeds', async () => {
    const runProbes = vi
      .fn<() => Promise<ProbeReport>>()
      .mockRejectedValueOnce(new Error('network down'))
      .mockResolvedValueOnce(report);
    renderPanel(runProbes);
    fireEvent.click(screen.getByTestId('run-probes'));
    await screen.findByTestId('probe-error');

    fireEvent.click(screen.getByTestId('run-probes'));

    expect(await screen.findByTestId('probe-results')).toBeInTheDocument();
    expect(screen.queryByTestId('probe-error')).not.toBeInTheDocument();
  });

  it('passes the configured client id to the probe run', async () => {
    const runProbes = vi.fn<RunProbes>().mockResolvedValue(report);
    const probeFetch = vi.fn<ProbeFetch>();
    renderPanelWith({ clientId: 'conformance-client', runProbes, probeFetch });

    fireEvent.click(screen.getByTestId('run-probes'));

    await screen.findByTestId('probe-results');
    expect(runProbes).toHaveBeenCalledExactlyOnceWith(discovery, probeFetch, {
      clientId: 'conformance-client',
    });
  });

  it('runs without a client id when none is configured', async () => {
    const runProbes = vi.fn<RunProbes>().mockResolvedValue(report);
    renderPanelWith({ runProbes, probeFetch: vi.fn<ProbeFetch>() });

    fireEvent.click(screen.getByTestId('run-probes'));

    await screen.findByTestId('probe-results');
    expect(runProbes.mock.calls[0]![2]).toStrictEqual({});
  });

  describe('default browser fetch', () => {
    function stubFetch() {
      const fetchStub = vi.fn<typeof fetch>(() =>
        Promise.resolve(new Response('{}', { status: 200 })),
      );
      vi.stubGlobal('fetch', fetchStub);
      return fetchStub;
    }

    /** Captures the fetch the panel hands the runner, so the test can drive it directly. */
    async function captureProbeFetch(): Promise<ProbeFetch> {
      const runProbes = vi.fn<RunProbes>().mockResolvedValue(report);
      renderPanelWith({ runProbes });
      fireEvent.click(screen.getByTestId('run-probes'));
      await screen.findByTestId('probe-results');
      return runProbes.mock.calls[0]![1];
    }

    it('forwards every supplied request option to the global fetch', async () => {
      const fetchStub = stubFetch();
      const probeFetch = await captureProbeFetch();
      const controller = new AbortController();
      const headers = { 'content-type': 'application/x-www-form-urlencoded' };

      await probeFetch('https://op.test/token', {
        method: 'POST',
        headers,
        body: '',
        signal: controller.signal,
      });

      // An empty body is still forwarded; only an absent one is omitted.
      expect(fetchStub).toHaveBeenCalledExactlyOnceWith('https://op.test/token', {
        method: 'POST',
        headers,
        body: '',
        signal: controller.signal,
      });
    });

    it('omits the request options that are absent', async () => {
      const fetchStub = stubFetch();
      const probeFetch = await captureProbeFetch();

      await probeFetch('https://op.test/jwks');
      await probeFetch('https://op.test/userinfo', { method: 'GET', signal: undefined });

      expect(fetchStub.mock.calls[0]).toStrictEqual(['https://op.test/jwks', {}]);
      expect(fetchStub.mock.calls[1]).toStrictEqual([
        'https://op.test/userinfo',
        { method: 'GET' },
      ]);
    });

    it('drives the built-in probes against the live endpoints', async () => {
      const fetchStub = vi.fn<typeof fetch>((input) => {
        const url = String(input);
        if (url === 'https://op.test/jwks') {
          return Promise.resolve(Response.json({ keys: [{ kty: 'RSA' }] }));
        }
        if (url === 'https://op.test/token') {
          return Promise.resolve(Response.json({ error: 'invalid_grant' }, { status: 400 }));
        }
        return Promise.resolve(new Response(null, { status: 401 }));
      });
      vi.stubGlobal('fetch', fetchStub);
      renderPanelWith({
        raw: {
          ...discovery,
          token_endpoint: 'https://op.test/token',
          userinfo_endpoint: 'https://op.test/userinfo',
        },
        clientId: 'conformance-client',
      });

      fireEvent.click(screen.getByTestId('run-probes'));

      const summary = await screen.findByTestId('probe-summary');
      expect(summary).toHaveTextContent('Verified 3');
      expect(summary).toHaveTextContent('Failed 0');
      // PAR is not advertised, so it is skipped without a request.
      expect(summary).toHaveTextContent('Skipped 1');
      expect(fetchStub).toHaveBeenCalledTimes(3);
      const tokenInit = fetchStub.mock.calls.find(([url]) => url === 'https://op.test/token')![1]!;
      expect(new URLSearchParams(String(tokenInit.body)).get('client_id')).toBe(
        'conformance-client',
      );
    });
  });
});
