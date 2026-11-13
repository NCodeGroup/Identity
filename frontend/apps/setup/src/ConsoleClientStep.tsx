import { useState, type FormEvent } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { RootAccount } from './RootAccountStep.js';
import { createRoot, type CreatedRoot, type ManagementConsoleRegistration } from './setupApi.js';

export type ConsoleClientStepProps = {
  /** The setup token the operator entered. */
  readonly token: string;
  /** The root administrator's account the previous step collected. */
  readonly account: RootAccount;
  /** Called with the root administrator once the instance created it. */
  readonly onCreated: (root: CreatedRoot) => void;
  /** Called when the instance refuses the setup token. */
  readonly onTokenRejected: () => void;
  /** Called when the instance is already set up. */
  readonly onNotFound: () => void;
};

const DEFAULT_CLIENT_ID = 'mgmt-console';
const NOT_CREATED = 'The root administrator was not created.';
const RATE_LIMITED = 'Too many attempts. Wait a minute, then try again.';
const FAILED = 'The instance could not create the root administrator. Try again.';
const URL_NOT_HTTPS = 'The console URL must start with https://.';
const JWKS_INVALID =
  'Paste the console’s public JWK Set: a JSON object with a non-empty keys array.';
const JWKS_PRIVATE = 'Paste the console’s public keys only.';

type JwksParse =
  | { readonly kind: 'ok'; readonly jwks: unknown }
  | { readonly kind: 'refused'; readonly message: string };

function parsePublicJwks(text: string): JwksParse {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    return { kind: 'refused', message: JWKS_INVALID };
  }

  const keys = (parsed as { readonly keys?: unknown } | null)?.keys;
  if (!Array.isArray(keys) || keys.length === 0) {
    return { kind: 'refused', message: JWKS_INVALID };
  }
  if (keys.some((key) => (key as { readonly d?: unknown } | null)?.d !== undefined)) {
    return { kind: 'refused', message: JWKS_PRIVATE };
  }
  return { kind: 'ok', jwks: parsed };
}

/**
 * The third step of the setup wizard: it optionally registers the management console's client in the same setup, then
 * creates the root administrator with the setup token.
 */
export function ConsoleClientStep({
  token,
  account,
  onCreated,
  onTokenRejected,
  onNotFound,
}: ConsoleClientStepProps) {
  const [register, setRegister] = useState(false);
  const [clientId, setClientId] = useState(DEFAULT_CLIENT_ID);
  const [baseUrl, setBaseUrl] = useState('');
  const [jwksText, setJwksText] = useState('');
  const [error, setError] = useState<string | undefined>(undefined);
  const [submitting, setSubmitting] = useState(false);

  async function submit(): Promise<void> {
    let managementConsole: ManagementConsoleRegistration | undefined;
    if (register) {
      const trimmedUrl = baseUrl.trim();
      if (!/^https:\/\//i.test(trimmedUrl)) {
        setError(URL_NOT_HTTPS);
        return;
      }
      const parsed = parsePublicJwks(jwksText);
      if (parsed.kind === 'refused') {
        setError(parsed.message);
        return;
      }
      const trimmedClientId = clientId.trim();
      managementConsole = {
        ...(trimmedClientId ? { clientId: trimmedClientId } : {}),
        baseUrl: trimmedUrl,
        jwks: parsed.jwks,
      };
    }

    setError(undefined);
    setSubmitting(true);
    const result = await createRoot({
      token,
      userName: account.userName,
      ...(account.email ? { email: account.email } : {}),
      password: account.password,
      ...(managementConsole ? { managementConsole } : {}),
    });

    switch (result.kind) {
      case 'created':
        onCreated(result.root);
        return;
      case 'forbidden':
        onTokenRejected();
        return;
      case 'notFound':
        onNotFound();
        return;
      case 'invalid':
        setError(result.problem.detail ?? result.problem.title ?? NOT_CREATED);
        break;
      case 'rateLimited':
        setError(RATE_LIMITED);
        break;
      case 'failed':
        setError(FAILED);
        break;
    }
    setSubmitting(false);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    void submit();
  }

  return (
    <Stack spacing={3} component="form" onSubmit={handleSubmit}>
      <Typography variant="h5" component="h1">
        Register the management console
      </Typography>

      <Typography variant="body2">
        The management console signs in with its own client. Register it now with the console’s URL
        and its public keys, or register it later.
      </Typography>

      {error ? <Alert severity="error">{error}</Alert> : null}

      <FormControlLabel
        control={
          <Checkbox
            checked={register}
            onChange={(event) => {
              setRegister(event.target.checked);
            }}
          />
        }
        label="Register the management console’s client now"
      />

      {register ? (
        <>
          <TextField
            id="console-client-id"
            name="console-client-id"
            label="Client ID"
            autoComplete="off"
            value={clientId}
            onChange={(event) => {
              setClientId(event.target.value);
            }}
            fullWidth
          />
          <TextField
            id="console-url"
            name="console-url"
            label="Console URL"
            autoComplete="off"
            slotProps={{ htmlInput: { inputMode: 'url' } }}
            helperText="The console’s origin, such as https://console.example.com."
            value={baseUrl}
            onChange={(event) => {
              setBaseUrl(event.target.value);
            }}
            fullWidth
          />
          <TextField
            id="console-jwks"
            name="console-jwks"
            label="Public JWK Set"
            autoComplete="off"
            helperText="The console’s public keys as a JSON Web Key Set. Never paste a private key."
            value={jwksText}
            onChange={(event) => {
              setJwksText(event.target.value);
            }}
            multiline
            minRows={5}
            fullWidth
          />
        </>
      ) : null}

      <Button type="submit" variant="contained" disabled={submitting} fullWidth>
        Create the root administrator
      </Button>
    </Stack>
  );
}
