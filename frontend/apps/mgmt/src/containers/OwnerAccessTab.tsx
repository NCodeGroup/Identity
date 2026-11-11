import Stack from '@mui/material/Stack';
import type { RoleOwner } from '@ncode/api';
import { RoleAssignmentsManager } from './RoleAssignmentsManager.js';

export type OwnerAccessTabProps = {
  readonly owner: RoleOwner;
};

/** The noun that names a role owner in the copy of its role lists. */
function ownerNoun(owner: RoleOwner): string {
  return owner.kind === 'resource-server' ? 'resource server' : owner.kind;
}

/**
 * Container: who holds roles on a team, tenant, client or resource server. A team and a tenant list their
 * administrators and their owners; a client and a resource server list their owners.
 */
export function OwnerAccessTab({ owner }: OwnerAccessTabProps) {
  return (
    <Stack spacing={3}>
      {owner.kind === 'team' && (
        <RoleAssignmentsManager
          list={{ role: 'admins', owner }}
          title="Administrators"
          description="Administrators manage the team and everything beneath it, and create its tenants and child teams."
        />
      )}
      {owner.kind === 'tenant' && (
        <RoleAssignmentsManager
          list={{ role: 'admins', owner }}
          title="Administrators"
          description="Administrators manage the tenant and everything beneath it, and create its clients and resource servers."
        />
      )}
      <RoleAssignmentsManager
        list={{ role: 'owners', owner }}
        title="Owners"
        description={`Owners manage this ${ownerNoun(owner)} and everything beneath it.`}
      />
    </Stack>
  );
}
