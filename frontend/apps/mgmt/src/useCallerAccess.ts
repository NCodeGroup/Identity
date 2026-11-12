import { useMemo } from 'react';
import {
  useGetCallerQuery,
  useListCallerTeamsQuery,
  useListCallerTenantsQuery,
} from '@ncode/store';
import { toCallerAccess, type CallerAccess } from './access.js';

/**
 * Where the signed-in caller holds authority (ADR-1465). Runs the caller's three discovery queries, skipped while
 * signed out, and combines their answers. The result is `undefined` until all three have answered, so callers keep
 * their defaults while the caller's access is unknown.
 */
export function useCallerAccess(isSignedIn: boolean): CallerAccess | undefined {
  const caller = useGetCallerQuery(undefined, { skip: !isSignedIn });
  const callerTeams = useListCallerTeamsQuery(undefined, { skip: !isSignedIn });
  const callerTenants = useListCallerTenantsQuery(undefined, { skip: !isSignedIn });
  const callerData = caller.data;
  const teamItems = callerTeams.data?.items;
  const tenantItems = callerTenants.data?.items;

  return useMemo(
    () =>
      isSignedIn && callerData && teamItems && tenantItems
        ? toCallerAccess(callerData, teamItems, tenantItems)
        : undefined,
    [isSignedIn, callerData, teamItems, tenantItems],
  );
}
