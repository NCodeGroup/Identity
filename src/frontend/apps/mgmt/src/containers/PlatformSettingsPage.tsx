import Typography from '@mui/material/Typography';
import { OwnerSettingsTab } from './OwnerSettingsTab.js';

/**
 * Container: the platform's own settings, the baseline every team and tenant inherits. Wires the
 * owner-generic {@link OwnerSettingsTab} to the platform owner.
 */
export function PlatformSettingsPage() {
  return (
    <>
      <Typography variant="h5" component="h2" sx={{ mb: 2 }}>
        Platform settings
      </Typography>
      <OwnerSettingsTab owner={{ kind: 'platform' }} />
    </>
  );
}
