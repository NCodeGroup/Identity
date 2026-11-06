import { describe, expect, it } from 'vitest';
import type { CallerResource, TeamResource, TenantResource } from '@ncode/api';
import {
  permittedRoute,
  soleDestination,
  teamExit,
  tenantExit,
  toCallerAccess,
  type CallerAccess,
} from './access.js';
import { HOME_ROUTE, platformRoute, teamRoute, tenantRoute } from './routes.js';

const admin: CallerAccess = { isPlatformAdministrator: true, teamIds: [], tenantIds: [] };
const member: CallerAccess = { isPlatformAdministrator: false, teamIds: ['acme'], tenantIds: [] };

describe('toCallerAccess', () => {
  it('combines the caller with the ids of its teams and tenants', () => {
    const caller: CallerResource = {
      principalId: 'p1',
      isPlatformAdministrator: false,
      assignments: [],
    };
    const teams = [{ teamId: 'acme' }, { teamId: 'beta' }] as TeamResource[];
    const tenants = [{ tenantId: 'web' }] as TenantResource[];

    expect(toCallerAccess(caller, teams, tenants)).toEqual({
      isPlatformAdministrator: false,
      teamIds: ['acme', 'beta'],
      tenantIds: ['web'],
    });
  });

  it('carries the platform administrator flag', () => {
    const caller: CallerResource = {
      principalId: null,
      isPlatformAdministrator: true,
      assignments: [],
    };

    expect(toCallerAccess(caller, [], []).isPlatformAdministrator).toBe(true);
  });
});

describe('soleDestination', () => {
  it('is null when the caller has no destination', () => {
    expect(soleDestination({ ...admin, isPlatformAdministrator: false })).toBeNull();
  });

  it('is the platform team list for a lone platform administrator', () => {
    expect(soleDestination(admin)).toEqual(platformRoute('teams'));
  });

  it('is the team of a caller holding one team', () => {
    expect(soleDestination(member)).toEqual(teamRoute('acme'));
  });

  it('is the tenant of a caller holding one tenant', () => {
    expect(soleDestination({ ...member, teamIds: [], tenantIds: ['web'] })).toEqual(
      tenantRoute('web'),
    );
  });

  it('is null when the caller has several destinations', () => {
    expect(soleDestination({ ...member, teamIds: ['acme', 'beta'] })).toBeNull();
    expect(soleDestination({ ...member, isPlatformAdministrator: true })).toBeNull();
  });
});

describe('teamExit', () => {
  it('leads to the platform while the access is unknown', () => {
    expect(teamExit(undefined)).toEqual({ route: platformRoute('teams'), label: 'Platform' });
  });

  it('leads a platform administrator to the platform', () => {
    expect(teamExit(admin)).toEqual({ route: platformRoute('teams'), label: 'Platform' });
  });

  it('leads any other caller home', () => {
    expect(teamExit(member)).toEqual({ route: HOME_ROUTE, label: 'Home' });
  });
});

describe('tenantExit', () => {
  it('leads to the team while the access is unknown', () => {
    expect(tenantExit(undefined, 'acme')).toEqual({
      route: teamRoute('acme', 'tenants'),
      label: 'Team',
    });
  });

  it('leads a platform administrator to the team', () => {
    expect(tenantExit(admin, 'acme')).toEqual({
      route: teamRoute('acme', 'tenants'),
      label: 'Team',
    });
  });

  it('leads a caller holding a role at the team to the team', () => {
    expect(tenantExit(member, 'acme')).toEqual({
      route: teamRoute('acme', 'tenants'),
      label: 'Team',
    });
  });

  it('leads a caller without a role at the team where a team leads', () => {
    expect(tenantExit(member, 'beta')).toEqual({ route: HOME_ROUTE, label: 'Home' });
  });

  it('leads where a team leads when the team is unknown', () => {
    expect(tenantExit(member, undefined)).toEqual({ route: HOME_ROUTE, label: 'Home' });
    expect(tenantExit(undefined, undefined)).toEqual({
      route: platformRoute('teams'),
      label: 'Platform',
    });
  });
});

describe('permittedRoute', () => {
  it('keeps a platform route while the access is unknown', () => {
    expect(permittedRoute(platformRoute('settings'), undefined)).toEqual(platformRoute('settings'));
  });

  it('keeps a platform route for a platform administrator', () => {
    expect(permittedRoute(platformRoute('settings'), admin)).toEqual(platformRoute('settings'));
  });

  it('replaces a platform route with home for any other caller', () => {
    expect(permittedRoute(platformRoute('settings'), member)).toEqual(HOME_ROUTE);
  });

  it('keeps a team route for any other caller', () => {
    expect(permittedRoute(teamRoute('acme'), member)).toEqual(teamRoute('acme'));
  });
});
