import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { DonePage, FIRST_INSTANCE_DOCS_URL } from './DonePage.js';
import type { CreatedRoot, SetupTokenSource } from './setupApi.js';

const ROOT: CreatedRoot = {
  tenantId: 'tenant-1',
  principalId: 'principal-1',
  userName: 'root',
  issuer: 'https://id.example.test/',
  discoveryUrl: 'https://id.example.test/.well-known/openid-configuration',
  managementApiUrl: 'https://id.example.test/api',
};

const FULL_ROOT: CreatedRoot = {
  ...ROOT,
  managementConsoleClientId: 'mgmt-console',
  managementConsoleBaseUrl: 'https://console.example.test',
  apiReferenceUrl: 'https://id.example.test/scalar/v1',
};

function renderPage(root: CreatedRoot, tokenSource: SetupTokenSource = 'configured') {
  return render(
    <AppThemeProvider>
      <DonePage root={root} tokenSource={tokenSource} />
    </AppThemeProvider>,
  );
}

function remaining(): (string | null)[] {
  return within(screen.getByRole('list', { name: 'What remains' }))
    .getAllByRole('listitem')
    .map((item) => item.textContent);
}

describe('DonePage', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('names the root administrator and the tenant and lists what remains', () => {
    renderPage(ROOT);

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('The instance is set up');
    expect(screen.getByText('root')).toBeInTheDocument();
    expect(screen.getByText('tenant-1')).toBeInTheDocument();
    expect(remaining()).toEqual([
      'Sign in to the management console as the root administrator.',
      'Register the management console client.',
      'Configure the tenants and their grants.',
      'Remove the setup token Secret.',
    ]);
  });

  it('omits the registration item when the console’s client is registered', () => {
    renderPage({ ...ROOT, managementConsoleClientId: 'mgmt-console' });

    expect(screen.getByText(/The management console’s client/)).toHaveTextContent(
      'The management console’s client mgmt-console is registered.',
    );
    expect(remaining()).toEqual([
      'Sign in to the management console as the root administrator.',
      'Configure the tenants and their grants.',
      'Remove the setup token Secret.',
    ]);
  });

  it('lists the setup token Secret only when the token was configured', () => {
    renderPage(ROOT, 'configured');

    expect(screen.getByText('Remove the setup token Secret.')).toBeInTheDocument();
    expect(screen.queryByText('The generated setup token is spent.')).not.toBeInTheDocument();
  });

  it('says a generated setup token is spent and lists no Secret', () => {
    renderPage(ROOT, 'generated');

    expect(screen.getByText('The generated setup token is spent.')).toBeInTheDocument();
    expect(screen.queryByText(/Secret/)).not.toBeInTheDocument();
    expect(remaining()).toEqual([
      'Sign in to the management console as the root administrator.',
      'Register the management console client.',
      'Configure the tenants and their grants.',
    ]);
  });

  it('links every address to the server’s URL', () => {
    renderPage(FULL_ROOT);

    const addresses = within(screen.getByRole('list', { name: 'Addresses' }));
    const links = addresses.getAllByRole('link');
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      'https://console.example.test',
      'https://id.example.test/',
      'https://id.example.test/.well-known/openid-configuration',
      'https://id.example.test/api',
      'https://id.example.test/scalar/v1',
    ]);
    for (const link of links) {
      expect(link).toHaveAttribute('target', '_blank');
      expect(link).toHaveAttribute('rel', 'noopener noreferrer');
    }
    expect(
      addresses.getAllByRole('listitem').map((item) => item.querySelector('span')?.textContent),
    ).toEqual([
      'Management console',
      'Issuer',
      'Discovery document',
      'Management API',
      'API reference',
    ]);
  });

  it('omits the addresses the instance does not report', () => {
    renderPage(ROOT);

    const addresses = within(screen.getByRole('list', { name: 'Addresses' }));
    expect(addresses.getAllByRole('link')).toHaveLength(3);
    expect(screen.queryByText('Management console')).not.toBeInTheDocument();
    expect(screen.queryByText('API reference')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Copy Management console' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Copy API reference' })).toBeNull();
  });

  it('copies an address to the clipboard and says so', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    renderPage(FULL_ROOT);

    const button = screen.getByRole('button', { name: 'Copy Issuer' });
    expect(button).toHaveTextContent('Copy');
    fireEvent.click(button);

    expect(writeText).toHaveBeenCalledExactlyOnceWith('https://id.example.test/');
    await waitFor(() => {
      expect(button).toHaveTextContent('Copied');
    });
    expect(screen.getByRole('button', { name: 'Copy Management API' })).toHaveTextContent('Copy');
  });

  it('shows no confirmation when the clipboard refuses the copy', async () => {
    const writeText = vi.fn().mockRejectedValue(new Error('denied'));
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    renderPage(FULL_ROOT);

    const button = screen.getByRole('button', { name: 'Copy Issuer' });
    fireEvent.click(button);

    await waitFor(() => {
      expect(writeText).toHaveBeenCalledOnce();
    });
    expect(button).toHaveTextContent('Copy');
    expect(button).not.toHaveTextContent('Copied');
  });

  it('links to the guide to the first use of a deployed instance', () => {
    renderPage(ROOT);

    const link = screen.getByRole('link', {
      name: 'Read the guide to the first use of a deployed instance',
    });
    expect(link).toHaveAttribute(
      'href',
      'https://github.com/NCodeGroup/identity/blob/main/docs/first-deployed-instance.md',
    );
    expect(link).toHaveAttribute('href', FIRST_INSTANCE_DOCS_URL);
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });
});
