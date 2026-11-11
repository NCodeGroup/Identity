import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { GrantTable, type GrantRow } from './GrantTable.js';

// A fixed reference instant, so the expired/active split does not depend on the real clock.
const now = Date.parse('2026-06-01T00:00:00Z');

const base = {
  grantType: 'authorization_code',
  createdWhen: '2026-01-01T12:00:00Z',
  expiresWhen: '2099-01-01T12:00:00Z',
  revokedWhen: null,
  consumedWhen: null,
};

const grants: GrantRow[] = [
  { ...base, grantId: 'g-active', clientId: 'web-spa', subjectId: 'user-1' },
  {
    ...base,
    grantId: 'g-revoked',
    clientId: 'web-spa',
    subjectId: 'user-2',
    revokedWhen: '2026-02-01T12:00:00Z',
  },
  {
    ...base,
    grantId: 'g-expired',
    clientId: 'worker',
    subjectId: null,
    expiresWhen: '2020-01-01T12:00:00Z',
  },
];

describe('GrantTable', () => {
  it('renders a row per grant with its derived status', () => {
    render(<GrantTable grants={grants} now={now} />);
    expect(screen.getByTestId('grant-row-g-active')).toHaveTextContent('Active');
    expect(screen.getByTestId('grant-row-g-revoked')).toHaveTextContent('Revoked');
    expect(screen.getByTestId('grant-row-g-expired')).toHaveTextContent('Expired');
  });

  it('renders an em dash for a null subject', () => {
    render(<GrantTable grants={grants} now={now} />);
    expect(screen.getByTestId('grant-row-g-expired')).toHaveTextContent('—');
  });

  it('exposes a revoke control only for active grants', () => {
    const onRevoke = vi.fn();
    render(<GrantTable grants={grants} now={now} onRevoke={onRevoke} />);
    expect(screen.getByRole('columnheader', { name: 'Actions' })).toBeInTheDocument();
    expect(screen.getByTestId('grant-revoke-g-active')).toBeInTheDocument();
    expect(screen.queryByTestId('grant-revoke-g-revoked')).not.toBeInTheDocument();
    expect(screen.queryByTestId('grant-revoke-g-expired')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('grant-revoke-g-active'));
    expect(onRevoke).toHaveBeenCalledExactlyOnceWith('g-active');
  });

  it('renders no actions column without an onRevoke callback', () => {
    render(<GrantTable grants={grants} now={now} />);
    expect(screen.queryByRole('columnheader', { name: 'Actions' })).not.toBeInTheDocument();
    expect(screen.queryByTestId('grant-revoke-g-active')).not.toBeInTheDocument();
  });

  it('renders em dashes for a grant with no client or subject', () => {
    render(
      <GrantTable
        grants={[{ ...base, grantId: 'g-anon', clientId: null, subjectId: null }]}
        now={now}
      />,
    );
    const cells = within(screen.getByTestId('grant-row-g-anon')).getAllByRole('cell');
    expect(cells.map((cell) => cell.textContent)).toEqual([
      'authorization_code',
      '—',
      '—',
      new Date(Date.parse(base.createdWhen)).toLocaleString(),
      'Active',
    ]);
  });

  it('treats a grant without an expiry as active', () => {
    render(
      <GrantTable
        grants={[
          { ...base, grantId: 'g-forever', clientId: 'web-spa', subjectId: 'u', expiresWhen: null },
        ]}
        now={now}
      />,
    );
    expect(screen.getByTestId('grant-row-g-forever')).toHaveTextContent('Active');
  });

  it('expires a grant exactly at its expiry instant', () => {
    render(
      <GrantTable
        grants={[
          {
            ...base,
            grantId: 'g-boundary',
            clientId: 'web-spa',
            subjectId: 'u',
            expiresWhen: '2026-06-01T00:00:00Z',
          },
        ]}
        now={now}
      />,
    );
    expect(screen.getByTestId('grant-row-g-boundary')).toHaveTextContent('Expired');
  });

  it('shows an unparseable created instant verbatim', () => {
    render(
      <GrantTable
        grants={[
          {
            ...base,
            grantId: 'g-odd',
            clientId: 'web-spa',
            subjectId: 'u',
            createdWhen: 'not-a-date',
          },
        ]}
        now={now}
      />,
    );
    expect(screen.getByTestId('grant-row-g-odd')).toHaveTextContent('not-a-date');
  });
});
