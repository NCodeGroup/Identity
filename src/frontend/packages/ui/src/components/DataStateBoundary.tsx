import type { ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';

export type DataStateBoundaryProps = {
  readonly isLoading: boolean;
  readonly error?: string | undefined;
  readonly isEmpty?: boolean;
  readonly emptyMessage?: string;
  readonly children: ReactNode;
};

/**
 * Stateless wrapper that renders the correct loading / error / empty / content state. Keeping this
 * out of every screen's container keeps presentational components prop-driven and Storybook-friendly.
 */
export function DataStateBoundary({
  isLoading,
  error,
  isEmpty = false,
  emptyMessage = 'Nothing to show yet.',
  children,
}: DataStateBoundaryProps) {
  if (isLoading) {
    return (
      <Box data-testid="data-loading" sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
        <CircularProgress />
      </Box>
    );
  }
  if (error) {
    return (
      <Alert severity="error" data-testid="data-error">
        {error}
      </Alert>
    );
  }
  if (isEmpty) {
    return (
      <Box data-testid="data-empty" sx={{ py: 6, textAlign: 'center' }}>
        <Typography sx={{ color: 'text.secondary' }}>{emptyMessage}</Typography>
      </Box>
    );
  }
  return <>{children}</>;
}
