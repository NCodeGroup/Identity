import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import {
  EncryptionRootKeyFormDialog,
  emptyEncryptionRootKeyFormValues,
  type EncryptionRootKeyFormDialogProps,
} from './EncryptionRootKeyFormDialog.js';

const providers = ['aws-kms', 'google-cloud-kms'];

function renderDialog(overrides: Partial<EncryptionRootKeyFormDialogProps> = {}) {
  const onSubmit = vi.fn();
  const onCancel = vi.fn();
  const props: EncryptionRootKeyFormDialogProps = {
    open: true,
    initialValues: emptyEncryptionRootKeyFormValues,
    providers,
    accessBinding: null,
    onSubmit,
    onCancel,
    ...overrides,
  };
  const view = render(<EncryptionRootKeyFormDialog {...props} />);
  return { onSubmit, onCancel, props, ...view };
}

describe('EncryptionRootKeyFormDialog', () => {
  it('offers the first provider and cannot be submitted without a key reference', () => {
    renderDialog();

    expect(screen.getByTestId('root-key-form-provider')).toHaveValue('aws-kms');
    expect(screen.getByTestId('root-key-form-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('root-key-form-reference'), { target: { value: '   ' } });
    expect(screen.getByTestId('root-key-form-submit')).toBeDisabled();
  });

  it('submits the chosen provider and the trimmed key reference', () => {
    const { onSubmit } = renderDialog();

    fireEvent.change(screen.getByTestId('root-key-form-provider'), {
      target: { value: 'google-cloud-kms' },
    });
    fireEvent.change(screen.getByTestId('root-key-form-reference'), {
      target: { value: '  projects/p/locations/l/keyRings/r/cryptoKeys/k  ' },
    });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      provider: 'google-cloud-kms',
      keyReference: 'projects/p/locations/l/keyRings/r/cryptoKeys/k',
      accessIdentity: null,
    });
  });

  it('submits the trimmed access identity, and null when it is blank', () => {
    const { onSubmit } = renderDialog();

    fireEvent.change(screen.getByTestId('root-key-form-reference'), { target: { value: 'arn' } });
    fireEvent.change(screen.getByTestId('root-key-form-access-identity'), {
      target: { value: '  arn:aws:iam::111122223333:role/tenant  ' },
    });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(onSubmit).toHaveBeenLastCalledWith({
      provider: 'aws-kms',
      keyReference: 'arn',
      accessIdentity: 'arn:aws:iam::111122223333:role/tenant',
    });

    fireEvent.change(screen.getByTestId('root-key-form-access-identity'), {
      target: { value: '   ' },
    });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(onSubmit).toHaveBeenLastCalledWith({
      provider: 'aws-kms',
      keyReference: 'arn',
      accessIdentity: null,
    });
  });

  it('names the access binding in the helper text only when it is set', () => {
    const { rerender, props } = renderDialog({ accessBinding: 'tenant:acme' });

    expect(
      screen.getByText(
        "Leave blank to reach the key with the deployment's identity. An AWS role's trust policy requires the external ID tenant:acme.",
      ),
    ).toBeInTheDocument();

    rerender(<EncryptionRootKeyFormDialog {...props} accessBinding={null} />);

    expect(
      screen.getByText("Leave blank to reach the key with the deployment's identity."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/external ID/)).not.toBeInTheDocument();
  });

  it('seeds the values and gives way to the first provider when the seeded one is not offered', () => {
    const { onSubmit } = renderDialog({
      initialValues: {
        provider: 'azure-key-vault',
        keyReference: 'https://v.example/keys/k',
        accessIdentity: null,
      },
    });

    expect(screen.getByTestId('root-key-form-provider')).toHaveValue('aws-kms');
    expect(screen.getByTestId('root-key-form-reference')).toHaveValue('https://v.example/keys/k');
    fireEvent.click(screen.getByTestId('root-key-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      provider: 'aws-kms',
      keyReference: 'https://v.example/keys/k',
      accessIdentity: null,
    });
  });

  it('shows the server error in the dialog', () => {
    renderDialog({ error: "The key could not wrap and unwrap a probe through 'aws-kms': denied" });

    expect(screen.getByTestId('root-key-form-error')).toHaveTextContent('denied');
  });

  it('cannot be submitted while submitting or without a provider', () => {
    const { rerender, props } = renderDialog({
      initialValues: { provider: 'aws-kms', keyReference: 'arn', accessIdentity: null },
      isSubmitting: true,
    });

    expect(screen.getByTestId('root-key-form-submit')).toBeDisabled();
    expect(screen.getByTestId('root-key-form-submit')).toHaveTextContent('Setting…');
    expect(screen.getByTestId('root-key-form-cancel')).toBeDisabled();

    rerender(<EncryptionRootKeyFormDialog {...props} isSubmitting={false} providers={[]} />);

    expect(screen.getByTestId('root-key-form-provider')).toHaveValue('');
    expect(screen.getByText('No root key providers are available.')).toBeInTheDocument();
    expect(screen.getByTestId('root-key-form-submit')).toBeDisabled();
  });

  it('does not submit when it cannot be submitted', () => {
    const { onSubmit } = renderDialog({ providers: [] });

    fireEvent.change(screen.getByTestId('root-key-form-reference'), { target: { value: 'arn' } });
    fireEvent.click(screen.getByTestId('root-key-form-submit'));

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('reseeds the values when the dialog opens again', () => {
    const { rerender, props } = renderDialog();

    fireEvent.change(screen.getByTestId('root-key-form-reference'), { target: { value: 'arn' } });
    rerender(<EncryptionRootKeyFormDialog {...props} open={false} />);
    rerender(<EncryptionRootKeyFormDialog {...props} open />);

    expect(screen.getByTestId('root-key-form-reference')).toHaveValue('');
  });

  it('calls onCancel from the cancel button', () => {
    const { onCancel } = renderDialog();

    fireEvent.click(screen.getByTestId('root-key-form-cancel'));

    expect(onCancel).toHaveBeenCalledTimes(1);
  });
});
