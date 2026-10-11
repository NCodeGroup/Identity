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

export type RevealSecretDialogProps = {
  readonly open: boolean;
  readonly secretId: string;
  /** The one-time key material, or `null` for asymmetric secrets that have no externally-held material. */
  readonly secretMaterial: string | null;
  readonly onClose: () => void;
};

/**
 * Stateless one-time secret reveal dialog. The server returns the generated key material exactly once
 * at creation (ADR-0050); this surfaces it with a copy affordance and a prominent warning that it can
 * never be retrieved again. Asymmetric secrets have no externally-held material, so only the generated
 * id is shown. Holds only transient "copied" feedback; the container owns the open flag.
 */
export function RevealSecretDialog({
  open,
  secretId,
  secretMaterial,
  onClose,
}: RevealSecretDialogProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    if (!secretMaterial) {
      return;
    }
    await navigator.clipboard.writeText(secretMaterial);
    setCopied(true);
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Secret created</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <DialogContentText component="div">
            Secret <code data-testid="reveal-secret-id">{secretId}</code> was created.
          </DialogContentText>
          {secretMaterial ? (
            <>
              <Alert severity="warning">
                Copy this secret now. It is shown only once and can never be retrieved again.
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
                <Box component="span" sx={{ flex: 1 }} data-testid="reveal-secret-material">
                  {secretMaterial}
                </Box>
                <Tooltip title={copied ? 'Copied' : 'Copy'}>
                  <IconButton
                    size="small"
                    aria-label="Copy secret"
                    data-testid="reveal-secret-copy"
                    onClick={handleCopy}
                  >
                    <ContentCopyIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Box>
            </>
          ) : (
            <Alert severity="info" data-testid="reveal-secret-asymmetric">
              This is an asymmetric secret; it has no externally-held key material to reveal.
            </Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onClose} data-testid="reveal-secret-done">
          Done
        </Button>
      </DialogActions>
    </Dialog>
  );
}
