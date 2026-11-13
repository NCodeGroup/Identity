import { useState, type FormEvent } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { createRoot } from './setupApi.js';

export type RootAccountStepProps = {
  /** The setup token the operator entered. */
  readonly token: string;
  /** The shortest password the instance accepts. */
  readonly minimumPasswordLength: number;
  /** Called with the root administrator's user name once the instance created it. */
  readonly onCreated: (userName: string) => void;
  /** Called when the instance refuses the setup token. */
  readonly onTokenRejected: () => void;
  /** Called when the instance is already set up. */
  readonly onNotFound: () => void;
};

const NOT_CREATED = 'The root administrator was not created.';
const RATE_LIMITED = 'Too many attempts. Wait a minute, then try again.';
const FAILED = 'The instance could not create the root administrator. Try again.';

/**
 * The second step of the setup wizard: it collects the root administrator's account and creates it with the setup
 * token. The password and its confirmation stay in the component's state.
 */
export function RootAccountStep({
  token,
  minimumPasswordLength,
  onCreated,
  onTokenRejected,
  onNotFound,
}: RootAccountStepProps) {
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [error, setError] = useState<string | undefined>(undefined);
  const [submitting, setSubmitting] = useState(false);

  async function submit(): Promise<void> {
    if (password.length < minimumPasswordLength) {
      setError(`The password must have at least ${String(minimumPasswordLength)} characters.`);
      return;
    }
    if (password !== confirmation) {
      setError('The password and its confirmation do not match.');
      return;
    }

    setError(undefined);
    setSubmitting(true);
    const trimmedEmail = email.trim();
    const result = await createRoot({
      token,
      userName: userName.trim(),
      ...(trimmedEmail ? { email: trimmedEmail } : {}),
      password,
    });

    switch (result.kind) {
      case 'created':
        onCreated(result.root.userName);
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
        Create the root administrator
      </Typography>

      <Typography variant="body2">
        The root administrator is a local account that holds every management authority on this
        instance.
      </Typography>

      {error ? <Alert severity="error">{error}</Alert> : null}

      <TextField
        id="username"
        name="username"
        label="User name"
        autoComplete="username"
        value={userName}
        onChange={(event) => {
          setUserName(event.target.value);
        }}
        autoFocus
        required
        fullWidth
      />
      <TextField
        id="email"
        name="email"
        label="Email (optional)"
        type="email"
        autoComplete="email"
        value={email}
        onChange={(event) => {
          setEmail(event.target.value);
        }}
        fullWidth
      />
      <TextField
        id="password"
        name="password"
        label="Password"
        type="password"
        autoComplete="new-password"
        helperText={`Use at least ${String(minimumPasswordLength)} characters.`}
        value={password}
        onChange={(event) => {
          setPassword(event.target.value);
        }}
        required
        fullWidth
      />
      <TextField
        id="confirm-password"
        name="confirm-password"
        label="Confirm password"
        type="password"
        autoComplete="new-password"
        value={confirmation}
        onChange={(event) => {
          setConfirmation(event.target.value);
        }}
        required
        fullWidth
      />

      <Button type="submit" variant="contained" disabled={submitting} fullWidth>
        Create the root administrator
      </Button>
    </Stack>
  );
}
