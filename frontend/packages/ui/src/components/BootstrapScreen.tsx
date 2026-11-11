import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';

export type BootstrapScreenProps = {
  /** Why the runtime configuration could not be loaded; absent while it is still loading. */
  readonly error?: string | undefined;
};

/**
 * Stateless full-page screen shown while a bundle discovers its runtime configuration, or after discovery fails. The
 * boot gate that loads the configuration renders it; nothing else can render before the configuration exists.
 */
export function BootstrapScreen({ error }: BootstrapScreenProps) {
  if (error) {
    return (
      <Box sx={{ p: 4 }}>
        <Alert severity="error">Configuration error: {error}</Alert>
      </Box>
    );
  }
  return (
    <Box
      sx={{
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        minHeight: '100vh',
      }}
    >
      <CircularProgress aria-label="Loading configuration" />
    </Box>
  );
}
