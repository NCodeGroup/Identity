import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import {
  ACCOUNT_PATH,
  ANTIFORGERY_FIELD,
  APPLICATIONS_LIST_ENDPOINT,
  APPLICATIONS_PATH,
  APPLICATIONS_REVOKE_ACTION,
  LOGIN_ACTION,
} from './params.js';

/** A single granted scope on an authorized application. */
export type AuthorizedScope = {
  readonly value: string;
  readonly description?: string | null;
};

/** One application the end-user has granted standing consent to (mirrors the OP's applications payload). */
export type AuthorizedApplication = {
  readonly clientId: string;
  readonly clientName?: string | null;
  readonly scopes: readonly AuthorizedScope[];
};

export type AuthorizedAppsPageProps = {
  /** The antiforgery request token echoed from the host-set cookie, posted by the revoke form. */
  readonly antiforgeryToken: string;
  /** The product/tenant display name shown in the heading. */
  readonly productName?: string;
  /** Loads the authorized applications; defaults to a same-origin fetch of the applications endpoint. */
  readonly loadApplications?: () => Promise<readonly AuthorizedApplication[] | null>;
};

/**
 * Default loader: fetches the OP's authorized-applications endpoint. A `401` means the cookie session is gone, so it
 * redirects to the login page (with the applications page as the return URL); any other non-OK response yields no
 * applications. It is a no-op when `fetch` is unavailable (e.g. a DOM-free unit test that does not inject a loader).
 */
async function fetchApplications(): Promise<readonly AuthorizedApplication[] | null> {
  if (typeof fetch !== 'function') {
    return null;
  }
  const response = await fetch(APPLICATIONS_LIST_ENDPOINT, {
    headers: { accept: 'application/json' },
  });
  if (response.status === 401) {
    window.location.assign(`${LOGIN_ACTION}?ReturnUrl=${encodeURIComponent(APPLICATIONS_PATH)}`);
    return null;
  }
  if (!response.ok) {
    return null;
  }
  const payload = (await response.json()) as { applications?: readonly AuthorizedApplication[] };
  return payload.applications ?? [];
}

/**
 * The self-service "authorized applications" page. It lists the applications the end-user has granted standing consent
 * to (with the scopes each was granted) and offers a per-application revoke control. Revoke submits a native HTML form
 * POST to the OP carrying the antiforgery token, so the server revokes the consent record and redirects back.
 */
export function AuthorizedAppsPage({
  antiforgeryToken,
  productName,
  loadApplications = fetchApplications,
}: AuthorizedAppsPageProps) {
  const [applications, setApplications] = useState<readonly AuthorizedApplication[] | null>(null);

  useEffect(() => {
    let active = true;
    loadApplications()
      .then((loaded) => {
        if (active) {
          setApplications(loaded);
        }
      })
      .catch(() => {
        /* a failed load leaves the page in its loading state */
      });
    return () => {
      active = false;
    };
  }, [loadApplications]);

  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        p: 2,
      }}
    >
      <Card sx={{ width: '100%', maxWidth: 520 }}>
        <CardContent>
          <Stack spacing={3}>
            <Typography variant="h5" component="h1">
              {productName ? `${productName} — authorized applications` : 'Authorized applications'}
            </Typography>

            {applications === null ? (
              <Typography color="text.secondary" data-testid="applications-loading">
                Loading your applications…
              </Typography>
            ) : applications.length === 0 ? (
              <Typography color="text.secondary" data-testid="applications-empty">
                You have not authorized any applications.
              </Typography>
            ) : (
              <Stack spacing={2} data-testid="applications-list">
                {applications.map((application) => (
                  <Box key={application.clientId} data-testid="application-item">
                    <Stack
                      direction="row"
                      justifyContent="space-between"
                      alignItems="center"
                      spacing={2}
                    >
                      <Typography sx={{ wordBreak: 'break-all' }}>
                        <strong>{application.clientName ?? application.clientId}</strong>
                      </Typography>
                      <Stack component="form" method="post" action={APPLICATIONS_REVOKE_ACTION}>
                        <input type="hidden" name="client_id" value={application.clientId} />
                        <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />
                        <Button
                          name="revoke"
                          value="revoke"
                          type="submit"
                          variant="outlined"
                          color="error"
                          size="small"
                        >
                          Revoke
                        </Button>
                      </Stack>
                    </Stack>
                    {application.scopes.length > 0 ? (
                      <Stack direction="row" spacing={1} flexWrap="wrap" sx={{ mt: 1 }}>
                        {application.scopes.map((scope) => (
                          <Chip
                            key={scope.value}
                            label={scope.description ?? scope.value}
                            size="small"
                            sx={{ mb: 1 }}
                          />
                        ))}
                      </Stack>
                    ) : null}
                    <Divider sx={{ mt: 1 }} />
                  </Box>
                ))}
              </Stack>
            )}

            <Button href={ACCOUNT_PATH} variant="text">
              Back to your account
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
