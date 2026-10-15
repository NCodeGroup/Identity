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
  LOGIN_ACTION,
  SESSIONS_LIST_ENDPOINT,
  SESSIONS_PATH,
  SESSIONS_REVOKE_ACTION,
  SESSIONS_REVOKE_ALL_ACTION,
} from './params.js';

/** One active session of the end-user (mirrors the OP's `/account/sessions/list` payload). */
export type ActiveSession = {
  readonly sessionId: string;
  readonly isCurrent: boolean;
  readonly createdWhen: number;
  readonly lastActivityWhen: number;
  readonly expiresWhen?: number | null;
  readonly amr?: readonly string[];
  readonly idp?: string | null;
  readonly userAgent?: string | null;
  readonly ipAddress?: string | null;
};

export type SessionsPageProps = {
  /** The antiforgery request token echoed from the host-set cookie, posted by the revoke forms. */
  readonly antiforgeryToken: string;
  /** The product/tenant display name shown in the heading. */
  readonly productName?: string;
  /** Loads the active sessions; defaults to a same-origin fetch of the sessions endpoint. */
  readonly loadSessions?: () => Promise<readonly ActiveSession[] | null>;
};

/**
 * Default loader: fetches the OP's active-sessions endpoint. A `401` means the cookie session is gone, so it redirects
 * to the login page (with the sessions page as the return URL); any other non-OK response yields no sessions. It is a
 * no-op when `fetch` is unavailable (e.g. a DOM-free unit test that does not inject a loader).
 */
async function fetchSessions(): Promise<readonly ActiveSession[] | null> {
  if (typeof fetch !== 'function') {
    return null;
  }
  const response = await fetch(SESSIONS_LIST_ENDPOINT, { headers: { accept: 'application/json' } });
  if (response.status === 401) {
    window.location.assign(`${LOGIN_ACTION}?ReturnUrl=${encodeURIComponent(SESSIONS_PATH)}`);
    return null;
  }
  if (!response.ok) {
    return null;
  }
  const payload = (await response.json()) as { sessions?: readonly ActiveSession[] };
  return payload.sessions ?? [];
}

function formatTimestamp(seconds: number | null | undefined): string | null {
  if (typeof seconds !== 'number') {
    return null;
  }
  return new Date(seconds * 1000).toLocaleString();
}

/**
 * The self-service "active sessions" page. It lists the sessions the end-user currently holds (device/origin captured
 * at sign-in, plus lifecycle timestamps), marks the session of the current request, and offers per-session revoke plus
 * bulk "sign out everywhere else" (retains the current session) and "sign out everywhere" (revokes all). Each control
 * submits a native HTML form POST carrying the antiforgery token, so the server runs the full session-termination seam.
 */
export function SessionsPage({
  antiforgeryToken,
  productName,
  loadSessions = fetchSessions,
}: SessionsPageProps) {
  const [sessions, setSessions] = useState<readonly ActiveSession[] | null>(null);

  useEffect(() => {
    let active = true;
    loadSessions()
      .then((loaded) => {
        if (active) {
          setSessions(loaded);
        }
      })
      .catch(() => {
        /* a failed load leaves the page in its loading state */
      });
    return () => {
      active = false;
    };
  }, [loadSessions]);

  const hasOthers = (sessions ?? []).some((session) => !session.isCurrent);

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
      <Card sx={{ width: '100%', maxWidth: 560 }}>
        <CardContent>
          <Stack spacing={3}>
            <Typography variant="h5" component="h1">
              {productName ? `${productName} — active sessions` : 'Active sessions'}
            </Typography>

            {sessions === null ? (
              <Typography color="text.secondary" data-testid="sessions-loading">
                Loading your sessions…
              </Typography>
            ) : sessions.length === 0 ? (
              <Typography color="text.secondary" data-testid="sessions-empty">
                You have no other active sessions.
              </Typography>
            ) : (
              <Stack spacing={2} data-testid="sessions-list">
                {sessions.map((session) => (
                  <Box key={session.sessionId} data-testid="session-item">
                    <Stack
                      direction="row"
                      justifyContent="space-between"
                      alignItems="center"
                      spacing={2}
                    >
                      <Typography sx={{ wordBreak: 'break-word' }}>
                        <strong>{session.userAgent ?? 'Unknown device'}</strong>
                      </Typography>
                      {session.isCurrent ? (
                        <Chip
                          label="This device"
                          color="success"
                          size="small"
                          data-testid="session-current"
                        />
                      ) : (
                        <Stack component="form" method="post" action={SESSIONS_REVOKE_ACTION}>
                          <input type="hidden" name="sid" value={session.sessionId} />
                          <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />
                          <Button
                            name="revoke"
                            value="revoke"
                            type="submit"
                            variant="outlined"
                            color="error"
                            size="small"
                          >
                            Sign out
                          </Button>
                        </Stack>
                      )}
                    </Stack>
                    <Stack spacing={0.5} sx={{ mt: 1 }}>
                      {session.ipAddress ? (
                        <DetailRow label="IP address" value={session.ipAddress} />
                      ) : null}
                      {formatTimestamp(session.lastActivityWhen) ? (
                        <DetailRow
                          label="Last active"
                          value={formatTimestamp(session.lastActivityWhen)!}
                        />
                      ) : null}
                      {formatTimestamp(session.createdWhen) ? (
                        <DetailRow
                          label="Signed in"
                          value={formatTimestamp(session.createdWhen)!}
                        />
                      ) : null}
                    </Stack>
                    <Divider sx={{ mt: 1 }} />
                  </Box>
                ))}
              </Stack>
            )}

            {hasOthers ? (
              <Stack
                component="form"
                method="post"
                action={SESSIONS_REVOKE_ALL_ACTION}
                data-testid="sessions-revoke-others"
              >
                <input type="hidden" name="scope" value="others" />
                <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />
                <Button type="submit" variant="outlined" color="warning" fullWidth>
                  Sign out everywhere else
                </Button>
              </Stack>
            ) : null}

            <Stack
              component="form"
              method="post"
              action={SESSIONS_REVOKE_ALL_ACTION}
              data-testid="sessions-revoke-all"
            >
              <input type="hidden" name="scope" value="all" />
              <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />
              <Button type="submit" variant="contained" color="error" fullWidth>
                Sign out everywhere
              </Button>
            </Stack>

            <Button href={ACCOUNT_PATH} variant="text">
              Back to your account
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}

function DetailRow({ label, value }: { readonly label: string; readonly value: string }) {
  return (
    <Stack direction="row" justifyContent="space-between" spacing={2}>
      <Typography color="text.secondary" variant="body2">
        {label}
      </Typography>
      <Typography variant="body2" sx={{ wordBreak: 'break-all', textAlign: 'right' }}>
        {value}
      </Typography>
    </Stack>
  );
}
