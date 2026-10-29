import Alert from '@mui/material/Alert';
import type { EnvironmentConfig } from '@ncode/config';

export type EnvironmentBannerProps = {
  /** The tenant's non-production environment; the host embeds it only for a tenant that is not production. */
  readonly environment: EnvironmentConfig;
};

/** Warns that the tenant is a non-production environment, so no one mistakes it for the real thing. */
export function EnvironmentBanner({ environment }: EnvironmentBannerProps) {
  const label = environment.name ?? environment.type;
  return (
    <Alert severity="warning" variant="filled" square sx={{ justifyContent: 'center' }}>
      {label} environment — not for production use
    </Alert>
  );
}
