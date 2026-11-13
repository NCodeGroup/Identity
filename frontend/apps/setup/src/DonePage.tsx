import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';

export type DonePageProps = {
  /** The root administrator's user name. */
  readonly userName: string;
};

/** The URL of the guide to the first use of a Production instance. */
export const FIRST_INSTANCE_DOCS_URL =
  'https://github.com/NCodeGroup/identity/blob/main/docs/first-production-instance.md';

/**
 * The last page of the setup wizard: it confirms the root administrator and lists the configuration that remains.
 */
export function DonePage({ userName }: DonePageProps) {
  return (
    <Stack spacing={3}>
      <Typography variant="h5" component="h1">
        The instance is set up
      </Typography>

      <Typography>
        The root administrator <strong>{userName}</strong> exists. The setup token no longer works.
      </Typography>

      <Typography variant="h6" component="h2">
        What remains
      </Typography>
      <Stack component="ol" spacing={1} sx={{ m: 0, pl: 3 }}>
        <li>Sign in to the management console as the root administrator.</li>
        <li>Register the management console client.</li>
        <li>Configure the tenants and their grants.</li>
        <li>Remove the setup token Secret.</li>
      </Stack>

      <Link href={FIRST_INSTANCE_DOCS_URL} target="_blank" rel="noopener noreferrer">
        Read the guide to the first use of a Production instance
      </Link>
    </Stack>
  );
}
