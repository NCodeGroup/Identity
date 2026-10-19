import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { Finding } from '@ncode/conformance-core';
import { SeverityChip } from './SeverityChip.js';

export type FindingsListProps = {
  readonly findings: readonly Finding[];
};

/** Stateless, severity-ordered list of audit findings with each finding's detail and authoritative reference. */
export function FindingsList({ findings }: FindingsListProps) {
  const order = { fail: 0, warn: 1, pass: 2 } as const;
  const sorted = [...findings].sort((a, b) => order[a.severity] - order[b.severity]);

  return (
    <List disablePadding data-testid="findings">
      {sorted.map((finding) => (
        <ListItem key={finding.id} divider alignItems="flex-start" sx={{ px: 0 }}>
          <ListItemText
            primary={
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <SeverityChip severity={finding.severity} />
                <Typography component="span" sx={{ fontWeight: 600 }}>
                  {finding.title}
                </Typography>
              </Stack>
            }
            secondary={
              <>
                <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
                  {finding.detail}
                </Typography>
                <Typography
                  component="span"
                  variant="caption"
                  sx={{ color: 'text.disabled', display: 'block' }}
                >
                  {finding.reference}
                </Typography>
              </>
            }
          />
        </ListItem>
      ))}
    </List>
  );
}
