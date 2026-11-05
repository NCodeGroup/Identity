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
  SESSIONS_LIST_ENDPOINT,
  SESSIONS_PATH,
  SESSIONS_REVOKE_ACTION,
  SESSIONS_REVOKE_ALL_ACTION,
  resolveSurfacePath,
} from './params.js';
import { useLocale } from './i18n/LocaleContext.js';
import { fetchSurfaceJson } from './surfaceFetch.js';

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
 * Default loader: loads the OP's active-sessions endpoint. The login redirect on a `401` comes from
 * `fetchSurfaceJson`.
 */
async function fetchSessions(): Promise<readonly ActiveSession[] | null> {
  const payload = await fetchSurfaceJson<{ sessions?: readonly ActiveSession[] }>(
    SESSIONS_LIST_ENDPOINT,
    SESSIONS_PATH,
  );
  return payload ? (payload.sessions ?? []) : null;
}

function formatTimestamp(seconds: number | null | undefined, locale: string): string | null {
  if (typeof seconds !== 'number') {
    return null;
  }
  return new Date(seconds * 1000).toLocaleString(locale);
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
  const { locale, messages } = useLocale();
  const text = messages.sessions;
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
              {productName ? text.titleFor(productName) : text.title}
            </Typography>

            {sessions === null ? (
              <Typography data-testid="sessions-loading" sx={{ color: 'text.secondary' }}>
                {text.loading}
              </Typography>
            ) : sessions.length === 0 ? (
              <Typography data-testid="sessions-empty" sx={{ color: 'text.secondary' }}>
                {text.empty}
              </Typography>
            ) : (
              <Stack spacing={2} data-testid="sessions-list">
                {sessions.map((session) => (
                  <Box key={session.sessionId} data-testid="session-item">
                    <Stack
                      direction="row"
                      spacing={2}
                      sx={{ justifyContent: 'space-between', alignItems: 'center' }}
                    >
                      <Typography sx={{ wordBreak: 'break-word' }}>
                        <strong>{session.userAgent ?? text.unknownDevice}</strong>
                      </Typography>
                      {session.isCurrent ? (
                        <Chip
                          label={text.thisDevice}
                          color="success"
                          size="small"
                          data-testid="session-current"
                        />
                      ) : (
                        <Stack
                          component="form"
                          method="post"
                          action={resolveSurfacePath(SESSIONS_REVOKE_ACTION)}
                        >
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
                            {text.signOut}
                          </Button>
                        </Stack>
                      )}
                    </Stack>
                    <Stack spacing={0.5} sx={{ mt: 1 }}>
                      <DetailRow label={text.ipAddress} value={session.ipAddress} />
                      <DetailRow
                        label={text.lastActive}
                        value={formatTimestamp(session.lastActivityWhen, locale)}
                      />
                      <DetailRow
                        label={text.signedIn}
                        value={formatTimestamp(session.createdWhen, locale)}
                      />
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
                action={resolveSurfacePath(SESSIONS_REVOKE_ALL_ACTION)}
                data-testid="sessions-revoke-others"
              >
                <input type="hidden" name="scope" value="others" />
                <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />
                <Button type="submit" variant="outlined" color="warning" fullWidth>
                  {text.signOutOthers}
                </Button>
              </Stack>
            ) : null}

            <Stack
              component="form"
              method="post"
              action={resolveSurfacePath(SESSIONS_REVOKE_ALL_ACTION)}
              data-testid="sessions-revoke-all"
            >
              <input type="hidden" name="scope" value="all" />
              <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />
              <Button type="submit" variant="contained" color="error" fullWidth>
                {text.signOutAll}
              </Button>
            </Stack>

            <Button href={resolveSurfacePath(ACCOUNT_PATH)} variant="text">
              {text.back}
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}

/** One labelled detail of a session; a detail without a value draws nothing. */
function DetailRow({
  label,
  value,
}: {
  readonly label: string;
  readonly value: string | null | undefined;
}) {
  if (!value) {
    return null;
  }
  return (
    <Stack direction="row" spacing={2} sx={{ justifyContent: 'space-between' }}>
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {label}
      </Typography>
      <Typography variant="body2" sx={{ wordBreak: 'break-all', textAlign: 'right' }}>
        {value}
      </Typography>
    </Stack>
  );
}
