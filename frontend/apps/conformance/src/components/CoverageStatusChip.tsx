import Chip from '@mui/material/Chip';
import type { CoverageStatus } from '@ncode/conformance-core';

export type CoverageStatusChipProps = {
  readonly status: CoverageStatus;
  readonly size?: 'small' | 'medium';
};

const STATUS_META: Record<
  CoverageStatus,
  { label: string; color: 'success' | 'info' | 'default' | 'error' }
> = {
  implemented: { label: 'Implemented', color: 'success' },
  partial: { label: 'Partial', color: 'info' },
  missing: { label: 'Missing', color: 'default' },
  'non-conformant': { label: 'Non-conformant', color: 'error' },
};

/** Stateless indicator mapping a {@link CoverageStatus} to a MUI color and label. */
export function CoverageStatusChip({ status, size = 'small' }: CoverageStatusChipProps) {
  const meta = STATUS_META[status];
  return (
    <Chip
      size={size}
      color={meta.color}
      label={meta.label}
      data-testid={`coverage-${status}`}
      variant={status === 'missing' ? 'outlined' : 'filled'}
    />
  );
}
