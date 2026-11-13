import { useState } from 'react';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import type { CreatedRoot, SetupTokenSource } from './setupApi.js';

export type DonePageProps = {
  /** The root administrator and the addresses the instance reported when it created them. */
  readonly root: CreatedRoot;
  /** The origin of the setup token, which decides what remains to clean up. */
  readonly tokenSource: SetupTokenSource;
};

type Address = { readonly label: string; readonly url: string };

function addressesOf(root: CreatedRoot): readonly Address[] {
  return [
    ...(root.managementConsoleBaseUrl
      ? [{ label: 'Management console', url: root.managementConsoleBaseUrl }]
      : []),
    { label: 'Issuer', url: root.issuer },
    { label: 'Discovery document', url: root.discoveryUrl },
    { label: 'Management API', url: root.managementApiUrl },
    ...(root.apiReferenceUrl ? [{ label: 'API reference', url: root.apiReferenceUrl }] : []),
  ];
}

/** The URL of the guide to the first use of a Production instance. */
export const FIRST_INSTANCE_DOCS_URL =
  'https://github.com/NCodeGroup/identity/blob/main/docs/first-production-instance.md';

/**
 * The last page of the setup wizard: it confirms the root administrator and lists the configuration that remains.
 */
export function DonePage({ root, tokenSource }: DonePageProps) {
  const { userName, tenantId, managementConsoleClientId } = root;
  const [copiedLabel, setCopiedLabel] = useState<string | undefined>(undefined);

  async function copy({ label, url }: Address): Promise<void> {
    try {
      await navigator.clipboard.writeText(url);
      setCopiedLabel(label);
    } catch {
      setCopiedLabel(undefined);
    }
  }

  return (
    <Stack spacing={3}>
      <Typography variant="h5" component="h1">
        The instance is set up
      </Typography>

      <Typography>
        The root administrator <strong>{userName}</strong> exists in the tenant{' '}
        <strong>{tenantId}</strong>.
      </Typography>

      {managementConsoleClientId ? (
        <Typography>
          The management console’s client <strong>{managementConsoleClientId}</strong> is
          registered.
        </Typography>
      ) : null}

      <Typography variant="h6" component="h2">
        Addresses
      </Typography>
      <Stack component="ul" aria-label="Addresses" spacing={1} sx={{ m: 0, p: 0 }}>
        {addressesOf(root).map((address) => (
          <Stack
            key={address.label}
            component="li"
            direction="row"
            spacing={1}
            sx={{ alignItems: 'center', justifyContent: 'space-between', listStyle: 'none' }}
          >
            <Stack sx={{ minWidth: 0 }}>
              <Typography variant="caption" color="text.secondary">
                {address.label}
              </Typography>
              <Link
                href={address.url}
                target="_blank"
                rel="noopener noreferrer"
                sx={{ overflowWrap: 'anywhere' }}
              >
                {address.url}
              </Link>
            </Stack>
            <Button
              size="small"
              aria-label={`Copy ${address.label}`}
              onClick={() => {
                void copy(address);
              }}
            >
              {copiedLabel === address.label ? 'Copied' : 'Copy'}
            </Button>
          </Stack>
        ))}
      </Stack>

      <Typography variant="h6" component="h2">
        What remains
      </Typography>
      <Stack component="ol" aria-label="What remains" spacing={1} sx={{ m: 0, pl: 3 }}>
        <li>Sign in to the management console as the root administrator.</li>
        {managementConsoleClientId ? null : <li>Register the management console client.</li>}
        <li>Configure the tenants and their grants.</li>
        {tokenSource === 'configured' ? <li>Remove the setup token Secret.</li> : null}
      </Stack>
      {tokenSource === 'generated' ? (
        <Typography>The generated setup token is spent.</Typography>
      ) : null}

      <Link href={FIRST_INSTANCE_DOCS_URL} target="_blank" rel="noopener noreferrer">
        Read the guide to the first use of a Production instance
      </Link>
    </Stack>
  );
}
