/**
 * Spec-coverage matrix (UI-free, unit-tested).
 *
 * Measures an OpenID Provider against the **full published OAuth 2.x / OpenID Connect / CIBA / FAPI spec catalog**,
 * independent of what the provider implements, so missing functionality surfaces as explicit, attributable gap rows.
 * Where the discovery security audit ([audit.ts](./audit.ts)) answers "is the advertised configuration secure?", this
 * module answers "of the entire published spec surface, what is implemented, missing, or non-conformant?".
 *
 * Phase 1 classification is **discovery-driven**: status derives entirely from the published
 * `/.well-known/openid-configuration`, so the matrix is read-only and needs no OAuth client. The column semantics are
 * therefore *advertised* — a secure-by-default provider (ADR-0083) may implement but not advertise a capability. Later
 * phases add active probes that refine *advertised* into *verified*. See
 * [ADR-0084](../../../../../docs/decisions/0084-spec-coverage-conformance-harness.md).
 */

import type { RawDiscovery } from './audit.js';

/** Coverage status of a single published spec feature against the provider. */
export type CoverageStatus = 'implemented' | 'partial' | 'missing' | 'non-conformant';

/**
 * Which actor can actually exercise a feature end to end. The browser SPA cannot drive the confidential or
 * server-initiated legs; those require the headless probe runner or the external certified suite (ADR-0084).
 */
export type CoverageChannel = 'browser' | 'probe-runner' | 'external-suite';

/** One published spec feature, classified against the provider's discovery metadata. */
export type CoverageEntry = {
  readonly id: string;
  /** Human-readable feature name (for example, `Pushed Authorization Requests (PAR)`). */
  readonly feature: string;
  /** The authoritative source the row measures against (for example, `RFC 9126`). */
  readonly reference: string;
  readonly status: CoverageStatus;
  readonly detail: string;
  /** How a harness would exercise this feature end to end. */
  readonly channel: CoverageChannel;
  /** The GitHub issue number that tracks a missing feature, when one exists. */
  readonly issue?: number;
};

/** A named group of catalog entries (endpoints, grants, client auth, etc.). */
export type CoverageCategory = {
  readonly id: string;
  readonly name: string;
  readonly entries: readonly CoverageEntry[];
};

/** The complete spec-coverage matrix plus per-status totals. */
export type CoverageReport = {
  readonly categories: readonly CoverageCategory[];
  readonly totals: Record<CoverageStatus, number>;
};

function asStringArray(raw: RawDiscovery, key: string): string[] {
  const value = raw[key];
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string')
    : [];
}

function hasString(raw: RawDiscovery, key: string): boolean {
  return typeof raw[key] === 'string' && (raw[key] as string).length > 0;
}

function isTrue(raw: RawDiscovery, key: string): boolean {
  return raw[key] === true;
}

/** The inputs that classify a catalog row; `classify` is the only provider-specific logic. */
type CatalogEntry = {
  readonly id: string;
  readonly feature: string;
  readonly reference: string;
  readonly channel: CoverageChannel;
  readonly issue?: number;
  readonly classify: (raw: RawDiscovery) => { status: CoverageStatus; detail: string };
};

type Catalog = {
  readonly id: string;
  readonly name: string;
  readonly entries: readonly CatalogEntry[];
};

/** Classifies a discovery endpoint: advertised → implemented, absent → missing. */
function endpoint(
  id: string,
  feature: string,
  key: string,
  reference: string,
  channel: CoverageChannel,
  issue?: number,
): CatalogEntry {
  return {
    id,
    feature,
    reference,
    channel,
    ...(issue ? { issue } : {}),
    classify: (raw) =>
      hasString(raw, key)
        ? { status: 'implemented', detail: `'${key}' is advertised.` }
        : { status: 'missing', detail: `'${key}' is not advertised.` },
  };
}

