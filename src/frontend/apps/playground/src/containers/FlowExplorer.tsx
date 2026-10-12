import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { RuntimeConfig } from '@ncode/config';
import { CodeBlock } from '../components/CodeBlock.js';
import { StepCard, type StepStatus } from '../components/StepCard.js';
import {
  AuthError,
  beginFlow,
  endSessionUrl,
  exchangeCode,
  fetchUserInfo,
  inspectJwt,
  isCallback,
  readCallback,
  refreshTokens,
  type FlowDeps,
  type TokenSet,
} from '../flow.js';

export type FlowExplorerProps = {
  readonly runtime: RuntimeConfig;
  readonly deps: FlowDeps;
  /** The app origin (defaults to the live location); the registered redirect URI derives from it. */
  readonly origin?: string;
  /** The current query string (defaults to the live location); drives automatic callback completion. */
  readonly initialSearch?: string;
  /** Navigates the user agent to an absolute URL (defaults to a full-page assign). */
  readonly navigate?: (url: string) => void;
  /** Clears the authorization response from the address bar after completion. */
  readonly onCleanUrl?: () => void;
};

function errorMessage(cause: unknown): string {
  if (cause instanceof AuthError) {
    return cause.code ? `${cause.code}: ${cause.message}` : cause.message;
  }
  return cause instanceof Error ? cause.message : 'Unexpected error.';
}

function parseScopes(text: string): string[] {
  return text.split(/\s+/).filter((scope) => scope.length > 0);
}

function JwtView({ label, token }: { label: string; token: string }) {
  const decoded = inspectJwt(token);
  if (!decoded) {
    return <CodeBlock label={`${label} (opaque)`} value={token} sensitive />;
  }
  return (
    <Stack spacing={1}>
      <CodeBlock label={`${label} — header`} value={JSON.stringify(decoded.header, null, 2)} />
      <CodeBlock label={`${label} — payload`} value={JSON.stringify(decoded.payload, null, 2)} />
    </Stack>
  );
}

/**
 * Stateful flow explorer. Drives a live OAuth 2.1 / OpenID Connect authorization-code + PKCE (S256) walkthrough
 * against the discovered OpenID Provider — the only interactive flow modern guidance recommends — and surfaces every
 * artifact (PKCE challenge, `state`, `nonce`, the authorize URL, the token response, decoded claims). All environment
 * seams (origin, query string, navigation) are injectable so the orchestration is testable without the DOM globals.
 */
