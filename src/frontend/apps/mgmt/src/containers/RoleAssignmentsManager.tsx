import { useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { ConfirmDialog, DataStateBoundary, RoleAssignmentTable } from '@ncode/ui';
import type { RoleList } from '@ncode/api';
import {
  apiErrorMessage,
  useAddRoleAssignmentMutation,
  useListRoleAssignmentsQuery,
  useRemoveRoleAssignmentMutation,
  type ApiError,
} from '@ncode/store';

export type RoleAssignmentsManagerProps = {
  /** The role and the node whose assignments are listed, granted and revoked. */
  readonly list: RoleList;
  readonly title: string;
  readonly description: string;
};

/**
 * Container: the principals that hold one role on a node (the owners of a team, tenant, client or resource server, or
 * the administrators of a team or tenant), with a form to grant the role to a principal and a confirmed revoke. One
 * container serves every list through the role-list endpoints; a refused grant or revoke shows the server's error.
 */
export function RoleAssignmentsManager({ list, title, description }: RoleAssignmentsManagerProps) {
  const { data, isLoading, error: loadError } = useListRoleAssignmentsQuery(list);
  const [addRoleAssignment, { isLoading: isGranting }] = useAddRoleAssignmentMutation();
  const [removeRoleAssignment, { isLoading: isRevoking }] = useRemoveRoleAssignmentMutation();
  const [principalId, setPrincipalId] = useState('');
  const [revoking, setRevoking] = useState<string>();
  const [error, setError] = useState<string>();

  const assignments = data?.items ?? [];
  const trimmed = principalId.trim();

  const handleGrant = async () => {
    if (trimmed.length === 0) {
      return;
    }
    setError(undefined);
    try {
      await addRoleAssignment({ list, principalId: trimmed }).unwrap();
      setPrincipalId('');
    } catch (grantError) {
      setError(apiErrorMessage(grantError as ApiError));
    }
  };

  const handleRevoke = async () => {
    if (revoking === undefined) {
      return;
    }
    setError(undefined);
    try {
      await removeRoleAssignment({ list, principalId: revoking }).unwrap();
    } catch (revokeError) {
      setError(apiErrorMessage(revokeError as ApiError));
    }
    setRevoking(undefined);
  };

  return (
    <Box>
      <Typography variant="subtitle1" component="h3">
        {title}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
        {description}
      </Typography>
      {error && (
        <Alert severity="error" sx={{ mb: 1 }} data-testid={`${list.role}-error`}>
          {error}
        </Alert>
      )}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <TextField
          size="small"
          label="Principal ID"
          value={principalId}
          onChange={(event) => setPrincipalId(event.target.value)}
          slotProps={{ htmlInput: { 'data-testid': `${list.role}-principal` } }}
        />
        <Button
          variant="contained"
          disabled={trimmed.length === 0 || isGranting}
          onClick={handleGrant}
          data-testid={`${list.role}-grant`}
        >
          Grant
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={loadError ? `Failed to load ${title.toLowerCase()}.` : undefined}
        isEmpty={assignments.length === 0}
        emptyMessage="No one holds this role."
      >
        <RoleAssignmentTable
          label={title}
          testIdPrefix={list.role}
          assignments={assignments}
          onRevoke={setRevoking}
        />
      </DataStateBoundary>
      <ConfirmDialog
        open={revoking !== undefined}
        title="Revoke role"
        message={`Revoke this role from ${revoking ?? ''}?`}
        confirmLabel="Revoke"
        isBusy={isRevoking}
        onConfirm={handleRevoke}
        onCancel={() => setRevoking(undefined)}
      />
    </Box>
  );
}