/** Classifies a published grant type: advertised in `grant_types_supported` → implemented, else missing. */
function grant(
  id: string,
  feature: string,
  grantType: string,
  reference: string,
  channel: CoverageChannel,
  issue?: number,
): CatalogEntry {
  return {
    id,
    feature,
    reference,
    channel,
    ...(issue ? { issue } : {}),
    classify: (raw) =>
      asStringArray(raw, 'grant_types_supported').includes(grantType)
        ? { status: 'implemented', detail: `The '${grantType}' grant is advertised.` }
        : { status: 'missing', detail: `The '${grantType}' grant is not advertised.` },
  };
}

/** Classifies a boolean capability flag (for example, `claims_parameter_supported`). */
function flag(
  id: string,
  feature: string,
  key: string,
  reference: string,
  channel: CoverageChannel,
  issue?: number,
): CatalogEntry {
  return {
    id,
    feature,
    reference,
    channel,
    ...(issue ? { issue } : {}),
    classify: (raw) =>
      isTrue(raw, key)
        ? { status: 'implemented', detail: `'${key}' is advertised as supported.` }
        : { status: 'missing', detail: `'${key}' is not advertised as supported.` },
  };
}

const ENDPOINTS: Catalog = {
  id: 'endpoints',
  name: 'Protocol endpoints',
  entries: [
    endpoint(
      'ep-authorize',
      'Authorization endpoint',
      'authorization_endpoint',
      'OAuth 2.0 RFC 6749 §3.1',
      'browser',
    ),
    endpoint('ep-token', 'Token endpoint', 'token_endpoint', 'OAuth 2.0 RFC 6749 §3.2', 'browser'),
    endpoint(
      'ep-userinfo',
      'UserInfo endpoint',
      'userinfo_endpoint',
      'OpenID Connect Core §5.3',
      'browser',
    ),
    endpoint('ep-jwks', 'JWKS endpoint', 'jwks_uri', 'OpenID Connect Discovery §3', 'browser'),
    endpoint(
      'ep-end-session',
      'RP-Initiated Logout',
      'end_session_endpoint',
      'OpenID Connect RP-Initiated Logout §2',
      'browser',
    ),
    endpoint(
      'ep-revocation',
      'Token revocation',
      'revocation_endpoint',
      'RFC 7009 §2',
      'probe-runner',
    ),
    endpoint(
      'ep-introspection',
      'Token introspection',
      'introspection_endpoint',
      'RFC 7662 §2',
      'probe-runner',
    ),
    endpoint(
      'ep-registration',
      'Dynamic Client Registration',
      'registration_endpoint',
      'RFC 7591 §3',
      'probe-runner',
    ),
    endpoint(
      'ep-device',
      'Device authorization',
      'device_authorization_endpoint',
      'RFC 8628 §3.1',
      'probe-runner',
    ),
    endpoint(
      'ep-par',
      'Pushed Authorization Requests (PAR)',
      'pushed_authorization_request_endpoint',
      'RFC 9126 §5',
      'probe-runner',
      46,
    ),
    endpoint(
      'ep-ciba',
      'CIBA backchannel authentication',
      'backchannel_authentication_endpoint',
      'OpenID Connect CIBA Core §4',
      'probe-runner',
    ),
    endpoint(
      'ep-check-session',
      'Session Management (check_session_iframe)',
      'check_session_iframe',
      'OpenID Connect Session Management §3',
      'browser',
      22,
    ),
  ],
};