export function FlowExplorer({
  runtime,
  deps,
  origin = window.location.origin,
  initialSearch = window.location.search,
  navigate = (url) => window.location.assign(url),
  onCleanUrl = () => window.history.replaceState({}, document.title, `${window.location.origin}/`),
}: FlowExplorerProps) {
  const [clientId, setClientId] = useState(runtime.clientId ?? '');
  const [scopesText, setScopesText] = useState(runtime.scopes.join(' '));
  const [beginError, setBeginError] = useState<string | null>(null);

  const [tokens, setTokens] = useState<TokenSet | null>(null);
  const [activeClientId, setActiveClientId] = useState<string | null>(null);
  const [callbackError, setCallbackError] = useState<string | null>(null);

  const [userInfo, setUserInfo] = useState<Record<string, unknown> | null>(null);
  const [userInfoError, setUserInfoError] = useState<string | null>(null);
  const [refreshError, setRefreshError] = useState<string | null>(null);

  // A single-use authorization code and the one-shot PKCE transaction must be consumed EXACTLY once; a run-once ref
  // (not an effect-cancellation flag) guards against React StrictMode's intentional double-invocation in development.
  const completionStarted = useRef(false);

  useEffect(() => {
    if (completionStarted.current || !isCallback(initialSearch)) {
      return;
    }
    completionStarted.current = true;
    (async () => {
      try {
        const callback = readCallback(initialSearch, deps);
        const issued = await exchangeCode(runtime, callback, deps);
        setTokens(issued);
        setActiveClientId(callback.transaction.clientId);
      } catch (cause) {
        setCallbackError(errorMessage(cause));
      } finally {
        onCleanUrl();
      }
    })();
  }, [deps, initialSearch, onCleanUrl, runtime]);

  const handleBegin = async () => {
    setBeginError(null);
    try {
      const { authorizeUrl } = await beginFlow(
        runtime,
        { clientId, scopes: parseScopes(scopesText) },
        origin,
        deps,
      );
      navigate(authorizeUrl);
    } catch (cause) {
      setBeginError(errorMessage(cause));
    }
  };

  const handleUserInfo = async () => {
    if (!tokens) {
      return;
    }
    setUserInfoError(null);
    try {
      setUserInfo(await fetchUserInfo(runtime, tokens.accessToken, deps));
    } catch (cause) {
      setUserInfoError(errorMessage(cause));
    }
  };

  const handleRefresh = async () => {
    if (!tokens?.refreshToken || !activeClientId) {
      return;
    }
    setRefreshError(null);
    try {
      setTokens(await refreshTokens(runtime, activeClientId, tokens.refreshToken, deps));
    } catch (cause) {
      setRefreshError(errorMessage(cause));
    }
  };

  const handleLogout = () => {
    const url = endSessionUrl(runtime, tokens?.idToken ?? null, activeClientId ?? clientId, origin);
    if (url) {
      navigate(url);
    }
  };

  const handleReset = () => {
    setTokens(null);
    setActiveClientId(null);
    setCallbackError(null);
    setUserInfo(null);
    setUserInfoError(null);
    setRefreshError(null);
    completionStarted.current = false;
  };

  const requestStatus: StepStatus = tokens ? 'done' : beginError ? 'error' : 'active';
  const tokenStatus: StepStatus = tokens ? 'done' : callbackError ? 'error' : 'idle';

  return (
    <Stack spacing={2}>
      <StepCard
        index={1}
        title="Build the authorization request"
        status={requestStatus}
        description="Authorization code + PKCE (S256) is the only interactive flow modern guidance recommends; implicit, hybrid, and password grants are intentionally unavailable."
      >
        <Stack spacing={2}>
          <TextField
            label="Client ID"
            value={clientId}
            onChange={(event) => setClientId(event.target.value)}
            size="small"
            fullWidth
            inputProps={{ 'data-testid': 'client-id' }}
          />
          <TextField
            label="Scopes (space-separated)"
            value={scopesText}
            onChange={(event) => setScopesText(event.target.value)}
            size="small"
            fullWidth
            inputProps={{ 'data-testid': 'scopes' }}
          />
          <CodeBlock label="Redirect URI (registered)" value={`${origin}/callback`} />
          {beginError ? <Alert severity="error">{beginError}</Alert> : null}
          <Box>
            <Button
              variant="contained"
              onClick={handleBegin}
              disabled={clientId.length === 0}
              data-testid="begin"
            >
              Start authorization
            </Button>
          </Box>
        </Stack>
      </StepCard>

      <StepCard
        index={2}
        title="Complete the callback & exchange the code"
        status={tokenStatus}
        description="On return the explorer validates state (CSRF), then exchanges the single-use code with the PKCE verifier for tokens."
      >
        {callbackError ? <Alert severity="error">{callbackError}</Alert> : null}
        {!tokens && !callbackError ? (
          <Typography color="text.secondary">
            No tokens yet — start the authorization above to complete the round trip.
          </Typography>
        ) : null}
        {tokens ? (
          <Stack spacing={1.5}>
            <Typography variant="body2" color="text.secondary">
              token_type <strong>{tokens.tokenType}</strong>
              {tokens.expiresInSeconds ? ` · expires_in ${tokens.expiresInSeconds}s` : ''}
              {tokens.scope ? ` · scope ${tokens.scope}` : ''}
            </Typography>
            <JwtView label="access_token" token={tokens.accessToken} />
            {tokens.idToken ? <JwtView label="id_token" token={tokens.idToken} /> : null}
            {tokens.refreshToken ? (
              <CodeBlock label="refresh_token" value={tokens.refreshToken} sensitive />
            ) : null}
          </Stack>
        ) : null}
      </StepCard>

      <StepCard
        index={3}
        title="Use the tokens"
        status={tokens ? 'active' : 'idle'}
        description="Call UserInfo with the access token, refresh it, or end the session with RP-Initiated Logout."
      >
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Button
              variant="outlined"
              onClick={handleUserInfo}
              disabled={!tokens || !runtime.userInfoEndpoint}
              data-testid="userinfo"
            >
              Call UserInfo
            </Button>
            <Button
              variant="outlined"
              onClick={handleRefresh}
              disabled={!tokens?.refreshToken}
              data-testid="refresh"
            >
              Refresh token
            </Button>
            <Button
              variant="outlined"
              color="warning"
              onClick={handleLogout}
              disabled={!tokens || !runtime.endSessionEndpoint}
              data-testid="logout"
            >
              Sign out (RP-initiated)
            </Button>
            <Button variant="text" onClick={handleReset} data-testid="reset">
              Reset
            </Button>
          </Stack>
          {refreshError ? <Alert severity="error">{refreshError}</Alert> : null}
          {userInfoError ? <Alert severity="error">{userInfoError}</Alert> : null}
          {userInfo ? (
            <>
              <Divider />
              <CodeBlock label="UserInfo response" value={JSON.stringify(userInfo, null, 2)} />
            </>
          ) : null}
        </Stack>
      </StepCard>
    </Stack>
  );
}
