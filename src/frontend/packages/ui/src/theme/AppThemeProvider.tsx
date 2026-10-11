import type { ReactNode } from 'react';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider } from '@mui/material/styles';
import type { BrandingConfig } from '@ncode/config';
import { createAppTheme } from './createAppTheme.js';

export type AppThemeProviderProps = {
  readonly branding?: BrandingConfig;
  readonly children: ReactNode;
};

/** Stateless theme provider: derives the MUI theme from runtime branding and applies CssBaseline. */
export function AppThemeProvider({ branding, children }: AppThemeProviderProps) {
  return (
    <ThemeProvider theme={createAppTheme(branding)}>
      <CssBaseline />
      {children}
    </ThemeProvider>
  );
}