const GRANTS: Catalog = {
  id: 'grants',
  name: 'Grant types & flows',
  entries: [
    grant(
      'grant-code',
      'Authorization code',
      'authorization_code',
      'OAuth 2.0 RFC 6749 §4.1 / OAuth 2.1',
      'browser',
    ),
    grant('grant-refresh', 'Refresh token', 'refresh_token', 'OAuth 2.0 RFC 6749 §6', 'browser'),
    grant(
      'grant-client-credentials',
      'Client credentials',
      'client_credentials',
      'OAuth 2.0 RFC 6749 §4.4',
      'probe-runner',
    ),
    grant(
      'grant-device-code',
      'Device code',
      'urn:ietf:params:oauth:grant-type:device_code',
      'RFC 8628 §3.4',
      'probe-runner',
    ),
    grant(
      'grant-token-exchange',
      'Token exchange',
      'urn:ietf:params:oauth:grant-type:token-exchange',
      'RFC 8693 §2',
      'probe-runner',
    ),
    grant(
      'grant-ciba',
      'CIBA (auth_req_id)',
      'urn:openid:params:grant-type:ciba',
      'OpenID Connect CIBA Core §7.1',
      'probe-runner',
    ),
    {
      id: 'grant-insecure',
      feature: 'Legacy grants (implicit / password)',
      reference: 'RFC 9700 §2.1.2 / §2.4',
      channel: 'external-suite',
      classify: (raw) => {
        const insecure = asStringArray(raw, 'grant_types_supported').filter(
          (value) => value === 'implicit' || value === 'password',
        );
        return insecure.length > 0
          ? {
              status: 'non-conformant',
              detail: `Prohibited grant(s) advertised: ${insecure.join(', ')}. The implicit and resource-owner-password grants are removed by modern guidance.`,
            }
          : {
              status: 'implemented',
              detail:
                'Neither the implicit nor the resource-owner-password grant is advertised (correct).',
            };
      },
    },
  ],
};

const CLIENT_AUTH: Catalog = {
  id: 'client-auth',
  name: 'Client authentication',
  entries: [
    {
      id: 'auth-secret',
      feature: 'client_secret_basic / client_secret_post',
      reference: 'OAuth 2.0 RFC 6749 §2.3 / OIDC Core §9',
      channel: 'probe-runner',
      classify: (raw) => {
        const methods = asStringArray(raw, 'token_endpoint_auth_methods_supported');
        const present = methods.filter(
          (m) => m === 'client_secret_basic' || m === 'client_secret_post',
        );
        return present.length > 0
          ? { status: 'implemented', detail: `Advertised: ${present.join(', ')}.` }
          : { status: 'missing', detail: 'No client-secret authentication method is advertised.' };
      },
    },
    {
      id: 'auth-private-key-jwt',
      feature: 'private_key_jwt (asymmetric client auth)',
      reference: 'OIDC Core §9',
      channel: 'probe-runner',
      issue: 54,
      classify: (raw) =>
        asStringArray(raw, 'token_endpoint_auth_methods_supported').includes('private_key_jwt')
          ? { status: 'implemented', detail: "'private_key_jwt' is advertised." }
          : { status: 'missing', detail: "'private_key_jwt' is not advertised." },
    },
    {
      id: 'auth-mtls',
      feature: 'tls_client_auth / mutual-TLS client auth',
      reference: 'RFC 8705 §2',
      channel: 'probe-runner',
      issue: 45,
      classify: (raw) => {
        const methods = asStringArray(raw, 'token_endpoint_auth_methods_supported');
        const present = methods.filter(
          (m) => m === 'tls_client_auth' || m === 'self_signed_tls_client_auth',
        );
        return present.length > 0
          ? { status: 'implemented', detail: `Advertised: ${present.join(', ')}.` }
          : {
              status: 'missing',
              detail: 'No mutual-TLS client authentication method is advertised.',
            };
      },
    },
  ],
};

