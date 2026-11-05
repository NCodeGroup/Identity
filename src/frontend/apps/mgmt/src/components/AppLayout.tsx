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
  /** Leaves the active scope for the one that contains it; the back control is rendered only when provided. */
  readonly onExitScope?: () => void;
  /** The label of the back control: the name of the scope it returns to. */
  readonly exitLabel?: string;
  /** The signed-in subject, shown in the header when a session is active. */
  readonly subject?: string;
  /** Sign-out handler; the sign-out control is only rendered when provided. */
  readonly onSignOut?: () => void;
  readonly children: ReactNode;
};

/**
 * Stateless console chrome. The security boundary (platform, team or tenant) is rendered prominently in the
 * header via {@link ScopeBadge}; the back control is only shown when the active scope has an enclosing
 * scope to return to, and the subject + sign-out control only when a session is active. Holds no state.
 */
export function AppLayout({
  title,
  scopeKind,
  scopeLabel,
  onExitScope,
  exitLabel,
  subject,
  onSignOut,
  children,
}: AppLayoutProps) {
  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="static" color="default" elevation={1}>
        <Toolbar sx={{ gap: 2 }}>
          {onExitScope && (
            <Button
              size="small"
              startIcon={<ArrowBackIcon />}
              onClick={onExitScope}
              data-testid="exit-scope"
            >
              {exitLabel}
            </Button>
          )}
          <Typography variant="h6" component="h1" sx={{ flexGrow: 1 }}>
            {title}
          </Typography>
          <ScopeBadge kind={scopeKind} {...(scopeLabel ? { label: scopeLabel } : {})} />
          {subject && (
            <Typography
              variant="body2"
              data-testid="session-subject"
              sx={{ color: 'text.secondary' }}
            >
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
