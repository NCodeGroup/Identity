import { useState } from 'react';
import AddIcon from '@mui/icons-material/Add';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import {
  ConfirmDialog,
  DataStateBoundary,
  GrantTable,
  InitialAccessTokenFormDialog,
  RevealInitialAccessTokenDialog,
  type InitialAccessTokenFormValues,
} from '@ncode/ui';
import type { CreatedInitialAccessTokenResource, GrantResource } from '@ncode/api';
import {
  apiErrorMessage,
  useCreateInitialAccessTokenMutation,
  useListGrantsQuery,
  useRevokeGrantMutation,
  useRevokeGrantsMutation,
  type ApiError,
} from '@ncode/store';

export type GrantsPageProps = {
  readonly tenantId: string;
};

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'revoke'; readonly grant: GrantResource }
  | { readonly kind: 'revoke-many' }
  | { readonly kind: 'mint' }
  | { readonly kind: 'reveal'; readonly created: CreatedInitialAccessTokenResource };

const closed: DialogState = { kind: 'none' };

/**
 * Container: browses and revokes the persisted grants within a tenant. Grants are read-only
 * administratively, so there is no create/edit — only single revocation and a filtered bulk revoke
 * (which the server requires to carry at least one filter). Subject/client filters are applied as the
 * list query parameters. Wires the RTK Query grant list and revocation mutations to the stateless
 * {@link GrantTable} and {@link ConfirmDialog}; owns only filter/dialog/error state.
 */
export function GrantsPage({ tenantId }: GrantsPageProps) {
  // Read the clock once, in a state initializer: reading it during render would make the table's output impure.
  const [now] = useState(() => Date.now());
  const [subjectId, setSubjectId] = useState('');
  const [clientId, setClientId] = useState('');

  const trimmedSubject = subjectId.trim();
  const trimmedClient = clientId.trim();
  const hasFilter = trimmedSubject.length > 0 || trimmedClient.length > 0;

  const { data, isLoading, error } = useListGrantsQuery({
    tenantId,
    ...(trimmedSubject ? { subjectId: trimmedSubject } : {}),
    ...(trimmedClient ? { clientId: trimmedClient } : {}),
  });
  const grants = data?.items ?? [];

  const [revokeGrant, { isLoading: isRevoking }] = useRevokeGrantMutation();
  const [revokeGrants, { isLoading: isRevokingMany }] = useRevokeGrantsMutation();
  const [mintToken, { isLoading: isMinting }] = useCreateInitialAccessTokenMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [dialogError, setDialogError] = useState<string>();
  const [mintError, setMintError] = useState<string>();

  const openRevoke = (grantId: string) => {
    const grant = grants.find((g) => g.grantId === grantId);
    if (!grant) {
      return;
    }
    setDialogError(undefined);
    setDialog({ kind: 'revoke', grant });
  };

  const handleRevoke = async () => {
    if (dialog.kind !== 'revoke') {
      return;
    }
    setDialogError(undefined);
    try {
      await revokeGrant({ tenantId, grantId: dialog.grant.grantId }).unwrap();
      setDialog(closed);
    } catch (revokeError) {
      setDialogError(apiErrorMessage(revokeError as ApiError));
    }
  };

  const handleRevokeMany = async () => {
    if (dialog.kind !== 'revoke-many') {
      return;
    }
    setDialogError(undefined);
    try {
      await revokeGrants({
        tenantId,
        ...(trimmedSubject ? { subjectId: trimmedSubject } : {}),
        ...(trimmedClient ? { clientId: trimmedClient } : {}),
      }).unwrap();
      setDialog(closed);
    } catch (revokeError) {
      setDialogError(apiErrorMessage(revokeError as ApiError));
    }
  };

  const handleMint = async (values: InitialAccessTokenFormValues) => {
    setMintError(undefined);
    try {
      const created = await mintToken({
        tenantId,
        body: {
          description: values.description.length > 0 ? values.description : null,
          lifetimeSeconds:
            values.lifetimeSeconds.length > 0 ? Number(values.lifetimeSeconds) : null,
        },
      }).unwrap();
      setDialog({ kind: 'reveal', created });
    } catch (mintTokenError) {
      setMintError(apiErrorMessage(mintTokenError as ApiError));
    }
  };

  const filterLabel = [
    trimmedSubject ? `subject “${trimmedSubject}”` : null,
    trimmedClient ? `client “${trimmedClient}”` : null,
  ]
    .filter(Boolean)
    .join(' and ');

  return (
    <>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
        <Typography variant="h5" component="h2">
          Grants
        </Typography>
        <Stack direction="row" spacing={1}>
          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={() => {
              setMintError(undefined);
              setDialog({ kind: 'mint' });
            }}
            data-testid="mint-initial-access-token"
          >
            Mint initial access token
          </Button>
          <Button
            variant="outlined"
            color="error"
            disabled={!hasFilter}
            onClick={() => {
              setDialogError(undefined);
              setDialog({ kind: 'revoke-many' });
            }}
            data-testid="revoke-matching-grants"
          >
            Revoke matching
          </Button>
        </Stack>
      </Box>
      <Stack direction="row" spacing={2} mb={2}>
        <TextField
          size="small"
          label="Subject"
          value={subjectId}
          onChange={(event) => setSubjectId(event.target.value)}
          data-testid="grant-filter-subject"
        />
        <TextField
          size="small"
          label="Client"
          value={clientId}
          onChange={(event) => setClientId(event.target.value)}
          data-testid="grant-filter-client"
        />
      </Stack>
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load grants.' : undefined}
        isEmpty={grants.length === 0}
        emptyMessage="No grants match the current filter."
      >
        <GrantTable grants={grants} now={now} onRevoke={openRevoke} />
      </DataStateBoundary>

      <ConfirmDialog
        open={dialog.kind === 'revoke'}
        title="Revoke grant"
        confirmLabel="Revoke"
        message={
          dialog.kind === 'revoke'
            ? `Revoke grant “${dialog.grant.grantId}”? The client must re-authorize.`
            : ''
        }
        isBusy={isRevoking}
        error={dialogError}
        onConfirm={handleRevoke}
        onCancel={() => setDialog(closed)}
      />

      <ConfirmDialog
        open={dialog.kind === 'revoke-many'}
        title="Revoke matching grants"
        confirmLabel="Revoke all"
        message={`Revoke all active grants for ${filterLabel}? This cannot be undone.`}
        isBusy={isRevokingMany}
        error={dialogError}
        onConfirm={handleRevokeMany}
        onCancel={() => setDialog(closed)}
      />

      <InitialAccessTokenFormDialog
        open={dialog.kind === 'mint'}
        isSubmitting={isMinting}
        error={mintError}
        onSubmit={handleMint}
        onCancel={() => setDialog(closed)}
      />

      <RevealInitialAccessTokenDialog
        open={dialog.kind === 'reveal'}
        token={dialog.kind === 'reveal' ? dialog.created.initialAccessToken : ''}
        grantId={dialog.kind === 'reveal' ? dialog.created.grantId : ''}
        expiresWhen={dialog.kind === 'reveal' ? dialog.created.expiresWhen : null}
        onClose={() => setDialog(closed)}
      />
    </>
  );
}