const SECURITY: Catalog = {
  id: 'security',
  name: 'Sender-constraining & request integrity',
  entries: [
    {
      id: 'sec-pkce',
      feature: 'PKCE (S256)',
      reference: 'RFC 7636 / RFC 9700 §2.1.1',
      channel: 'browser',
      classify: (raw) => {
        const methods = asStringArray(raw, 'code_challenge_methods_supported');
        if (!methods.includes('S256')) {
          return {
            status: 'missing',
            detail: "'S256' is not advertised in code_challenge_methods_supported.",
          };
        }
        return methods.includes('plain')
          ? {
              status: 'partial',
              detail: "'S256' is advertised, but the weaker 'plain' method is also offered.",
            }
          : { status: 'implemented', detail: "PKCE 'S256' is advertised and 'plain' is not." };
      },
    },
    {
      id: 'sec-dpop',
      feature: 'DPoP — sender-constrained tokens',
      reference: 'RFC 9449',
      channel: 'probe-runner',
      issue: 45,
      classify: (raw) =>
        asStringArray(raw, 'dpop_signing_alg_values_supported').length > 0
          ? { status: 'implemented', detail: "'dpop_signing_alg_values_supported' is advertised." }
          : { status: 'missing', detail: "'dpop_signing_alg_values_supported' is not advertised." },
    },
    {
      id: 'sec-mtls-bound',
      feature: 'mTLS-bound access tokens',
      reference: 'RFC 8705 §3',
      channel: 'probe-runner',
      issue: 45,
      classify: (raw) =>
        isTrue(raw, 'tls_client_certificate_bound_access_tokens')
          ? {
              status: 'implemented',
              detail: "'tls_client_certificate_bound_access_tokens' is advertised.",
            }
          : {
              status: 'missing',
              detail: "'tls_client_certificate_bound_access_tokens' is not advertised.",
            },
    },
    flag(
      'sec-jar',
      'JAR — signed request objects',
      'request_parameter_supported',
      'RFC 9101 §4',
      'browser',
    ),
    flag(
      'sec-request-uri',
      'request_uri parameter',
      'request_uri_parameter_supported',
      'RFC 9101 §5 / OIDC Core §6.2',
      'browser',
    ),
    {
      id: 'sec-jarm',
      feature: 'JARM — signed authorization responses',
      reference: 'OAuth JARM / FAPI',
      channel: 'browser',
      classify: (raw) => {
        const modes = asStringArray(raw, 'response_modes_supported');
        const signing = asStringArray(raw, 'authorization_signing_alg_values_supported');
        const jwtMode = modes.some((m) => m.includes('jwt'));
        return jwtMode || signing.length > 0
          ? {
              status: 'implemented',
              detail: 'A JWT (JARM) response mode / authorization signing alg is advertised.',
            }
          : {
              status: 'missing',
              detail: 'No JARM (JWT) response mode or authorization signing alg is advertised.',
            };
      },
    },
    {
      id: 'sec-jwt-introspection',
      feature: 'JWT introspection responses',
      reference: 'RFC 9701 §5',
      channel: 'probe-runner',
      classify: (raw) =>
        asStringArray(raw, 'introspection_signing_alg_values_supported').length > 0
          ? {
              status: 'implemented',
              detail: "'introspection_signing_alg_values_supported' is advertised.",
            }
          : {
              status: 'missing',
              detail: "'introspection_signing_alg_values_supported' is not advertised.",
            },
    },
  ],
};

