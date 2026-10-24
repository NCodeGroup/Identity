import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';

/** One ledger entry: a tenant's count of a meter on a UTC day (decoupled from the API contract). */
export type TenantUsageRow = {
  readonly day: string;
  readonly meter: string;
  readonly count: number;
};

export type TenantUsageTableProps = {
  readonly entries: readonly TenantUsageRow[];
};

const numberFormat = new Intl.NumberFormat();

/**
 * Stateless per-day usage table for a tenant's usage ledger (ADR-0199). Meters are an open vocabulary, so the
 * columns are the meters present in the entries, in name order; a day without an entry for a meter shows zero. Days
 * are listed newest first and a final row totals each meter over the range. Holds no state and does no fetching.
 */
export function TenantUsageTable({ entries }: TenantUsageTableProps) {
  const meters = [...new Set(entries.map((entry) => entry.meter))].sort();
  const days = new Map<string, Map<string, number>>();
  const totals = new Map<string, number>();
  for (const entry of entries) {
    const counts = days.get(entry.day) ?? new Map<string, number>();
    counts.set(entry.meter, (counts.get(entry.meter) ?? 0) + entry.count);
    days.set(entry.day, counts);
    totals.set(entry.meter, (totals.get(entry.meter) ?? 0) + entry.count);
  }
  const orderedDays = [...days.keys()].sort().reverse();

  return (
    <Table size="small" aria-label="Usage">
      <TableHead>
        <TableRow>
          <TableCell>Day (UTC)</TableCell>
          {meters.map((meter) => (
            <TableCell key={meter} align="right">
              {meter}
            </TableCell>
          ))}
        </TableRow>
      </TableHead>
      <TableBody>
        {orderedDays.map((day) => (
          <TableRow key={day} data-testid={`usage-row-${day}`}>
            <TableCell>{day}</TableCell>
            {meters.map((meter) => (
              <TableCell key={meter} align="right">
                {numberFormat.format(days.get(day)?.get(meter) ?? 0)}
              </TableCell>
            ))}
          </TableRow>
        ))}
        <TableRow data-testid="usage-total">
          <TableCell sx={{ fontWeight: 'bold' }}>Total</TableCell>
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
