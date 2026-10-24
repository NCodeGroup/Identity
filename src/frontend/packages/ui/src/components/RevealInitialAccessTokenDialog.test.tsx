import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { RevealInitialAccessTokenDialog } from './RevealInitialAccessTokenDialog.js';

describe('RevealInitialAccessTokenDialog', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the one-time token with its grant id, expiry, and a reveal-once warning', () => {
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
    expect(screen.queryByTestId('reveal-iat-no-expiry')).not.toBeInTheDocument();
    expect(screen.queryByTestId('reveal-iat-resource-server')).not.toBeInTheDocument();
    expect(screen.getByTestId('reveal-iat-token')).toHaveTextContent('iat-123');
    expect(
      screen.getByText(/shown only once and can never be retrieved again/),
    ).toBeInTheDocument();
  });

  it('copies the token and confirms it in the copy tooltip', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    render(
      <RevealInitialAccessTokenDialog
        open
        token="iat-123"
        grantId="g-1"
        expiresWhen={null}
        onClose={vi.fn()}
      />,
    );
    const copy = screen.getByTestId('reveal-iat-copy');

    fireEvent.mouseOver(copy);
    expect(await screen.findByRole('tooltip', { name: 'Copy' })).toBeInTheDocument();

    fireEvent.click(copy);

    expect(await screen.findByRole('tooltip', { name: 'Copied' })).toBeInTheDocument();
    expect(writeText).toHaveBeenCalledExactlyOnceWith('iat-123');
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
    expect(screen.getByTestId('reveal-iat-no-expiry')).toHaveTextContent(
      'This token does not expire.',
    );
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
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('names the resource server a scoped token binds its client to', () => {
    render(
      <RevealInitialAccessTokenDialog
        open
        token="iat-123"
        grantId="g-3"
        expiresWhen={null}
        resourceServer="Orders"
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByTestId('reveal-iat-resource-server')).toHaveTextContent('Orders');
  });
});
