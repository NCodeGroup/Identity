import { fireEvent, render, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import type { RawDiscovery } from '../audit.js';
import type { ProbeReport } from '../probes.js';
import { ProbePanel } from './ProbePanel.js';

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
      detail: 'Not advertised (gap: F8).',
      reference: 'RFC 9126',
    },
  ],
  totals: { verified: 1, failed: 0, skipped: 1 },
};

function renderPanel(runProbes: () => Promise<ProbeReport>) {
  return render(
    <AppThemeProvider>
      <ProbePanel raw={discovery} runProbes={runProbes} probeFetch={vi.fn()} />
    </AppThemeProvider>,
  );
}

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
});
