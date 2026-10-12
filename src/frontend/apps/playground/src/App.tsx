import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

/**
 * The Playground developer surface shell. It is the scaffold for an interactive OAuth/OIDC flow explorer built on the
 * shared `@ncode/*` platform (see ADR-0064). Flow-builder panels are layered onto this shell additively.
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
              NCode.Identity Playground
            </Typography>
            <Typography color="text.secondary">
              A developer surface for exploring OAuth 2.0 and OpenID Connect flows against the
              reference provider, built on the shared presentation platform.
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
