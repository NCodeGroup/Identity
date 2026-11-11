import Chip from '@mui/material/Chip';
import PublicIcon from '@mui/icons-material/Public';
import BusinessIcon from '@mui/icons-material/Business';
import GroupsIcon from '@mui/icons-material/Groups';

export type ScopeKind = 'platform' | 'team' | 'tenant';

export type ScopeBadgeProps = {
  readonly kind: ScopeKind;
  readonly label?: string;
};

const appearances = {
  platform: {
    color: 'warning',
    variant: 'filled',
    icon: <PublicIcon fontSize="small" />,
    label: 'Platform',
  },
  team: {
    color: 'secondary',
    variant: 'outlined',
    icon: <GroupsIcon fontSize="small" />,
    label: 'Team',
  },
  tenant: {
    color: 'primary',
    variant: 'outlined',
    icon: <BusinessIcon fontSize="small" />,
    label: 'Tenant',
  },
} as const;

/**
 * Stateless visual marker of the security boundary a view belongs to. The platform, a team and a
 * tenant are each styled distinctly so the boundary is always visible (see docs/decisions/1064).
 */
export function ScopeBadge({ kind, label }: ScopeBadgeProps) {
  const appearance = appearances[kind];
  return (
    <Chip
      size="small"
      color={appearance.color}
      variant={appearance.variant}
      icon={appearance.icon}
      label={label ?? appearance.label}
      data-testid="scope-badge"
    />
  );
}
