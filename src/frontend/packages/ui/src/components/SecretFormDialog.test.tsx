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
      curve: '',
      use: '',
      algorithm: '',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it('offers the discovered ECC curves and applies the curve size on select', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'ecc', keySizeBits: 256 }}
        eccCurves={[
          { curveName: 'P-256', curveSizeBits: 256 },
          { curveName: 'P-384', curveSizeBits: 384 },
        ]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-curve'), { target: { value: 'P-384' } });
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ secretType: 'ecc', curve: 'P-384', keySizeBits: 384 }),
    );
  });

  it('offers only the discovered algorithms for the selected type, deriving the use on select', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'rsa' }}
        algorithms={[
          {
            code: 'RS256',
            use: 'sig',
            secretTypes: ['rsa'],
            keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
          },
          {
            code: 'ES256',
            use: 'sig',
            secretTypes: ['ecc'],
            keyBitSizes: [{ minBits: 256, maxBits: 256, skipBits: 0 }],
          },
        ]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    // The use field is derived from discovery and cannot be hand-entered while algorithms are offered.
    expect(screen.getByTestId('secret-use')).toHaveAttribute('readonly');

    // The use is derived from the selected algorithm and the key size is clamped into its legal range.
    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'RS256' } });
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        secretType: 'rsa',
        algorithm: 'RS256',
        use: 'sig',
        keySizeBits: 2048,
      }),
    );
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
