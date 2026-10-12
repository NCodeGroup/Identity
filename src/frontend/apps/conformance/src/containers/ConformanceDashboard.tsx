import { useEffect, useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Divider from '@mui/material/Divider';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { DataStateBoundary } from '@ncode/ui';
import type { RuntimeConfig } from '@ncode/config';
import { auditDiscovery, summarize, type AuditReport, type RawDiscovery } from '../audit.js';
import { loadDiscovery as defaultLoadDiscovery } from '../bootstrap.js';
import { FindingsList } from '../components/FindingsList.js';
import { ProfileCard } from '../components/ProfileCard.js';
import { SeverityChip } from '../components/SeverityChip.js';

export type ConformanceDashboardProps = {
  readonly runtime: RuntimeConfig;
  /** Injectable discovery loader so the dashboard is testable without a network. */
  readonly loadDiscovery?: (issuer: string, signal?: AbortSignal) => Promise<RawDiscovery>;
  /** The external conformance suite base URL whose RP callback the OP must accept. */
  readonly defaultSuiteBaseUrl?: string;
};

/**
 * Stateful conformance & security-posture dashboard. It fetches the OpenID Provider's published discovery metadata,
 * audits it against modern OAuth/OIDC security guidance and the conformance Basic/Config OP profiles, and surfaces the
 * configuration the external OpenID Foundation Conformance Suite needs to drive this provider. Read-only; the audit
 * and suite hints derive entirely from discovery. The discovery loader is injectable so the surface is testable
 * without a network.
 */
export function ConformanceDashboard({
  runtime,
  loadDiscovery = defaultLoadDiscovery,
  defaultSuiteBaseUrl = 'https://localhost.emobix.co.uk:8443',
}: ConformanceDashboardProps) {
  const [report, setReport] = useState<AuditReport | null>(null);
  const [error, setError] = useState<string | undefined>(undefined);
  const [isLoading, setLoading] = useState(true);

  const [suiteBaseUrl, setSuiteBaseUrl] = useState(defaultSuiteBaseUrl);
  const [alias, setAlias] = useState('ncode');

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    loadDiscovery(runtime.issuer, controller.signal)
      .then((raw) => {
        setReport(auditDiscovery(raw));
        setError(undefined);
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setError(cause instanceof Error ? cause.message : 'Failed to load discovery metadata.');
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      });
    return () => controller.abort();
  }, [loadDiscovery, runtime.issuer]);

  const counts = useMemo(() => (report ? summarize(report) : null), [report]);
  const callbackUrl = `${suiteBaseUrl.replace(/\/$/, '')}/test/a/${alias || '<alias>'}/callback`;

  return (
    <Stack spacing={2}>
      <DataStateBoundary isLoading={isLoading} error={error}>
        {report && counts ? (
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
              <Typography variant="h6" component="h2" gutterBottom>
                Profile readiness
              </Typography>
              <Stack spacing={1.5}>
                {report.profiles.map((profile) => (
                  <ProfileCard key={profile.id} profile={profile} />
                ))}
              </Stack>
            </Box>

            <Box>
              <Typography variant="h6" component="h2" gutterBottom>
                Discovery security audit
              </Typography>
              <Card variant="outlined">
                <CardContent>
                  <FindingsList findings={report.findings} />
                </CardContent>
              </Card>
            </Box>
          </Stack>
        ) : null}
      </DataStateBoundary>

      <Box>
        <Typography variant="h6" component="h2" gutterBottom>
          External suite configuration
        </Typography>
        <Card variant="outlined">
          <CardContent>
            <Stack spacing={2}>
              <Typography variant="body2" color="text.secondary">
                Drive this provider with the{' '}
                <Link href="https://www.certification.openid.net/" target="_blank" rel="noreferrer">
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
      </Box>
    </Stack>
  );
}
