/**
 * Runner configuration (UI-free, unit-tested) — parses CLI arguments and environment variables into a
 * {@link RunnerConfig}. Secrets are accepted from the environment (never logged) so a CI runner can pass the
 * confidential client's credentials without putting them on the command line.
 */

import type { ClientAuthMethod } from './probeRunner.js';
import { isSupportedAlg, type JwaSignatureAlg } from './signer.js';

/** The fully-resolved configuration the runner executes with. */
export type RunnerConfig = {
  readonly issuer: string;
  readonly clientId?: string;
  readonly clientSecret?: string;
  readonly authMethod: ClientAuthMethod;
  readonly publicClientId?: string;
  readonly scope?: string;
  readonly loginHint?: string;
  /** Path to a PEM/JWK private key signing the `private_key_jwt` client assertion. */
  readonly privateKeyPath?: string;
  /** Overrides the `private_key_jwt` signing algorithm (needed to pick an RSA `RS*` scheme). */
  readonly privateKeyAlg?: JwaSignatureAlg;
  /** Path to a PEM/JWK private key signing DPoP proofs. */
  readonly dpopKeyPath?: string;
  /** Overrides the DPoP proof signing algorithm (needed to pick an RSA `RS*` scheme). */
  readonly dpopKeyAlg?: JwaSignatureAlg;
  /** Emit the report as JSON (for CI consumption) instead of a human-readable table. */
  readonly json: boolean;
  readonly timeoutMs: number;
};

/** The outcome of parsing: either a config, a list of errors, or a request to print help. */
export type ParseResult =
  | { readonly kind: 'config'; readonly config: RunnerConfig }
  | { readonly kind: 'help' }
  | { readonly kind: 'error'; readonly errors: readonly string[] };

const DEFAULT_TIMEOUT_MS = 10_000;

/** The `--help` / usage text. */
export const USAGE = `ncode-conformance-runner — headless OAuth/OIDC confidential-leg conformance probes

Usage:
  conformance-runner --issuer <url> [options]

Options:
  --issuer <url>            OpenID Provider issuer (or env CONFORMANCE_ISSUER). Required.
  --client-id <id>          Confidential client id (or env CONFORMANCE_CLIENT_ID).
  --client-secret <secret>  Confidential client secret (or env CONFORMANCE_CLIENT_SECRET).
  --auth-method <m>         Client auth: 'basic' (default) or 'post'.
  --public-client-id <id>   Public client id for the device-code / CIBA / private_key_jwt legs.
  --scope <scope>           Scope requested by the token-issuing probes.
  --login-hint <hint>       login_hint for the CIBA probe.
  --private-key <path>      PEM/JWK private key for the private_key_jwt client assertion.
  --private-key-alg <alg>   Override the private_key_jwt signing alg (e.g. RS256; default inferred).
  --dpop-key <path>         PEM/JWK private key for the DPoP proof.
  --dpop-key-alg <alg>      Override the DPoP signing alg (e.g. RS256; default inferred).
  --timeout <ms>            Per-request timeout in milliseconds (default ${DEFAULT_TIMEOUT_MS}).
  --json                    Emit the report as JSON instead of a table.
  --help                    Show this help.

The runner exits 0 when no probe fails, 1 when any probe fails, and 2 on invalid arguments or a run that could not
complete.`;

function normalizeAuthMethod(value: string | undefined): ClientAuthMethod | undefined {
  if (value === undefined || value === 'basic') {
    return 'client_secret_basic';
  }
  if (value === 'post') {
    return 'client_secret_post';
  }
  return undefined;
}

/** Validates an optional signing-algorithm option against the supported set, recording an error when invalid. */
function resolveAlgOption(
  key: string,
  values: ReadonlyMap<string, string>,
  errors: string[],
): JwaSignatureAlg | undefined {
  const value = values.get(key);
  if (value === undefined) {
    return undefined;
  }
  if (!isSupportedAlg(value)) {
    errors.push(`Invalid '--${key}' (unsupported signature algorithm '${value}').`);
    return undefined;
  }
  return value;
}

