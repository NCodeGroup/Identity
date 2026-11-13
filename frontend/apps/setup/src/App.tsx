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
import { getStatus, type SetupStatus } from './setupApi.js';
import { takeSetupToken } from './setupToken.js';

type Load =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready'; readonly status: SetupStatus }
  | { readonly kind: 'notFound' }
  | { readonly kind: 'error' };

type Step =
  | { readonly name: 'token'; readonly errorMessage?: string }
  | { readonly name: 'account' }
  | { readonly name: 'console'; readonly account: RootAccount }
  | {
      readonly name: 'done';
      readonly userName: string;
      readonly managementConsoleClientId?: string;
    };

const TOKEN_REJECTED = 'The instance did not accept the setup token. Check it and enter it again.';

/**
 * The setup wizard. It reads the instance's setup status, then walks the operator through the setup token, the root
 * administrator's account, the optional registration of the management console's client and the page that lists what
 * remains. The token lives only in this component's state.
 */
export function App() {
  const [load, setLoad] = useState<Load>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  const [token, setToken] = useState(() => takeSetupToken() ?? '');
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
            onContinue={(entered) => {
              setToken(entered);
              setStep({ name: 'account' });
            }}
          />
        );
      case 'account':
        return (
          <RootAccountStep
            minimumPasswordLength={load.status.minimumPasswordLength}
            onContinue={(account) => {
              setStep({ name: 'console', account });
            }}
          />
        );
      case 'console':
        return (
          <ConsoleClientStep
            token={token}
            account={step.account}
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
