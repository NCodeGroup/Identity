import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

export type AuthorizationErrorPageProps = {
  /** The authorization error code the host reported, for example `invalid_client`. */
  readonly code: string;
  /** The human-readable description the host reported, or `null` when it gave none. */
  readonly description: string | null;
};

/**
 * The non-redirectable authorization error the host rendered at the protocol endpoint, redrawn by the bundle with the
 * host's stable automation contract: the element ids `authorization-error` (carrying the code in `data-error-code`)
 * and `authorization-error-description`.
 */
export function AuthorizationErrorPage({ code, description }: AuthorizationErrorPageProps) {
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
          <Stack spacing={3}>
            <Typography variant="h5" component="h1">
              Authorization error
            </Typography>

            <Alert severity="error" id="authorization-error" data-error-code={code}>
              {code}
            </Alert>

            {description ? (
              <Typography id="authorization-error-description" variant="body2">
                {description}
              </Typography>
            ) : null}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
