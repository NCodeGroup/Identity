import { useMemo } from 'react';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { useAppSelector } from '@ncode/store';
import { FlowExplorer } from './containers/FlowExplorer.js';
import { createFlowDeps } from './flow.js';

/**
 * The Playground shell. Reads the discovered runtime config (guaranteed present by the bootstrap gate), builds the
 * injected flow dependencies once, and hosts the authorization-code + PKCE flow explorer under a branded header.
 */
export function App() {
  const runtime = useAppSelector((state) => state.config.runtime);
  const deps = useMemo(createFlowDeps, []);

  if (!runtime) {
    return null;
  }

  const productName = runtime.branding?.displayName ?? 'NCode.Identity';

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="static" color="default" elevation={1}>
        <Toolbar>
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1 }}>
            {productName} · Playground
          </Typography>
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {runtime.issuer}
          </Typography>
        </Toolbar>
      </AppBar>
      <Container maxWidth="md" sx={{ py: 3 }}>
        <Typography sx={{ color: 'text.secondary', mb: 2 }}>
          An interactive OAuth 2.1 / OpenID Connect flow explorer. It discovers this provider and
          walks the authorization-code + PKCE flow end to end, surfacing every artifact along the
          way.
        </Typography>
        <FlowExplorer runtime={runtime} deps={deps} />
      </Container>
    </Box>
  );
}
