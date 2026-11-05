import { useState } from 'react';
import Alert from '@mui/material/Alert';
import AutorenewIcon from '@mui/icons-material/Autorenew';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { EncryptionKeyOwner } from '@ncode/api';
import {
  apiErrorMessage,
  useListEncryptionKeysQuery,
  useRekeyEncryptionKeysMutation,
  type ApiError,
} from '@ncode/store';
import { ConfirmDialog, DataStateBoundary, EncryptionKeyTable } from '@ncode/ui';

export type OwnerEncryptionKeysTabProps = {
  readonly owner: EncryptionKeyOwner;
  /** The owner noun used in headings and copy (e.g. "tenant", "platform"). */
  readonly ownerLabel: string;
};

/**
 * Container: the master keys of a tenant or of the platform (ADR-1407) with a "Rekey" action. A rekey retires the active
 * key and starts the next version, so it is confirmed first. A tenant's values are rewrapped under the next version in
 * the background. The platform's next version protects the Data Protection keys and host data created afterwards,
 * existing Data Protection keys keep naming the old version until they expire, and the old key is destroyed once
 * nothing names it. The API returns no key material, and neither does this view. A rekey that loses a race to another
 * rekey shows the server's explanation in the dialog.
 */
export function OwnerEncryptionKeysTab({ owner, ownerLabel }: OwnerEncryptionKeysTabProps) {
  const { data, isLoading, error } = useListEncryptionKeysQuery(owner);
  const [rekey, { isLoading: isRekeying }] = useRekeyEncryptionKeysMutation();
  const [confirming, setConfirming] = useState(false);
  const [rekeyError, setRekeyError] = useState<string | undefined>(undefined);

  const closeDialog = () => {
    setConfirming(false);
    setRekeyError(undefined);
  };

  const handleConfirm = async () => {
    setRekeyError(undefined);
    try {
      await rekey(owner).unwrap();
      closeDialog();
    } catch (failure) {
      setRekeyError(apiErrorMessage(failure as ApiError));
    }
  };

  const rekeyMessage =
    owner.kind === 'platform'
      ? `Retire the active encryption key of this ${ownerLabel} and start the next version? The next version protects the Data Protection keys and host data created afterwards. Existing Data Protection keys keep naming the old version until they expire, and the old key is destroyed once nothing names it.`
      : `Retire the active encryption key of this ${ownerLabel} and start the next version? The values the old key protects are rewrapped under the new one in the background, and the old key is destroyed once nothing uses it.`;

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
        <Typography variant="h6" component="h3">
          Encryption keys
        </Typography>
        <Button
          variant="outlined"
          startIcon={<AutorenewIcon />}
          onClick={() => setConfirming(true)}
          data-testid="encryption-keys-rekey"
        >
          Rekey
        </Button>
      </Stack>
      {data && !data.recoveryConfigured && (
        <Alert severity="warning" data-testid="encryption-keys-no-recovery">
          New keys are not wrapped under a recovery key, so losing the root key loses every value
          this {ownerLabel} protects.
        </Alert>
      )}
      <DataStateBoundary
        isLoading={isLoading}
        error={error ? 'Failed to load encryption keys.' : undefined}
        isEmpty={(data?.keys.length ?? 0) === 0}
        emptyMessage={`This ${ownerLabel} has no encryption keys yet; the first is created when it first protects a secret.`}
      >
        <EncryptionKeyTable keys={data?.keys ?? []} />
      </DataStateBoundary>
      <ConfirmDialog
        open={confirming}
        title="Rekey"
        message={rekeyMessage}
        confirmLabel="Rekey"
        confirmColor="primary"
        isBusy={isRekeying}
        error={rekeyError}
        onConfirm={() => void handleConfirm()}
        onCancel={closeDialog}
      />
    </Stack>
  );
}
