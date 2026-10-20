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

describe('buildCoverageReport — fully advertised surface', () => {
  const advertised: RawDiscovery = {
    ...baseline,
    token_endpoint_auth_methods_supported: ['private_key_jwt', 'tls_client_auth'],
    tls_client_certificate_bound_access_tokens: true,
    response_modes_supported: ['query', 'query.jwt'],
    subject_types_supported: ['public', 'pairwise'],
    acr_values_supported: ['urn:mace:incommon:iap:silver'],
    prompt_values_supported: ['none', 'login', 'consent'],
    id_token_encryption_alg_values_supported: ['RSA-OAEP-256'],
    frontchannel_logout_supported: true,
    pushed_authorization_request_endpoint: 'https://op.test/par',
  };

  it('classifies every advertised optional capability as implemented', () => {
    const report = buildCoverageReport(advertised);
    for (const id of [
      'auth-private-key-jwt',
      'auth-mtls',
      'sec-mtls-bound',
      'sec-jarm',
      'oidc-subject-pairwise',
      'oidc-acr',
      'oidc-prompt',
      'oidc-idtoken-encryption',
      'oidc-frontchannel-logout',
    ]) {
      expect(entry(report, id)?.status, id).toBe('implemented');
    }
    expect(entry(report, 'auth-mtls')?.detail).toBe('Advertised: tls_client_auth.');
    expect(entry(report, 'oidc-prompt')?.detail).toBe('Advertised: none, login, consent.');
  });

  it('reports a client-secret method as missing when only asymmetric methods are advertised', () => {
    const report = buildCoverageReport(advertised);
    expect(entry(report, 'auth-secret')?.status).toBe('missing');
  });

  it('marks a non-advertised response mode missing when the mode list is present', () => {
    const report = buildCoverageReport(advertised);
    expect(entry(report, 'mode-query')?.status).toBe('implemented');
    expect(entry(report, 'mode-fragment')?.status).toBe('missing');
    expect(entry(report, 'mode-fragment')?.detail).toBe(
      "'fragment' is not advertised in response_modes_supported.",
    );
  });

  it('accepts mTLS-bound tokens as the FAPI 2.0 sender-constraining mechanism', () => {
    const report = buildCoverageReport(advertised);
    expect(entry(report, 'profile-fapi2')?.status).toBe('implemented');
  });

  it('classifies JARM from an authorization signing alg alone', () => {
    const report = buildCoverageReport({
      ...baseline,
      authorization_signing_alg_values_supported: ['PS256'],
    });
    expect(entry(report, 'sec-jarm')?.status).toBe('implemented');
  });

  it('classifies JWT introspection responses from an introspection signing alg', () => {
    expect(entry(buildCoverageReport(baseline), 'sec-jwt-introspection')?.status).toBe('missing');

    const report = buildCoverageReport({
      ...baseline,
      introspection_signing_alg_values_supported: ['PS256'],
    });
    expect(entry(report, 'sec-jwt-introspection')?.status).toBe('implemented');
    expect(entry(report, 'sec-jwt-introspection')?.reference).toBe('RFC 9701 §5');
  });
});

describe('buildCoverageReport — empty discovery document', () => {
  const report = buildCoverageReport({});

  it('marks every endpoint, grant, and flag missing', () => {
    expect(entry(report, 'ep-authorize')?.status).toBe('missing');
    expect(entry(report, 'ep-authorize')?.detail).toBe(
      "'authorization_endpoint' is not advertised.",
    );
    expect(entry(report, 'grant-code')?.status).toBe('missing');
    expect(entry(report, 'sec-jar')?.status).toBe('missing');
    expect(entry(report, 'sec-jar')?.detail).toBe(
      "'request_parameter_supported' is not advertised as supported.",
    );
    expect(entry(report, 'oidc-claims-param')?.status).toBe('missing');
    expect(entry(report, 'oidc-backchannel-logout')?.status).toBe('missing');
    expect(entry(report, 'sec-jarm')?.status).toBe('missing');
  });

  it('reports the missing client-auth and PKCE capabilities', () => {
    expect(entry(report, 'auth-secret')?.status).toBe('missing');
    expect(entry(report, 'auth-mtls')?.status).toBe('missing');
    expect(entry(report, 'sec-pkce')?.status).toBe('missing');
    expect(entry(report, 'sec-pkce')?.detail).toContain("'S256' is not advertised");
  });

  it('treats the OIDC Core prompt values and OAuth default response modes as partial', () => {
    expect(entry(report, 'oidc-prompt')?.status).toBe('partial');
    expect(entry(report, 'mode-query')?.status).toBe('partial');
    expect(entry(report, 'mode-fragment')?.status).toBe('partial');
    expect(entry(report, 'mode-fragment')?.detail).toContain("'fragment' is an OAuth 2.0 default");
  });

  it('marks form_post missing because it is not an OAuth 2.0 default', () => {
    expect(entry(report, 'mode-form-post')?.status).toBe('missing');
    expect(entry(report, 'mode-form-post')?.detail).toContain("'form_post' is not a default");
  });

  it('lists every unmet FAPI 2.0 prerequisite', () => {
    expect(entry(report, 'profile-fapi2')?.detail).toBe(
      'Unmet FAPI 2.0 prerequisites: PAR, DPoP or mTLS-bound tokens, PKCE S256.',
    );
  });

  it('treats the absence of legacy grants as correct', () => {
    expect(entry(report, 'grant-insecure')?.status).toBe('implemented');
  });

  it('carries backlog ids only for rows that track one', () => {
    expect(entry(report, 'ep-check-session')?.backlog).toBe('D2');
    expect(entry(report, 'ep-authorize')).not.toHaveProperty('backlog');
    expect(entry(report, 'grant-code')).not.toHaveProperty('backlog');
    expect(entry(report, 'sec-jar')).not.toHaveProperty('backlog');
  });

  it('assigns each row the channel that can exercise it', () => {
    expect(entry(report, 'ep-authorize')?.channel).toBe('browser');
    expect(entry(report, 'ep-revocation')?.channel).toBe('probe-runner');
    expect(entry(report, 'profile-fapi2')?.channel).toBe('external-suite');
  });
});
