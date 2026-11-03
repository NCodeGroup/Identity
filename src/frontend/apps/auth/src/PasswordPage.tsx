import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useMessages } from './i18n/LocaleContext.js';
import { ACCOUNT_PATH, ANTIFORGERY_FIELD, PASSWORD_PATH, resolveSurfacePath } from './params.js';

export type PasswordPageProps = {
  /** The antiforgery request token echoed from the host-set cookie. */
  readonly antiforgeryToken: string;
  /** The product/tenant display name shown in the heading. */
  readonly productName?: string;
  /** A human-readable error to show above the form, or `null` when none. */
  readonly errorMessage?: string | null;
  /** True when the server reports that the password was just changed. */
  readonly changed?: boolean;
};

/**
 * The self-service change-password page. It submits as a native HTML form POST to the same path, carrying the
 * antiforgery token; the server verifies the current password, then redirects back with a status or an error code.
 */
export function PasswordPage({
  antiforgeryToken,
  productName,
  errorMessage,
  changed,
}: PasswordPageProps) {
  const text = useMessages().password;
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
      <Card sx={{ width: '100%', maxWidth: 440 }}>
        <CardContent>
          <Stack
            spacing={3}
            component="form"
            method="post"
            action={resolveSurfacePath(PASSWORD_PATH)}
          >
            <Typography variant="h5" component="h1">
              {productName ? text.titleFor(productName) : text.title}
            </Typography>

            {changed ? (
              <Alert severity="success" data-testid="password-changed">
                {text.changed}
              </Alert>
            ) : null}

            {errorMessage ? <Alert severity="error">{errorMessage}</Alert> : null}

            <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />

            <TextField
              id="current_password"
              name="current_password"
              label={text.current}
              type="password"
              autoComplete="current-password"
              required
              fullWidth
            />
            <TextField
              id="new_password"
              name="new_password"
              label={text.next}
              type="password"
              autoComplete="new-password"
              helperText={text.nextHelp}
              required
              fullWidth
            />
            <TextField
              id="confirm_password"
              name="confirm_password"
              label={text.confirm}
              type="password"
              autoComplete="new-password"
              required
              fullWidth
            />

            <Button id="change-password" type="submit" variant="contained" fullWidth>
              {text.submit}
            </Button>

            <Button href={resolveSurfacePath(ACCOUNT_PATH)} variant="text">
              {text.back}
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
