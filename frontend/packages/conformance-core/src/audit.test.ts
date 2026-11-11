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

  it("fails and reports 'none' when response_types_supported is absent or not an array", () => {
    const report = auditDiscovery({ ...secureDiscovery, response_types_supported: 'code' });
    const finding = findingById(report, 'response-types');
    expect(finding?.severity).toBe('fail');
    expect(finding?.detail).toContain('(none)');
  });

  it('ignores non-string entries in a metadata array', () => {
    const report = auditDiscovery({ ...secureDiscovery, response_types_supported: ['code', 42] });
    expect(findingById(report, 'response-types')?.severity).toBe('pass');
  });

  it('fails a space-delimited hybrid response type carrying a token', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      response_types_supported: ['code', 'code id_token'],
    });
    const finding = findingById(report, 'response-types');
    expect(finding?.severity).toBe('fail');
    expect(finding?.detail).toContain('code id_token');
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

  it('warns when grants are advertised without authorization_code', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      grant_types_supported: ['client_credentials'],
    });
    expect(findingById(report, 'grant-types')?.severity).toBe('warn');
    expect(report.profiles.find((p) => p.id === 'basic-op')?.status).toBe('warn');
  });

  it('passes when grant_types_supported is omitted (the authorization_code default)', () => {
    const withoutGrants: RawDiscovery = { ...secureDiscovery };
    delete withoutGrants.grant_types_supported;
    const report = auditDiscovery(withoutGrants);
    expect(findingById(report, 'grant-types')?.severity).toBe('pass');
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

  it("fails and reports 'none' when no signing algorithm is advertised at all", () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      id_token_signing_alg_values_supported: [],
    });
    const finding = findingById(report, 'id-token-signing');
    expect(finding?.severity).toBe('fail');
    expect(finding?.detail).toContain('(none)');
  });

  it('passes ES256 alone as a preferred asymmetric algorithm', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      id_token_signing_alg_values_supported: ['ES256'],
    });
    const finding = findingById(report, 'id-token-signing');
    expect(finding?.severity).toBe('pass');
    expect(finding?.detail).not.toContain('preferred');
  });

  it('passes but recommends PS256/ES256 when only other asymmetric algorithms are advertised', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      id_token_signing_alg_values_supported: ['RS256', 'EdDSA'],
    });
    const finding = findingById(report, 'id-token-signing');
    expect(finding?.severity).toBe('pass');
    expect(finding?.detail).toContain('PS256 or ES256 is preferred');
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

  it('allows plaintext HTTP for localhost endpoints', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      token_endpoint: 'http://localhost:5000/token',
      jwks_uri: 'http://localhost:5000/jwks',
    });
    expect(findingById(report, 'https')?.severity).toBe('pass');
  });

  it('flags a plaintext jwks_uri and ignores non-string endpoint values', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      jwks_uri: 'http://op.test/jwks',
      registration_endpoint: 42,
    });
    const finding = findingById(report, 'https');
    expect(finding?.severity).toBe('fail');
    expect(finding?.detail).toBe(
      'Endpoints are advertised over plaintext HTTP: http://op.test/jwks.',
    );
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

  it('treats a null required field as missing', () => {
    const report = auditDiscovery({ ...secureDiscovery, subject_types_supported: null });
    const finding = findingById(report, 'required-subject_types_supported');
    expect(finding?.severity).toBe('fail');
    expect(finding?.detail).toBe("Required discovery field 'subject_types_supported' is missing.");
  });
});

describe('auditDiscovery — openid scope', () => {
  it('warns and degrades Config OP when openid is not in scopes_supported', () => {
    const report = auditDiscovery({ ...secureDiscovery, scopes_supported: ['profile'] });
    const finding = findingById(report, 'scope-openid');
    expect(finding?.severity).toBe('warn');
    expect(finding?.detail).toBe("scopes_supported does not advertise 'openid'.");
    expect(report.profiles.find((p) => p.id === 'config-op')?.status).toBe('warn');
  });
});

describe('auditDiscovery — recommended endpoints', () => {
  it('warns when PAR is not advertised', () => {
    const withoutPar: RawDiscovery = { ...secureDiscovery };
    delete withoutPar.pushed_authorization_request_endpoint;
    const report = auditDiscovery(withoutPar);
    expect(findingById(report, 'par-endpoint')?.severity).toBe('warn');
  });

  it('warns when a recommended endpoint is advertised as a non-string', () => {
    const report = auditDiscovery({ ...secureDiscovery, revocation_endpoint: true });
    const finding = findingById(report, 'revocation-endpoint');
    expect(finding?.severity).toBe('warn');
    expect(finding?.detail).toBe("'revocation_endpoint' is not advertised (recommended).");
  });
});

describe('summarize', () => {
  it('counts every finding by severity', () => {
    const report = auditDiscovery({
      ...secureDiscovery,
      code_challenge_methods_supported: ['S256', 'plain'],
      id_token_signing_alg_values_supported: ['none'],
    });
    const counts = summarize(report);
    expect(counts.fail).toBe(1);
    expect(counts.warn).toBe(1);
    expect(counts.pass + counts.warn + counts.fail).toBe(report.findings.length);
  });
});
