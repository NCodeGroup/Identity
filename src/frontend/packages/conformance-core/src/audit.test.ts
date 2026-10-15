import { describe, expect, it } from 'vitest';
import { auditDiscovery, summarize, type RawDiscovery } from './audit.js';

/** A modern, secure baseline discovery document that should pass every rule. */
const secureDiscovery: RawDiscovery = {
  issuer: 'https://op.test',
  authorization_endpoint: 'https://op.test/authorize',
  token_endpoint: 'https://op.test/token',
  userinfo_endpoint: 'https://op.test/userinfo',
  end_session_endpoint: 'https://op.test/end-session',
  revocation_endpoint: 'https://op.test/revoke',
  introspection_endpoint: 'https://op.test/introspect',
  pushed_authorization_request_endpoint: 'https://op.test/par',
  jwks_uri: 'https://op.test/jwks',
  response_types_supported: ['code'],
  grant_types_supported: ['authorization_code', 'refresh_token'],
  code_challenge_methods_supported: ['S256'],
  id_token_signing_alg_values_supported: ['PS256', 'ES256'],
  subject_types_supported: ['public'],
  scopes_supported: ['openid', 'profile', 'email'],
};

function findingById(report: ReturnType<typeof auditDiscovery>, id: string) {
  return report.findings.find((finding) => finding.id === id);
}

describe('auditDiscovery — secure baseline', () => {
  it('reports no failures and both profiles ready', () => {
    const report = auditDiscovery(secureDiscovery);
    const counts = summarize(report);
    expect(counts.fail).toBe(0);
    expect(report.profiles.find((p) => p.id === 'basic-op')?.status).toBe('pass');
    expect(report.profiles.find((p) => p.id === 'config-op')?.status).toBe('pass');
  });
});

describe('auditDiscovery — response types', () => {
  it('fails when implicit/hybrid front-channel token delivery is advertised', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      response_types_supported: ['code', 'token', 'id_token'],
    });
    expect(findingById(report, 'response-types')?.severity).toBe('fail');
    expect(report.profiles.find((p) => p.id === 'basic-op')?.status).toBe('fail');
  });

  it('fails when the code response type is absent', () => {
    const report = auditDiscovery({ ...secureDiscovery, response_types_supported: ['id_token'] });
    expect(findingById(report, 'response-types')?.severity).toBe('fail');
  });
});

describe('auditDiscovery — grant types', () => {
  it('fails on the implicit or password grant', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      grant_types_supported: ['authorization_code', 'implicit'],
    });
    expect(findingById(report, 'grant-types')?.severity).toBe('fail');
  });
});

describe('auditDiscovery — PKCE', () => {
  it('fails when S256 is not advertised', () => {
    const report = auditDiscovery({ ...secureDiscovery, code_challenge_methods_supported: [] });
    expect(findingById(report, 'pkce')?.severity).toBe('fail');
  });

  it('warns when plain is advertised', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      code_challenge_methods_supported: ['S256', 'plain'],
    });
    expect(findingById(report, 'pkce')?.severity).toBe('warn');
  });
});

describe('auditDiscovery — ID token signing', () => {
  it('fails on unsigned (none) id tokens', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      id_token_signing_alg_values_supported: ['none'],
    });
    expect(findingById(report, 'id-token-signing')?.severity).toBe('fail');
  });

  it('fails when no asymmetric algorithm is advertised', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      id_token_signing_alg_values_supported: ['HS256'],
    });
    expect(findingById(report, 'id-token-signing')?.severity).toBe('fail');
  });

  it('warns when symmetric signing is advertised alongside asymmetric', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      id_token_signing_alg_values_supported: ['PS256', 'HS256'],
    });
    expect(findingById(report, 'id-token-signing')?.severity).toBe('warn');
  });
});

describe('auditDiscovery — TLS', () => {
  it('fails when a non-localhost endpoint uses plaintext HTTP', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      token_endpoint: 'http://op.test/token',
    });
    expect(findingById(report, 'https')?.severity).toBe('fail');
  });
});

describe('auditDiscovery — required metadata', () => {
  it('fails Config OP when a required field is missing', () => {
    const withoutJwks: RawDiscovery = { ...secureDiscovery };
    delete withoutJwks.jwks_uri;
    const report = auditDiscovery(withoutJwks);
    expect(findingById(report, 'required-jwks_uri')?.severity).toBe('fail');
    expect(report.profiles.find((p) => p.id === 'config-op')?.status).toBe('fail');
  });
});

describe('auditDiscovery — recommended endpoints', () => {
  it('warns when PAR is not advertised', () => {
    const withoutPar: RawDiscovery = { ...secureDiscovery };
    delete withoutPar.pushed_authorization_request_endpoint;
    const report = auditDiscovery(withoutPar);
    expect(findingById(report, 'par-endpoint')?.severity).toBe('warn');
  });
});
