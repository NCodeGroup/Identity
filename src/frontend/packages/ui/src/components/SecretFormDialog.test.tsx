import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import {
  SecretFormDialog,
  emptySecretFormValues,
  type AlgorithmOption,
  type SecretFormValues,
} from './SecretFormDialog.js';

const initial: SecretFormValues = { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' };

const rs256: AlgorithmOption = {
  code: 'RS256',
  use: 'sig',
  secretTypes: ['rsa'],
  keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
};
const rsaOaep: AlgorithmOption = {
  code: 'RSA-OAEP',
  use: 'enc',
  secretTypes: ['rsa'],
  keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
};
const es256: AlgorithmOption = {
  code: 'ES256',
  use: 'sig',
  secretTypes: ['ecc'],
  keyBitSizes: [{ minBits: 256, maxBits: 256, skipBits: 0 }],
};
const algorithms = [rs256, rsaOaep, es256];

const eccCurves = [
  { curveName: 'P-256', curveSizeBits: 256 },
  { curveName: 'P-384', curveSizeBits: 384 },
];

/** Opens the discovered-algorithm picker and returns the labels of the options it offers. */
function offeredAlgorithms(): (string | null)[] {
  fireEvent.mouseDown(screen.getByRole('combobox', { name: /Algorithm/ }));
  return within(screen.getByRole('listbox'))
    .getAllByRole('option')
    .map((option) => option.textContent);
}

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

  it('submits hand-entered values when no algorithms are discovered', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog open initialValues={initial} onSubmit={onSubmit} onCancel={vi.fn()} />,
    );

    // Without discovery the use is free text and the key size is unconstrained beyond being positive.
    expect(screen.getByTestId('secret-use')).not.toHaveAttribute('readonly');
    expect(screen.getByTestId('secret-key-size')).toHaveAttribute('min', '1');
    expect(screen.getByTestId('secret-key-size')).not.toHaveAttribute('max');
    expect(screen.getByTestId('secret-key-size')).not.toHaveAttribute('step');

    fireEvent.change(screen.getByTestId('secret-type'), { target: { value: 'rsa' } });
    fireEvent.change(screen.getByTestId('secret-key-size'), { target: { value: '3072' } });
    fireEvent.change(screen.getByTestId('secret-use'), { target: { value: '  enc  ' } });
    fireEvent.change(screen.getByTestId('secret-algorithm'), {
      target: { value: '  RSA-OAEP  ' },
    });
    fireEvent.change(screen.getByTestId('secret-expires'), {
      target: { value: '2027-06-01T08:30' },
    });
    fireEvent.click(screen.getByTestId('secret-form-submit'));

    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      secretType: 'rsa',
      keySizeBits: 3072,
      curve: '',
      use: 'enc',
      algorithm: 'RSA-OAEP',
      expiresWhen: '2027-06-01T08:30',
    });
  });

  it('blocks submit when the key size is not positive', () => {
    render(<SecretFormDialog open initialValues={initial} onSubmit={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByTestId('secret-form-submit')).toBeEnabled();
    fireEvent.change(screen.getByTestId('secret-key-size'), { target: { value: '0' } });
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();
  });

  it('offers the discovered ECC curves and applies the curve size on select', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'ecc', keySizeBits: 256 }}
        eccCurves={eccCurves}
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

  it('keeps the key size when the curve is chosen by key size', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'ecc', keySizeBits: 521, curve: 'P-256' }}
        eccCurves={eccCurves}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-curve'), { target: { value: '' } });
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ secretType: 'ecc', curve: '', keySizeBits: 521 }),
    );
  });

  it.each([
    ['no curves are discovered', undefined],
    ['the discovered curve list is empty', []],
  ])('offers no curve picker for ECC when %s', (_, curves) => {
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'ecc' }}
        {...(curves ? { eccCurves: curves } : {})}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.queryByTestId('secret-curve')).not.toBeInTheDocument();
  });

  it('drops the curve and keeps hand-entered fields when the type changes away from ECC', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{
          ...initial,
          secretType: 'ecc',
          keySizeBits: 384,
          curve: 'P-384',
          use: 'sig',
          algorithm: 'ES384',
        }}
        eccCurves={eccCurves}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-type'), { target: { value: 'rsa' } });
    expect(screen.queryByTestId('secret-curve')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      secretType: 'rsa',
      keySizeBits: 384,
      curve: '',
      use: 'sig',
      algorithm: 'ES384',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it('offers only the discovered algorithms for the selected type, deriving the use on select', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'rsa' }}
        algorithms={algorithms}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    // The use field is derived from discovery and cannot be hand-entered while algorithms are offered.
    expect(screen.getByTestId('secret-use')).toHaveAttribute('readonly');
    expect(offeredAlgorithms()).toEqual(['Any', 'RS256', 'RSA-OAEP']);

    // The use is derived from the selected algorithm and the key size is clamped into its legal range.
    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'RSA-OAEP' } });
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        secretType: 'rsa',
        algorithm: 'RSA-OAEP',
        use: 'enc',
        keySizeBits: 2048,
      }),
    );
  });

  it('keeps an in-range key size and describes the allowed range of the selected algorithm', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'rsa', keySizeBits: 4096 }}
        algorithms={algorithms}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'RS256' } });

    expect(screen.getByText('Allowed: 2048–16384 bits for RS256.')).toBeInTheDocument();
    const keySize = screen.getByTestId('secret-key-size');
    expect(keySize).toHaveValue(4096);
    expect(keySize).toHaveAttribute('min', '2048');
    expect(keySize).toHaveAttribute('max', '16384');
    expect(keySize).toHaveAttribute('step', '8');
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ algorithm: 'RS256', use: 'sig', keySizeBits: 4096 }),
    );
  });

  it('pins the key size of a fixed-size algorithm', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'ecc', keySizeBits: 384 }}
        algorithms={algorithms}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    expect(offeredAlgorithms()).toEqual(['Any', 'ES256']);
    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'ES256' } });

    expect(screen.getByText('Fixed at 256 bits for ES256.')).toBeInTheDocument();
    const keySize = screen.getByTestId('secret-key-size');
    expect(keySize).toHaveValue(256);
    expect(keySize).toHaveAttribute('min', '256');
    expect(keySize).toHaveAttribute('max', '256');
    expect(keySize).not.toHaveAttribute('step');
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ algorithm: 'ES256', use: 'sig', keySizeBits: 256 }),
    );
  });

  it('clears the derived use and keeps the key size when no specific algorithm is chosen', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{
          ...initial,
          secretType: 'rsa',
          keySizeBits: 3072,
          algorithm: 'RS256',
          use: 'sig',
        }}
        algorithms={algorithms}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: '' } });

    expect(screen.queryByText(/bits for/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ algorithm: '', use: '', keySizeBits: 3072 }),
    );
  });

  it('resets the discovered algorithm and use when the type changes', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={{ ...initial, secretType: 'rsa', keySizeBits: 2048 }}
        algorithms={algorithms}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'RS256' } });
    expect(screen.getByTestId('secret-use')).toHaveValue('sig');

    fireEvent.change(screen.getByTestId('secret-type'), { target: { value: 'ecc' } });

    expect(screen.getByTestId('secret-use')).toHaveValue('');
    expect(screen.getByTestId('secret-algorithm')).toHaveValue('');
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      secretType: 'ecc',
      keySizeBits: 2048,
      curve: '',
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
    expect(screen.getByTestId('secret-form-submit')).toHaveTextContent('Creating…');
    expect(screen.getByTestId('secret-form-cancel')).toBeDisabled();
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog open initialValues={initial} onSubmit={onSubmit} onCancel={onCancel} />,
    );
    expect(screen.queryByTestId('secret-form-error')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('secret-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = { onSubmit: vi.fn(), onCancel: vi.fn() };
    const { rerender } = render(<SecretFormDialog {...props} open initialValues={initial} />);
    fireEvent.change(screen.getByTestId('secret-expires'), {
      target: { value: '2028-01-01T00:00' },
    });

    rerender(<SecretFormDialog {...props} open initialValues={{ ...initial }} />);
    expect(screen.getByTestId('secret-expires')).toHaveValue('2028-01-01T00:00');

    rerender(<SecretFormDialog {...props} open={false} initialValues={initial} />);
    rerender(
      <SecretFormDialog
        {...props}
        open
        initialValues={{
          ...emptySecretFormValues,
          secretType: 'rsa',
          keySizeBits: 2048,
          expiresWhen: '2030-01-01T00:00',
        }}
      />,
    );
    expect(screen.getByTestId('secret-type')).toHaveValue('rsa');
    expect(screen.getByTestId('secret-key-size')).toHaveValue(2048);
    expect(screen.getByTestId('secret-expires')).toHaveValue('2030-01-01T00:00');
  });
});
