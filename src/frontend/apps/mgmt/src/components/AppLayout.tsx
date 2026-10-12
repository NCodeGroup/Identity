import type { ReactNode } from 'react';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import LogoutIcon from '@mui/icons-material/Logout';
import { ScopeBadge, type ScopeKind } from '@ncode/ui';

export type AppLayoutProps = {
  readonly title: string;
  readonly scopeKind: ScopeKind;
  readonly scopeLabel?: string;
  readonly onExitTenant?: () => void;
  /** The signed-in subject, shown in the header when a session is active. */
  readonly subject?: string;
  /** Sign-out handler; the sign-out control is only rendered when provided. */
  readonly onSignOut?: () => void;
  readonly children: ReactNode;
};

/**
 * Stateless console chrome. The security boundary (global vs tenant) is rendered prominently in the
 * header via {@link ScopeBadge}; the "exit tenant" control is only shown when a tenant scope is
 * active, and the subject + sign-out control only when a session is active. Holds no state.
 */
export function AppLayout({
  title,
  scopeKind,
  scopeLabel,
  onExitTenant,
  subject,
  onSignOut,
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
          {subject && (
            <Typography variant="body2" color="text.secondary" data-testid="session-subject">
              {subject}
            </Typography>
          )}
          {onSignOut && (
            <Button
              size="small"
              startIcon={<LogoutIcon />}
              onClick={onSignOut}
              data-testid="sign-out"
            >
              Sign out
            </Button>
          )}
        </Toolbar>
      </AppBar>
      <Container maxWidth="lg" sx={{ py: 4 }}>
        {children}
      </Container>
    </Box>
  );
}
