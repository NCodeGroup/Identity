import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { TenantUsageTable, type TenantUsageRow } from './TenantUsageTable.js';

const entries: TenantUsageRow[] = [
  { day: '2026-10-20', meter: 'requests', count: 10 },
  { day: '2026-10-21', meter: 'requests', count: 5 },
  { day: '2026-10-21', meter: 'token_requests', count: 2 },
];

describe('TenantUsageTable', () => {
  it('renders a column per meter present, in name order', () => {
    render(<TenantUsageTable entries={entries} />);
    const headers = screen.getAllByRole('columnheader').map((header) => header.textContent);
    expect(headers).toEqual(['Day (UTC)', 'requests', 'token_requests']);
  });

  it('lists days newest first and shows zero for a meter without an entry that day', () => {
    render(<TenantUsageTable entries={entries} />);
    const rows = screen.getAllByTestId(/^usage-row-/);
    expect(rows.map((row) => row.getAttribute('data-testid'))).toEqual([
      'usage-row-2026-10-21',
      'usage-row-2026-10-20',
    ]);
    const older = within(screen.getByTestId('usage-row-2026-10-20')).getAllByRole('cell');
    expect(older.map((cell) => cell.textContent)).toEqual(['2026-10-20', '10', '0']);
  });

  it('totals each meter over the range', () => {
    render(<TenantUsageTable entries={entries} />);
    const total = within(screen.getByTestId('usage-total')).getAllByRole('cell');
    expect(total.map((cell) => cell.textContent)).toEqual(['Total', '15', '2']);
  });
});
