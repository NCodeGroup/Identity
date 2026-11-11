import Typography from '@mui/material/Typography';
import { OwnerEncryptionKeysTab } from './OwnerEncryptionKeysTab.js';

/**
 * Container: the platform's master keys, which protect the Data Protection key ring and the host's own
 * data, with a confirmed rekey action (ADR-1407). Wires the owner-generic {@link OwnerEncryptionKeysTab}
 * to the platform owner.
 */
export function PlatformEncryptionKeysPage() {
  return (
    <>
      <Typography variant="h5" component="h2" sx={{ mb: 2 }}>
        Platform encryption keys
      </Typography>
      <OwnerEncryptionKeysTab owner={{ kind: 'platform' }} ownerLabel="platform" />
    </>
  );
}
