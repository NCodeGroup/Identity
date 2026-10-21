import Box from '@mui/material/Box';
import type { BrandingConfig } from '@ncode/config';

export type BrandHeaderProps = {
  /** The tenant's branding; only its logo is drawn here, since each surface titles itself with the display name. */
  readonly branding: BrandingConfig;
};

/** Shows the tenant's logo above the surface, or nothing when the tenant has none. */
export function BrandHeader({ branding }: BrandHeaderProps) {
  if (!branding.logoUrl) {
    return null;
  }
  return (
    <Box component="header" sx={{ display: 'flex', justifyContent: 'center', pt: 4 }}>
      <Box
        component="img"
        src={branding.logoUrl}
        alt={branding.displayName ?? ''}
        sx={{ maxHeight: 48, maxWidth: '60%' }}
      />
    </Box>
  );
}
