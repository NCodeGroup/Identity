import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { RevealSecretDialog } from './RevealSecretDialog.js';

describe('RevealSecretDialog', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the one-time material with a reveal-once warning', () => {
    render(
      <RevealSecretDialog open secretId="s-1" secretMaterial="material-123" onClose={vi.fn()} />,
    );

    expect(screen.getByTestId('reveal-secret-id')).toHaveTextContent('s-1');
    expect(screen.getByTestId('reveal-secret-material')).toHaveTextContent('material-123');
    expect(
      screen.getByText(/shown only once and can never be retrieved again/),
    ).toBeInTheDocument();
    expect(screen.queryByTestId('reveal-secret-asymmetric')).not.toBeInTheDocument();
  });

  it('copies the material and confirms it in the copy tooltip', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    render(
      <RevealSecretDialog open secretId="s-1" secretMaterial="material-123" onClose={vi.fn()} />,
    );
    const copy = screen.getByTestId('reveal-secret-copy');

    fireEvent.mouseOver(copy);
    expect(await screen.findByRole('tooltip', { name: 'Copy' })).toBeInTheDocument();

    fireEvent.click(copy);

    expect(await screen.findByRole('tooltip', { name: 'Copied' })).toBeInTheDocument();
    expect(writeText).toHaveBeenCalledExactlyOnceWith('material-123');
  });

  it('explains there is no material for an asymmetric secret', () => {
    render(<RevealSecretDialog open secretId="s-2" secretMaterial={null} onClose={vi.fn()} />);
    expect(screen.getByTestId('reveal-secret-id')).toHaveTextContent('s-2');
    expect(screen.getByTestId('reveal-secret-asymmetric')).toBeInTheDocument();
    expect(screen.queryByTestId('reveal-secret-material')).not.toBeInTheDocument();
    expect(screen.queryByTestId('reveal-secret-copy')).not.toBeInTheDocument();
  });

  it('invokes onClose when done', () => {
    const onClose = vi.fn();
    render(<RevealSecretDialog open secretId="s-1" secretMaterial="x" onClose={onClose} />);
    fireEvent.click(screen.getByTestId('reveal-secret-done'));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
