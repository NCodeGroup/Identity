import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SecretTable, type SecretRow } from './SecretTable.js';

// A fixed reference instant, so the expired/active split does not depend on the real clock.
const now = Date.parse('2026-06-01T00:00:00Z');

const base = {
  use: null,
  algorithm: null,
  createdWhen: '2026-01-01T12:00:00Z',
};

const secrets: SecretRow[] = [
  {
    ...base,
    secretId: 's-active',
    keyId: 's-active',
    secretType: 'symmetric',
    keySizeBits: 256,
    expiresWhen: '2099-01-01T12:00:00Z',
  },
  {
    ...base,
    secretId: 's-expired',
    keyId: 'imported-kid',
    secretType: 'rsa',
    keySizeBits: 2048,
    expiresWhen: '2020-01-01T12:00:00Z',
  },
];

describe('SecretTable', () => {
  it('renders a row per secret with its type and key size', () => {
    render(<SecretTable secrets={secrets} now={now} />);
    expect(screen.getByTestId('secret-row-s-active')).toHaveTextContent('symmetric');
    expect(screen.getByTestId('secret-row-s-active')).toHaveTextContent('256');
  });

  it('shows the key id each key is selected by', () => {
    render(<SecretTable secrets={secrets} now={now} />);
    expect(screen.getByRole('columnheader', { name: 'Key ID' })).toBeInTheDocument();
    expect(screen.getByTestId('secret-kid-s-active')).toHaveTextContent('s-active');
    expect(screen.getByTestId('secret-kid-s-expired')).toHaveTextContent('imported-kid');
  });

  it('flags an expired secret', () => {
    render(<SecretTable secrets={secrets} now={now} />);
    expect(screen.getByTestId('secret-row-s-expired')).toHaveTextContent('Expired');
    expect(screen.getByTestId('secret-row-s-active')).not.toHaveTextContent('Expired');
  });

  it('invokes onDelete with the secret id', () => {
    const onDelete = vi.fn();
    render(<SecretTable secrets={secrets} now={now} onDelete={onDelete} />);
    fireEvent.click(screen.getByTestId('secret-delete-s-active'));
    expect(onDelete).toHaveBeenCalledWith('s-active');
  });

  it('shows the export action only for exportable secret types', () => {
    const onExportPublicKey = vi.fn();
    render(
      <SecretTable
        secrets={secrets}
        now={now}
        onExportPublicKey={onExportPublicKey}
        exportableSecretTypes={['rsa', 'ecc']}
      />,
    );

    // 's-active' is symmetric (not exportable); 's-expired' is rsa (exportable).
    expect(screen.queryByTestId('secret-export-s-active')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('secret-export-s-expired'));
    expect(onExportPublicKey).toHaveBeenCalledWith('s-expired');
  });

  it('shows the rotate action only for rotatable secret types', () => {
    const onRotate = vi.fn();
    render(
      <SecretTable
        secrets={secrets}
        now={now}
        onRotate={onRotate}
        rotatableSecretTypes={['symmetric']}
      />,
    );

    // 's-active' is symmetric (rotatable); 's-expired' is rsa (not rotatable).
    expect(screen.queryByTestId('secret-rotate-s-expired')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('secret-rotate-s-active'));
    expect(onRotate).toHaveBeenCalledWith('s-active');
  });

  it('hides the rotate action when rotatable types are not supplied', () => {
    render(<SecretTable secrets={secrets} now={now} onRotate={vi.fn()} />);

    expect(screen.queryByTestId('secret-rotate-s-active')).not.toBeInTheDocument();
  });

  it('hides the export action when no exportable types are known', () => {
    render(
      <SecretTable
        secrets={secrets}
        now={now}
        onExportPublicKey={vi.fn()}
        exportableSecretTypes={[]}
      />,
    );
    expect(screen.queryByTestId('secret-export-s-expired')).not.toBeInTheDocument();
  });

  it('hides the export action when exportable types are not supplied', () => {
    render(<SecretTable secrets={secrets} now={now} onExportPublicKey={vi.fn()} />);
    expect(screen.getByRole('columnheader', { name: 'Actions' })).toBeInTheDocument();
    expect(screen.queryByTestId('secret-export-s-expired')).not.toBeInTheDocument();
    expect(screen.queryByTestId('secret-delete-s-expired')).not.toBeInTheDocument();
  });

  it('renders no actions column without callbacks', () => {
    render(<SecretTable secrets={secrets} now={now} exportableSecretTypes={['rsa']} />);
    expect(screen.queryByRole('columnheader', { name: 'Actions' })).not.toBeInTheDocument();
    expect(screen.queryByTestId('secret-export-s-expired')).not.toBeInTheDocument();
    expect(screen.queryByTestId('secret-delete-s-expired')).not.toBeInTheDocument();
  });

  it('formats a parseable expiry and shows an unparseable one verbatim', () => {
    render(
      <SecretTable
        secrets={[
          ...secrets,
          {
            ...base,
            secretId: 's-odd',
            keyId: 's-odd',
            secretType: 'ecc',
            keySizeBits: 256,
            expiresWhen: 'not-a-date',
          },
        ]}
        now={now}
      />,
    );
    expect(screen.getByTestId('secret-row-s-active')).toHaveTextContent(
      new Date(Date.parse('2099-01-01T12:00:00Z')).toLocaleString(),
    );
    const odd = screen.getByTestId('secret-row-s-odd');
    expect(odd).toHaveTextContent('not-a-date');
    expect(odd).not.toHaveTextContent('Expired');
  });

  it('expires a secret exactly at its expiry instant', () => {
    render(
      <SecretTable
        secrets={[
          {
            ...base,
            secretId: 's-boundary',
            keyId: 's-boundary',
            secretType: 'rsa',
            keySizeBits: 2048,
            expiresWhen: '2026-06-01T00:00:00Z',
          },
        ]}
        now={now}
      />,
    );
    expect(screen.getByTestId('secret-row-s-boundary')).toHaveTextContent('Expired');
  });
});
