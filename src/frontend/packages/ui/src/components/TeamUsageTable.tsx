import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';

/** One ledger entry: a tenant's count of a meter on a UTC day (decoupled from the API contract). */
export type TeamUsageRow = {
  readonly teamId: string;
  readonly tenantId: string;
  readonly day: string;
  readonly meter: string;
  readonly count: number;
};

export type TeamUsageTableProps = {
  readonly entries: readonly TeamUsageRow[];
};

const numberFormat = new Intl.NumberFormat();

/**
 * Stateless per-day usage table for the usage ledgers of the tenants a team manages (ADR-1199). Meters are an open
 * vocabulary, so the columns after the team, tenant and day are the meters present in the entries, in name order; a
 * tenant's day without an entry for a meter shows zero. Rows are listed newest day first, then by team, then by
 * tenant, and a final row totals each meter over the range. Holds no state and does no fetching.
 */
export function TeamUsageTable({ entries }: TeamUsageTableProps) {
  const meters = [...new Set(entries.map((entry) => entry.meter))].sort();
  const rows = new Map<
    string,
    { teamId: string; tenantId: string; day: string; counts: Map<string, number> }
  >();
  const totals = new Map<string, number>();
  for (const entry of entries) {
    const key = `${entry.day}\u0000${entry.teamId}\u0000${entry.tenantId}`;
    const row = rows.get(key) ?? {
      teamId: entry.teamId,
      tenantId: entry.tenantId,
      day: entry.day,
      counts: new Map<string, number>(),
    };
    row.counts.set(entry.meter, (row.counts.get(entry.meter) ?? 0) + entry.count);
    rows.set(key, row);
    totals.set(entry.meter, (totals.get(entry.meter) ?? 0) + entry.count);
  }
  const orderedRows = [...rows.values()].sort(
    (a, b) =>
      b.day.localeCompare(a.day) ||
      a.teamId.localeCompare(b.teamId) ||
      a.tenantId.localeCompare(b.tenantId),
  );

  return (
    <Table size="small" aria-label="Usage">
      <TableHead>
        <TableRow>
          <TableCell>Team</TableCell>
          <TableCell>Tenant</TableCell>
          <TableCell>Day (UTC)</TableCell>
          {meters.map((meter) => (
            <TableCell key={meter} align="right">
              {meter}
            </TableCell>
          ))}
        </TableRow>
      </TableHead>
      <TableBody>
        {orderedRows.map((row) => (
          <TableRow
            key={`${row.day}/${row.teamId}/${row.tenantId}`}
            data-testid={`usage-row-${row.tenantId}-${row.day}`}
          >
            <TableCell>{row.teamId}</TableCell>
            <TableCell>{row.tenantId}</TableCell>
            <TableCell>{row.day}</TableCell>
            {meters.map((meter) => (
              <TableCell key={meter} align="right">
                {numberFormat.format(row.counts.get(meter) ?? 0)}
              </TableCell>
            ))}
          </TableRow>
        ))}
        <TableRow data-testid="usage-total">
          <TableCell />
          <TableCell colSpan={2} sx={{ fontWeight: 'bold' }}>
            Total
          </TableCell>
          {meters.map((meter) => (
            <TableCell key={meter} align="right" sx={{ fontWeight: 'bold' }}>
              {numberFormat.format(totals.get(meter) ?? 0)}
            </TableCell>
          ))}
        </TableRow>
      </TableBody>
    </Table>
  );
}
