import { useState, type FormEvent } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';

/** The root administrator's account, as the operator entered it. */
export type RootAccount = {
  readonly userName: string;
  readonly email?: string;
  readonly password: string;
};

export type RootAccountStepProps = {
  /** The shortest password the instance accepts. */
  readonly minimumPasswordLength: number;
  /** Called with the checked account when the operator continues. */
  readonly onContinue: (account: RootAccount) => void;
};

/**
 * The second step of the setup wizard: it collects and checks the root administrator's account. The password and its
 * confirmation stay in the component's state; the next step creates the account.
 */
export function RootAccountStep({ minimumPasswordLength, onContinue }: RootAccountStepProps) {
  const [userName, setUserName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [error, setError] = useState<string | undefined>(undefined);

  function submit(): void {
    if (password.length < minimumPasswordLength) {
      setError(`The password must have at least ${String(minimumPasswordLength)} characters.`);
      return;
    }
    if (password !== confirmation) {
      setError('The password and its confirmation do not match.');
      return;
    }

    setError(undefined);
    const trimmedEmail = email.trim();
    onContinue({
      userName: userName.trim(),
      ...(trimmedEmail ? { email: trimmedEmail } : {}),
      password,
    });
  }
  function handleSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    submit();
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

      <Button type="submit" variant="contained" fullWidth>
        Continue
      </Button>
    </Stack>
  );
}
