import { useEffect } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { DataStateBoundary, TeamTable, TenantTable } from '@ncode/ui';
import {
  useGetCallerQuery,
  useListCallerTeamsQuery,
  useListCallerTenantsQuery,
} from '@ncode/store';
import { soleDestination } from '../access.js';
import { platformRoute, teamRoute, tenantRoute, type ConsoleRoute } from '../routes.js';
import { useCallerAccess } from '../useCallerAccess.js';

export type HomePageProps = {
  /** Opens a route; `replace` swaps the home entry in the history instead of adding one. */
  readonly onNavigate: (route: ConsoleRoute, options?: { readonly replace?: boolean }) => void;
};

/**
 * Container: the caller's home (ADR-1465). Lists the places the signed-in caller holds authority: the
 * platform for a platform administrator, and the teams and tenants at which it holds a role. When exactly one
 * destination exists it opens it at once, replacing the home entry. Owns no state; the caller's access comes
 * from the store layer.
 */
export function HomePage({ onNavigate }: HomePageProps) {
  const caller = useGetCallerQuery();
  const teamsQuery = useListCallerTeamsQuery();
  const tenantsQuery = useListCallerTenantsQuery();

  const teams = teamsQuery.data?.items;
  const tenants = tenantsQuery.data?.items;
  const callerData = caller.data;

  const access = useCallerAccess(true);

  useEffect(() => {
    if (!access) {
      return;
    }
    const destination = soleDestination(access);
    if (destination) {
      onNavigate(destination, { replace: true });
    }
  }, [access, onNavigate]);

  const isLoading = caller.isLoading || teamsQuery.isLoading || tenantsQuery.isLoading;
  const failed = Boolean(caller.error || teamsQuery.error || tenantsQuery.error);
  const teamItems = teams ?? [];
  const tenantItems = tenants ?? [];
  const isPlatformAdministrator = callerData?.isPlatformAdministrator ?? false;
  const isEmpty = !isPlatformAdministrator && teamItems.length === 0 && tenantItems.length === 0;

  return (
    <>
      <Typography variant="h5" component="h2" sx={{ mb: 1 }}>
        Home
      </Typography>
      <DataStateBoundary
        isLoading={isLoading}
        error={failed ? 'Failed to load your access.' : undefined}
        isEmpty={isEmpty}
        emptyMessage="You hold no role in this console. Ask an administrator for access to a team or a tenant."
      >
        {isPlatformAdministrator && (
          <Box sx={{ mb: 3 }}>
            <Button
              variant="contained"
              onClick={() => onNavigate(platformRoute('teams'))}
              data-testid="home-open-platform"
            >
              Open the platform
            </Button>
          </Box>
        )}
        {teamItems.length > 0 && (
          <Box sx={{ mb: 3 }}>
            <Typography variant="h6" component="h3" sx={{ mb: 1 }}>
              Teams
            </Typography>
            <TeamTable
              teams={teamItems.map((t) => ({
                teamId: t.teamId,
                displayName: t.displayName,
                isDisabled: t.isDisabled,
              }))}
              onSelect={(teamId) => onNavigate(teamRoute(teamId))}
            />
          </Box>
        )}
        {tenantItems.length > 0 && (
          <Box sx={{ mb: 3 }}>
            <Typography variant="h6" component="h3" sx={{ mb: 1 }}>
              Tenants
            </Typography>
            <TenantTable
              tenants={tenantItems.map((t) => ({
                tenantId: t.tenantId,
                displayName: t.displayName,
                issuer: t.issuer,
                mtlsEndpointAliasAuthority: t.mtlsEndpointAliasAuthority,
                isDisabled: t.isDisabled,
              }))}
              onSelect={(tenantId) => onNavigate(tenantRoute(tenantId))}
            />
          </Box>
        )}
      </DataStateBoundary>
    </>
  );
}
