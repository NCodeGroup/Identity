import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { ANTIFORGERY_FIELD, CONSENT_ACTION, CONSENT_DETAILS_ENDPOINT } from './params.js';

/** The pending authorization request the consent page presents (mirrors the OP's consent-details payload). */
export type ConsentDetails = {
  readonly clientId: string;
  readonly scopes: readonly string[];
};

export type ConsentFormProps = {
  /** The validated local return URL the server redirects to after the consent decision. */
  readonly returnUrl: string;
  /** The antiforgery request token echoed from the host-set cookie. */
  readonly antiforgeryToken: string;
  /** The product/tenant display name shown in the heading. */
  readonly productName?: string;
  /** Loads the pending request; defaults to a same-origin fetch of the consent-details endpoint. */
  readonly loadDetails?: (returnUrl: string) => Promise<ConsentDetails | null>;
};

/**
 * Default details loader: fetches the OP's consent-details endpoint for the pending authorization request. It is a
 * no-op when `fetch` is unavailable (e.g. a DOM-free unit test that does not inject `loadDetails`).
 */
async function fetchDetails(returnUrl: string): Promise<ConsentDetails | null> {
  if (typeof fetch !== 'function') {
    return null;
  }
  const response = await fetch(
    `${CONSENT_DETAILS_ENDPOINT}?ReturnUrl=${encodeURIComponent(returnUrl)}`,
    { headers: { accept: 'application/json' } },
  );
  if (!response.ok) {
    return null;
  }
  return (await response.json()) as ConsentDetails;
}

/**
 * The consent form. It loads the pending authorization request (the client and the requested scopes) and renders it,
 * then submits the allow/deny decision as a native HTML form POST. The stable element ids (`consent-allow`,
 * `consent-deny`) are the automation contract and are set explicitly.
 */
export function ConsentForm({
  returnUrl,
  antiforgeryToken,
  productName,
  loadDetails = fetchDetails,
}: ConsentFormProps) {
  const [details, setDetails] = useState<ConsentDetails | null>(null);

  useEffect(() => {
    let active = true;
    loadDetails(returnUrl)
      .then((loaded) => {
        if (active) {
          setDetails(loaded);
        }
      })
      .catch(() => {
        /* a failed load leaves the requested-scope list hidden; the decision controls remain available */
      });
    return () => {
      active = false;
    };
  }, [loadDetails, returnUrl]);

  const appName = productName ?? details?.clientId ?? 'An application';

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
              {`${appName} is requesting access to your account.`}
            </Typography>

            {details && details.scopes.length > 0 ? (
              <Stack spacing={1} data-testid="consent-scopes">
                <Typography variant="subtitle2">This will allow it to:</Typography>
                <List dense disablePadding>
                  {details.scopes.map((scope) => (
                    <ListItem key={scope} disablePadding>
                      <ListItemText primary={scope} />
                    </ListItem>
                  ))}
                </List>
              </Stack>
            ) : null}

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
