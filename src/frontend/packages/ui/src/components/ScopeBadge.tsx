import Chip from '@mui/material/Chip';
import PublicIcon from '@mui/icons-material/Public';
import BusinessIcon from '@mui/icons-material/Business';

export type ScopeKind = 'global' | 'tenant';

export type ScopeBadgeProps = {
  readonly kind: ScopeKind;
  readonly label?: string;
};

/**
 * Stateless visual marker of the security boundary a view belongs to. The global scope is styled
 * distinctly from a tenant scope so the boundary is always visible (see docs/decisions/0064).
 */
export function ScopeBadge({ kind, label }: ScopeBadgeProps) {
  const isGlobal = kind === 'global';
  return (
    <Chip
      size="small"
      color={isGlobal ? 'warning' : 'primary'}
      variant={isGlobal ? 'filled' : 'outlined'}
      icon={isGlobal ? <PublicIcon fontSize="small" /> : <BusinessIcon fontSize="small" />}
      label={label ?? (isGlobal ? 'Global' : 'Tenant')}
      data-testid="scope-badge"
    />
  );
}