const OIDC: Catalog = {
  id: 'oidc',
  name: 'OpenID Connect features',
  entries: [
    flag(
      'oidc-claims-param',
      'claims request parameter',
      'claims_parameter_supported',
      'OIDC Core §5.5',
      'browser',
    ),
    {
      id: 'oidc-subject-pairwise',
      feature: 'Pairwise subject identifiers (PPID)',
      reference: 'OIDC Core §8',
      channel: 'browser',
      issue: 47,
      classify: (raw) =>
        asStringArray(raw, 'subject_types_supported').includes('pairwise')
          ? {
              status: 'implemented',
              detail: "'pairwise' is advertised in subject_types_supported.",
            }
          : {
              status: 'missing',
              detail: "'pairwise' is not advertised in subject_types_supported.",
            },
    },
    {
      id: 'oidc-acr',
      feature: 'acr_values / acr claim',
      reference: 'OIDC Core §2 / §3.1.2.1',
      channel: 'browser',
      issue: 49,
      classify: (raw) =>
        asStringArray(raw, 'acr_values_supported').length > 0
          ? { status: 'implemented', detail: "'acr_values_supported' is advertised." }
          : { status: 'missing', detail: "'acr_values_supported' is not advertised." },
    },
    {
      id: 'oidc-prompt',
      feature: 'prompt parameter (none/login/consent/select_account)',
      reference: 'OIDC Core §3.1.2.1',
      channel: 'browser',
      classify: (raw) => {
        const values = asStringArray(raw, 'prompt_values_supported');
        // prompt_values_supported is optional; the core-defined values are the baseline every OP honors.
        return values.length > 0
          ? { status: 'implemented', detail: `Advertised: ${values.join(', ')}.` }
          : {
              status: 'partial',
              detail:
                "'prompt_values_supported' is not advertised; the OIDC Core prompt values are assumed by default.",
            };
      },
    },
    {
      id: 'oidc-idtoken-encryption',
      feature: 'ID token encryption',
      reference: 'OIDC Core §10.2',
      channel: 'browser',
      classify: (raw) =>
        asStringArray(raw, 'id_token_encryption_alg_values_supported').length > 0
          ? {
              status: 'implemented',
              detail: "'id_token_encryption_alg_values_supported' is advertised.",
            }
          : {
              status: 'missing',
              detail: "'id_token_encryption_alg_values_supported' is not advertised.",
            },
    },
    {
      id: 'oidc-backchannel-logout',
      feature: 'Back-Channel Logout',
      reference: 'OpenID Connect Back-Channel Logout §2',
      channel: 'probe-runner',
      classify: (raw) =>
        isTrue(raw, 'backchannel_logout_supported')
          ? { status: 'implemented', detail: "'backchannel_logout_supported' is advertised." }
          : { status: 'missing', detail: "'backchannel_logout_supported' is not advertised." },
    },
    {
      id: 'oidc-frontchannel-logout',
      feature: 'Front-Channel Logout',
      reference: 'OpenID Connect Front-Channel Logout §3',
      channel: 'browser',
      issue: 22,
      classify: (raw) =>
        isTrue(raw, 'frontchannel_logout_supported')
          ? { status: 'implemented', detail: "'frontchannel_logout_supported' is advertised." }
          : {
              status: 'missing',
              detail:
                "'frontchannel_logout_supported' is not advertised (intentionally deferred — depends on third-party cookies).",
            },
    },
  ],
};

const RESPONSE_MODES: Catalog = {
  id: 'response-modes',
  name: 'Response modes',
  entries: [
    {
      id: 'mode-query',
      feature: 'query response mode',
      reference: 'OAuth 2.0 Multiple Response Type Encoding',
      channel: 'browser',
      classify: (raw) => responseMode(raw, 'query'),
    },
    {
      id: 'mode-fragment',
      feature: 'fragment response mode',
      reference: 'OAuth 2.0 Multiple Response Type Encoding',
      channel: 'browser',
      classify: (raw) => responseMode(raw, 'fragment'),
    },
    {
      id: 'mode-form-post',
      feature: 'form_post response mode',
      reference: 'OAuth 2.0 Form Post Response Mode',
      channel: 'browser',
      classify: (raw) => responseMode(raw, 'form_post'),
    },
  ],
};

function responseMode(raw: RawDiscovery, mode: string): { status: CoverageStatus; detail: string } {
  const modes = asStringArray(raw, 'response_modes_supported');
  // response_modes_supported is optional; query + fragment are the RFC 6749 defaults when it is absent.
  if (modes.length === 0) {
    return mode === 'form_post'
      ? {
          status: 'missing',
          detail: "'response_modes_supported' is not advertised; 'form_post' is not a default.",
        }
      : {
          status: 'partial',
          detail: `'response_modes_supported' is not advertised; '${mode}' is an OAuth 2.0 default.`,
        };
  }
  return modes.includes(mode)
    ? { status: 'implemented', detail: `'${mode}' is advertised in response_modes_supported.` }
    : { status: 'missing', detail: `'${mode}' is not advertised in response_modes_supported.` };
}

