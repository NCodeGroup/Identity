import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import {
  SecretFormDialog,
  emptySecretFormValues,
  type AlgorithmOption,
  type SecretFormValues,
} from './SecretFormDialog.js';
import type { RemoteKeyProviderOption } from './remoteKeyAlgorithms.js';

const initial: SecretFormValues = { ...emptySecretFormValues, expiresWhen: '2027-01-01T00:00' };

const rs256: AlgorithmOption = {
  code: 'RS256',
  use: 'sig',
  keyTypes: [
    {
      secretType: 'rsa',
      keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
      curves: [],
    },
  ],
};
const rsaOaep: AlgorithmOption = {
  code: 'RSA-OAEP',
  use: 'enc',
  keyTypes: [
    {
      secretType: 'rsa',
      keyBitSizes: [{ minBits: 2048, maxBits: 16384, skipBits: 8 }],
      curves: [],
    },
  ],
};
const es256: AlgorithmOption = {
  code: 'ES256',
  use: 'sig',
  keyTypes: [
    {
      secretType: 'ecc',
      keyBitSizes: [{ minBits: 256, maxBits: 256, skipBits: 0 }],
      curves: [{ curveName: 'P-256', curveSizeBits: 256 }],
    },
  ],
};
const algorithms = [rs256, rsaOaep, es256];

const secretTypes = ['symmetric', 'rsa', 'ecc'];

const eccCurves = [
  { curveName: 'P-256', curveSizeBits: 256 },
  { curveName: 'P-384', curveSizeBits: 384 },
];

