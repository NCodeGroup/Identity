import { describe, expect, it } from 'vitest';
import {
  isPlatformScope,
  LIST_TAG,
  PLATFORM_SCOPE,
  scopeApiPrefix,
  scopeKey,
  scopeTagId,
  teamScope,
  tenantScope,
} from './scope.js';

describe('teamScope', () => {
  it('builds a team scope', () => {
    expect(teamScope('core')).toEqual({ kind: 'team', teamId: 'core' });
  });

  it('rejects a blank team id', () => {
    expect(() => teamScope('   ')).toThrow(/non-empty/);
  });
});

describe('tenantScope', () => {
  it('builds a tenant scope', () => {
    expect(tenantScope('acme')).toEqual({ kind: 'tenant', tenantId: 'acme' });
  });

  it('rejects a blank tenant id', () => {
    expect(() => tenantScope('   ')).toThrow(/non-empty/);
  });
});

describe('scopeKey', () => {
  it('keys the platform scope', () => {
    expect(scopeKey(PLATFORM_SCOPE)).toBe('platform');
  });

  it('keys a team scope', () => {
    expect(scopeKey(teamScope('core'))).toBe('team:core');
  });

  it('keys a tenant scope', () => {
    expect(scopeKey(tenantScope('acme'))).toBe('tenant:acme');
  });
});

describe('isPlatformScope', () => {
  it('is true for platform', () => {
    expect(isPlatformScope(PLATFORM_SCOPE)).toBe(true);
  });

  it('is false for a team', () => {
    expect(isPlatformScope(teamScope('core'))).toBe(false);
  });

  it('is false for a tenant', () => {
    expect(isPlatformScope(tenantScope('acme'))).toBe(false);
  });
});

describe('scopeApiPrefix', () => {
  it('maps platform to api', () => {
    expect(scopeApiPrefix(PLATFORM_SCOPE)).toBe('api');
  });

  it('nests team resources under their team', () => {
    expect(scopeApiPrefix(teamScope('core'))).toBe('api/teams/core');
  });

  it('url-encodes the team id to prevent path escape', () => {
    expect(scopeApiPrefix(teamScope('a/b'))).toBe('api/teams/a%2Fb');
  });

  it('nests tenant resources under their tenant', () => {
    expect(scopeApiPrefix(tenantScope('acme'))).toBe('api/tenants/acme');
  });

  it('url-encodes the tenant id to prevent path escape', () => {
    expect(scopeApiPrefix(tenantScope('a/b'))).toBe('api/tenants/a%2Fb');
  });
});

describe('scopeTagId', () => {
  it('prefixes a platform tag with the platform scope', () => {
    expect(scopeTagId(PLATFORM_SCOPE, LIST_TAG)).toBe('platform:LIST');
  });

  it('prefixes a team tag with its team', () => {
    expect(scopeTagId(teamScope('core'), LIST_TAG)).toBe('team:core:LIST');
  });

  it('prefixes a tenant tag with its tenant', () => {
    expect(scopeTagId(tenantScope('acme'), 'client:web')).toBe('tenant:acme:client:web');
  });

  it('gives the same resource id a different tag in every scope', () => {
    const ids = [PLATFORM_SCOPE, teamScope('acme'), tenantScope('acme'), tenantScope('other')].map(
      (scope) => scopeTagId(scope, 'web'),
    );

    expect(new Set(ids).size).toBe(4);
  });

  it('url-encodes the team id so it cannot run into the resource id', () => {
    expect(scopeTagId(teamScope('a:b'), 'c')).toBe('team:a%3Ab:c');
  });

  it('url-encodes the tenant id so it cannot run into the resource id', () => {
    expect(scopeTagId(tenantScope('a:b'), 'c')).toBe('tenant:a%3Ab:c');
    expect(scopeTagId(tenantScope('a:b'), 'c')).not.toBe(scopeTagId(tenantScope('a'), 'b:c'));
  });
});
