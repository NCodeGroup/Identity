import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import Accordion from '@mui/material/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary';
import Chip from '@mui/material/Chip';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { CoverageChannel, CoverageReport, CoverageStatus } from '@ncode/conformance-core';
import { CoverageStatusChip } from './CoverageStatusChip.js';

export type CoverageMatrixProps = {
  readonly report: CoverageReport;
};

type CoverageCategory = CoverageReport['categories'][number];

const STATUS_ORDER: Record<CoverageStatus, number> = {
  'non-conformant': 0,
  missing: 1,
  partial: 2,
  implemented: 3,
};

const CHANNEL_LABEL: Record<CoverageChannel, string> = {
  browser: 'browser',
  'probe-runner': 'probe runner',
  'external-suite': 'external suite',
};

/** Counts entries by status within a category. */
function countByStatus(entries: CoverageCategory['entries']): Record<CoverageStatus, number> {
  return entries.reduce<Record<CoverageStatus, number>>(
    (acc, entry) => {
      acc[entry.status] += 1;
      return acc;
    },
    { implemented: 0, partial: 0, missing: 0, 'non-conformant': 0 },
  );
}

/**
 * Stateless spec-coverage matrix. Each catalog category is a collapsible panel whose header summarizes its status
 * counts; a category carrying a gap (missing or non-conformant) starts expanded so attention lands on it first. Within
 * a category, entries are ordered worst-status-first, showing the coverage status, the feature, its authoritative
 * reference, the channel that would exercise it, and the tracking backlog id for a gap. Classification reflects what
 * the provider *advertises* (Phase 1 semantics).
 */
export function CoverageMatrix({ report }: CoverageMatrixProps) {
  return (
    <Stack spacing={1} data-testid="coverage-matrix">
      {report.categories.map((category) => {
        const counts = countByStatus(category.entries);
        const hasGap = counts.missing > 0 || counts['non-conformant'] > 0;
        const sorted = [...category.entries].sort(
          (a, b) => STATUS_ORDER[a.status] - STATUS_ORDER[b.status],
        );
        return (
          <Accordion
            key={category.id}
            defaultExpanded={hasGap}
            disableGutters
            data-testid={`coverage-category-${category.id}`}
          >
            <AccordionSummary expandIcon={<ExpandMoreIcon />}>
              <Stack
                direction="row"
                spacing={1}
                useFlexGap
                sx={{ alignItems: 'center', flexWrap: 'wrap', width: '100%', pr: 1 }}
              >
                <Typography
                  variant="subtitle1"
                  component="h3"
                  sx={{ fontWeight: 600, flexGrow: 1 }}
                >
                  {category.name}
                </Typography>
                {counts['non-conformant'] > 0 ? (
                  <Chip
                    size="small"
                    color="error"
                    label={`${counts['non-conformant']} non-conformant`}
                  />
                ) : null}
                {counts.missing > 0 ? (
                  <Chip size="small" variant="outlined" label={`${counts.missing} missing`} />
                ) : null}
                {counts.partial > 0 ? (
                  <Chip size="small" color="info" label={`${counts.partial} partial`} />
                ) : null}
                <Chip size="small" color="success" label={`${counts.implemented} implemented`} />
              </Stack>
            </AccordionSummary>
            <AccordionDetails sx={{ pt: 0 }}>
              <List disablePadding>
                {sorted.map((entry) => (
                  <ListItem key={entry.id} divider alignItems="flex-start" sx={{ px: 0 }}>
                    <ListItemText
                      primary={
                        <Stack
                          direction="row"
                          spacing={1}
                          useFlexGap
                          sx={{ alignItems: 'center', flexWrap: 'wrap' }}
                        >
                          <CoverageStatusChip status={entry.status} />
                          <Typography component="span" sx={{ fontWeight: 600 }}>
                            {entry.feature}
                          </Typography>
                          <Chip
                            size="small"
                            variant="outlined"
                            label={CHANNEL_LABEL[entry.channel]}
                            data-testid={`channel-${entry.channel}`}
                          />
                          {entry.backlog ? (
                            <Chip
                              size="small"
                              variant="outlined"
                              color="warning"
                              label={entry.backlog}
                              data-testid={`backlog-${entry.backlog}`}
                            />
                          ) : null}
                        </Stack>
                      }
                      secondary={
                        <>
                          <Typography
                            component="span"
                            variant="body2"
                            sx={{ color: 'text.secondary' }}
                          >
                            {entry.detail}
                          </Typography>
                          <Typography
                            component="span"
                            variant="caption"
                            sx={{ color: 'text.disabled', display: 'block' }}
                          >
                            {entry.reference}
                          </Typography>
                        </>
                      }
                    />
                  </ListItem>
                ))}
              </List>
            </AccordionDetails>
          </Accordion>
        );
      })}
    </Stack>
  );
}
