import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Checkbox from '@mui/material/Checkbox';
import CircularProgress from '@mui/material/CircularProgress';
import FormControlLabel from '@mui/material/FormControlLabel';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { RootAccount } from './RootAccountStep.js';
import type { ConsoleHint } from './consoleHint.js';
import {
  consoleKeySetUrl,
  createRoot,
  type CreatedRoot,
  type FetchKeySetResult,
  type ManagementConsoleRegistration,
} from './setupApi.js';

export type ConsoleClientStepProps = {
  /** The setup token the operator entered. */
  readonly token: string;
  /** The root administrator's account the previous step collected. */
  readonly account: RootAccount;
  /**
   * The console the deployment or the page's URL fragment names. When it has a URL, the step opens with the
   * registration checked, the fields filled and the console's key set fetched.
   */
  readonly consoleHint?: ConsoleHint;
  /** Loads the management console's public key set from the console at the given base URL. */
  readonly loadKeySet: (baseUrl: string, signal?: AbortSignal) => Promise<FetchKeySetResult>;
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
const PASTE_THE_SET = 'Paste the console’s public JWK Set.';

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
  consoleHint,
  loadKeySet,
  onCreated,
  onTokenRejected,
  onNotFound,
}: ConsoleClientStepProps) {
  const [hintedUrl] = useState(consoleHint?.baseUrl);
  const [register, setRegister] = useState(Boolean(consoleHint?.baseUrl));
  const [clientId, setClientId] = useState(consoleHint?.clientId ?? DEFAULT_CLIENT_ID);
  const [baseUrl, setBaseUrl] = useState(consoleHint?.baseUrl ?? '');
  const [jwksText, setJwksText] = useState('');
  const [error, setError] = useState<string | undefined>(undefined);
  const [submitting, setSubmitting] = useState(false);
  const [fetching, setFetching] = useState(Boolean(consoleHint?.baseUrl));
  const [fetchedFrom, setFetchedFrom] = useState<string | undefined>(undefined);
  const [fetchWarning, setFetchWarning] = useState<string | undefined>(undefined);
  const fetchControllerRef = useRef<AbortController | undefined>(undefined);

  const fetchKeySet = useCallback(
    async (url: string): Promise<void> => {
      fetchControllerRef.current?.abort();
      const controller = new AbortController();
      fetchControllerRef.current = controller;

      let target = url;
      try {
        target = consoleKeySetUrl(url);
      } catch {
        // The warning names the console URL itself when it cannot be turned into a key set address.
      }
      setFetching(true);
      setFetchedFrom(undefined);
      setFetchWarning(undefined);
      const result = await loadKeySet(url, controller.signal);
      if (controller.signal.aborted) {
        return;
      }
      setFetching(false);

      switch (result.kind) {
        case 'ok': {
          const parsed = parsePublicJwks(JSON.stringify(result.jwks));
          if (parsed.kind === 'refused') {
            setFetchWarning(parsed.message);
            return;
          }
          setJwksText(JSON.stringify(parsed.jwks, null, 2));
          setFetchedFrom(target);
          return;
        }
        case 'status':
          setFetchWarning(
            `The console answered HTTP ${String(result.status)} for its key set at ${target}. ${PASTE_THE_SET}`,
          );
          return;
        case 'unreachable':
          setFetchWarning(
            `This page could not read the console’s key set at ${target}. The browser refuses a console origin this deployment does not name, and a console that is not running or not reachable from this browser cannot answer. ${PASTE_THE_SET}`,
          );
          return;
        case 'aborted':
          return;
      }
    },
    [loadKeySet],
  );

  useEffect(() => {
    if (hintedUrl) {
      void fetchKeySet(hintedUrl);
    }
    return () => {
      fetchControllerRef.current?.abort();
    };
  }, [hintedUrl, fetchKeySet]);

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
          {baseUrl.trim() ? (
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
              <Button
                variant="outlined"
                disabled={fetching}
                onClick={() => {
                  void fetchKeySet(baseUrl.trim());
                }}
              >
                Fetch from the console
              </Button>
              {fetching ? <CircularProgress size={24} aria-label="Fetching the key set" /> : null}
            </Stack>
          ) : null}
          {fetchWarning ? <Alert severity="warning">{fetchWarning}</Alert> : null}
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
          {fetchedFrom ? (
            <Typography variant="caption">Fetched from {fetchedFrom}</Typography>
          ) : null}
        </>
      ) : null}

      <Button type="submit" variant="contained" disabled={submitting} fullWidth>
        Create the root administrator
      </Button>
    </Stack>
  );
}
