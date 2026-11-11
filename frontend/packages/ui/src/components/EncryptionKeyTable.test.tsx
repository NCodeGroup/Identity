import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EncryptionKeyTable, type EncryptionKeyRow } from './EncryptionKeyTable.js';

const active: EncryptionKeyRow = {
  keyId: 'k-2',
  version: 2,
  state: 'active',
  createdWhen: '2026-10-01T09:00:00Z',
  retiredWhen: null,
  rootKeyProvider: 'aws-kms',
  recoveryKeyId: 'recovery-1',
};

const retired: EncryptionKeyRow = {
  keyId: 'k-1',
  version: 1,
  state: 'retired',
  createdWhen: '2025-10-01T09:00:00Z',
  retiredWhen: '2026-10-01T09:00:00Z',
  rootKeyProvider: 'local',
  recoveryKeyId: null,
};

describe('EncryptionKeyTable', () => {
  it('shows each key with its version, state, provider and recovery copy', () => {
    render(<EncryptionKeyTable keys={[active, retired]} />);

    const activeRow = screen.getByTestId('encryption-key-row-k-2');
    expect(activeRow).toHaveTextContent('2');
    expect(screen.getByTestId('encryption-key-state-k-2')).toHaveTextContent('active');
    expect(activeRow).toHaveTextContent('aws-kms');
    expect(activeRow).toHaveTextContent('Yes');

    const retiredRow = screen.getByTestId('encryption-key-row-k-1');
    expect(screen.getByTestId('encryption-key-state-k-1')).toHaveTextContent('retired');
    expect(retiredRow).toHaveTextContent('local');
    expect(retiredRow).toHaveTextContent('No');
  });

  it('shows the root key reference of a tenant key and "Deployment" for any other', () => {
    render(
      <EncryptionKeyTable
        keys={[{ ...active, rootKeyReference: 'arn:aws:kms:key/abc' }, retired]}
      />,
    );

    expect(screen.getByTestId('encryption-key-root-key-k-2')).toHaveTextContent(
      'arn:aws:kms:key/abc',
    );
    expect(screen.getByTestId('encryption-key-root-key-k-1')).toHaveTextContent('Deployment');
  });

  it('shows a dash for the retirement of an active key', () => {
    render(<EncryptionKeyTable keys={[active]} />);

    expect(screen.getByTestId('encryption-key-row-k-2')).toHaveTextContent('—');
  });

  it('reads an unknown state as retired', () => {
    render(<EncryptionKeyTable keys={[{ ...retired, state: 'something-new' }]} />);

    expect(screen.getByTestId('encryption-key-state-k-1')).toHaveTextContent('retired');
  });
});
