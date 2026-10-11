import { createTheme, type Theme } from '@mui/material/styles';
import type { BrandingConfig } from '@ncode/config';

/**
 * Builds the application theme from optional runtime {@link BrandingConfig}. Branding is injected at
 * runtime (discovery), never baked at build time, so a single bundle can be re-themed per
 * deployment/tenant (see docs/adr/0064 branding seam).
 */
export function createAppTheme(branding?: BrandingConfig): Theme {
  return createTheme({
    palette: {
      mode: branding?.mode ?? 'light',
      ...(branding?.primaryColor ? { primary: { main: branding.primaryColor } } : {}),
      ...(branding?.secondaryColor ? { secondary: { main: branding.secondaryColor } } : {}),
    },
    shape: { borderRadius: 8 },
    typography: {
      fontFamily: ['Inter', 'Roboto', 'Helvetica', 'Arial', 'sans-serif'].join(','),
    },
  });
}
