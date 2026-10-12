import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { ProfileReadiness } from '../audit.js';
import { SeverityChip } from './SeverityChip.js';

export type ProfileCardProps = {
  readonly profile: ProfileReadiness;
};

const STATUS_HINT = {
  pass: 'Ready — the advertised metadata satisfies this profile.',
  warn: 'Ready with warnings — review the flagged findings before certifying.',
  fail: 'Blocked — resolve the failing findings before running this profile.',
} as const;

/** Stateless card summarizing a conformance profile's readiness derived from the discovery audit. */
export function ProfileCard({ profile }: ProfileCardProps) {
  return (
    <Card variant="outlined" data-testid={`profile-${profile.id}`} data-status={profile.status}>
      <CardContent>
        <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 1 }}>
          <Typography variant="h6" component="h3" sx={{ flexGrow: 1 }}>
            {profile.name}
          </Typography>
          <SeverityChip severity={profile.status} size="medium" />
        </Stack>
        <Typography color="text.secondary" variant="body2">
          {STATUS_HINT[profile.status]}
        </Typography>
      </CardContent>
    </Card>
  );
}
