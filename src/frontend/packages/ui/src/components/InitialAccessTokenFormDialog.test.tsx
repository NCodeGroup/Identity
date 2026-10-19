import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { InitialAccessTokenFormDialog } from './InitialAccessTokenFormDialog.js';

describe('InitialAccessTokenFormDialog', () => {
  it('submits an empty description and blank lifetime by default', () => {
    const onSubmit = vi.fn();
    render(<InitialAccessTokenFormDialog open onSubmit={onSubmit} onCancel={vi.fn()} />);
    fireEvent.click(screen.getByTestId('iat-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({ description: '', lifetimeSeconds: '' });
  });

  it('trims the description and forwards a positive lifetime', () => {
    const onSubmit = vi.fn();
    render(<InitialAccessTokenFormDialog open onSubmit={onSubmit} onCancel={vi.fn()} />);
    fireEvent.change(screen.getByTestId('iat-description'), { target: { value: '  Partner X  ' } });
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: '3600' } });
    fireEvent.click(screen.getByTestId('iat-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({ description: 'Partner X', lifetimeSeconds: '3600' });
  });

  it('blocks submit on a non-positive lifetime', () => {
    const onSubmit = vi.fn();
    render(<InitialAccessTokenFormDialog open onSubmit={onSubmit} onCancel={vi.fn()} />);
    expect(screen.getByText('Leave blank for a token that never expires.')).toBeInTheDocument();
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: '0' } });
    expect(screen.getByTestId('iat-form-submit')).toBeDisabled();
    expect(
      screen.getByText('Lifetime must be a positive whole number of seconds.'),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('iat-form-submit'));
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it.each(['-5', '1.5', '1e3'])('rejects the non-integer lifetime %s', (lifetime) => {
    render(<InitialAccessTokenFormDialog open onSubmit={vi.fn()} onCancel={vi.fn()} />);
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: lifetime } });
    expect(screen.getByTestId('iat-lifetime')).toHaveValue(Number(lifetime));
    expect(screen.getByTestId('iat-form-submit')).toBeDisabled();
    expect(
      screen.getByText('Lifetime must be a positive whole number of seconds.'),
    ).toBeInTheDocument();
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    const onSubmit = vi.fn();
    render(<InitialAccessTokenFormDialog open onSubmit={onSubmit} onCancel={onCancel} />);
    fireEvent.click(screen.getByTestId('iat-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('keeps in-progress edits while open and resets the fields on reopen', () => {
    const props = { onSubmit: vi.fn(), onCancel: vi.fn() };
    const { rerender } = render(<InitialAccessTokenFormDialog {...props} open />);
    fireEvent.change(screen.getByTestId('iat-description'), { target: { value: 'Partner X' } });
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: '3600' } });

    rerender(<InitialAccessTokenFormDialog {...props} open />);
    expect(screen.getByTestId('iat-description')).toHaveValue('Partner X');

    rerender(<InitialAccessTokenFormDialog {...props} open={false} />);
    rerender(<InitialAccessTokenFormDialog {...props} open />);
    expect(screen.getByTestId('iat-description')).toHaveValue('');
    expect(screen.getByTestId('iat-lifetime')).toHaveValue(null);
  });

  it('surfaces a server error and disables actions while submitting', () => {
    render(
      <InitialAccessTokenFormDialog
        open
        isSubmitting
        error="Boom"
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('iat-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('iat-form-submit')).toBeDisabled();
    expect(screen.getByTestId('iat-form-submit')).toHaveTextContent('Minting…');
    expect(screen.getByTestId('iat-form-cancel')).toBeDisabled();
  });
});
