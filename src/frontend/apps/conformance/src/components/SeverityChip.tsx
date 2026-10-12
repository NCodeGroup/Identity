import Chip from '@mui/material/Chip';
import type { Severity } from '../audit.js';

export type SeverityChipProps = {
  readonly severity: Severity;
  readonly size?: 'small' | 'medium';
};

const SEVERITY_META: Record<Severity, { label: string; color: 'success' | 'warning' | 'error' }> = {
  pass: { label: 'Pass', color: 'success' },
  warn: { label: 'Warn', color: 'warning' },
  fail: { label: 'Fail', color: 'error' },
};

/** Stateless severity indicator mapping an audit {@link Severity} to a MUI color and label. */
export function SeverityChip({ severity, size = 'small' }: SeverityChipProps) {
  const meta = SEVERITY_META[severity];
  return (
    <Chip
      size={size}
      color={meta.color}
      label={meta.label}
      data-testid={`severity-${severity}`}
      variant="filled"
    />
  );
}