/**
 * Parses `argv` (the arguments after the node/script prefix) and `env` into a {@link ParseResult}. Pure and
 * synchronous so it is fully unit-testable without a process.
 */
export function parseArgs(
  argv: readonly string[],
  env: Readonly<Record<string, string | undefined>> = {},
): ParseResult {
  const errors: string[] = [];
  const values = new Map<string, string>();
  let json = false;

  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === '--help' || arg === '-h') {
      return { kind: 'help' };
    }
    if (arg === '--json') {
      json = true;
      continue;
    }
    if (arg === '--') {
      // The end-of-options delimiter forwarded by the launcher (vite-node / pnpm); ignore it.
      continue;
    }
    if (arg !== undefined && arg.startsWith('--')) {
      const key = arg.slice(2);
      const next = argv[index + 1];
      if (next === undefined || next.startsWith('--')) {
        errors.push(`Option '${arg}' expects a value.`);
        continue;
      }
      values.set(key, next);
      index += 1;
      continue;
    }
    errors.push(`Unexpected argument '${arg}'.`);
  }

  const issuer = values.get('issuer') ?? env.CONFORMANCE_ISSUER;
  if (!issuer) {
    errors.push("Missing required '--issuer' (or env CONFORMANCE_ISSUER).");
  }

  const authMethod = normalizeAuthMethod(values.get('auth-method'));
  if (authMethod === undefined) {
    errors.push("Invalid '--auth-method' (expected 'basic' or 'post').");
  }

  const timeoutRaw = values.get('timeout');
  let timeoutMs = DEFAULT_TIMEOUT_MS;
  if (timeoutRaw !== undefined) {
    const parsed = Number(timeoutRaw);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      errors.push("Invalid '--timeout' (expected a positive number of milliseconds).");
    } else {
      timeoutMs = parsed;
    }
  }

  const clientId = values.get('client-id') ?? env.CONFORMANCE_CLIENT_ID;
  const clientSecret = values.get('client-secret') ?? env.CONFORMANCE_CLIENT_SECRET;
  if ((clientId && !clientSecret) || (!clientId && clientSecret)) {
    errors.push('A confidential client needs both --client-id and --client-secret.');
  }

  const privateKeyAlg = resolveAlgOption('private-key-alg', values, errors);
  const dpopKeyAlg = resolveAlgOption('dpop-key-alg', values, errors);

  if (errors.length > 0 || !issuer || authMethod === undefined) {
    return { kind: 'error', errors };
  }

  const publicClientId = values.get('public-client-id');
  const scope = values.get('scope');
  const loginHint = values.get('login-hint');
  const privateKeyPath = values.get('private-key');
  const dpopKeyPath = values.get('dpop-key');
  const config: RunnerConfig = {
    issuer,
    authMethod,
    json,
    timeoutMs,
    ...(clientId ? { clientId } : {}),
    ...(clientSecret ? { clientSecret } : {}),
    ...(publicClientId ? { publicClientId } : {}),
    ...(scope ? { scope } : {}),
    ...(loginHint ? { loginHint } : {}),
    ...(privateKeyPath ? { privateKeyPath } : {}),
    ...(privateKeyAlg ? { privateKeyAlg } : {}),
    ...(dpopKeyPath ? { dpopKeyPath } : {}),
    ...(dpopKeyAlg ? { dpopKeyAlg } : {}),
  };
  return { kind: 'config', config };
}

/** Projects the parsed config into the probe-runner context (the confidential client, public client, scope). */
export function toProbeContext(config: RunnerConfig): {
  client?: { clientId: string; clientSecret: string; authMethod: ClientAuthMethod };
  publicClientId?: string;
  scope?: string;
  loginHint?: string;
} {
  return {
    ...(config.clientId && config.clientSecret
      ? {
          client: {
            clientId: config.clientId,
            clientSecret: config.clientSecret,
            authMethod: config.authMethod,
          },
        }
      : {}),
    ...(config.publicClientId ? { publicClientId: config.publicClientId } : {}),
    ...(config.scope ? { scope: config.scope } : {}),
    ...(config.loginHint ? { loginHint: config.loginHint } : {}),
  };
}
