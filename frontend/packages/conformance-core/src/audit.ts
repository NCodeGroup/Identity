/**
 * Discovery-metadata security & conformance audit (UI-free, unit-tested).
 *
 * Evaluates an OpenID Provider's published `/.well-known/openid-configuration` against current, authoritative
 * guidance — OAuth 2.1, the OAuth 2.0 Security Best Current Practice (RFC 9700), OpenID Connect Core/Discovery, and
 * RFC 7636/8414/7009/7662/9126 — and the OpenID Foundation conformance Basic/Config OP profiles. Every rule is a plain
 * function over the raw metadata so the posture is deterministic and testable without a browser.
 */

/** The severity of a single audit finding. */
export type Severity = 'pass' | 'warn' | 'fail';

/** One audit finding against the published discovery metadata. */
export type Finding = {
  readonly id: string;
  readonly title: string;
  readonly severity: Severity;
  readonly detail: string;
  /** The authoritative source the rule enforces (for example, `RFC 9700 §2.1.2`). */
  readonly reference: string;
};

/** A conformance profile and its readiness, derived from the findings it depends on. */
export type ProfileReadiness = {
  readonly id: string;
  readonly name: string;
  readonly status: Severity;
  readonly findingIds: readonly string[];
};

/** The complete audit of a provider's discovery metadata. */
export type AuditReport = {
  readonly findings: readonly Finding[];
  readonly profiles: readonly ProfileReadiness[];
};

/** The raw discovery document as published (arbitrary JSON object). */
export type RawDiscovery = Record<string, unknown>;

function asStringArray(raw: RawDiscovery, key: string): string[] {
  const value = raw[key];
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string')
    : [];
}

function asString(raw: RawDiscovery, key: string): string | undefined {
  const value = raw[key];
  return typeof value === 'string' ? value : undefined;
}

const SEVERITY_RANK: Record<Severity, number> = { pass: 0, warn: 1, fail: 2 };

/** The worst severity among a set of findings (defaults to `pass` when the set is empty). */
function worst(findings: readonly Finding[]): Severity {
  return findings.reduce<Severity>(
    (acc, finding) =>
      SEVERITY_RANK[finding.severity] > SEVERITY_RANK[acc] ? finding.severity : acc,
    'pass',
  );
}

function auditResponseTypes(raw: RawDiscovery): Finding {
  const values = asStringArray(raw, 'response_types_supported');
  const hasCode = values.includes('code');
  // Any response type that delivers a token or id_token through the front channel is implicit/hybrid delivery, which
  // the Security BCP and OAuth 2.1 remove in favor of the confidential back-channel code exchange.
  const frontChannelToken = values.filter(
    (value) => value.split(' ').includes('token') || value.split(' ').includes('id_token'),
  );

  if (!hasCode) {
    return {
      id: 'response-types',
      title: 'Response types',
      severity: 'fail',
      detail: `'code' is not advertised in response_types_supported (${values.join(', ') || 'none'}).`,
      reference: 'OAuth 2.1 §1.3',
    };
  }
  if (frontChannelToken.length > 0) {
    return {
      id: 'response-types',
      title: 'Response types',
      severity: 'fail',
      detail: `Front-channel token delivery is advertised (${frontChannelToken.join(', ')}); implicit and hybrid flows are removed by modern guidance.`,
      reference: 'RFC 9700 §2.1.2',
    };
  }
  return {
    id: 'response-types',
    title: 'Response types',
    severity: 'pass',
    detail: "Only the authorization 'code' response type is advertised.",
    reference: 'OAuth 2.1 §1.3',
  };
}

function auditGrantTypes(raw: RawDiscovery): Finding {
  const values = asStringArray(raw, 'grant_types_supported');
  // grant_types_supported is optional and defaults to authorization_code + implicit; treat an empty set as the
  // authorization_code baseline rather than a failure.
  const insecure = values.filter((value) => value === 'implicit' || value === 'password');
  if (insecure.length > 0) {
    return {
      id: 'grant-types',
      title: 'Grant types',
      severity: 'fail',
      detail: `Insecure grant types are advertised (${insecure.join(', ')}); the implicit and resource-owner-password grants are prohibited by modern guidance.`,
      reference: 'RFC 9700 §2.4 / §2.1.2',
    };
  }
  if (values.length > 0 && !values.includes('authorization_code')) {
    return {
      id: 'grant-types',
      title: 'Grant types',
      severity: 'warn',
      detail: 'authorization_code is not advertised in grant_types_supported.',
      reference: 'OAuth 2.1 §1.3',
    };
  }
  return {
    id: 'grant-types',
    title: 'Grant types',
    severity: 'pass',
    detail: 'No prohibited grant types are advertised.',
    reference: 'RFC 9700 §2.4',
  };
}

