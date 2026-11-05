import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { TeamUsageTable, type TeamUsageRow } from './TeamUsageTable.js';

const entries: TeamUsageRow[] = [
  { tenantId: 'acme', day: '2026-10-20', meter: 'requests', count: 10 },
  { tenantId: 'acme', day: '2026-10-21', meter: 'requests', count: 5 },
  { tenantId: 'acme', day: '2026-10-21', meter: 'token_requests', count: 2 },
  { tenantId: 'globex', day: '2026-10-21', meter: 'requests', count: 7 },
  { tenantId: 'globex', day: '2026-10-21', meter: 'requests', count: 1 },
];

describe('TeamUsageTable', () => {
  it('renders a leading tenant column and a column per meter present, in name order', () => {
    render(<TeamUsageTable entries={entries} />);
    const headers = screen.getAllByRole('columnheader').map((header) => header.textContent);
    expect(headers).toEqual(['Tenant', 'Day (UTC)', 'requests', 'token_requests']);
  });

  it('lists days newest first, then tenants, and shows zero for a meter without an entry', () => {
    render(<TeamUsageTable entries={entries} />);
    const rows = screen.getAllByTestId(/^usage-row-/);
    expect(rows.map((row) => row.getAttribute('data-testid'))).toEqual([
      'usage-row-acme-2026-10-21',
      'usage-row-globex-2026-10-21',
      'usage-row-acme-2026-10-20',
    ]);
    const older = within(screen.getByTestId('usage-row-acme-2026-10-20')).getAllByRole('cell');
    expect(older.map((cell) => cell.textContent)).toEqual(['acme', '2026-10-20', '10', '0']);
  });

  it('sums entries of the same tenant, day and meter', () => {
    render(<TeamUsageTable entries={entries} />);
    const row = within(screen.getByTestId('usage-row-globex-2026-10-21')).getAllByRole('cell');
    expect(row.map((cell) => cell.textContent)).toEqual(['globex', '2026-10-21', '8', '0']);
  });

  it('totals each meter over the range', () => {
    render(<TeamUsageTable entries={entries} />);
    const total = within(screen.getByTestId('usage-total')).getAllByRole('cell');
    expect(total.map((cell) => cell.textContent)).toEqual(['Total', '23', '2']);
  });
});
