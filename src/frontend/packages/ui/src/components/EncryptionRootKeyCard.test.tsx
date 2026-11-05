import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { EncryptionRootKeyCard, type EncryptionRootKeyRow } from './EncryptionRootKeyCard.js';

const active: EncryptionRootKeyRow = {
  provider: 'aws-kms',
  keyReference: 'arn:aws:kms:us-east-1:111122223333:key/abc',
  state: 'active',
  createdWhen: '2026-10-01T09:00:00Z',
  validatedWhen: '2026-10-01T10:00:00Z',
};

function renderCard(
  rootKey: EncryptionRootKeyRow | null | undefined,
  overrides: { canSet?: boolean; isBusy?: boolean } = {},
) {
  const handlers = { onSet: vi.fn(), onValidate: vi.fn(), onRemove: vi.fn() };
  render(<EncryptionRootKeyCard rootKey={rootKey} canSet={true} {...overrides} {...handlers} />);
  return handlers;
}

describe('EncryptionRootKeyCard', () => {
  it('shows the provider, reference, state, creation and validation of the root key', () => {
    renderCard(active);

    expect(screen.getByTestId('root-key-provider')).toHaveTextContent('aws-kms');
    expect(screen.getByTestId('root-key-reference')).toHaveTextContent(
      'arn:aws:kms:us-east-1:111122223333:key/abc',
    );
    expect(screen.getByTestId('root-key-state')).toHaveTextContent('active');
    expect(screen.getByTestId('root-key-created')).not.toHaveTextContent('—');
    expect(screen.getByTestId('root-key-validated')).not.toHaveTextContent('—');
    expect(screen.getByTestId('root-key-failure')).toHaveTextContent('—');
    expect(screen.queryByTestId('root-key-unavailable')).not.toBeInTheDocument();
  });

  it('shows a dash for a root key that has not been validated', () => {
    renderCard({ ...active, validatedWhen: null });

    expect(screen.getByTestId('root-key-validated')).toHaveTextContent('—');
  });

  it('shows an error with the failure when the root key is unavailable', () => {
    renderCard({ ...active, state: 'unavailable', failure: 'The key is disabled.' });

    expect(screen.getByTestId('root-key-state')).toHaveTextContent('unavailable');
    expect(screen.getByTestId('root-key-unavailable')).toHaveTextContent(
      'The root key is unavailable: The key is disabled.',
    );
    expect(screen.getByTestId('root-key-failure')).toHaveTextContent('The key is disabled.');
  });

  it('reads an unknown state as unavailable, even without a failure', () => {
    renderCard({ ...active, state: 'something-new' });

    expect(screen.getByTestId('root-key-state')).toHaveTextContent('unavailable');
    expect(screen.getByTestId('root-key-unavailable')).toHaveTextContent(
      'The root key is unavailable.',
    );
  });

  it('shows a removing root key as removing', () => {
    renderCard({ ...active, state: 'removing' });

    expect(screen.getByTestId('root-key-state')).toHaveTextContent('removing');
    expect(screen.queryByTestId('root-key-unavailable')).not.toBeInTheDocument();
  });

  it('calls the handlers of the set, validate and remove actions', () => {
    const handlers = renderCard(active);

    fireEvent.click(screen.getByTestId('root-key-set'));
    fireEvent.click(screen.getByTestId('root-key-validate'));
    fireEvent.click(screen.getByTestId('root-key-remove'));

    expect(handlers.onSet).toHaveBeenCalledTimes(1);
    expect(handlers.onValidate).toHaveBeenCalledTimes(1);
    expect(handlers.onRemove).toHaveBeenCalledTimes(1);
  });

  it('disables every action while busy', () => {
    renderCard(active, { isBusy: true });

    expect(screen.getByTestId('root-key-set')).toBeDisabled();
    expect(screen.getByTestId('root-key-validate')).toBeDisabled();
    expect(screen.getByTestId('root-key-remove')).toBeDisabled();
  });

  it('offers only "Set root key" when the tenant has none and a provider is available', () => {
    const handlers = renderCard(undefined);

    expect(screen.getByTestId('root-key-none')).toHaveTextContent(
      "This tenant's keys are wrapped under the deployment's root key.",
    );
    expect(screen.queryByTestId('root-key-validate')).not.toBeInTheDocument();
    expect(screen.queryByTestId('root-key-remove')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('root-key-set'));
    expect(handlers.onSet).toHaveBeenCalledTimes(1);
  });

  it('shows no action when the tenant has no root key and no provider is available', () => {
    renderCard(null, { canSet: false });

    expect(screen.getByTestId('root-key-none')).toHaveTextContent(
      'No root key provider is available to this tenant.',
    );
    expect(screen.queryByTestId('root-key-set')).not.toBeInTheDocument();
  });

  it('hides "Set root key" for a root key when no provider is available', () => {
    renderCard(active, { canSet: false });

    expect(screen.queryByTestId('root-key-set')).not.toBeInTheDocument();
    expect(screen.getByTestId('root-key-validate')).toBeInTheDocument();
  });
});
