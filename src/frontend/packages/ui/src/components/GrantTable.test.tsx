import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
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
    expect(screen.getByTestId('grant-revoke-g-active')).toBeInTheDocument();
    expect(screen.queryByTestId('grant-revoke-g-revoked')).not.toBeInTheDocument();
    expect(screen.queryByTestId('grant-revoke-g-expired')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('grant-revoke-g-active'));
    expect(onRevoke).toHaveBeenCalledWith('g-active');
  });
});
