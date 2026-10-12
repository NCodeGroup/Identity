import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

/**
 * The OIDC conformance harness surface shell. It is the scaffold for the browser-drivable surface that the OpenID
 * Foundation conformance suite automates (see backlog `T1`/`T2`), built on the shared `@ncode/*` platform
 * (ADR-0064). Harness control panels are layered onto this shell additively.
 */
export function App() {
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
      <Card sx={{ width: '100%', maxWidth: 560 }}>
        <CardContent>
          <Stack spacing={2}>
            <Typography variant="h4" component="h1">
              NCode.Identity Conformance
            </Typography>
            <Typography color="text.secondary">
              A surface for driving the OpenID Foundation conformance suite against the reference
              provider, built on the shared presentation platform.
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
