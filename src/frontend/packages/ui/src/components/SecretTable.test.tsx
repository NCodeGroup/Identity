import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SecretTable, type SecretRow } from './SecretTable.js';

const base = {
  use: null,
  algorithm: null,
  createdWhen: '2026-01-01T12:00:00Z',
};

const secrets: SecretRow[] = [
  {
    ...base,
    secretId: 's-active',
    secretType: 'symmetric',
    keySizeBits: 256,
    expiresWhen: '2099-01-01T12:00:00Z',
  },
  {
    ...base,
    secretId: 's-expired',
    secretType: 'rsa',
    keySizeBits: 2048,
    expiresWhen: '2020-01-01T12:00:00Z',
  },
];

describe('SecretTable', () => {
  it('renders a row per secret with its type and key size', () => {
    render(<SecretTable secrets={secrets} />);
    expect(screen.getByTestId('secret-row-s-active')).toHaveTextContent('symmetric');
    expect(screen.getByTestId('secret-row-s-active')).toHaveTextContent('256');
  });

  it('flags an expired secret', () => {
    render(<SecretTable secrets={secrets} />);
    expect(screen.getByTestId('secret-row-s-expired')).toHaveTextContent('Expired');
    expect(screen.getByTestId('secret-row-s-active')).not.toHaveTextContent('Expired');
  });

  it('invokes onDelete with the secret id', () => {
    const onDelete = vi.fn();
    render(<SecretTable secrets={secrets} onDelete={onDelete} />);
    fireEvent.click(screen.getByTestId('secret-delete-s-active'));
    expect(onDelete).toHaveBeenCalledWith('s-active');
  });
});
