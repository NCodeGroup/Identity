import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import LoginIcon from '@mui/icons-material/Login';

export type SignInScreenProps = {
  readonly productName?: string;
  readonly error?: string | undefined;
  readonly isBusy?: boolean;
  readonly onSignIn: () => void;
};

/**
 * Stateless sign-in screen. Renders a single call-to-action that the container wires to the
 * authorization-code + PKCE redirect; holds no state and performs no navigation itself.
 */
export function SignInScreen({
  productName = 'Management',
  error,
  isBusy = false,
  onSignIn,
}: SignInScreenProps) {
  return (
    <Box
      sx={{
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        minHeight: '100vh',
        p: 2,
      }}
    >
      <Card sx={{ maxWidth: 420, width: '100%' }}>
        <CardContent sx={{ textAlign: 'center', py: 5 }}>
          <Typography variant="h5" component="h1" gutterBottom>
            {productName}
          </Typography>
          <Typography sx={{ color: 'text.secondary', mb: 3 }}>Sign in to continue.</Typography>
          {error && (
            <Alert severity="error" sx={{ mb: 3, textAlign: 'left' }} data-testid="signin-error">
              {error}
            </Alert>
          )}
          <Button
            variant="contained"
            size="large"
            startIcon={<LoginIcon />}
            disabled={isBusy}
            onClick={onSignIn}
            data-testid="signin-button"
          >
            {isBusy ? 'Signing in…' : 'Sign in'}
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
