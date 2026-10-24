import { describe, expect, it } from 'vitest';
import { ownerApiPath, ownerEffectiveTagId, ownerScope, ownerTagId } from './owner.js';
import type { ManagementOwner } from './owner.js';
import { GLOBAL_SCOPE, tenantScope } from './scope.js';

const acme = tenantScope('acme');

const server: ManagementOwner = { kind: 'server', serverId: 'root' };
const tenant: ManagementOwner = { kind: 'tenant', tenantId: 'acme' };
const tenantClient: ManagementOwner = { kind: 'client', scope: acme, clientId: 'web' };
const globalClient: ManagementOwner = { kind: 'client', scope: GLOBAL_SCOPE, clientId: 'web' };
const tenantResourceServer: ManagementOwner = {
  kind: 'resource-server',
  scope: acme,
  resourceServerId: 'rs-1',
};
const globalResourceServer: ManagementOwner = {
  kind: 'resource-server',
  scope: GLOBAL_SCOPE,
  resourceServerId: 'rs-1',
};

describe('ownerScope', () => {
  it('places a server in the global scope', () => {
    expect(ownerScope(server)).toEqual(GLOBAL_SCOPE);
  });

  it('makes a tenant its own scope', () => {
    expect(ownerScope(tenant)).toEqual({ kind: 'tenant', tenantId: 'acme' });
  });

  it('uses the scope of a client', () => {
    expect(ownerScope(tenantClient)).toEqual(acme);
    expect(ownerScope(globalClient)).toEqual(GLOBAL_SCOPE);
  });

  it('uses the scope of a resource server', () => {
    expect(ownerScope(tenantResourceServer)).toEqual(acme);
    expect(ownerScope(globalResourceServer)).toEqual(GLOBAL_SCOPE);
  });
});

describe('ownerApiPath', () => {
  it('addresses a server', () => {
    expect(ownerApiPath(server)).toBe('api/servers/root');
  });

  it('addresses a tenant', () => {
    expect(ownerApiPath(tenant)).toBe('api/tenants/acme');
  });

  it('nests a client under its scope', () => {
    expect(ownerApiPath(tenantClient)).toBe('api/tenants/acme/clients/web');
    expect(ownerApiPath(globalClient)).toBe('api/clients/web');
  });

  it('nests a resource server under its scope', () => {
    expect(ownerApiPath(tenantResourceServer)).toBe('api/tenants/acme/resource-servers/rs-1');
    expect(ownerApiPath(globalResourceServer)).toBe('api/resource-servers/rs-1');
  });

  it('url-encodes the owner id to prevent path escape', () => {
    expect(ownerApiPath({ kind: 'server', serverId: 'a/b' })).toBe('api/servers/a%2Fb');
  });
});

describe('ownerTagId', () => {
  it('tags a server in the global scope', () => {
    expect(ownerTagId(server)).toBe('global:server:root');
  });

  it('tags a tenant in its own scope', () => {
    expect(ownerTagId(tenant)).toBe('tenant:acme:tenant:acme');
  });

  it('tags a client in its scope', () => {
    expect(ownerTagId(tenantClient)).toBe('tenant:acme:client:web');
  });

  it('tags a resource server in its scope', () => {
    expect(ownerTagId(globalResourceServer)).toBe('global:resource-server:rs-1');
  });

  it('url-encodes the owner id so it cannot run into the key', () => {
    expect(ownerTagId({ kind: 'client', scope: GLOBAL_SCOPE, clientId: 'a:b' })).toBe(
      'global:client:a%3Ab',
    );
  });
});

describe('ownerEffectiveTagId', () => {
  it('tags the effective settings of an owner apart from its own tag', () => {
    expect(ownerEffectiveTagId(tenantClient)).toBe('tenant:acme:client:web:effective');
    expect(ownerEffectiveTagId(tenantClient)).not.toBe(ownerTagId(tenantClient));
  });

  it('never equals the tag of another owner of the same kind', () => {
    const other: ManagementOwner = { kind: 'client', scope: acme, clientId: 'web:effective' };

    expect(ownerEffectiveTagId(tenantClient)).not.toBe(ownerTagId(other));
  });
});
