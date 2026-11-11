import { describe, expect, it } from 'vitest';
import {
  isPlatformScope,
  LIST_TAG,
  PLATFORM_SCOPE,
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
