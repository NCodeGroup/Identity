import { describe, expect, it } from 'vitest';
import { PLATFORM_SCOPE, teamScope, tenantScope } from '@ncode/api';
import {
  DEFAULT_ROUTE,
  HOME_ROUTE,
  parseRoute,
  platformRoute,
  PLATFORM_SURFACES,
  resolveRoute,
  routePath,
  routeScope,
  teamRoute,
  TEAM_SURFACES,
  tenantRoute,
  TENANT_SURFACES,
} from './routes.js';

describe('routePath', () => {
  it('formats the caller home as /home', () => {
    expect(routePath(HOME_ROUTE)).toBe('/home');
  });

  it('formats the platform surfaces without a scope segment', () => {
    expect(routePath(platformRoute('teams'))).toBe('/teams');
    expect(routePath(platformRoute('settings'))).toBe('/settings');
  });

  it('nests a team surface under its team', () => {
    expect(routePath(teamRoute('acme', 'usage'))).toBe('/teams/acme/usage');
    expect(routePath(teamRoute('acme', 'settings'))).toBe('/teams/acme/settings');
  });

  it('nests the child teams surface under its team', () => {
    expect(routePath(teamRoute('acme', 'teams'))).toBe('/teams/acme/teams');
    expect(parseRoute('/teams/acme/teams')).toEqual(teamRoute('acme', 'teams'));
  });

  it('opens a team on its tenants by default', () => {
    expect(routePath(teamRoute('acme'))).toBe('/teams/acme/tenants');
  });

  it('percent-encodes the team id as a single segment', () => {
    expect(routePath(teamRoute('a/b c', 'tenants'))).toBe('/teams/a%2Fb%20c/tenants');
  });

  it('nests a tenant surface under its tenant', () => {
    expect(routePath(tenantRoute('acme', 'resource-servers'))).toBe(
      '/tenants/acme/resource-servers',
    );
  });

  it("nests each of the tenant's own surfaces under its tenant", () => {
    expect(routePath(tenantRoute('acme', 'secrets'))).toBe('/tenants/acme/secrets');
    expect(routePath(tenantRoute('acme', 'encryption-keys'))).toBe('/tenants/acme/encryption-keys');
    expect(routePath(tenantRoute('acme', 'usage'))).toBe('/tenants/acme/usage');
  });

  it('opens a tenant on its clients by default', () => {
    expect(routePath(tenantRoute('acme'))).toBe('/tenants/acme/clients');
  });

  it('percent-encodes the tenant id as a single segment', () => {
    expect(routePath(tenantRoute('a/b c', 'grants'))).toBe('/tenants/a%2Fb%20c/grants');
  });
});

describe('TENANT_SURFACES', () => {
  it('lists the tenant encryption keys right after its secrets', () => {
    expect(TENANT_SURFACES.indexOf('encryption-keys')).toBe(TENANT_SURFACES.indexOf('secrets') + 1);
  });
});

describe('teamRoute', () => {
  it('rejects a blank team id', () => {
    expect(() => teamRoute('  ')).toThrow(/non-empty/);
  });
});

describe('tenantRoute', () => {
  it('rejects a blank tenant id', () => {
    expect(() => tenantRoute('  ')).toThrow(/non-empty/);
  });
});

describe('parseRoute', () => {
  it.each(PLATFORM_SURFACES)('round-trips the platform %s surface', (surface) => {
    expect(parseRoute(routePath(platformRoute(surface)))).toEqual(platformRoute(surface));
  });

  it.each(TEAM_SURFACES)('round-trips the team %s surface', (surface) => {
    expect(parseRoute(routePath(teamRoute('acme', surface)))).toEqual(teamRoute('acme', surface));
  });

  it.each(TENANT_SURFACES)('round-trips the tenant %s surface', (surface) => {
    expect(parseRoute(routePath(tenantRoute('acme', surface)))).toEqual(
      tenantRoute('acme', surface),
    );
  });

  it('parses /home as the caller home', () => {
    expect(parseRoute('/home')).toEqual(HOME_ROUTE);
  });

  it('decodes a percent-encoded team id', () => {
    expect(parseRoute('/teams/a%2Fb%20c/usage')).toEqual(teamRoute('a/b c', 'usage'));
  });

  it('decodes a percent-encoded tenant id', () => {
    expect(parseRoute('/tenants/a%2Fb%20c/grants')).toEqual(tenantRoute('a/b c', 'grants'));
  });

  it('tolerates a single trailing slash', () => {
    expect(parseRoute('/settings/')).toEqual(platformRoute('settings'));
    expect(parseRoute('/teams/acme/usage/')).toEqual(teamRoute('acme', 'usage'));
    expect(parseRoute('/tenants/acme/grants/')).toEqual(tenantRoute('acme', 'grants'));
  });

  it('names the default surface for a team path without one', () => {
    expect(parseRoute('/teams/acme')).toEqual(teamRoute('acme'));
  });

  it('names the default surface for a tenant path without one', () => {
    expect(parseRoute('/tenants/acme')).toEqual(tenantRoute('acme'));
  });

  it.each([
    ['the bare origin', '/'],
    ['a relative path', 'teams'],
    ['an unknown platform surface', '/servers'],
    ['an unknown platform root', '/platform/x'],
    ['an unknown team surface', '/teams/acme/clients'],
    ['an unknown tenant surface', '/tenants/acme/servers'],
    ['a surface outside the teams root', '/settings/acme/usage'],
    ['a blank team id', '/teams/%20/tenants'],
    ['an empty team id', '/teams//tenants'],
    ['a malformed team percent-encoding', '/teams/%E0%A4%A/tenants'],
    ['extra team segments', '/teams/acme/tenants/web'],
    ['a blank tenant id', '/tenants/%20/clients'],
    ['an empty tenant id', '/tenants//clients'],
    ['a malformed percent-encoding', '/tenants/%E0%A4%A/clients'],
    ['extra segments', '/tenants/acme/clients/web'],
  ])('names no route for %s', (_name, pathname) => {
    expect(parseRoute(pathname)).toBeNull();
  });
});

describe('resolveRoute', () => {
  it('keeps a canonical path', () => {
    expect(resolveRoute('/tenants/acme/grants')).toEqual({
      route: tenantRoute('acme', 'grants'),
      path: '/tenants/acme/grants',
    });
  });

  it('canonicalizes a path that names a route', () => {
    expect(resolveRoute('/tenants/acme').path).toBe('/tenants/acme/clients');
    expect(resolveRoute('/teams/acme').path).toBe('/teams/acme/tenants');
  });

  it('falls back to the default route for a path that names none', () => {
    expect(resolveRoute('/nowhere')).toEqual({ route: DEFAULT_ROUTE, path: '/home' });
  });
});

describe('routeScope', () => {
  it('derives the platform scope from a platform route', () => {
    expect(routeScope(platformRoute('settings'))).toEqual(PLATFORM_SCOPE);
  });

  it('derives the team scope from a team route', () => {
    expect(routeScope(teamRoute('acme', 'usage'))).toEqual(teamScope('acme'));
  });

  it('derives the tenant scope from a tenant route', () => {
    expect(routeScope(tenantRoute('acme', 'settings'))).toEqual(tenantScope('acme'));
  });
});
