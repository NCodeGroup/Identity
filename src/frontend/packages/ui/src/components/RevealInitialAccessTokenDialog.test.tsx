import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { RevealInitialAccessTokenDialog } from './RevealInitialAccessTokenDialog.js';

describe('RevealInitialAccessTokenDialog', () => {
  it('shows the one-time token with its grant id and expiry, and copies it', () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    render(
      <RevealInitialAccessTokenDialog
        open
        token="iat-123"
        grantId="g-1"
        expiresWhen="2027-01-01T00:00:00Z"
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByTestId('reveal-iat-grant-id')).toHaveTextContent('g-1');
    expect(screen.getByTestId('reveal-iat-expires')).toHaveTextContent('2027-01-01T00:00:00Z');
    expect(screen.getByTestId('reveal-iat-token')).toHaveTextContent('iat-123');

    fireEvent.click(screen.getByTestId('reveal-iat-copy'));
    expect(writeText).toHaveBeenCalledWith('iat-123');

    vi.unstubAllGlobals();
  });

  it('notes a non-expiring token', () => {
    render(
      <RevealInitialAccessTokenDialog
        open
        token="iat-123"
        grantId="g-2"
        expiresWhen={null}
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByTestId('reveal-iat-no-expiry')).toBeInTheDocument();
    expect(screen.queryByTestId('reveal-iat-expires')).not.toBeInTheDocument();
  });

  it('invokes onClose when done', () => {
    const onClose = vi.fn();
    render(
      <RevealInitialAccessTokenDialog
        open
        token="x"
        grantId="g-3"
        expiresWhen={null}
        onClose={onClose}
      />,
    );
    fireEvent.click(screen.getByTestId('reveal-iat-done'));
    expect(onClose).toHaveBeenCalled();
  });
});
