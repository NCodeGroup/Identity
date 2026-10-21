import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import {
  ACCOUNT_PATH,
  ANTIFORGERY_FIELD,
  LOGIN_ACTION,
  LOGOUT_ACTION,
  ME_ENDPOINT,
  SESSIONS_PATH,
  resolveSurfacePath,
} from './params.js';

/** The current end-user session the account page renders (mirrors the OP's `/account/me` payload). */
export type AccountSession = {
  readonly subject: string;
  readonly name?: string | null;
  readonly authTime?: number | null;
  readonly amr?: readonly string[];
  readonly idp?: string | null;
  readonly sessionId?: string | null;
};

export type AccountPageProps = {
  /** The antiforgery request token echoed from the host-set cookie, posted by the sign-out form. */
  readonly antiforgeryToken: string;
  /** The product/tenant display name shown in the heading. */
  readonly productName?: string;
  /** Loads the current session; defaults to a same-origin fetch of the `/account/me` endpoint. */
  readonly loadSession?: () => Promise<AccountSession | null>;
};

/**
 * Default session loader: fetches the OP's `/account/me` endpoint. A `401` means the cookie session is gone, so it
 * redirects to the login page (with the account page as the return URL); any other non-OK response yields no session.
 * It is a no-op when `fetch` is unavailable (e.g. a DOM-free unit test that does not inject `loadSession`).
 */
async function fetchSession(): Promise<AccountSession | null> {
  if (typeof fetch !== 'function') {
    return null;
  }
  const response = await fetch(resolveSurfacePath(ME_ENDPOINT), {
    headers: { accept: 'application/json' },
  });
  if (response.status === 401) {
    window.location.assign(
      `${resolveSurfacePath(LOGIN_ACTION)}?ReturnUrl=${encodeURIComponent(resolveSurfacePath(ACCOUNT_PATH))}`,
    );
    return null;
  }
  if (!response.ok) {
    return null;
  }
  return (await response.json()) as AccountSession;
}

function formatAuthTime(authTime: number | null | undefined): string | null {
  if (typeof authTime !== 'number') {
    return null;
  }
  return new Date(authTime * 1000).toLocaleString();
}

/**
 * The account/session ("me") page. It loads the current end-user's session and renders it alongside a first-party
 * sign-out control. Sign-out submits a native HTML form POST to the OP's logout endpoint carrying the antiforgery
 * token, so the server runs its full logout pipeline (revoke session + grants, back-channel notify) and redirects.
 */
export function AccountPage({
  antiforgeryToken,
  productName,
  loadSession = fetchSession,
}: AccountPageProps) {
  const [session, setSession] = useState<AccountSession | null>(null);

  useEffect(() => {
    let active = true;
    loadSession()
      .then((loaded) => {
        if (active) {
          setSession(loaded);
        }
      })
      .catch(() => {
        /* a failed load leaves the page in its loading state; sign-out remains available */
      });
    return () => {
      active = false;
    };
  }, [loadSession]);

  const authTime = formatAuthTime(session?.authTime);

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
      <Card sx={{ width: '100%', maxWidth: 440 }}>
        <CardContent>
          <Stack spacing={3}>
            <Typography variant="h5" component="h1">
              {productName ? `${productName} account` : 'Your account'}
            </Typography>

            {session ? (
              <Stack spacing={1} data-testid="account-session">
                <Typography>
                  Signed in as <strong>{session.name ?? session.subject}</strong>
                </Typography>
                <Divider />
                <DetailRow label="Subject" value={session.subject} testId="account-subject" />
                {authTime ? (
                  <DetailRow label="Signed in at" value={authTime} testId="account-auth-time" />
                ) : null}
                {session.amr && session.amr.length > 0 ? (
                  <DetailRow label="Methods" value={session.amr.join(', ')} testId="account-amr" />
                ) : null}
                {session.idp ? (
                  <DetailRow label="Identity provider" value={session.idp} testId="account-idp" />
                ) : null}
                {session.sessionId ? (
                  <DetailRow label="Session" value={session.sessionId} testId="account-sid" />
                ) : null}
              </Stack>
            ) : (
              <Typography data-testid="account-loading" sx={{ color: 'text.secondary' }}>
                Loading your session…
              </Typography>
            )}

            <Stack component="form" method="post" action={resolveSurfacePath(LOGOUT_ACTION)}>
              <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />
              <input type="hidden" name="ReturnUrl" value={resolveSurfacePath(LOGIN_ACTION)} />
              <Button
                id="logout"
                name="logout"
                value="logout"
                type="submit"
                variant="contained"
                color="error"
                fullWidth
              >
                Sign out
              </Button>
            </Stack>

            <Button href={resolveSurfacePath(SESSIONS_PATH)} variant="text">
              Manage active sessions
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}

function DetailRow({
  label,
  value,
  testId,
}: {
  readonly label: string;
  readonly value: string;
  readonly testId: string;
}) {
  return (
    <Stack direction="row" spacing={2} sx={{ justifyContent: 'space-between' }}>
      <Typography sx={{ color: 'text.secondary' }}>{label}</Typography>
      <Typography sx={{ wordBreak: 'break-all', textAlign: 'right' }} data-testid={testId}>
        {value}
      </Typography>
    </Stack>
  );
}
