import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { useLocale } from './i18n/LocaleContext.js';
import {
  ANTIFORGERY_FIELD,
  LOGOUT_CONFIRM_PATH,
  UI_LOCALES_FIELD,
  resolveSurfacePath,
} from './params.js';

export type LogoutConfirmPageProps = {
  /** The one-time continue URL that resumes the parked logout once the end-user confirms it. */
  readonly returnUrl: string;
  /** The antiforgery request token echoed from the host-set cookie. */
  readonly antiforgeryToken: string;
  /** The product/tenant display name shown in the question. */
  readonly productName?: string;
};

/**
 * The logout confirmation page, which the end-session endpoint redirects to when a logout carries no valid
 * `id_token_hint` for the current session (RP-Initiated Logout 1.0 §2). The decision submits as a native HTML form
 * POST. The stable element ids (`logout-confirm`, `logout-cancel`) are the automation contract and are set
 * explicitly; they match the server-rendered form the bundle replaces on mount.
 */
export function LogoutConfirmPage({
  returnUrl,
  antiforgeryToken,
  productName,
}: LogoutConfirmPageProps) {
  const { pageLanguage, messages } = useLocale();
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
            action={resolveSurfacePath(LOGOUT_CONFIRM_PATH)}
          >
            <Typography variant="h5" component="h1">
              {messages.logoutConfirm.title}
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {productName
                ? messages.logoutConfirm.questionFor(productName)
                : messages.logoutConfirm.question}
            </Typography>

            <input type="hidden" name="ReturnUrl" value={returnUrl} />
            <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />
            {pageLanguage ? (
              <input type="hidden" name={UI_LOCALES_FIELD} value={pageLanguage} />
            ) : null}

            <Stack direction="row" spacing={2}>
              <Button
                id="logout-cancel"
                name="action"
                value="cancel"
                type="submit"
                variant="outlined"
                fullWidth
              >
                {messages.logoutConfirm.stay}
              </Button>
              <Button
                id="logout-confirm"
                name="action"
                value="logout"
                type="submit"
                variant="contained"
                fullWidth
              >
                {messages.logoutConfirm.signOut}
              </Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
