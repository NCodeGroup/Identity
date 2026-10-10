import { useEffect, useState, type ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import { AppThemeProvider } from '@ncode/ui';
import { runtimeConfigLoaded, useAppDispatch, useAppSelector } from '@ncode/store';
import { bootstrapRuntimeConfig } from './bootstrap.js';

/**
 * Top-level stateful gate: discovers runtime configuration before rendering the app. Until config is
 * loaded, no API base URL exists, so nothing downstream can render meaningfully. Branding from the
 * discovered config themes the loading/error states too.
 */
export function BootstrapGate({ children }: { children: ReactNode }) {
  const dispatch = useAppDispatch();
  const runtime = useAppSelector((state) => state.config.runtime);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (runtime) {
      return;
    }
    const controller = new AbortController();
    bootstrapRuntimeConfig(controller.signal)
      .then((config) => dispatch(runtimeConfigLoaded(config)))
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : 'Failed to load configuration.');
        }
      });
    return () => controller.abort();
  }, [dispatch, runtime]);

  if (error) {
    return (
      <AppThemeProvider>
        <Box p={4}>
          <Alert severity="error">Configuration error: {error}</Alert>
        </Box>
      </AppThemeProvider>
    );
  }

  if (!runtime) {
    return (
      <AppThemeProvider>
        <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
          <CircularProgress />
        </Box>
      </AppThemeProvider>
    );
  }

  return (
    <AppThemeProvider {...(runtime.branding ? { branding: runtime.branding } : {})}>
      {children}
    </AppThemeProvider>
  );
}
