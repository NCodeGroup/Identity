import { describe, expect, it } from 'vitest';
import { ownerApiPath, ownerEffectiveTagId, ownerScope, ownerTagId } from './owner.js';
import type { ManagementOwner } from './owner.js';
import { PLATFORM_SCOPE, teamScope, tenantScope } from './scope.js';

const acme = tenantScope('acme');

const platform: ManagementOwner = { kind: 'platform' };
const team: ManagementOwner = { kind: 'team', teamId: 'acme' };
const tenant: ManagementOwner = { kind: 'tenant', tenantId: 'acme' };
const tenantClient: ManagementOwner = { kind: 'client', scope: acme, clientId: 'web' };
const platformClient: ManagementOwner = { kind: 'client', scope: PLATFORM_SCOPE, clientId: 'web' };
const tenantResourceServer: ManagementOwner = {
  kind: 'resource-server',
  scope: acme,
  resourceServerId: 'rs-1',
};
const platformResourceServer: ManagementOwner = {
  kind: 'resource-server',
  scope: PLATFORM_SCOPE,
  resourceServerId: 'rs-1',
};

describe('ownerScope', () => {
  it('places the platform in the platform scope', () => {
    expect(ownerScope(platform)).toEqual(PLATFORM_SCOPE);
  });

  it('makes a team its own scope', () => {
    expect(ownerScope(team)).toEqual(teamScope('acme'));
  });

  it('makes a tenant its own scope', () => {
    expect(ownerScope(tenant)).toEqual({ kind: 'tenant', tenantId: 'acme' });
  });

  it('uses the scope of a client', () => {
    expect(ownerScope(tenantClient)).toEqual(acme);
    expect(ownerScope(platformClient)).toEqual(PLATFORM_SCOPE);
  });

  it('uses the scope of a resource server', () => {
    expect(ownerScope(tenantResourceServer)).toEqual(acme);
    expect(ownerScope(platformResourceServer)).toEqual(PLATFORM_SCOPE);
  });
});

describe('ownerApiPath', () => {
  it('addresses the platform', () => {
    expect(ownerApiPath(platform)).toBe('api/platform');
  });

  it('addresses a team', () => {
    expect(ownerApiPath(team)).toBe('api/teams/acme');
  });

  it('addresses a tenant', () => {
    expect(ownerApiPath(tenant)).toBe('api/tenants/acme');
  });

  it('nests a client under its scope', () => {
    expect(ownerApiPath(tenantClient)).toBe('api/tenants/acme/clients/web');
    expect(ownerApiPath(platformClient)).toBe('api/clients/web');
  });

  it('nests a resource server under its scope', () => {
    expect(ownerApiPath(tenantResourceServer)).toBe('api/tenants/acme/resource-servers/rs-1');
    expect(ownerApiPath(platformResourceServer)).toBe('api/resource-servers/rs-1');
  });

  it('url-encodes the owner id to prevent path escape', () => {
    expect(ownerApiPath({ kind: 'team', teamId: 'a/b' })).toBe('api/teams/a%2Fb');
  });
});

describe('ownerTagId', () => {
  it('tags the platform in the platform scope', () => {
    expect(ownerTagId(platform)).toBe('platform:platform');
  });

  it('tags a team in its own scope', () => {
    expect(ownerTagId(team)).toBe('team:acme:team:acme');
  });

  it('url-encodes a team id with a reserved character', () => {
    expect(ownerTagId({ kind: 'team', teamId: 'a:b' })).toBe('team:a%3Ab:team:a%3Ab');
  });

  it('tags a tenant in its own scope', () => {
    expect(ownerTagId(tenant)).toBe('tenant:acme:tenant:acme');
  });

  it('tags a client in its scope', () => {
    expect(ownerTagId(tenantClient)).toBe('tenant:acme:client:web');
  });

  it('tags a resource server in its scope', () => {
    expect(ownerTagId(platformResourceServer)).toBe('platform:resource-server:rs-1');
  });

  it('url-encodes the owner id so it cannot run into the key', () => {
    expect(ownerTagId({ kind: 'client', scope: PLATFORM_SCOPE, clientId: 'a:b' })).toBe(
      'platform:client:a%3Ab',
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
