import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import {
  RemoteSecretBindDialog,
  emptyRemoteSecretBindValues,
  type RemoteSecretBindValues,
} from './RemoteSecretBindDialog.js';
import type { RemoteKeyProviderOption } from './remoteKeyAlgorithms.js';

const initial: RemoteSecretBindValues = {
  ...emptyRemoteSecretBindValues,
  expiresWhen: '2027-01-01T00:00',
};

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
  { name: 'empty-kms', algorithms: [] },
];

/** Opens the algorithm picker and returns the labels of the options it offers. */
function offeredAlgorithms(): (string | null)[] {
  fireEvent.mouseDown(screen.getByRole('combobox', { name: /Algorithm/ }));
  return within(screen.getByRole('listbox'))
    .getAllByRole('option')
    .map((option) => option.textContent);
}

describe('RemoteSecretBindDialog', () => {
  it("submits the trimmed values with the chosen row's type, size, curve and algorithm", () => {
    const onSubmit = vi.fn();
    render(
      <RemoteSecretBindDialog
        open
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('remote-bind-provider')).toHaveValue('aws-kms');
    fireEvent.change(screen.getByTestId('remote-bind-key-id'), {
      target: { value: '  arn:aws:kms:us-east-1:111122223333:key/abc  ' },
    });
    fireEvent.change(screen.getByTestId('remote-bind-algorithm'), {
      target: { value: 'ES256|ecc|256|P-256' },
    });
    fireEvent.change(screen.getByTestId('remote-bind-expires'), {
      target: { value: '2027-06-01T08:30' },
    });
    fireEvent.click(screen.getByTestId('remote-bind-submit'));

    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      remoteKeyProvider: 'aws-kms',
      remoteKeyId: 'arn:aws:kms:us-east-1:111122223333:key/abc',
      secretType: 'ecc',
      keySizeBits: 256,
      curve: 'P-256',
      algorithm: 'ES256',
      expiresWhen: '2027-06-01T08:30',
    });
  });

  it('submits an encryption row with its type, size and algorithm and no use', () => {
    const onSubmit = vi.fn();
    render(
      <RemoteSecretBindDialog
        open
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('remote-bind-key-id'), { target: { value: 'key-1' } });
    fireEvent.change(screen.getByTestId('remote-bind-algorithm'), {
      target: { value: 'RSA-OAEP-256|rsa|3072|' },
    });
    fireEvent.click(screen.getByTestId('remote-bind-submit'));

    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      remoteKeyProvider: 'aws-kms',
      remoteKeyId: 'key-1',
      secretType: 'rsa',
      keySizeBits: 3072,
      curve: '',
      algorithm: 'RSA-OAEP-256',
      expiresWhen: '2027-01-01T00:00',
    });
  });

  it("offers only the chosen provider's rows and clears the choice when the provider changes", () => {
    render(
      <RemoteSecretBindDialog
        open
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.mouseDown(screen.getByRole('combobox', { name: /Provider/ }));
    expect(
      within(screen.getByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['aws-kms', 'gcp-kms']);
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' });

    expect(offeredAlgorithms()).toEqual([
      'ES256 · ECC P-256 · signing',
      'RS256 · RSA 2048 bits · signing',
      'RSA-OAEP-256 · RSA 3072 bits · encryption',
    ]);
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' });

    fireEvent.change(screen.getByTestId('remote-bind-key-id'), { target: { value: 'key-1' } });
    fireEvent.change(screen.getByTestId('remote-bind-algorithm'), {
      target: { value: 'RS256|rsa|2048|' },
    });
    expect(screen.getByTestId('remote-bind-submit')).toBeEnabled();

    fireEvent.change(screen.getByTestId('remote-bind-provider'), { target: { value: 'gcp-kms' } });
    expect(screen.getByTestId('remote-bind-algorithm')).toHaveValue('');
    expect(screen.getByTestId('remote-bind-submit')).toBeDisabled();
    expect(offeredAlgorithms()).toEqual(['PS256 · RSA 3072 bits · signing']);
  });

  it('blocks submit until the key identifier and an algorithm are given', () => {
    const onSubmit = vi.fn();
    render(
      <RemoteSecretBindDialog
        open
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('remote-bind-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('remote-bind-key-id'), { target: { value: '   ' } });
    fireEvent.change(screen.getByTestId('remote-bind-algorithm'), {
      target: { value: 'RS256|rsa|2048|' },
    });
    expect(screen.getByTestId('remote-bind-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('remote-bind-key-id'), { target: { value: 'key-1' } });
    expect(screen.getByTestId('remote-bind-submit')).toBeEnabled();
    fireEvent.click(screen.getByTestId('remote-bind-submit'));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('falls back to the first offered provider when the seeded provider is not offered', () => {
    const onSubmit = vi.fn();
    render(
      <RemoteSecretBindDialog
        open
        initialValues={{ ...initial, remoteKeyProvider: 'empty-kms' }}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('remote-bind-provider')).toHaveValue('aws-kms');
    expect(offeredAlgorithms()).toEqual([
      'ES256 · ECC P-256 · signing',
      'RS256 · RSA 2048 bits · signing',
      'RSA-OAEP-256 · RSA 3072 bits · encryption',
    ]);
  });

  it('offers no provider and blocks submit when no provider lists an algorithm', () => {
    render(
      <RemoteSecretBindDialog
        open
        initialValues={initial}
        remoteKeyProviders={[{ name: 'empty-kms', algorithms: [] }]}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('remote-bind-provider')).toHaveValue('');
    expect(screen.getByTestId('remote-bind-algorithm')).toHaveValue('');
    fireEvent.change(screen.getByTestId('remote-bind-key-id'), { target: { value: 'key-1' } });
    expect(screen.getByTestId('remote-bind-submit')).toBeDisabled();
  });

  it('blocks submit when the expiry is empty', () => {
    render(
      <RemoteSecretBindDialog
        open
        initialValues={{ ...initial, expiresWhen: '' }}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByTestId('remote-bind-key-id'), { target: { value: 'key-1' } });
    fireEvent.change(screen.getByTestId('remote-bind-algorithm'), {
      target: { value: 'RS256|rsa|2048|' },
    });
    expect(screen.getByTestId('remote-bind-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('remote-bind-expires'), {
      target: { value: '2027-06-01T08:30' },
    });
    expect(screen.getByTestId('remote-bind-submit')).toBeEnabled();
  });

  it('disables the actions while submitting', () => {
    render(
      <RemoteSecretBindDialog
        open
        isSubmitting
        initialValues={{ ...initial, remoteKeyId: 'key-1' }}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('remote-bind-submit')).toBeDisabled();
    expect(screen.getByTestId('remote-bind-cancel')).toBeDisabled();
  });

  it('renders nothing while closed and reseeds from the initial values when it opens again', () => {
    const props = { remoteKeyProviders, onSubmit: vi.fn(), onCancel: vi.fn() };
    const { rerender } = render(
      <RemoteSecretBindDialog {...props} open={false} initialValues={initial} />,
    );
    expect(screen.queryByTestId('remote-bind-key-id')).not.toBeInTheDocument();

    rerender(
      <RemoteSecretBindDialog
        {...props}
        open
        initialValues={{ ...initial, remoteKeyId: 'seeded-key', expiresWhen: '2030-01-01T00:00' }}
      />,
    );
    expect(screen.getByTestId('remote-bind-key-id')).toHaveValue('seeded-key');
    expect(screen.getByTestId('remote-bind-expires')).toHaveValue('2030-01-01T00:00');

    fireEvent.change(screen.getByTestId('remote-bind-key-id'), { target: { value: 'edited' } });
    rerender(<RemoteSecretBindDialog {...props} open={false} initialValues={initial} />);
    rerender(<RemoteSecretBindDialog {...props} open initialValues={initial} />);
    expect(screen.getByTestId('remote-bind-key-id')).toHaveValue('');
  });

  it('shows the given error', () => {
    render(
      <RemoteSecretBindDialog
        open
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        error="The key does not match the algorithm."
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByTestId('remote-bind-error')).toHaveTextContent(
      'The key does not match the algorithm.',
    );
  });

  it('calls onCancel', () => {
    const onCancel = vi.fn();
    render(
      <RemoteSecretBindDialog
        open
        initialValues={initial}
        remoteKeyProviders={remoteKeyProviders}
        onSubmit={vi.fn()}
        onCancel={onCancel}
      />,
    );

    fireEvent.click(screen.getByTestId('remote-bind-cancel'));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