const remoteKeyProviders: readonly RemoteKeyProviderOption[] = [
  {
    name: 'aws-kms',
    algorithms: [
      { algorithm: 'ES256', use: 'sig', secretType: 'ecc', keySizeBits: 256, curve: 'P-256' },
      { algorithm: 'RS256', use: 'sig', secretType: 'rsa', keySizeBits: 2048, curve: null },
      { algorithm: 'RSA-OAEP-256', use: 'enc', secretType: 'rsa', keySizeBits: 3072 },
    ],
  },
  {
    name: 'gcp-kms',
    algorithms: [{ algorithm: 'PS256', use: 'sig', secretType: 'rsa', keySizeBits: 3072 }],
  },
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
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={initial}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      secretType: 'symmetric',
      keySizeBits: 256,
      curve: '',
      use: '',
      algorithm: '',
      custody: '',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it('submits hand-entered values when no algorithms are discovered', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={initial}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
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
      custody: '',
      expiresWhen: '2027-06-01T08:30',
    });
  });

  it('blocks submit when the key size is not positive', () => {
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={initial}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('secret-form-submit')).toBeEnabled();
    fireEvent.change(screen.getByTestId('secret-key-size'), { target: { value: '0' } });
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();
  });

  it('offers the discovered ECC curves and applies the curve size on select', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
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

  // Two curves can share a key size, so a secret type with curves takes a curve and the size never selects one.
  it('requires a curve for a secret type with curves', () => {
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, secretType: 'ecc', keySizeBits: 256, curve: '' }}
        eccCurves={eccCurves}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('secret-curve')).toHaveValue('');
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();
    fireEvent.mouseDown(screen.getByRole('combobox', { name: /Curve/ }));
    expect(
      within(screen.getByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Choose a curve', 'P-256', 'P-384']);
  });

  it('selects the first curve of a secret type with curves when the type is chosen', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, secretType: 'rsa', keySizeBits: 2048 }}
        eccCurves={eccCurves}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-type'), { target: { value: 'ecc' } });
    expect(screen.getByTestId('secret-curve')).toHaveValue('P-256');
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ secretType: 'ecc', curve: 'P-256', keySizeBits: 256 }),
    );
  });

  it.each([
    ['no curves are discovered', undefined],
    ['the discovered curve list is empty', []],
  ])('offers no curve picker for ECC when %s', (_, curves) => {
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
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
        secretTypes={secretTypes}
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
      custody: '',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it('offers only the discovered algorithms for the selected type, deriving the use on select', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
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
        secretTypes={secretTypes}
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
        secretTypes={secretTypes}
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

  // One algorithm name may serve several secret types, each with its own sizes (ADR-1389), such as ECDH-ES over EC
  // and OKP keys; the key size is bounded by the chosen type's entry.
  it.each([
    {
      secretType: 'ecc' as const,
      expected: 'Allowed: 256–384 bits for ECDH-ES.',
      min: '256',
      max: '384',
    },
    {
      secretType: 'rsa' as const,
      expected: 'Allowed: 2048–4096 bits for ECDH-ES.',
      min: '2048',
      max: '4096',
    },
  ])('bounds the key size by the $secretType entry of a multi-type algorithm', (row) => {
    const multiType: AlgorithmOption = {
      code: 'ECDH-ES',
      use: 'enc',
      keyTypes: [
        {
          secretType: 'ecc',
          keyBitSizes: [{ minBits: 256, maxBits: 384, skipBits: 128 }],
          curves: [],
        },
        {
          secretType: 'rsa',
          keyBitSizes: [{ minBits: 2048, maxBits: 4096, skipBits: 1024 }],
          curves: [],
        },
      ],
    };
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, secretType: row.secretType, keySizeBits: 1 }}
        algorithms={[multiType]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'ECDH-ES' } });

    expect(screen.getByText(row.expected)).toBeInTheDocument();
    const keySize = screen.getByTestId('secret-key-size');
    expect(keySize).toHaveValue(Number(row.min));
    expect(keySize).toHaveAttribute('min', row.min);
    expect(keySize).toHaveAttribute('max', row.max);
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ algorithm: 'ECDH-ES', use: 'enc', keySizeBits: Number(row.min) }),
    );
  });

  it('offers only the curves an algorithm bound to a curve accepts and selects the first of them', () => {
    // secp256k1 has the size of P-256, so the key size alone cannot select it.
    const es256k: AlgorithmOption = {
      code: 'ES256K',
      use: 'sig',
      keyTypes: [
        {
          secretType: 'ecc',
          keyBitSizes: [{ minBits: 256, maxBits: 256, skipBits: 0 }],
          curves: [{ curveName: 'secp256k1', curveSizeBits: 256 }],
        },
      ],
    };
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, secretType: 'ecc', keySizeBits: 256, curve: 'P-256' }}
        eccCurves={eccCurves}
        algorithms={[es256, es256k]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'ES256K' } });

    expect(screen.getByTestId('secret-curve')).toHaveValue('secp256k1');
    fireEvent.mouseDown(screen.getByRole('combobox', { name: /Curve/ }));
    expect(
      within(screen.getByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['secp256k1']);
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' });
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ algorithm: 'ES256K', curve: 'secp256k1', keySizeBits: 256 }),
    );
  });

  // ECDH-ES accepts EC keys on three curves of three sizes, so each size is legal only with its own curve.
  it('creates a P-384 key for ECDH-ES from the curve the algorithm offers', () => {
    const ecdhEs: AlgorithmOption = {
      code: 'ECDH-ES',
      use: 'enc',
      keyTypes: [
        {
          secretType: 'ecc',
          keyBitSizes: [
            { minBits: 256, maxBits: 256, skipBits: 0 },
            { minBits: 384, maxBits: 384, skipBits: 0 },
            { minBits: 521, maxBits: 521, skipBits: 0 },
          ],
          curves: [
            { curveName: 'P-256', curveSizeBits: 256 },
            { curveName: 'P-384', curveSizeBits: 384 },
            { curveName: 'P-521', curveSizeBits: 521 },
          ],
        },
      ],
    };
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, secretType: 'ecc', keySizeBits: 256 }}
        algorithms={[ecdhEs]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'ECDH-ES' } });
    expect(screen.getByText('Allowed: 256, 384 or 521 bits for ECDH-ES.')).toBeInTheDocument();
    expect(screen.getByTestId('secret-key-size')).toHaveAttribute('max', '521');

    fireEvent.change(screen.getByTestId('secret-curve'), { target: { value: 'P-384' } });
    expect(screen.getByTestId('secret-key-size')).toHaveValue(384);
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      secretType: 'ecc',
      keySizeBits: 384,
      curve: 'P-384',
      use: 'enc',
      algorithm: 'ECDH-ES',
      custody: '',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  // The secret types and the curves come from discovery, so an OKP type a package contributes is offered with its own
  // curves, and an Ed448 key takes Ed448's size.
  it('creates an Ed448 key for EdDSA as a discovered secret type', () => {
    const edDsa: AlgorithmOption = {
      code: 'EdDSA',
      use: 'sig',
      keyTypes: [
        {
          secretType: 'okp',
          keyBitSizes: [
            { minBits: 256, maxBits: 256, skipBits: 0 },
            { minBits: 456, maxBits: 456, skipBits: 0 },
          ],
          curves: [
            { curveName: 'Ed25519', curveSizeBits: 256 },
            { curveName: 'Ed448', curveSizeBits: 456 },
          ],
        },
      ],
    };
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        initialValues={initial}
        secretTypes={['symmetric', 'rsa', 'ecc', 'okp']}
        algorithms={[rs256, edDsa]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-type'), { target: { value: 'okp' } });
    expect(offeredAlgorithms()).toEqual(['Any', 'EdDSA']);
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' });
    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'EdDSA' } });
    expect(screen.getByTestId('secret-curve')).toHaveValue('Ed25519');

    fireEvent.change(screen.getByTestId('secret-curve'), { target: { value: 'Ed448' } });
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      secretType: 'okp',
      keySizeBits: 456,
      curve: 'Ed448',
      use: 'sig',
      algorithm: 'EdDSA',
      custody: '',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it('blocks submit when the key size is not one the algorithm accepts', () => {
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, secretType: 'rsa', keySizeBits: 2048 }}
        algorithms={algorithms}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'RS256' } });
    fireEvent.change(screen.getByTestId('secret-key-size'), { target: { value: '2049' } });
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();
  });

  it('clears the derived use and keeps the key size when no specific algorithm is chosen', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
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
        secretTypes={secretTypes}
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
      keySizeBits: 256,
      curve: 'P-256',
      use: '',
      algorithm: '',
      custody: '',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  // The secret types are discovered, so the dialog can open before they load; the type select never holds a value it
  // has no option for, which MUI reports as an out-of-range value.
  it('offers no type and blocks submit until the secret types are discovered', () => {
    const onSubmit = vi.fn();
    const props = { initialValues: initial, onSubmit, onCancel: vi.fn() };
    const { rerender } = render(<SecretFormDialog {...props} open secretTypes={[]} />);

    expect(screen.getByTestId('secret-type')).toHaveValue('');
    expect(screen.getByText('No secret types are available.')).toBeInTheDocument();
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();

    rerender(<SecretFormDialog {...props} open secretTypes={secretTypes} />);

    expect(screen.getByTestId('secret-type')).toHaveValue('symmetric');
    expect(screen.queryByText('No secret types are available.')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ secretType: 'symmetric', keySizeBits: 256 }),
    );
  });

  it('selects the first offered type when the seeded type is not offered', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={['rsa', 'ecc']}
        initialValues={{ ...initial, keySizeBits: 2048 }}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('secret-type')).toHaveValue('rsa');
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      secretType: 'rsa',
      keySizeBits: 2048,
      curve: '',
      use: '',
      algorithm: '',
      custody: '',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it('requires a curve when the first offered type has curves', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={['ecc']}
        initialValues={initial}
        eccCurves={eccCurves}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('secret-type')).toHaveValue('ecc');
    expect(screen.getByTestId('secret-curve')).toHaveValue('');
    expect(screen.getByTestId('secret-key-size')).not.toHaveAttribute('readonly');
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('secret-curve'), { target: { value: 'P-384' } });

    // The selected curve fixes the key size, which can no longer be hand-entered.
    expect(screen.getByTestId('secret-key-size')).toHaveValue(384);
    expect(screen.getByTestId('secret-key-size')).toHaveAttribute('readonly');
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ secretType: 'ecc', curve: 'P-384', keySizeBits: 384 }),
    );
  });

  it('keeps a selected curve that the chosen algorithm accepts', () => {
    const ecdhEs: AlgorithmOption = {
      code: 'ECDH-ES',
      use: 'enc',
      keyTypes: [
        {
          secretType: 'ecc',
          keyBitSizes: [
            { minBits: 256, maxBits: 256, skipBits: 0 },
            { minBits: 384, maxBits: 384, skipBits: 0 },
          ],
          curves: [
            { curveName: 'P-256', curveSizeBits: 256 },
            { curveName: 'P-384', curveSizeBits: 384 },
          ],
        },
      ],
    };
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, secretType: 'ecc', curve: 'P-384' }}
        eccCurves={eccCurves}
        algorithms={[ecdhEs]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'ECDH-ES' } });

    expect(screen.getByTestId('secret-curve')).toHaveValue('P-384');
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ algorithm: 'ECDH-ES', curve: 'P-384', keySizeBits: 384 }),
    );
  });

  // A secret type's entry may list both a stepped range and a fixed size; the description names each of them, and a
  // size between them is not legal.
  it('describes and enforces a size range and a fixed size of one secret type', () => {
    const mixed: AlgorithmOption = {
      code: 'PS256',
      use: 'sig',
      keyTypes: [
        {
          secretType: 'rsa',
          keyBitSizes: [
            { minBits: 2048, maxBits: 4096, skipBits: 1024 },
            { minBits: 8192, maxBits: 8192, skipBits: 0 },
          ],
          curves: [],
        },
      ],
    };
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, secretType: 'rsa', keySizeBits: 8192 }}
        algorithms={[mixed]}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-algorithm'), { target: { value: 'PS256' } });

    expect(screen.getByText('Allowed: 2048–4096 or 8192 bits for PS256.')).toBeInTheDocument();
    const keySize = screen.getByTestId('secret-key-size');
    expect(keySize).toHaveValue(8192);
    expect(keySize).toHaveAttribute('min', '2048');
    expect(keySize).toHaveAttribute('max', '8192');
    // Two ranges have no single step.
    expect(keySize).not.toHaveAttribute('step');
    fireEvent.change(keySize, { target: { value: '6144' } });
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();
    fireEvent.change(keySize, { target: { value: '3072' } });
    fireEvent.click(screen.getByTestId('secret-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ algorithm: 'PS256', keySizeBits: 3072 }),
    );
  });

  it('blocks submit when the expiry is empty', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
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
        secretTypes={secretTypes}
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
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={initial}
        onSubmit={onSubmit}
        onCancel={onCancel}
      />,
    );
    expect(screen.queryByTestId('secret-form-error')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('secret-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = { secretTypes, onSubmit: vi.fn(), onCancel: vi.fn() };
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

  it('offers no custody without a remote key provider that lists an algorithm', () => {
    const props = { initialValues: initial, secretTypes, onSubmit: vi.fn(), onCancel: vi.fn() };
    const { rerender } = render(<SecretFormDialog {...props} open />);
    expect(screen.queryByTestId('secret-custody')).not.toBeInTheDocument();

    rerender(
      <SecretFormDialog
        {...props}
        open
        remoteKeyProviders={[{ name: 'aws-kms', algorithms: [] }]}
      />,
    );
    expect(screen.queryByTestId('secret-custody')).not.toBeInTheDocument();
  });

  it('offers the server and each remote key provider as custody', () => {
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('secret-custody')).toHaveValue('');
    fireEvent.mouseDown(screen.getByRole('combobox', { name: /Custody/ }));
    expect(
      within(screen.getByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['Server', 'aws-kms', 'gcp-kms']);
  });

  it("creates a remote key from one of the provider's algorithms", () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-custody'), { target: { value: 'aws-kms' } });
    expect(screen.queryByTestId('secret-type')).not.toBeInTheDocument();
    expect(screen.queryByTestId('secret-key-size')).not.toBeInTheDocument();
    expect(screen.queryByTestId('secret-use')).not.toBeInTheDocument();
    expect(screen.queryByTestId('secret-algorithm')).not.toBeInTheDocument();
    expect(
      screen.getByText(/The key management service of aws-kms creates the key/),
    ).toBeInTheDocument();
    expect(offeredAlgorithms()).toEqual([
      'ES256 · ECC P-256 · signing',
      'RS256 · RSA 2048 bits · signing',
      'RSA-OAEP-256 · RSA 3072 bits · encryption',
    ]);
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' });
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('secret-remote-algorithm'), {
      target: { value: 'ES256|ecc|256|P-256' },
    });
    expect(screen.getByTestId('secret-form-submit')).toBeEnabled();
    fireEvent.click(screen.getByTestId('secret-form-submit'));

    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      secretType: 'ecc',
      keySizeBits: 256,
      curve: 'P-256',
      use: 'sig',
      algorithm: 'ES256',
      custody: 'aws-kms',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it('creates an encryption key in a provider from one of its encryption rows', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-custody'), { target: { value: 'aws-kms' } });
    fireEvent.change(screen.getByTestId('secret-remote-algorithm'), {
      target: { value: 'RSA-OAEP-256|rsa|3072|' },
    });
    fireEvent.click(screen.getByTestId('secret-form-submit'));

    expect(onSubmit).toHaveBeenCalledExactlyOnceWith(
      expect.objectContaining({
        secretType: 'rsa',
        keySizeBits: 3072,
        curve: '',
        algorithm: 'RSA-OAEP-256',
        custody: 'aws-kms',
        expiresWhen: '2027-01-01T00:00',
      }),
    );
  });

  it('blocks a remote create until an algorithm is chosen and the expiry is given', () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={{ ...initial, expiresWhen: '' }}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-custody'), { target: { value: 'gcp-kms' } });
    expect(screen.getByTestId('secret-remote-algorithm')).toHaveValue('');
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('secret-remote-algorithm'), {
      target: { value: 'PS256|rsa|3072|' },
    });
    expect(screen.getByTestId('secret-remote-algorithm')).toHaveValue('PS256|rsa|3072|');
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('secret-expires'), {
      target: { value: '2027-06-01T08:30' },
    });
    expect(screen.getByTestId('secret-form-submit')).toBeEnabled();
    fireEvent.click(screen.getByTestId('secret-form-submit'));

    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      secretType: 'rsa',
      keySizeBits: 3072,
      curve: '',
      use: 'sig',
      algorithm: 'PS256',
      custody: 'gcp-kms',
      expiresWhen: '2027-06-01T08:30',
    });
  });

  it("clears the algorithm when the custody provider changes and restores the server's inputs when it is chosen again", () => {
    const onSubmit = vi.fn();
    render(
      <SecretFormDialog
        open
        secretTypes={secretTypes}
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('secret-custody'), { target: { value: 'aws-kms' } });
    fireEvent.change(screen.getByTestId('secret-remote-algorithm'), {
      target: { value: 'RS256|rsa|2048|' },
    });
    expect(screen.getByTestId('secret-form-submit')).toBeEnabled();

    fireEvent.change(screen.getByTestId('secret-custody'), { target: { value: 'gcp-kms' } });
    expect(screen.getByTestId('secret-remote-algorithm')).toHaveValue('');
    expect(screen.getByTestId('secret-form-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('secret-custody'), { target: { value: '' } });
    expect(screen.getByTestId('secret-custody')).toHaveValue('');
    expect(screen.queryByTestId('secret-remote-algorithm')).not.toBeInTheDocument();
    expect(screen.getByTestId('secret-type')).toBeInTheDocument();
    expect(screen.getByTestId('secret-algorithm')).toBeInTheDocument();
    expect(screen.getByText(/generated by the server and shown only once/)).toBeInTheDocument();
  });
});
