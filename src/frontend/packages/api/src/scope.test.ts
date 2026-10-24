import { describe, expect, it } from 'vitest';
import {
  GLOBAL_SCOPE,
  isGlobalScope,
  LIST_TAG,
  scopeApiPrefix,
  scopeKey,
  scopeTagId,
  tenantScope,
} from './scope.js';

describe('tenantScope', () => {
  it('builds a tenant scope', () => {
    expect(tenantScope('acme')).toEqual({ kind: 'tenant', tenantId: 'acme' });
  });

  it('rejects a blank tenant id', () => {
    expect(() => tenantScope('   ')).toThrow(/non-empty/);
  });
});

describe('scopeKey', () => {
  it('keys the global scope', () => {
    expect(scopeKey(GLOBAL_SCOPE)).toBe('global');
  });

  it('keys a tenant scope', () => {
    expect(scopeKey(tenantScope('acme'))).toBe('tenant:acme');
  });
});

describe('isGlobalScope', () => {
  it('is true for global', () => {
    expect(isGlobalScope(GLOBAL_SCOPE)).toBe(true);
  });

  it('is false for a tenant', () => {
    expect(isGlobalScope(tenantScope('acme'))).toBe(false);
  });
});

describe('scopeApiPrefix', () => {
  it('maps global to api', () => {
    expect(scopeApiPrefix(GLOBAL_SCOPE)).toBe('api');
  });

  it('nests tenant resources under their tenant', () => {
    expect(scopeApiPrefix(tenantScope('acme'))).toBe('api/tenants/acme');
  });

  it('url-encodes the tenant id to prevent path escape', () => {
    expect(scopeApiPrefix(tenantScope('a/b'))).toBe('api/tenants/a%2Fb');
  });
});

describe('scopeTagId', () => {
  it('prefixes a global tag with the global scope', () => {
    expect(scopeTagId(GLOBAL_SCOPE, LIST_TAG)).toBe('global:LIST');
  });

  it('prefixes a tenant tag with its tenant', () => {
    expect(scopeTagId(tenantScope('acme'), 'client:web')).toBe('tenant:acme:client:web');
  });

  it('gives the same resource id a different tag in every scope', () => {
    const ids = [GLOBAL_SCOPE, tenantScope('acme'), tenantScope('other')].map((scope) =>
      scopeTagId(scope, 'web'),
    );

    expect(new Set(ids).size).toBe(3);
  });

  it('url-encodes the tenant id so it cannot run into the resource id', () => {
    expect(scopeTagId(tenantScope('a:b'), 'c')).toBe('tenant:a%3Ab:c');
    expect(scopeTagId(tenantScope('a:b'), 'c')).not.toBe(scopeTagId(tenantScope('a'), 'b:c'));
  });
});
