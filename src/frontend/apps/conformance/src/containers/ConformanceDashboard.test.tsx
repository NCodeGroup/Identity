import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AppThemeProvider } from '@ncode/ui';
import type { RuntimeConfig } from '@ncode/config';
import { ConformanceDashboard } from './ConformanceDashboard.js';
import type { RawDiscovery } from '../audit.js';

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

function renderDashboard(
  loadDiscovery: (issuer: string, signal?: AbortSignal) => Promise<RawDiscovery>,
) {
  return render(
    <AppThemeProvider>
      <ConformanceDashboard runtime={runtime} loadDiscovery={loadDiscovery} />
    </AppThemeProvider>,
  );
}

describe('ConformanceDashboard', () => {
  it('audits the fetched discovery and shows profile readiness', async () => {
    const loadDiscovery = vi.fn(() => Promise.resolve(secureDiscovery));
    const { getByTestId, findByTestId } = renderDashboard(loadDiscovery);

    await findByTestId('summary');
    expect(getByTestId('profile-basic-op')).toHaveAttribute('data-status', 'pass');
    expect(getByTestId('profile-config-op')).toHaveAttribute('data-status', 'pass');
  });

  it('flags an insecure provider as blocked', async () => {
    const loadDiscovery = vi.fn(() =>
      Promise.resolve({
        ...secureDiscovery,
        response_types_supported: ['code', 'token'],
        grant_types_supported: ['authorization_code', 'implicit'],
      }),
    );
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
    // CIBA is not advertised by the baseline, so its tracking backlog id surfaces as a gap.
    expect(getAllByTestId('backlog-F3').length).toBeGreaterThan(0);
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
});
