import { useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import Tooltip from '@mui/material/Tooltip';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';

export type RevealInitialAccessTokenDialogProps = {
  readonly open: boolean;
  /** The one-time plaintext token value revealed exactly once at minting. */
  readonly token: string;
  /** The opaque grant id backing the token, used to revoke it later. */
  readonly grantId: string;
  /** When the token expires, or `null` when it never expires. */
  readonly expiresWhen: string | null;
  readonly onClose: () => void;
};

/**
 * Stateless one-time initial-access-token reveal dialog (RFC 7591). The server returns the plaintext token
 * exactly once at minting (only its salted hash is persisted); this surfaces it with a copy affordance and a
 * prominent warning that it can never be retrieved again. Holds only transient "copied" feedback; the
 * container owns the open flag.
 */
export function RevealInitialAccessTokenDialog({
  open,
  token,
  grantId,
  expiresWhen,
  onClose,
}: RevealInitialAccessTokenDialogProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(token);
    setCopied(true);
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Initial access token minted</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <DialogContentText component="div">
            Grant <code data-testid="reveal-iat-grant-id">{grantId}</code> was created.{' '}
            {expiresWhen ? (
              <>
                It expires at <code data-testid="reveal-iat-expires">{expiresWhen}</code>.
              </>
            ) : (
              <span data-testid="reveal-iat-no-expiry">This token does not expire.</span>
            )}
          </DialogContentText>
          <Alert severity="warning">
            Copy this token now. It is shown only once and can never be retrieved again.
          </Alert>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              p: 1,
              borderRadius: 1,
              bgcolor: 'action.hover',
              fontFamily: 'monospace',
              wordBreak: 'break-all',
            }}
          >
            <Box component="span" sx={{ flex: 1 }} data-testid="reveal-iat-token">
              {token}
            </Box>
            <Tooltip title={copied ? 'Copied' : 'Copy'}>
              <IconButton
                size="small"
                aria-label="Copy token"
                data-testid="reveal-iat-copy"
                onClick={handleCopy}
              >
                <ContentCopyIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Box>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onClose} data-testid="reveal-iat-done">
          Done
        </Button>
      </DialogActions>
    </Dialog>
  );
}