function auditPkce(raw: RawDiscovery): Finding {
  const methods = asStringArray(raw, 'code_challenge_methods_supported');
  if (!methods.includes('S256')) {
    return {
      id: 'pkce',
      title: 'PKCE (S256)',
      severity: 'fail',
      detail: `code_challenge_methods_supported does not advertise 'S256' (${methods.join(', ') || 'none'}); PKCE with S256 is mandatory for the authorization-code flow.`,
      reference: 'RFC 9700 §2.1.1 / RFC 7636',
    };
  }
  if (methods.includes('plain')) {
    return {
      id: 'pkce',
      title: 'PKCE (S256)',
      severity: 'warn',
      detail: "The 'plain' PKCE method is advertised; only 'S256' should be offered.",
      reference: 'RFC 7636 §4.2',
    };
  }
  return {
    id: 'pkce',
    title: 'PKCE (S256)',
    severity: 'pass',
    detail: "PKCE 'S256' is advertised and 'plain' is not.",
    reference: 'RFC 9700 §2.1.1',
  };
}

function auditIdTokenSigning(raw: RawDiscovery): Finding {
  const algs = asStringArray(raw, 'id_token_signing_alg_values_supported');
  if (algs.includes('none')) {
    return {
      id: 'id-token-signing',
      title: 'ID token signing',
      severity: 'fail',
      detail: "Unsigned ID tokens ('none') are advertised.",
      reference: 'OpenID Connect Core §3.1.3.7',
    };
  }
  const symmetric = algs.filter((alg) => alg.startsWith('HS'));
  const asymmetric = algs.filter(
    (alg) =>
      alg.startsWith('RS') || alg.startsWith('PS') || alg.startsWith('ES') || alg.startsWith('Ed'),
  );
  if (asymmetric.length === 0) {
    return {
      id: 'id-token-signing',
      title: 'ID token signing',
      severity: 'fail',
      detail: `No asymmetric ID token signing algorithm is advertised (${algs.join(', ') || 'none'}); relying parties cannot verify tokens without the shared secret.`,
      reference: 'RFC 9700 §2.6',
    };
  }
  if (symmetric.length > 0) {
    return {
      id: 'id-token-signing',
      title: 'ID token signing',
      severity: 'warn',
      detail: `Symmetric signing is advertised (${symmetric.join(', ')}); prefer asymmetric algorithms (PS256/ES256) across trust boundaries.`,
      reference: 'RFC 9700 §2.6',
    };
  }
  const preferred = asymmetric.some((alg) => alg === 'PS256' || alg === 'ES256');
  return {
    id: 'id-token-signing',
    title: 'ID token signing',
    severity: 'pass',
    detail: preferred
      ? `Asymmetric signing is advertised (${asymmetric.join(', ')}).`
      : `Asymmetric signing is advertised (${asymmetric.join(', ')}); PS256 or ES256 is preferred.`,
    reference: 'RFC 9700 §2.6',
  };
}

function auditHttps(raw: RawDiscovery): Finding {
  const endpointKeys = Object.keys(raw).filter(
    (key) => key.endsWith('_endpoint') || key === 'jwks_uri',
  );
  const insecure = endpointKeys
    .map((key) => asString(raw, key))
    .filter((value): value is string => typeof value === 'string')
    .filter((value) => value.startsWith('http://') && !value.startsWith('http://localhost'));
  if (insecure.length > 0) {
    return {
      id: 'https',
      title: 'TLS everywhere',
      severity: 'fail',
      detail: `Endpoints are advertised over plaintext HTTP: ${insecure.join(', ')}.`,
      reference: 'RFC 9700 §2.3 / RFC 8414 §2',
    };
  }
  return {
    id: 'https',
    title: 'TLS everywhere',
    severity: 'pass',
    detail: 'All advertised endpoints use HTTPS (or localhost).',
    reference: 'RFC 8414 §2',
  };
}

type RequiredField = { readonly key: string; readonly reference: string };

const CONFIG_REQUIRED_FIELDS: readonly RequiredField[] = [
  { key: 'issuer', reference: 'OpenID Connect Discovery §3' },
  { key: 'authorization_endpoint', reference: 'OpenID Connect Discovery §3' },
  { key: 'token_endpoint', reference: 'OpenID Connect Discovery §3' },
  { key: 'jwks_uri', reference: 'OpenID Connect Discovery §3' },
  { key: 'response_types_supported', reference: 'OpenID Connect Discovery §3' },
  { key: 'subject_types_supported', reference: 'OpenID Connect Discovery §3' },
  { key: 'id_token_signing_alg_values_supported', reference: 'OpenID Connect Discovery §3' },
];

