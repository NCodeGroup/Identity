import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { ANTIFORGERY_FIELD, LOGIN_ACTION } from './params.js';

export type LoginFormProps = {
  /** The validated local return URL the server redirects to after a successful sign-in. */
  readonly returnUrl: string;
  /** The antiforgery request token echoed from the host-set cookie. */
  readonly antiforgeryToken: string;
  /** A human-readable error to show above the form, or `null` when none. */
  readonly errorMessage?: string | null;
  /** The product/tenant display name shown in the heading. */
  readonly productName?: string;
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
}: LoginFormProps) {
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
          <Stack spacing={3} component="form" method="post" action={LOGIN_ACTION}>
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
              autoFocus
              required
              fullWidth
            />
            <TextField
              id="password"
              name="password"
              label="Password"
              type="password"
              autoComplete="current-password"
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
