import { describe, expect, it } from 'vitest';
import type { RawDiscovery } from './audit.js';
import { buildCoverageReport, type CoverageEntry } from './coverage.js';

/** A secure, authorization-code + PKCE baseline that advertises the recommended core surface. */
const baseline: RawDiscovery = {
  issuer: 'https://op.test',
  authorization_endpoint: 'https://op.test/authorize',
  token_endpoint: 'https://op.test/token',
  userinfo_endpoint: 'https://op.test/userinfo',
  end_session_endpoint: 'https://op.test/end-session',
  revocation_endpoint: 'https://op.test/revoke',
  introspection_endpoint: 'https://op.test/introspect',
  registration_endpoint: 'https://op.test/register',
  device_authorization_endpoint: 'https://op.test/device',
  jwks_uri: 'https://op.test/jwks',
  response_types_supported: ['code'],
  response_modes_supported: ['query', 'fragment', 'form_post'],
  grant_types_supported: [
    'authorization_code',
    'refresh_token',
    'client_credentials',
    'urn:ietf:params:oauth:grant-type:device_code',
  ],
  code_challenge_methods_supported: ['S256'],
  id_token_signing_alg_values_supported: ['PS256', 'ES256'],
  subject_types_supported: ['public'],
  scopes_supported: ['openid', 'profile', 'email'],
  token_endpoint_auth_methods_supported: ['client_secret_basic', 'client_secret_post', 'none'],
  claims_parameter_supported: true,
  request_parameter_supported: true,
  request_uri_parameter_supported: true,
  backchannel_logout_supported: true,
};

function entry(
  report: ReturnType<typeof buildCoverageReport>,
  id: string,
): CoverageEntry | undefined {
  return report.categories.flatMap((category) => category.entries).find((e) => e.id === id);
}

describe('buildCoverageReport — implemented baseline', () => {
  it('classifies advertised core endpoints and grants as implemented', () => {
    const report = buildCoverageReport(baseline);
    expect(entry(report, 'ep-authorize')?.status).toBe('implemented');
    expect(entry(report, 'ep-introspection')?.status).toBe('implemented');
    expect(entry(report, 'grant-code')?.status).toBe('implemented');
    expect(entry(report, 'grant-device-code')?.status).toBe('implemented');
    expect(entry(report, 'sec-pkce')?.status).toBe('implemented');
    expect(entry(report, 'mode-form-post')?.status).toBe('implemented');
  });

  it('totals sum to the number of catalog entries', () => {
    const report = buildCoverageReport(baseline);
    const entryCount = report.categories.reduce((sum, c) => sum + c.entries.length, 0);
    const totalCount =
      report.totals.implemented +
      report.totals.partial +
      report.totals.missing +
      report.totals['non-conformant'];
    expect(totalCount).toBe(entryCount);
  });
});

describe('buildCoverageReport — gaps surface as missing with backlog ids', () => {
  it('marks CIBA, PAR, DPoP, token exchange, and FAPI as missing against the baseline', () => {
    const report = buildCoverageReport(baseline);
    expect(entry(report, 'ep-ciba')?.status).toBe('missing');
    expect(entry(report, 'ep-ciba')?.backlog).toBe('F3');
    expect(entry(report, 'ep-par')?.status).toBe('missing');
    expect(entry(report, 'ep-par')?.backlog).toBe('F8');
    expect(entry(report, 'sec-dpop')?.status).toBe('missing');
    expect(entry(report, 'sec-dpop')?.backlog).toBe('F7');
    expect(entry(report, 'grant-token-exchange')?.status).toBe('missing');
    expect(entry(report, 'profile-fapi2')?.status).toBe('missing');
    expect(entry(report, 'profile-fapi2')?.detail).toContain('PAR');
  });
});

describe('buildCoverageReport — status transitions (gap → implemented, regression → non-conformant)', () => {
  it('lights up CIBA and FAPI once the discovery document advertises their prerequisites', () => {
    const report = buildCoverageReport({
      ...baseline,
      backchannel_authentication_endpoint: 'https://op.test/bc-authorize',
      grant_types_supported: [
        ...(baseline.grant_types_supported as string[]),
        'urn:openid:params:grant-type:ciba',
      ],
      pushed_authorization_request_endpoint: 'https://op.test/par',
      dpop_signing_alg_values_supported: ['ES256'],
    });
    expect(entry(report, 'ep-ciba')?.status).toBe('implemented');
    expect(entry(report, 'grant-ciba')?.status).toBe('implemented');
    expect(entry(report, 'profile-fapi2')?.status).toBe('implemented');
  });

  it('flags a regression to a prohibited grant as non-conformant', () => {
    const report = buildCoverageReport({
      ...baseline,
      grant_types_supported: [...(baseline.grant_types_supported as string[]), 'implicit'],
    });
    expect(entry(report, 'grant-insecure')?.status).toBe('non-conformant');
    expect(entry(report, 'grant-insecure')?.detail).toContain('implicit');
    expect(report.totals['non-conformant']).toBeGreaterThan(0);
  });

  it('marks PKCE partial when the weak plain method is also advertised', () => {
    const report = buildCoverageReport({
      ...baseline,
      code_challenge_methods_supported: ['S256', 'plain'],
    });
    expect(entry(report, 'sec-pkce')?.status).toBe('partial');
  });
});
