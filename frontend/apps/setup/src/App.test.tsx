import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { App } from './App.js';
import { createRoot, getStatus, type CreateRootResult, type GetStatusResult } from './setupApi.js';
import { takeSetupToken } from './setupToken.js';

vi.mock('./setupApi.js', () => ({ createRoot: vi.fn(), getStatus: vi.fn() }));
vi.mock('./setupToken.js', () => ({ takeSetupToken: vi.fn() }));

const READY: GetStatusResult = {
  kind: 'ok',
  status: { tokenSource: 'generated', minimumPasswordLength: 15 },
};

function renderApp() {
  return render(
    <AppThemeProvider>
      <App />
    </AppThemeProvider>,
  );
}

function fill(label: RegExp, value: string): void {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

async function enterToken(token: string): Promise<void> {
  await screen.findByLabelText(/^Setup token/);
  fill(/^Setup token/, token);
  fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
}

function submitAccount(): void {
  fill(/^User name/, 'root');
  fill(/^Password/, 'a long passphrase!');
  fill(/^Confirm password/, 'a long passphrase!');
  fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
  fireEvent.click(screen.getByRole('button', { name: 'Create the root administrator' }));
}

describe('App', () => {
  beforeEach(() => {
    vi.mocked(getStatus).mockReset();
    vi.mocked(createRoot).mockReset();
    vi.mocked(takeSetupToken).mockReset();
    vi.mocked(takeSetupToken).mockReturnValue(undefined);
  });

  it('shows progress while it loads the status', async () => {
    vi.mocked(getStatus).mockResolvedValue(READY);
    renderApp();

    expect(screen.getByRole('progressbar', { name: 'Loading' })).toBeInTheDocument();
    await screen.findByLabelText(/^Setup token/);
  });

  it('shows the token step for the status’s token source', async () => {
    vi.mocked(getStatus).mockResolvedValue(READY);
    renderApp();

    expect(await screen.findByText(/startup log of one replica/)).toBeInTheDocument();
    expect(screen.getByLabelText(/^Setup token/)).toHaveValue('');
  });

  it('prefills the token step with the token from the URL fragment', async () => {
    vi.mocked(takeSetupToken).mockReturnValue('from-fragment');
    vi.mocked(getStatus).mockResolvedValue(READY);
    renderApp();

    expect(await screen.findByLabelText(/^Setup token/)).toHaveValue('from-fragment');
  });

  it('reads the fragment token once', async () => {
    vi.mocked(getStatus).mockResolvedValue(READY);
    const { rerender } = renderApp();
    await screen.findByLabelText(/^Setup token/);

    rerender(
      <AppThemeProvider>
        <App />
      </AppThemeProvider>,
    );

    expect(takeSetupToken).toHaveBeenCalledOnce();
  });

  it('says the instance is already set up when the status is not found', async () => {
    vi.mocked(getStatus).mockResolvedValue({ kind: 'notFound' });
    renderApp();

    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent(
      'This instance is already set up.',
    );
  });

  it('offers a retry when the status cannot be loaded', async () => {
    vi.mocked(getStatus).mockResolvedValueOnce({ kind: 'error' }).mockResolvedValueOnce(READY);
    renderApp();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The instance’s setup status could not be loaded.',
    );
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByLabelText(/^Setup token/)).toBeInTheDocument();
    expect(getStatus).toHaveBeenCalledTimes(2);
  });

  it('ignores a status that arrives after it unmounted', async () => {
    let resolve: (result: GetStatusResult) => void = () => undefined;
    vi.mocked(getStatus).mockReturnValue(
      new Promise<GetStatusResult>((r) => {
        resolve = r;
      }),
    );
    const { unmount } = renderApp();

    unmount();
    resolve(READY);
    await Promise.resolve();

    expect(getStatus).toHaveBeenCalledOnce();
  });

  it('walks from the token through the account to the done page', async () => {
    vi.mocked(getStatus).mockResolvedValue(READY);
    vi.mocked(createRoot).mockResolvedValue({
      kind: 'created',
      root: { tenantId: 't', principalId: 'p', userName: 'root' },
    } satisfies CreateRootResult);
    renderApp();

    await enterToken('the-token');
    submitAccount();

    expect(
      await screen.findByRole('heading', { level: 1, name: 'The instance is set up' }),
    ).toBeInTheDocument();
    expect(screen.getByText('root')).toBeInTheDocument();
    expect(screen.getByText('Register the management console client.')).toBeInTheDocument();
    expect(createRoot).toHaveBeenCalledExactlyOnceWith({
      token: 'the-token',
      userName: 'root',
      password: 'a long passphrase!',
    });
  });

  it('registers the management console and reports its client on the done page', async () => {
    vi.mocked(getStatus).mockResolvedValue(READY);
    vi.mocked(createRoot).mockResolvedValue({
      kind: 'created',
      root: {
        tenantId: 't',
        principalId: 'p',
        userName: 'root',
        managementConsoleClientId: 'mgmt-console',
      },
    } satisfies CreateRootResult);
    renderApp();

    await enterToken('the-token');
    fill(/^User name/, 'root');
    fill(/^Password/, 'a long passphrase!');
    fill(/^Confirm password/, 'a long passphrase!');
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    fireEvent.click(
      screen.getByRole('checkbox', { name: 'Register the management console’s client now' }),
    );
    fill(/^Console URL/, 'https://console.example.test');
    fill(/^Public JWK Set/, '{"keys":[{"kty":"EC","crv":"P-256","x":"x","y":"y"}]}');
    fireEvent.click(screen.getByRole('button', { name: 'Create the root administrator' }));

    expect(await screen.findByText(/is registered\./)).toHaveTextContent(
      'The management console’s client mgmt-console is registered.',
    );
    expect(screen.queryByText('Register the management console client.')).not.toBeInTheDocument();
    expect(createRoot).toHaveBeenCalledExactlyOnceWith({
      token: 'the-token',
      userName: 'root',
      password: 'a long passphrase!',
      managementConsole: {
        clientId: 'mgmt-console',
        baseUrl: 'https://console.example.test',
        jwks: { keys: [{ kty: 'EC', crv: 'P-256', x: 'x', y: 'y' }] },
      },
    });
  });

  it('returns to the token step with an error when the token is refused', async () => {
    vi.mocked(getStatus).mockResolvedValue(READY);
    vi.mocked(createRoot).mockResolvedValue({ kind: 'forbidden', problem: {} });
    renderApp();

    await enterToken('wrong-token');
    submitAccount();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The instance did not accept the setup token.',
    );
    expect(screen.getByLabelText(/^Setup token/)).toHaveValue('wrong-token');
  });

  it('shows the already-set-up page when the instance finishes setup meanwhile', async () => {
    vi.mocked(getStatus).mockResolvedValue(READY);
    vi.mocked(createRoot).mockResolvedValue({ kind: 'notFound' });
    renderApp();

    await enterToken('the-token');
    submitAccount();

    await screen.findByRole('heading', { level: 1, name: 'This instance is already set up.' });
    expect(screen.queryByLabelText(/^Password/)).not.toBeInTheDocument();
  });
});
