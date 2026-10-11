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
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: '0' } });
    expect(screen.getByTestId('iat-form-submit')).toBeDisabled();
    fireEvent.click(screen.getByTestId('iat-form-submit'));
    expect(onSubmit).not.toHaveBeenCalled();
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
    expect(screen.getByTestId('iat-form-cancel')).toBeDisabled();
  });
});
