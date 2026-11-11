import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ExportPublicKeyDialog, type PublicKeyMaterial } from './ExportPublicKeyDialog.js';

const publicKey: PublicKeyMaterial = {
  secretId: 's-1',
  secretType: 'rsa',
  jwk: { kty: 'RSA', n: 'bW9k', e: 'AQAB' },
  pem: '-----BEGIN PUBLIC KEY-----\nMIIB\n-----END PUBLIC KEY-----',
};

const jwkText = JSON.stringify(publicKey.jwk, null, 2);

function renderLoaded() {
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
}

describe('ExportPublicKeyDialog', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('renders the JWK and PEM for the secret', () => {
    renderLoaded();

    expect(screen.getByTestId('export-public-key-id')).toHaveTextContent('s-1');
    expect(screen.getByTestId('export-public-key-jwk-value').textContent).toBe(jwkText);
    expect(screen.getByTestId('export-public-key-pem-value').textContent).toBe(publicKey.pem);
    expect(screen.queryByTestId('export-public-key-loading')).not.toBeInTheDocument();
    expect(screen.queryByTestId('export-public-key-error')).not.toBeInTheDocument();
  });

  it.each([
    ['jwk', jwkText],
    ['pem', publicKey.pem],
  ])('copies the %s and confirms it in the copy tooltip', async (kind, expectedText) => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    renderLoaded();
    const copy = screen.getByTestId(`export-public-key-${kind}-copy`);

    fireEvent.mouseOver(copy);
    expect(await screen.findByRole('tooltip', { name: 'Copy' })).toBeInTheDocument();

    fireEvent.click(copy);

    expect(await screen.findByRole('tooltip', { name: 'Copied' })).toBeInTheDocument();
    expect(writeText).toHaveBeenCalledExactlyOnceWith(expectedText);
  });

  it.each([
    ['jwk', 's-1.jwk.json', 'application/json', jwkText],
    ['pem', 's-1.pem', 'application/x-pem-file', publicKey.pem],
  ])('downloads the %s as %s', async (kind, expectedFilename, expectedMime, expectedContent) => {
    const createObjectURL = vi.fn().mockReturnValue('blob:x');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { createObjectURL, revokeObjectURL });
    const clicked: { href: string; download: string }[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      clicked.push({ href: this.href, download: this.download });
    });
    renderLoaded();

    fireEvent.click(screen.getByTestId(`export-public-key-${kind}-download`));

    expect(createObjectURL).toHaveBeenCalledOnce();
    const blob = createObjectURL.mock.calls[0]?.[0] as Blob;
    expect(blob.type).toBe(expectedMime);
    expect(await blob.text()).toBe(expectedContent);
    expect(clicked).toEqual([{ href: 'blob:x', download: expectedFilename }]);
    expect(revokeObjectURL).toHaveBeenCalledExactlyOnceWith('blob:x');
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
    expect(screen.queryByTestId('export-public-key-pem-value')).not.toBeInTheDocument();
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
    expect(screen.queryByTestId('export-public-key-loading')).not.toBeInTheDocument();
    expect(screen.queryByTestId('export-public-key-jwk-value')).not.toBeInTheDocument();
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
    expect(onClose).toHaveBeenCalledOnce();
  });
});
