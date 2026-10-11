import type { ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';

export type ConfirmDialogProps = {
  readonly open: boolean;
  readonly title: string;
  readonly message: ReactNode;
  readonly confirmLabel?: string;
  readonly confirmColor?: 'primary' | 'error';
  readonly isBusy?: boolean;
  readonly error?: string | undefined;
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
};

/**
 * Stateless confirmation dialog reused by every destructive Management action (delete tenant/client/
 * secret, revoke grant). Holds no state: the container owns the open flag, busy state, and any server
 * error surfaced after a failed confirm.
 */
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Delete',
  confirmColor = 'error',
  isBusy = false,
  error,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Dialog open={open} onClose={onCancel} maxWidth="xs" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }} data-testid="confirm-error">
            {error}
          </Alert>
        )}
        <DialogContentText component="div">{message}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={isBusy} data-testid="confirm-cancel">
          Cancel
        </Button>
        <Button
          variant="contained"
          color={confirmColor}
          onClick={onConfirm}
          disabled={isBusy}
          data-testid="confirm-accept"
        >
          {isBusy ? 'Working…' : confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
