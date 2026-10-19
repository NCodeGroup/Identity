import { useEffect, useMemo, useState, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Tab from '@mui/material/Tab';
import Tabs from '@mui/material/Tabs';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { DataStateBoundary } from '@ncode/ui';
import type { RuntimeConfig } from '@ncode/config';
import {
  auditDiscovery,
  buildCoverageReport,
  summarize,
  type AuditReport,
  type CoverageReport,
  type RawDiscovery,
} from '@ncode/conformance-core';
import { loadDiscovery as defaultLoadDiscovery } from '../bootstrap.js';
import { CoverageMatrix } from '../components/CoverageMatrix.js';
import { CoverageStatusChip } from '../components/CoverageStatusChip.js';
import { FindingsList } from '../components/FindingsList.js';
import { ProbePanel } from '../components/ProbePanel.js';
import { ProfileCard } from '../components/ProfileCard.js';
import { SeverityChip } from '../components/SeverityChip.js';

export type ConformanceDashboardProps = {
  readonly runtime: RuntimeConfig;
  /** Injectable discovery loader so the dashboard is testable without a network. */
  readonly loadDiscovery?: (issuer: string, signal?: AbortSignal) => Promise<RawDiscovery>;
  /** The external conformance suite base URL whose RP callback the OP must accept. */
  readonly defaultSuiteBaseUrl?: string;
};

/** A keep-mounted tab panel: inactive panels stay in the DOM (so probe state and deep links survive tab switches). */
function TabPanel({
  value,
  index,
  children,
}: {
  readonly value: number;
  readonly index: number;
  readonly children: ReactNode;
}) {
  return (
    <Box role="tabpanel" hidden={value !== index} sx={{ pt: 2 }}>
      {children}
    </Box>
  );
}

/**
 * Stateful conformance & security-posture dashboard. It fetches the OpenID Provider's published discovery metadata,
 * audits it against modern OAuth/OIDC security guidance and the conformance Basic/Config OP profiles, measures
 * spec coverage, runs active probes, and surfaces the configuration the external OpenID Foundation Conformance Suite
 * needs — organized into tabs so the surface stays scannable. Read-only; the audit, coverage, and suite hints derive
 * from discovery. The discovery loader is injectable so the surface is testable without a network.
 */
export function ConformanceDashboard({
  runtime,
  loadDiscovery = defaultLoadDiscovery,
  defaultSuiteBaseUrl = 'https://localhost.emobix.co.uk:8443',
}: ConformanceDashboardProps) {
  const [report, setReport] = useState<AuditReport | null>(null);
  const [coverage, setCoverage] = useState<CoverageReport | null>(null);
  const [discovery, setDiscovery] = useState<RawDiscovery | null>(null);
  const [error, setError] = useState<string | undefined>(undefined);
  // The request whose load has settled. Loading is derived from it rather than set at the start of the effect, so a
  // changed issuer or loader reads as loading on the very render that changes it, with no synchronous state update.
  const [settled, setSettled] = useState<{
    readonly issuer: string;
    readonly load: typeof loadDiscovery;
  }>();
  const isLoading = !settled || settled.issuer !== runtime.issuer || settled.load !== loadDiscovery;
  const [tab, setTab] = useState(0);

  const [suiteBaseUrl, setSuiteBaseUrl] = useState(defaultSuiteBaseUrl);
  const [alias, setAlias] = useState('ncode');

  useEffect(() => {
    const controller = new AbortController();
    loadDiscovery(runtime.issuer, controller.signal)
      .then((raw) => {
        setReport(auditDiscovery(raw));
        setCoverage(buildCoverageReport(raw));
        setDiscovery(raw);
        setError(undefined);
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : 'Failed to load discovery metadata.');
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setSettled({ issuer: runtime.issuer, load: loadDiscovery });
        }
      });
    return () => controller.abort();
  }, [loadDiscovery, runtime.issuer]);

  const counts = useMemo(() => (report ? summarize(report) : null), [report]);
  const callbackUrl = `${suiteBaseUrl.replace(/\/$/, '')}/test/a/${alias || '<alias>'}/callback`;

  return (
    <DataStateBoundary isLoading={isLoading} error={error}>
      {report && counts && coverage && discovery ? (
        <Box>
          <Tabs
            value={tab}
            onChange={(_event, value: number) => setTab(value)}
            variant="scrollable"
            scrollButtons="auto"
            allowScrollButtonsMobile
          >
            <Tab label="Overview" data-testid="tab-overview" />
            <Tab
              label={<TabLabel text="Security audit" count={counts.fail} color="error" />}
              data-testid="tab-audit"
            />
            <Tab
              label={<TabLabel text="Coverage" count={coverage.totals.missing} color="default" />}
              data-testid="tab-coverage"
            />
            <Tab label="Probes" data-testid="tab-probes" />
            <Tab label="Suite config" data-testid="tab-suite" />
          </Tabs>

          <TabPanel value={tab} index={0}>
            <Stack spacing={2}>
              <Stack direction="row" spacing={1} alignItems="center" data-testid="summary">
                <SeverityChip severity="fail" size="medium" />
                <Typography sx={{ mr: 2 }}>{counts.fail}</Typography>
                <SeverityChip severity="warn" size="medium" />
                <Typography sx={{ mr: 2 }}>{counts.warn}</Typography>
                <SeverityChip severity="pass" size="medium" />
                <Typography>{counts.pass}</Typography>
              </Stack>

              <Box>
                <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                  Spec coverage
                </Typography>
                <Stack
                  direction="row"
                  spacing={1}
                  alignItems="center"
                  flexWrap="wrap"
                  useFlexGap
                  data-testid="coverage-summary"
                >
                  <CoverageStatusChip status="implemented" />
                  <Typography sx={{ mr: 1 }}>{coverage.totals.implemented}</Typography>
                  <CoverageStatusChip status="partial" />
                  <Typography sx={{ mr: 1 }}>{coverage.totals.partial}</Typography>
                  <CoverageStatusChip status="missing" />
                  <Typography sx={{ mr: 1 }}>{coverage.totals.missing}</Typography>
                  <CoverageStatusChip status="non-conformant" />
                  <Typography>{coverage.totals['non-conformant']}</Typography>
                </Stack>
              </Box>

              <Box>
                <Typography variant="h6" component="h2" gutterBottom>
                  Profile readiness
                </Typography>
                <Stack spacing={1.5}>
                  {report.profiles.map((profile) => (
                    <ProfileCard key={profile.id} profile={profile} />
                  ))}
                </Stack>
              </Box>
            </Stack>
          </TabPanel>

          <TabPanel value={tab} index={1}>
            <Card variant="outlined">
              <CardContent>
                <FindingsList findings={report.findings} />
              </CardContent>
            </Card>
          </TabPanel>

          <TabPanel value={tab} index={2}>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
              Coverage of the full published OAuth&nbsp;2.x / OpenID&nbsp;Connect / CIBA / FAPI
              surface, classified from the live discovery document (semantics: <em>advertised</em>).
              Categories with a gap start expanded; missing rows link to their tracking backlog id,
              and the channel tag records which actor can exercise each feature end to end.
            </Typography>
            <CoverageMatrix report={coverage} />
          </TabPanel>

          <TabPanel value={tab} index={3}>
            <Card variant="outlined">
              <CardContent>
                <ProbePanel
                  raw={discovery}
                  {...(runtime.clientId ? { clientId: runtime.clientId } : {})}
                />
              </CardContent>
            </Card>
          </TabPanel>

          <TabPanel value={tab} index={4}>
            <Card variant="outlined">
              <CardContent>
                <Stack spacing={2}>
                  <Typography variant="body2" color="text.secondary">
                    Drive this provider with the{' '}
                    <Link
                      href="https://www.certification.openid.net/"
                      target="_blank"
                      rel="noreferrer"
                    >
                      OpenID Foundation Conformance Suite
                    </Link>
                    . The suite acts as the relying party; register the callback below as an allowed
                    redirect URI for the conformance client.
                  </Typography>
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
                    <TextField
                      label="Suite base URL"
                      value={suiteBaseUrl}
                      onChange={(event) => setSuiteBaseUrl(event.target.value)}
                      size="small"
                      fullWidth
                      inputProps={{ 'data-testid': 'suite-base' }}
                    />
                    <TextField
                      label="Alias"
                      value={alias}
                      onChange={(event) => setAlias(event.target.value)}
                      size="small"
                      inputProps={{ 'data-testid': 'alias' }}
                    />
                  </Stack>
                  <Divider />
                  <Box>
                    <Typography variant="caption" color="text.secondary" display="block">
                      Issuer (must byte-match the discovery document)
                    </Typography>
                    <Typography fontFamily="monospace" data-testid="issuer">
                      {runtime.issuer}
                    </Typography>
                  </Box>
                  <Box>
                    <Typography variant="caption" color="text.secondary" display="block">
                      RP callback the OP must accept
                    </Typography>
                    <Typography fontFamily="monospace" data-testid="callback-url">
                      {callbackUrl}
                    </Typography>
                  </Box>
                </Stack>
              </CardContent>
            </Card>
          </TabPanel>
        </Box>
      ) : null}
    </DataStateBoundary>
  );
}

/** A tab label with an optional count badge (shown only when the count is non-zero). */
function TabLabel({
  text,
  count,
  color,
}: {
  readonly text: string;
  readonly count: number;
  readonly color: 'error' | 'default';
}) {
  return (
    <Stack direction="row" spacing={1} alignItems="center">
      <span>{text}</span>
      {count > 0 ? <Chip size="small" color={color} label={count} /> : null}
    </Stack>
  );
}
