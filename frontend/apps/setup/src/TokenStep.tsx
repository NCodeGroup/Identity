import { useState, type FormEvent } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import type { SetupTokenSource } from './setupApi.js';

export type TokenStepProps = {
  /** Where the instance's setup token comes from, which decides how the step tells the operator to find it. */
  readonly tokenSource: SetupTokenSource;
  /** The token the page's URL fragment carried, or an empty string when it carried none. */
  readonly initialToken: string;
  /** A human-readable error to show above the field, or `undefined` when none. */
  readonly errorMessage?: string | undefined;
  /** Called with the trimmed token when the operator continues. */
  readonly onContinue: (token: string) => void;
};

const TOKEN_HELP: Readonly<Record<SetupTokenSource, string>> = {
  configured:
    'The operator configured this instance’s setup token (the Setup__Token setting, usually read from a Kubernetes Secret). Enter that value.',
  generated:
    'The host generated this instance’s setup token and wrote it once, at Warning level, to the startup log of one replica. The token expires one hour after the host issues it, and restarting the host issues a new one.',
};

/**
 * The first step of the setup wizard: it collects the one-time setup token. The token stays in the component's state;
 * it is never written to the URL, a log or storage.
 */
export function TokenStep({ tokenSource, initialToken, errorMessage, onContinue }: TokenStepProps) {
  const [token, setToken] = useState(initialToken);

  function handleSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    onContinue(token.trim());
  }

  return (
    <Stack spacing={3} component="form" onSubmit={handleSubmit}>
      <Typography variant="h5" component="h1">
        Set up NCode.Identity
      </Typography>

      <Typography variant="body2">{TOKEN_HELP[tokenSource]}</Typography>

      {errorMessage ? <Alert severity="error">{errorMessage}</Alert> : null}

      <TextField
        id="setup-token"
        name="setup-token"
        label="Setup token"
        type="password"
        autoComplete="off"
        value={token}
        onChange={(event) => {
          setToken(event.target.value);
        }}
        required
        fullWidth
      />

      <Button type="submit" variant="contained" disabled={token.trim() === ''} fullWidth>
        Continue
      </Button>
    </Stack>
  );
}
