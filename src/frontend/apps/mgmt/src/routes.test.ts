import { describe, expect, it } from 'vitest';
import { GLOBAL_SCOPE, tenantScope } from '@ncode/api';
import {
  DEFAULT_ROUTE,
  globalRoute,
  parseRoute,
  resolveRoute,
  routePath,
  routeScope,
  tenantRoute,
  TENANT_SURFACES,
  GLOBAL_SURFACES,
} from './routes.js';

describe('routePath', () => {
  it('formats the global surfaces without a tenant segment', () => {
    expect(routePath(globalRoute('tenants'))).toBe('/tenants');
    expect(routePath(globalRoute('servers'))).toBe('/servers');
  });

  it('nests a tenant surface under its tenant', () => {
    expect(routePath(tenantRoute('acme', 'resource-servers'))).toBe(
      '/tenants/acme/resource-servers',
    );
  });

  it("nests each of the tenant's own surfaces under its tenant", () => {
    expect(routePath(tenantRoute('acme', 'secrets'))).toBe('/tenants/acme/secrets');
    expect(routePath(tenantRoute('acme', 'usage'))).toBe('/tenants/acme/usage');
  });

  it('opens a tenant on its clients by default', () => {
    expect(routePath(tenantRoute('acme'))).toBe('/tenants/acme/clients');
  });

  it('percent-encodes the tenant id as a single segment', () => {
    expect(routePath(tenantRoute('a/b c', 'grants'))).toBe('/tenants/a%2Fb%20c/grants');
  });
});

describe('tenantRoute', () => {
  it('rejects a blank tenant id', () => {
    expect(() => tenantRoute('  ')).toThrow(/non-empty/);
  });
});

describe('parseRoute', () => {
  it.each(GLOBAL_SURFACES)('round-trips the global %s surface', (surface) => {
    expect(parseRoute(routePath(globalRoute(surface)))).toEqual(globalRoute(surface));
  });

  it.each(TENANT_SURFACES)('round-trips the tenant %s surface', (surface) => {
    expect(parseRoute(routePath(tenantRoute('acme', surface)))).toEqual(
      tenantRoute('acme', surface),
    );
  });

  it('decodes a percent-encoded tenant id', () => {
    expect(parseRoute('/tenants/a%2Fb%20c/grants')).toEqual(tenantRoute('a/b c', 'grants'));
  });

  it('tolerates a single trailing slash', () => {
    expect(parseRoute('/servers/')).toEqual(globalRoute('servers'));
    expect(parseRoute('/tenants/acme/grants/')).toEqual(tenantRoute('acme', 'grants'));
  });

  it('names the default surface for a tenant path without one', () => {
    expect(parseRoute('/tenants/acme')).toEqual(tenantRoute('acme'));
  });

  it.each([
    ['the bare origin', '/'],
    ['a relative path', 'tenants'],
    ['an unknown global surface', '/settings'],
    ['an unknown tenant surface', '/tenants/acme/servers'],
    ['a surface outside the tenants root', '/servers/acme/clients'],
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
  });

  it('falls back to the default route for a path that names none', () => {
    expect(resolveRoute('/nowhere')).toEqual({ route: DEFAULT_ROUTE, path: '/tenants' });
  });
});

describe('routeScope', () => {
  it('derives the global scope from a global route', () => {
    expect(routeScope(globalRoute('servers'))).toEqual(GLOBAL_SCOPE);
  });

  it('derives the tenant scope from a tenant route', () => {
    expect(routeScope(tenantRoute('acme', 'settings'))).toEqual(tenantScope('acme'));
  });
});
