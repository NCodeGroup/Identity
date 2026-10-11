import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { RevealSecretDialog } from './RevealSecretDialog.js';

describe('RevealSecretDialog', () => {
  it('shows the one-time material with a reveal-once warning and copies it', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    render(
      <RevealSecretDialog open secretId="s-1" secretMaterial="material-123" onClose={vi.fn()} />,
    );

    expect(screen.getByTestId('reveal-secret-id')).toHaveTextContent('s-1');
    expect(screen.getByTestId('reveal-secret-material')).toHaveTextContent('material-123');

    fireEvent.click(screen.getByTestId('reveal-secret-copy'));
    expect(writeText).toHaveBeenCalledWith('material-123');

    vi.unstubAllGlobals();
  });

  it('explains there is no material for an asymmetric secret', () => {
    render(<RevealSecretDialog open secretId="s-2" secretMaterial={null} onClose={vi.fn()} />);
    expect(screen.getByTestId('reveal-secret-asymmetric')).toBeInTheDocument();
    expect(screen.queryByTestId('reveal-secret-material')).not.toBeInTheDocument();
  });

  it('invokes onClose when done', () => {
    const onClose = vi.fn();
    render(<RevealSecretDialog open secretId="s-1" secretMaterial="x" onClose={onClose} />);
    fireEvent.click(screen.getByTestId('reveal-secret-done'));
    expect(onClose).toHaveBeenCalled();
  });
});
