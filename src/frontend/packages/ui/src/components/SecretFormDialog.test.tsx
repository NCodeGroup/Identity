import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SecretFormDialog, emptySecretFormValues } from './SecretFormDialog.js';

const initial = { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' };

describe('SecretFormDialog', () => {
  it('submits the seeded values, trimming optional fields', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog open initialValues={initial} onSubmit={onSubmit} onCancel={vi.fn()} />,
    );
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      secretType: 'symmetric',
      keySizeBits: 256,
      use: '',
      algorithm: '',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it('blocks submit when the expiry is empty', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={emptySecretFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();
  });

  it('surfaces a server error and disables actions while submitting', () => {
    render(
      <SecretFormDialog
        open
        initialValues={initial}
        isSubmitting
        error="Boom"
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('secret-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();
    expect(screen.getByTestId('secret-form-cancel')).toBeDisabled();
  });
});
