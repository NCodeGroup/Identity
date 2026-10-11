import { useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import DownloadIcon from '@mui/icons-material/Download';

/** The public key material to export, in both JWK and PEM representations (mirror of `PublicKeyResource`). */
export type PublicKeyMaterial = {
  readonly secretId: string;
  readonly secretType: string;
  readonly jwk: Readonly<Record<string, unknown>>;
  readonly pem: string;
};

export type ExportPublicKeyDialogProps = {
  readonly open: boolean;
  /** The id of the secret whose public key is being exported (shown while loading). */
  readonly secretId: string;
  /** The fetched public key, or `undefined` while loading or on error. */
  readonly publicKey: PublicKeyMaterial | undefined;
  readonly isLoading: boolean;
  /** A display error when the export failed, or `undefined`. */
  readonly error: string | undefined;
  readonly onClose: () => void;
};

/** Triggers a browser download of the given text content as a file. */
function downloadText(filename: string, content: string, mime: string): void {
  const blob = new Blob([content], { type: mime });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

/**
 * Stateless public-key export dialog for an asymmetric secret. The public key is read on demand from the
 * server's `/public-key` sub-resource (never private material) and shown in two representations: a JSON Web
 * Key (JWK, the same shape the JWKS endpoint publishes) and a `SubjectPublicKeyInfo` PEM. Each offers copy and
 * download affordances. Holds only transient "copied" feedback; the container owns the open flag and fetch.
 */
export function ExportPublicKeyDialog({
  open,
  secretId,
  publicKey,
  isLoading,
  error,
  onClose,
}: ExportPublicKeyDialogProps) {
  const [copied, setCopied] = useState<'jwk' | 'pem' | null>(null);

  const jwkText = publicKey ? JSON.stringify(publicKey.jwk, null, 2) : '';
  const pemText = publicKey?.pem ?? '';

  const handleCopy = async (kind: 'jwk' | 'pem', text: string) => {
    await navigator.clipboard.writeText(text);
    setCopied(kind);
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Export public key</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary" component="div">
            Public key for secret <code data-testid="export-public-key-id">{secretId}</code>. Only
            public key material is exported; the private key never leaves the server.
          </Typography>

          {isLoading && (
            <Box display="flex" justifyContent="center" py={2}>
              <CircularProgress size={24} data-testid="export-public-key-loading" />
            </Box>
          )}

          {error && (
            <Alert severity="error" data-testid="export-public-key-error">
              {error}
            </Alert>
          )}

          {publicKey && (
            <>
              <KeySection
                label="JSON Web Key (JWK)"
                testPrefix="export-public-key-jwk"
                value={jwkText}
                copied={copied === 'jwk'}
                onCopy={() => handleCopy('jwk', jwkText)}
                onDownload={() =>
                  downloadText(`${publicKey.secretId}.jwk.json`, jwkText, 'application/json')
                }
              />
              <KeySection
                label="PEM (SubjectPublicKeyInfo)"
                testPrefix="export-public-key-pem"
                value={pemText}
                copied={copied === 'pem'}
                onCopy={() => handleCopy('pem', pemText)}
                onDownload={() =>
                  downloadText(`${publicKey.secretId}.pem`, pemText, 'application/x-pem-file')
                }
              />
            </>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onClose} data-testid="export-public-key-done">
          Done
        </Button>
      </DialogActions>
    </Dialog>
  );
}

type KeySectionProps = {
  readonly label: string;
  readonly testPrefix: string;
  readonly value: string;
  readonly copied: boolean;
  readonly onCopy: () => void;
  readonly onDownload: () => void;
};

/** A labelled, monospace block of key material with copy and download affordances. */
function KeySection({ label, testPrefix, value, copied, onCopy, onDownload }: KeySectionProps) {
  return (
    <Box>
      <Box display="flex" alignItems="center" justifyContent="space-between" mb={0.5}>
        <Typography variant="subtitle2">{label}</Typography>
        <Stack direction="row" spacing={0.5}>
          <Tooltip title={copied ? 'Copied' : 'Copy'}>
            <IconButton
              size="small"
              aria-label={`Copy ${label}`}
              data-testid={`${testPrefix}-copy`}
              onClick={onCopy}
            >
              <ContentCopyIcon fontSize="small" />
            </IconButton>
          </Tooltip>
          <Tooltip title="Download">
            <IconButton
              size="small"
              aria-label={`Download ${label}`}
              data-testid={`${testPrefix}-download`}
              onClick={onDownload}
            >
              <DownloadIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Stack>
      </Box>
      <Box
        component="pre"
        data-testid={`${testPrefix}-value`}
        sx={{
          m: 0,
          p: 1,
          borderRadius: 1,
          bgcolor: 'action.hover',
          fontFamily: 'monospace',
          fontSize: '0.8125rem',
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-all',
          maxHeight: 200,
          overflow: 'auto',
        }}
      >
        {value}
      </Box>
    </Box>
  );
}
