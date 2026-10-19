import { useEffect, useState } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import FormGroup from '@mui/material/FormGroup';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { ANTIFORGERY_FIELD, CONSENT_ACTION, CONSENT_DETAILS_ENDPOINT } from './params.js';

/** A single requested scope, with a friendly description and whether the subject has already granted it. */
export type ConsentScope = {
  readonly value: string;
  readonly description?: string | null;
  readonly granted: boolean;
};

/** The pending authorization request the consent page presents (mirrors the OP's consent-details payload). */
export type ConsentDetails = {
  readonly clientId: string;
  readonly clientName?: string | null;
  readonly scopes: readonly ConsentScope[];
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
 * The consent form. It loads the pending authorization request (the client — with its friendly display name — and the
 * requested scopes with friendly descriptions and already-granted state) and renders a per-scope checkbox so the
 * end-user can grant a subset (the OP down-scopes the issued tokens to the selection). The allow/deny decision submits
 * as a native HTML form POST. The stable element ids (`consent-allow`, `consent-deny`) are the automation contract and
 * are set explicitly.
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

  const appName = productName ?? details?.clientName ?? details?.clientId ?? 'An application';

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
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {`${appName} is requesting access to your account. Choose what to allow:`}
            </Typography>

            {details && details.scopes.length > 0 ? (
              <FormGroup data-testid="consent-scopes">
                {details.scopes.map((scope) => (
                  <FormControlLabel
                    key={scope.value}
                    control={
                      // A consent screen pre-selects every requested scope so Allow authorizes the request; the
                      // subject unchecks a scope to down-scope. Defaulting to the prior-granted state would silently
                      // drop a freshly-requested scope (e.g. openid) when the subject clicks Allow.
                      <Checkbox name="scope" value={scope.value} defaultChecked />
                    }
                    label={
                      <Box>
                        <Typography variant="body2">{scope.description ?? scope.value}</Typography>
                        {scope.description ? (
                          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
                            {scope.value}
                          </Typography>
                        ) : null}
                      </Box>
                    }
                  />
                ))}
              </FormGroup>
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
