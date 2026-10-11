import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { ANTIFORGERY_FIELD, CONSENT_ACTION } from './params.js';

export type ConsentFormProps = {
  /** The validated local return URL the server redirects to after the consent decision. */
  readonly returnUrl: string;
  /** The antiforgery request token echoed from the host-set cookie. */
  readonly antiforgeryToken: string;
  /** The product/tenant display name shown in the heading. */
  readonly productName?: string;
};

/**
 * The stateless consent form. Allow and deny submit as native HTML form POSTs carrying the decision; the
 * stable element ids (`consent-allow`, `consent-deny`) are the automation contract and are set explicitly.
 */
export function ConsentForm({ returnUrl, antiforgeryToken, productName }: ConsentFormProps) {
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
          <Stack spacing={3} component="form" method="post" action={CONSENT_ACTION}>
            <Typography variant="h5" component="h1">
              Authorize access
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {productName
                ? `${productName} is requesting access to your account.`
                : 'An application is requesting access to your account.'}
            </Typography>

            <input type="hidden" name="ReturnUrl" value={returnUrl} />
            <input type="hidden" name={ANTIFORGERY_FIELD} value={antiforgeryToken} />

            <Stack direction="row" spacing={2}>
              <Button
                id="consent-deny"
                name="action"
                value="deny"
                type="submit"
                variant="outlined"
                fullWidth
              >
                Deny
              </Button>
              <Button
                id="consent-allow"
                name="action"
                value="allow"
                type="submit"
                variant="contained"
                fullWidth
              >
                Allow
              </Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
