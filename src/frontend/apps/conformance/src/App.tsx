import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { useAppSelector } from '@ncode/store';
import { ConformanceDashboard } from './containers/ConformanceDashboard.js';

/**
 * The conformance surface shell. Reads the discovered runtime config (guaranteed present by the bootstrap gate) and
 * hosts the conformance & security-posture dashboard under a branded header.
 */
export function App() {
  const runtime = useAppSelector((state) => state.config.runtime);

  if (!runtime) {
    return null;
  }

  const productName = runtime.branding?.displayName ?? 'NCode.Identity';

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="static" color="default" elevation={1}>
        <Toolbar>
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1 }}>
            {productName} · Conformance
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {runtime.issuer}
          </Typography>
        </Toolbar>
      </AppBar>
      <Container maxWidth="md" sx={{ py: 3 }}>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          A security-posture and OpenID Foundation conformance readiness dashboard. It audits the
          provider&rsquo;s published discovery metadata against modern OAuth 2.1 / OIDC guidance and
          reports Basic/Config OP readiness.
        </Typography>
        <ConformanceDashboard runtime={runtime} />
      </Container>
    </Box>
  );
}