const PROFILES: Catalog = {
  id: 'profiles',
  name: 'Security profiles',
  entries: [
    {
      id: 'profile-fapi2',
      feature: 'FAPI 2.0 Security Profile',
      reference: 'FAPI 2.0 Security Profile',
      channel: 'external-suite',
      issue: 54,
      classify: (raw) => {
        // FAPI 2.0 requires PAR, a sender-constraining mechanism (DPoP or mTLS-bound tokens), PKCE S256, and
        // asymmetric-only signing. Derive readiness from those discovery signals.
        const missing: string[] = [];
        if (!hasString(raw, 'pushed_authorization_request_endpoint')) missing.push('PAR');
        const senderConstrained =
          asStringArray(raw, 'dpop_signing_alg_values_supported').length > 0 ||
          isTrue(raw, 'tls_client_certificate_bound_access_tokens');
        if (!senderConstrained) missing.push('DPoP or mTLS-bound tokens');
        if (!asStringArray(raw, 'code_challenge_methods_supported').includes('S256'))
          missing.push('PKCE S256');
        return missing.length === 0
          ? {
              status: 'implemented',
              detail: 'PAR, sender-constrained tokens, and PKCE S256 are all advertised.',
            }
          : { status: 'missing', detail: `Unmet FAPI 2.0 prerequisites: ${missing.join(', ')}.` };
      },
    },
    {
      id: 'profile-fapi1-advanced',
      feature: 'FAPI 1.0 Advanced Security Profile',
      reference: 'FAPI 1.0 Part 2: Advanced',
      channel: 'external-suite',
      issue: 124,
      classify: (raw) => {
        // FAPI 1.0 Advanced (as the fapi1-advanced profile serves it) requires PAR, mTLS-bound tokens, PKCE S256,
        // PS256/ES256-signed request objects, and JARM for a code response. Derive readiness from those signals.
        const missing: string[] = [];
        if (!hasString(raw, 'pushed_authorization_request_endpoint')) missing.push('PAR');
        if (!isTrue(raw, 'tls_client_certificate_bound_access_tokens'))
          missing.push('mTLS-bound tokens');
        if (!asStringArray(raw, 'code_challenge_methods_supported').includes('S256'))
          missing.push('PKCE S256');
        const requestObjectAlgs = asStringArray(raw, 'request_object_signing_alg_values_supported');
        if (!requestObjectAlgs.some((alg) => alg === 'PS256' || alg === 'ES256'))
          missing.push('PS256/ES256 request objects');
        if (asStringArray(raw, 'authorization_signing_alg_values_supported').length === 0)
          missing.push('JARM');
        return missing.length === 0
          ? {
              status: 'implemented',
              detail:
                'PAR, mTLS-bound tokens, PKCE S256, signed request objects, and JARM are all advertised.',
            }
          : {
              status: 'missing',
              detail: `Unmet FAPI 1.0 Advanced prerequisites: ${missing.join(', ')}.`,
            };
      },
    },
  ],
};

const CATALOG: readonly Catalog[] = [
  ENDPOINTS,
  GRANTS,
  CLIENT_AUTH,
  SECURITY,
  OIDC,
  RESPONSE_MODES,
  PROFILES,
];

const EMPTY_TOTALS: Record<CoverageStatus, number> = {
  implemented: 0,
  partial: 0,
  missing: 0,
  'non-conformant': 0,
};

/**
 * Builds the full spec-coverage matrix for a provider from its published discovery metadata. Every catalog row is
 * classified `implemented` / `partial` / `missing` / `non-conformant`; the status reflects what the provider
 * *advertises* (Phase 1 semantics), not an active probe.
 */
export function buildCoverageReport(raw: RawDiscovery): CoverageReport {
  const totals: Record<CoverageStatus, number> = { ...EMPTY_TOTALS };
  const categories = CATALOG.map((category) => ({
    id: category.id,
    name: category.name,
    entries: category.entries.map((entry): CoverageEntry => {
      const { status, detail } = entry.classify(raw);
      totals[status] += 1;
      return {
        id: entry.id,
        feature: entry.feature,
        reference: entry.reference,
        status,
        detail,
        channel: entry.channel,
        ...(entry.issue ? { issue: entry.issue } : {}),
      };
    }),
  }));
  return { categories, totals };
}
