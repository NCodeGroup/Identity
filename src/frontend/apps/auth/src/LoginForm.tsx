import { useCallback } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { ANTIFORGERY_FIELD, LOGIN_ACTION, resolveSurfacePath } from './params.js';
import { writeCarriedValue } from './serverPage.js';

export type LoginFormProps = {
  /** The validated local return URL the server redirects to after a successful sign-in. */
  readonly returnUrl: string;
  /** The antiforgery request token echoed from the host-set cookie. */
  readonly antiforgeryToken: string;
  /** A human-readable error to show above the form, or `null` when none. */
  readonly errorMessage?: string | null;
  /** The product/tenant display name shown in the heading. */
  readonly productName?: string;
  /**
   * The field values typed or autofilled into the server-rendered form before the bundle mounted, by field id.
   */
  readonly carriedValues?: Readonly<Record<string, string>>;
  /** The id of the field that had focus before the bundle mounted, which keeps it. */
  readonly focusedFieldId?: string | null;
};

/**
 * The stateless login form. It submits as a native HTML form POST so the browser follows the cookie
 * session and the server-validated return-URL redirect; the stable element ids (`username`, `password`,
 * `login`) are the automation contract and are set explicitly.
 */
export function LoginForm({
  returnUrl,
  antiforgeryToken,
  errorMessage,
  productName,
  carriedValues,
  focusedFieldId,
}: LoginFormProps) {
  const carriedUsername = carriedValues?.['username'];
  const carriedPassword = carriedValues?.['password'];
  // A carried value is written to the DOM property, never to React's `defaultValue`, which would put a password in
  // the `value` attribute (ADR-0253).
  const carryUsername = useCallback(
    (input: HTMLInputElement | null) => writeCarriedValue(input, carriedUsername),
    [carriedUsername],
  );
  const carryPassword = useCallback(
    (input: HTMLInputElement | null) => writeCarriedValue(input, carriedPassword),
    [carriedPassword],
  );

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
      <Card sx={{ width: '100%', maxWidth: 400 }}>
        <CardContent>
          <Stack
            spacing={3}
            component="form"
            method="post"
            action={resolveSurfacePath(LOGIN_ACTION)}
          >
            <Typography variant="h5" component="h1">
              {productName ? `Sign in to ${productName}` : 'Sign in'}
            </Typography>

            {errorMessage ? <Alert severity="error">{errorMessage}</Alert> : null}

            <input type="hidden" name="ReturnUrl" value={returnUrl} />
            <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />

            <TextField
              id="username"
              name="username"
              label="Username"
              autoComplete="username"
              autoFocus={focusedFieldId !== 'password'}
              inputRef={carryUsername}
              required
              fullWidth
            />
            <TextField
              id="password"
              name="password"
              label="Password"
              type="password"
              autoComplete="current-password"
              autoFocus={focusedFieldId === 'password'}
              inputRef={carryPassword}
              required
              fullWidth
            />

            <Button
              id="login"
              name="login"
              value="login"
              type="submit"
              variant="contained"
              fullWidth
            >
              Sign in
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
