import { useEffect, useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CircularProgress from '@mui/material/CircularProgress';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { ConsoleClientStep } from './ConsoleClientStep.js';
import { DonePage } from './DonePage.js';
import { RootAccountStep, type RootAccount } from './RootAccountStep.js';
import { TokenStep } from './TokenStep.js';
import { checkToken, getStatus, type ManagementConsoleHint, type SetupStatus } from './setupApi.js';
import { takeSetupFragment } from './setupToken.js';

type Load =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready'; readonly status: SetupStatus }
  | { readonly kind: 'notFound' }
  | { readonly kind: 'error' };

/** The console the deployment or the page's URL fragment names, which the console step prefills. */
type ConsoleHint = { readonly baseUrl?: string; readonly clientId?: string };

type Step =
  | { readonly name: 'token'; readonly errorMessage?: string }
  | { readonly name: 'account'; readonly consoleHint: ConsoleHint }
  | { readonly name: 'console'; readonly account: RootAccount; readonly consoleHint: ConsoleHint }
  | {
      readonly name: 'done';
      readonly userName: string;
      readonly managementConsoleClientId?: string;
    };

const TOKEN_REJECTED = 'The instance did not accept the setup token. Check it and enter it again.';
const TOKEN_NOT_CHECKED = 'The setup token could not be checked. Try again.';
const RATE_LIMITED = 'Too many attempts. Wait a minute, then try again.';

/**
 * Merges the console the server hints with the one the page's URL fragment names. The server's hint wins on each
 * field, and the fragment fills only what the hint leaves empty.
 */
export function mergeConsoleHint(
  hint: ManagementConsoleHint | undefined,
  fragment: { readonly consoleUrl?: string; readonly consoleClientId?: string },
): ConsoleHint {
  const baseUrl = hint?.baseUrl || fragment.consoleUrl;
  const clientId = hint?.clientId || fragment.consoleClientId;
  return {
    ...(baseUrl ? { baseUrl } : {}),
    ...(clientId ? { clientId } : {}),
  };
}

/**
 * The setup wizard. It reads the instance's setup status, then walks the operator through the setup token, the root
 * administrator's account, the optional registration of the management console's client and the page that lists what
 * remains. The token lives only in this component's state.
 */
export function App() {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  const [fragment] = useState(() => takeSetupFragment());
  const [token, setToken] = useState(fragment.token ?? '');
  const [step, setStep] = useState<Step>({ name: 'token' });

  useEffect(() => {
    let cancelled = false;
    void getStatus().then((result) => {
      if (cancelled) {
        return;
      }
      setLoad(result.kind === 'ok' ? { kind: 'ready', status: result.status } : result);
    });
    return () => {
      cancelled = true;
    };
  }, [attempt]);

  function retry(): void {
    setLoad({ kind: 'loading' });
    setAttempt((value) => value + 1);
  }

  function renderContent() {
    switch (load.kind) {
      case 'loading':
        return (
          <Stack sx={{ alignItems: 'center' }}>
            <CircularProgress aria-label="Loading" />
          </Stack>
        );
      case 'notFound':
        return (
          <Typography variant="h5" component="h1">
            This instance is already set up.
          </Typography>
        );
      case 'error':
        return (
          <Stack spacing={3}>
            <Alert severity="error">The instance’s setup status could not be loaded.</Alert>
            <Button variant="contained" onClick={retry}>
              Try again
            </Button>
          </Stack>
        );
      case 'ready':
        break;
    }

    switch (step.name) {
      case 'token':
        return (
          <TokenStep
            tokenSource={load.status.tokenSource}
            initialToken={token}
            errorMessage={step.errorMessage}
            onContinue={async (entered) => {
              const result = await checkToken(entered);
              switch (result.kind) {
                case 'ok': {
                  setToken(entered);
                  setStep({
                    name: 'account',
                    consoleHint: mergeConsoleHint(result.hint, fragment),
                  });
                  return undefined;
                }
                case 'forbidden':
                  return TOKEN_REJECTED;
                case 'rateLimited':
                  return result.problem.detail ?? RATE_LIMITED;
                case 'notFound':
                  setLoad({ kind: 'notFound' });
                  return undefined;
                case 'invalid':
                case 'failed':
                  return TOKEN_NOT_CHECKED;
              }
            }}
          />
        );
      case 'account':
        return (
          <RootAccountStep
            minimumPasswordLength={load.status.minimumPasswordLength}
            onContinue={(account) => {
              setStep({ name: 'console', account, consoleHint: step.consoleHint });
            }}
          />
        );
      case 'console':
        return (
          <ConsoleClientStep
            token={token}
            account={step.account}
            consoleHint={step.consoleHint}
            onCreated={(root) => {
              setToken('');
              setStep({
                name: 'done',
                userName: root.userName,
                ...(root.managementConsoleClientId
                  ? { managementConsoleClientId: root.managementConsoleClientId }
                  : {}),
              });
            }}
            onTokenRejected={() => {
              setStep({ name: 'token', errorMessage: TOKEN_REJECTED });
            }}
            onNotFound={() => {
              setToken('');
              setLoad({ kind: 'notFound' });
            }}
          />
        );
      case 'done':
        return (
          <DonePage
            userName={step.userName}
            managementConsoleClientId={step.managementConsoleClientId}
          />
        );
    }
  }

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
        <CardContent>{renderContent()}</CardContent>
      </Card>
    </Box>
  );
}
