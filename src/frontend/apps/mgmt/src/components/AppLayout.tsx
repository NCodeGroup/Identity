import type { ReactNode } from 'react';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { ScopeBadge, type ScopeKind } from '@ncode/ui';

export type AppLayoutProps = {
  readonly title: string;
  readonly scopeKind: ScopeKind;
  readonly scopeLabel?: string;
  readonly onExitTenant?: () => void;
  readonly children: ReactNode;
}

/**
 * Stateless console chrome. The security boundary (global vs tenant) is rendered prominently in the
 * header via {@link ScopeBadge}; the "exit tenant" control is only shown when a tenant scope is
 * active. Holds no state.
 */
export function AppLayout({
  title,
  scopeKind,
  scopeLabel,
  onExitTenant,
  children,
}: AppLayoutProps) {
  return (
    <Box minHeight="100vh" bgcolor="background.default">
      <AppBar position="static" color="default" elevation={1}>
        <Toolbar sx={{ gap: 2 }}>
          {onExitTenant && (
            <Button
              size="small"
              startIcon={<ArrowBackIcon />}
              onClick={onExitTenant}
              data-testid="exit-tenant"
            >
              Global
            </Button>
          )}
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1 }}>
            {title}
          </Typography>
          <ScopeBadge kind={scopeKind} {...(scopeLabel ? { label: scopeLabel } : {})} />
        </Toolbar>
      </AppBar>
      <Container maxWidth="lg" sx={{ py: 4 }}>
        {children}
      </Container>
    </Box>
  );
}
