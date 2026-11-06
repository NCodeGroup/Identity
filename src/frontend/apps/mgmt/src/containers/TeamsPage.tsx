import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  TeamFormDialog,
  TeamTable,
  emptyTeamFormValues,
  type TeamFormValues,
} from '@ncode/ui';
import type { TeamResource } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateChildTeamMutation,
  useCreateTeamMutation,
  useDeleteTeamMutation,
  useListChildTeamsQuery,
  useListTeamsQuery,
  useUpdateTeamMutation,
  type ApiError,
} from '@ncode/store';

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly team: TeamResource }
  | { readonly kind: 'delete'; readonly team: TeamResource };

const closed: DialogState = { kind: 'none' };

export type TeamsPageProps = {
  /** Opens a team's own page. */
  readonly onOpenTeam: (teamId: string) => void;
  /** The team whose child teams this lists and creates; unset lists every team the caller can see. */
  readonly parentTeamId?: string;
};

/**
 * Container: the team browser and CRUD surface. Teams group tenants and child teams under one
 * administrative boundary. Without `parentTeamId` this is the platform scope's list of every team;
 * with it, the child teams of that team. It wires the create/edit/delete mutations to the stateless
 * {@link TeamTable}, {@link TeamFormDialog}, and {@link ConfirmDialog}, and hands a selected team to
 * the caller. Owns only dialog/error state; all persistence, cache invalidation, and error shaping
 * come from the store layer.
 */
export function TeamsPage({ onOpenTeam, parentTeamId }: TeamsPageProps) {
  const isChildList = parentTeamId !== undefined;
  const allTeams = useListTeamsQuery(undefined, { skip: isChildList });
  const childTeams = useListChildTeamsQuery({ teamId: parentTeamId ?? '' }, { skip: !isChildList });
  const { data, isLoading, error } = isChildList ? childTeams : allTeams;
  const teams = data?.items ?? [];

  const [createRootTeam, { isLoading: isCreatingRoot }] = useCreateTeamMutation();
  const [createChildTeam, { isLoading: isCreatingChild }] = useCreateChildTeamMutation();
  const isCreating = isCreatingRoot || isCreatingChild;
  const [updateTeam, { isLoading: isUpdating }] = useUpdateTeamMutation();
  const [deleteTeam, { isLoading: isDeleting }] = useDeleteTeamMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();

  const openCreate = () => {
    setFormError(undefined);
    setDialog({ kind: 'create' });
  };
  const openEdit = (teamId: string) => {
    const team = teams.find((t) => t.teamId === teamId);
    if (!team) {
      return;
    }
    setFormError(undefined);
    setDialog({ kind: 'edit', team });
  };
  const openDelete = (teamId: string) => {
    const team = teams.find((t) => t.teamId === teamId);
    if (!team) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', team });
  };

  const formOpen = dialog.kind === 'create' || dialog.kind === 'edit';
  const formInitial: TeamFormValues =
    dialog.kind === 'edit'
      ? {
          teamId: dialog.team.teamId,
          displayName: dialog.team.displayName,
          isDisabled: dialog.team.isDisabled,
        }
      : emptyTeamFormValues;

  const handleSubmit = async (values: TeamFormValues) => {
    setFormError(undefined);
    try {
      if (dialog.kind === 'create') {
        const body = {
          teamId: values.teamId,
          displayName: values.displayName,
          isDisabled: values.isDisabled,
          settings: {},
        };
        if (parentTeamId === undefined) {
          await createRootTeam(body).unwrap();
        } else {
          await createChildTeam({ teamId: parentTeamId, body }).unwrap();
        }
      } else if (dialog.kind === 'edit') {
        await updateTeam({
          teamId: dialog.team.teamId,
          concurrencyToken: dialog.team.concurrencyToken,
          fields: { displayName: values.displayName, isDisabled: values.isDisabled },
        }).unwrap();
      }
      setDialog(closed);
    } catch (submitError) {
      setFormError(apiErrorMessage(submitError as ApiError));
    }
  };

  const handleDelete = async () => {
    if (dialog.kind !== 'delete') {
      return;
    }
    setDeleteError(undefined);
    try {
      await deleteTeam({ teamId: dialog.team.teamId }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
        <Typography variant="h5" component="h2">
          Teams
        </Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={openCreate}
          data-testid="create-team"
        >
          Create team
        </Button>
      </Box>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load teams.' : undefined}
        isEmpty={teams.length === 0}
        emptyMessage={isChildList ? 'No teams beneath this team yet.' : 'No teams exist yet.'}
      >
        <TeamTable
          showParent={!isChildList}
          teams={teams.map((t) => ({
            teamId: t.teamId,
            displayName: t.displayName,
            isDisabled: t.isDisabled,
            ...(t.parentTeamId === undefined ? {} : { parentTeamId: t.parentTeamId }),
          }))}
          onSelect={onOpenTeam}
          onEdit={openEdit}
          onDelete={openDelete}
        />
      </DataStateBoundary>

      <TeamFormDialog
        open={formOpen}
        title={dialog.kind === 'edit' ? 'Edit team' : 'Create team'}
        submitLabel={dialog.kind === 'edit' ? 'Save' : 'Create'}
        initialValues={formInitial}
        isTeamIdLocked={dialog.kind === 'edit'}
        isSubmitting={isCreating || isUpdating}
        error={formError}
        onSubmit={handleSubmit}
        onCancel={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'delete'}
        title="Delete team"
        message={
          dialog.kind === 'delete'
            ? `Delete “${dialog.team.displayName}”? Its tenants and teams must be deleted first.`
            : ''
        }
        isBusy={isDeleting}
        error={deleteError}
        onConfirm={handleDelete}
        onCancel={() => setDialog(closed)}
      />
    </>
  );
}