function auditRequiredMetadata(raw: RawDiscovery): Finding[] {
  return CONFIG_REQUIRED_FIELDS.map((field) => {
    const present = field.key in raw && raw[field.key] !== null && raw[field.key] !== undefined;
    return {
      id: `required-${field.key}`,
      title: `Metadata: ${field.key}`,
      severity: present ? ('pass' as const) : ('fail' as const),
      detail: present
        ? `'${field.key}' is advertised.`
        : `Required discovery field '${field.key}' is missing.`,
      reference: field.reference,
    };
  });
}

function auditRecommendedEndpoint(
  raw: RawDiscovery,
  id: string,
  key: string,
  title: string,
  reference: string,
): Finding {
  const present = typeof asString(raw, key) === 'string';
  return {
    id,
    title,
    severity: present ? 'pass' : 'warn',
    detail: present ? `'${key}' is advertised.` : `'${key}' is not advertised (recommended).`,
    reference,
  };
}

function auditOpenIdScope(raw: RawDiscovery): Finding {
  const scopes = asStringArray(raw, 'scopes_supported');
  const present = scopes.includes('openid');
  return {
    id: 'scope-openid',
    title: 'OpenID scope',
    severity: present ? 'pass' : 'warn',
    detail: present
      ? "The 'openid' scope is advertised."
      : "scopes_supported does not advertise 'openid'.",
    reference: 'OpenID Connect Discovery §3',
  };
}

/** Audits a raw discovery document and returns the findings plus per-profile readiness. */
export function auditDiscovery(raw: RawDiscovery): AuditReport {
  const required = auditRequiredMetadata(raw);
  const responseTypes = auditResponseTypes(raw);
  const grantTypes = auditGrantTypes(raw);
  const pkce = auditPkce(raw);
  const idTokenSigning = auditIdTokenSigning(raw);
  const https = auditHttps(raw);
  const openIdScope = auditOpenIdScope(raw);
  const userInfo = auditRecommendedEndpoint(
    raw,
    'userinfo-endpoint',
    'userinfo_endpoint',
    'UserInfo endpoint',
    'OpenID Connect Core §5.3',
  );
  const endSession = auditRecommendedEndpoint(
    raw,
    'end-session-endpoint',
    'end_session_endpoint',
    'RP-Initiated Logout',
    'OpenID Connect RP-Initiated Logout §2',
  );
  const revocation = auditRecommendedEndpoint(
    raw,
    'revocation-endpoint',
    'revocation_endpoint',
    'Token revocation',
    'RFC 7009 §2',
  );
  const introspection = auditRecommendedEndpoint(
    raw,
    'introspection-endpoint',
    'introspection_endpoint',
    'Token introspection',
    'RFC 7662 §2',
  );
  const par = auditRecommendedEndpoint(
    raw,
    'par-endpoint',
    'pushed_authorization_request_endpoint',
    'Pushed Authorization Requests',
    'RFC 9126 §5',
  );

  const findings: Finding[] = [
    responseTypes,
    grantTypes,
    pkce,
    idTokenSigning,
    https,
    openIdScope,
    userInfo,
    endSession,
    revocation,
    introspection,
    par,
    ...required,
  ];

  const byId = new Map(findings.map((finding) => [finding.id, finding]));
  const pick = (ids: readonly string[]): Finding[] =>
    ids.map((id) => byId.get(id)).filter((finding): finding is Finding => finding !== undefined);

  const basicIds = ['response-types', 'grant-types', 'pkce', 'id-token-signing', 'https'];
  const configIds = [
    ...CONFIG_REQUIRED_FIELDS.map((field) => `required-${field.key}`),
    'scope-openid',
  ];

  const profiles: ProfileReadiness[] = [
    {
      id: 'basic-op',
      name: 'Basic OP (authorization code + PKCE)',
      status: worst(pick(basicIds)),
      findingIds: basicIds,
    },
    {
      id: 'config-op',
      name: 'Config OP (discovery metadata)',
      status: worst(pick(configIds)),
      findingIds: configIds,
    },
  ];

  return { findings, profiles };
}

/** Summarizes a report into pass/warn/fail counts for a headline. */
export function summarize(report: AuditReport): Record<Severity, number> {
  return report.findings.reduce<Record<Severity, number>>(
    (acc, finding) => {
      acc[finding.severity] += 1;
      return acc;
    },
    { pass: 0, warn: 0, fail: 0 },
  );
}
