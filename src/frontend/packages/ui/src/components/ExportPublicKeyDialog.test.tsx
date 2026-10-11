import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ExportPublicKeyDialog, type PublicKeyMaterial } from './ExportPublicKeyDialog.js';

const publicKey: PublicKeyMaterial = {
  secretId: 's-1',
  secretType: 'rsa',
  jwk: { kty: 'RSA', n: 'bW9k', e: 'AQAB' },
  pem: '-----BEGIN PUBLIC KEY-----\nMIIB\n-----END PUBLIC KEY-----',
};

describe('ExportPublicKeyDialog', () => {
  it('renders the JWK and PEM and copies each', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });

    render(
      <ExportPublicKeyDialog
        open
        secretId="s-1"
        publicKey={publicKey}
        isLoading={false}
        error={undefined}
        onClose={vi.fn()}
      />,
    );

    expect(screen.getByTestId('export-public-key-id')).toHaveTextContent('s-1');
    expect(screen.getByTestId('export-public-key-jwk-value')).toHaveTextContent('"kty": "RSA"');
    expect(screen.getByTestId('export-public-key-pem-value')).toHaveTextContent('BEGIN PUBLIC KEY');

    fireEvent.click(screen.getByTestId('export-public-key-jwk-copy'));
    expect(writeText).toHaveBeenCalledWith(JSON.stringify(publicKey.jwk, null, 2));

    fireEvent.click(screen.getByTestId('export-public-key-pem-copy'));
    expect(writeText).toHaveBeenCalledWith(publicKey.pem);

    vi.unstubAllGlobals();
  });

  it('downloads the PEM as a file', () => {
    const createObjectURL = vi.fn().mockReturnValue('blob:x');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { createObjectURL, revokeObjectURL });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    render(
      <ExportPublicKeyDialog
        open
        secretId="s-1"
        publicKey={publicKey}
        isLoading={false}
        error={undefined}
        onClose={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByTestId('export-public-key-pem-download'));
    expect(createObjectURL).toHaveBeenCalledTimes(1);
    expect(click).toHaveBeenCalledTimes(1);

    click.mockRestore();
    vi.unstubAllGlobals();
  });

  it('shows a spinner while loading', () => {
    render(
      <ExportPublicKeyDialog
        open
        secretId="s-1"
        publicKey={undefined}
        isLoading
        error={undefined}
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByTestId('export-public-key-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('export-public-key-jwk-value')).not.toBeInTheDocument();
  });

  it('shows an error message on failure', () => {
    render(
      <ExportPublicKeyDialog
        open
        secretId="s-1"
        publicKey={undefined}
        isLoading={false}
        error="Not found."
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByTestId('export-public-key-error')).toHaveTextContent('Not found.');
  });

  it('invokes onClose when done', () => {
    const onClose = vi.fn();
    render(
      <ExportPublicKeyDialog
        open
        secretId="s-1"
        publicKey={publicKey}
        isLoading={false}
        error={undefined}
        onClose={onClose}
      />,
    );
    fireEvent.click(screen.getByTestId('export-public-key-done'));
    expect(onClose).toHaveBeenCalled();
  });
});
