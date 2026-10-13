import { useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { RawDiscovery } from '../audit.js';
import {
  runProbes as defaultRunProbes,
  type ProbeFetch,
  type ProbeOutcome,
  type ProbeReport,
  type ProbeRunOptions,
} from '../probes.js';

export type ProbePanelProps = {
  readonly raw: RawDiscovery;
  /** The public client id used for the token/PAR probes, when one is configured. */
  readonly clientId?: string;
  /** Injectable probe runner so the panel is testable without a network. */
  readonly runProbes?: (
    raw: RawDiscovery,
    fetchFn: ProbeFetch,
    options?: ProbeRunOptions,
  ) => Promise<ProbeReport>;
  /** Injectable fetch so the panel is testable without the DOM `fetch`. */
  readonly probeFetch?: ProbeFetch;
};

const OUTCOME_META: Record<
  ProbeOutcome,
  { label: string; color: 'success' | 'error' | 'default' }
> = {
  verified: { label: 'Verified', color: 'success' },
  failed: { label: 'Failed', color: 'error' },
  skipped: { label: 'Skipped', color: 'default' },
};

const browserFetch: ProbeFetch = (url, init) => {
  const requestInit: RequestInit = {};
  if (init?.method) requestInit.method = init.method;
  if (init?.headers) requestInit.headers = init.headers;
  if (init?.body !== undefined) requestInit.body = init.body;
  if (init?.signal) requestInit.signal = init.signal;
  return fetch(url, requestInit);
};

/**
 * Stateful active-probe panel. On demand, it exercises the browser-drivable, CORS-reachable endpoints against the live
 * provider (JWKS, the token-error contract, UserInfo's 401, and PAR when advertised) and reports each as verified,
 * failed, or skipped. Probes make real network calls, so the run is user-triggered rather than automatic. The runner
 * and fetch are injectable so the panel is testable without a network.
 */
export function ProbePanel({
  raw,
  clientId,
  runProbes = defaultRunProbes,
  probeFetch = browserFetch,
}: ProbePanelProps) {
  const [report, setReport] = useState<ProbeReport | null>(null);
  const [isRunning, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleRun = async () => {
    setRunning(true);
    setError(null);
    try {
      setReport(await runProbes(raw, probeFetch, clientId ? { clientId } : {}));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Probe run failed.');
    } finally {
      setRunning(false);
    }
  };

  return (
    <Stack spacing={1.5} data-testid="probe-panel">
      <Typography variant="body2" color="text.secondary">
        Actively exercise the browser-reachable endpoints against the live provider to turn{' '}
        <em>advertised</em> into <em>verified</em>. Probes make real network calls (CORS applies);
        the confidential and interactive-session legs are left to the probe runner.
      </Typography>
      <Box>
        <Button
          variant="contained"
          onClick={handleRun}
          disabled={isRunning}
          data-testid="run-probes"
        >
          {isRunning ? 'Running…' : 'Run live probes'}
        </Button>
      </Box>
      {error ? (
        <Typography color="error" data-testid="probe-error">
          {error}
        </Typography>
      ) : null}
      {report ? (
        <>
          <Stack
            direction="row"
            spacing={1}
            alignItems="center"
            flexWrap="wrap"
            useFlexGap
            data-testid="probe-summary"
          >
            <Chip size="small" color="success" label={`Verified ${report.totals.verified}`} />
            <Chip size="small" color="error" label={`Failed ${report.totals.failed}`} />
            <Chip size="small" label={`Skipped ${report.totals.skipped}`} />
          </Stack>
          <List disablePadding data-testid="probe-results">
            {report.results.map((result) => (
              <ListItem key={result.id} divider alignItems="flex-start" sx={{ px: 0 }}>
                <ListItemText
                  primary={
                    <Stack
                      direction="row"
                      spacing={1}
                      alignItems="center"
                      flexWrap="wrap"
                      useFlexGap
                    >
                      <Chip
                        size="small"
                        color={OUTCOME_META[result.outcome].color}
                        label={OUTCOME_META[result.outcome].label}
                        data-testid={`probe-${result.outcome}`}
                      />
                      <Typography component="span" fontWeight={600}>
                        {result.feature}
                      </Typography>
                    </Stack>
                  }
                  secondary={
                    <>
                      <Typography component="span" variant="body2" color="text.secondary">
                        {result.detail}
                      </Typography>
                      <Typography
                        component="span"
                        variant="caption"
                        color="text.disabled"
                        display="block"
                      >
                        {result.reference}
                      </Typography>
                    </>
                  }
                />
              </ListItem>
            ))}
          </List>
        </>
      ) : null}
    </Stack>
  );
}
