import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { ConsoleClientStep } from './ConsoleClientStep.js';
import type { RootAccount } from './RootAccountStep.js';
import { createRoot, type CreateRootResult } from './setupApi.js';

vi.mock('./setupApi.js', () => ({ createRoot: vi.fn(), getStatus: vi.fn() }));

const PUBLIC_JWKS = { keys: [{ kty: 'EC', crv: 'P-256', x: 'x', y: 'y', kid: 'k1' }] };

function renderStep(account: RootAccount = { userName: 'root', password: 'a long passphrase!' }) {
  const onCreated = vi.fn();
  const onTokenRejected = vi.fn();
  const onNotFound = vi.fn();
  render(
    <AppThemeProvider>
      <ConsoleClientStep
        token="the-token"
        account={account}
        onCreated={onCreated}
        onTokenRejected={onTokenRejected}
        onNotFound={onNotFound}
      />
    </AppThemeProvider>,
  );
  return { onCreated, onTokenRejected, onNotFound };
}

function fill(label: RegExp, value: string): void {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

function check(): void {
  fireEvent.click(
    screen.getByRole('checkbox', { name: 'Register the management console’s client now' }),
  );
}

function fillConsole(
  url = 'https://console.example.test',
  jwks: string = JSON.stringify(PUBLIC_JWKS),
): void {
  check();
  fill(/^Console URL/, url);
  fill(/^Public JWK Set/, jwks);
}

function submit(): void {
  fireEvent.click(screen.getByRole('button', { name: 'Create the root administrator' }));
}

const CREATED_ROOT = { tenantId: 't', principalId: 'p', userName: 'root' };
const CREATED: CreateRootResult = { kind: 'created', root: CREATED_ROOT };

describe('ConsoleClientStep', () => {
  beforeEach(() => {
    vi.mocked(createRoot).mockReset();
  });

  it('offers the registration unchecked and shows no console fields', () => {
    renderStep();

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      'Register the management console',
    );
    expect(
      screen.getByRole('checkbox', { name: 'Register the management console’s client now' }),
    ).not.toBeChecked();
    expect(screen.queryByLabelText(/^Client ID/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^Console URL/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^Public JWK Set/)).not.toBeInTheDocument();
  });

  it('shows the console fields with the client ID prefilled when checked', () => {
    renderStep();

    check();

    expect(screen.getByLabelText(/^Client ID/)).toHaveValue('mgmt-console');
    expect(screen.getByLabelText(/^Console URL/)).toHaveValue('');
    expect(screen.getByLabelText(/^Public JWK Set/)).toHaveValue('');

    check();

    expect(screen.queryByLabelText(/^Console URL/)).not.toBeInTheDocument();
  });

  it('sends no console when unchecked', async () => {
    vi.mocked(createRoot).mockResolvedValue(CREATED);
    const { onCreated } = renderStep();

    submit();

    await waitFor(() => {
      expect(onCreated).toHaveBeenCalledExactlyOnceWith(CREATED_ROOT);
    });
    expect(createRoot).toHaveBeenCalledExactlyOnceWith({
      token: 'the-token',
      userName: 'root',
      password: 'a long passphrase!',
    });
  });

  it('sends the account’s email when it has one', async () => {
    vi.mocked(createRoot).mockResolvedValue(CREATED);
    const { onCreated } = renderStep({
      userName: 'root',
      email: 'root@example.com',
      password: 'a long passphrase!',
    });

    submit();

    await waitFor(() => {
      expect(onCreated).toHaveBeenCalled();
    });
    expect(createRoot).toHaveBeenCalledExactlyOnceWith({
      token: 'the-token',
      userName: 'root',
      email: 'root@example.com',
      password: 'a long passphrase!',
    });
  });

  it('sends the console when checked', async () => {
    vi.mocked(createRoot).mockResolvedValue(CREATED);
    const { onCreated } = renderStep();

    fillConsole('  https://console.example.test ');
    fill(/^Client ID/, '  my-console ');
    submit();

    await waitFor(() => {
      expect(onCreated).toHaveBeenCalled();
    });
    expect(createRoot).toHaveBeenCalledExactlyOnceWith({
      token: 'the-token',
      userName: 'root',
      password: 'a long passphrase!',
      managementConsole: {
        clientId: 'my-console',
        baseUrl: 'https://console.example.test',
        jwks: PUBLIC_JWKS,
      },
    });
  });

  it('omits a blank client ID so the instance applies its default', async () => {
    vi.mocked(createRoot).mockResolvedValue(CREATED);
    renderStep();

    fillConsole();
    fill(/^Client ID/, '   ');
    submit();

    await waitFor(() => {
      expect(createRoot).toHaveBeenCalled();
    });
    expect(createRoot).toHaveBeenCalledExactlyOnceWith({
      token: 'the-token',
      userName: 'root',
      password: 'a long passphrase!',
      managementConsole: { baseUrl: 'https://console.example.test', jwks: PUBLIC_JWKS },
    });
  });

  it.each([
    ['text that is not JSON', 'not json'],
    ['JSON null', 'null'],
    ['a JSON array', '[]'],
    ['a set without keys', '{}'],
    ['a set with an empty keys array', '{"keys":[]}'],
  ])('refuses %s as the JWK Set', (_name, jwks) => {
    renderStep();

    fillConsole('https://console.example.test', jwks);
    submit();

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Paste the console’s public JWK Set: a JSON object with a non-empty keys array.',
    );
    expect(createRoot).not.toHaveBeenCalled();
  });

  it('refuses a JWK Set that carries a private key', () => {
    renderStep();

    fillConsole(
      'https://console.example.test',
      JSON.stringify({ keys: [{ ...PUBLIC_JWKS.keys[0], d: 'private' }] }),
    );
    submit();

    expect(screen.getByRole('alert')).toHaveTextContent('Paste the console’s public keys only.');
    expect(createRoot).not.toHaveBeenCalled();
  });

  it('refuses a console URL that does not start with https://', () => {
    renderStep();

    fillConsole('http://console.example.test');
    submit();

    expect(screen.getByRole('alert')).toHaveTextContent(
      'The console URL must start with https://.',
    );
    expect(createRoot).not.toHaveBeenCalled();
  });

  it('shows the problem detail the instance gives and enables another try', async () => {
    vi.mocked(createRoot).mockResolvedValue({
      kind: 'invalid',
      problem: { title: 'Bad', detail: 'The password is too common.' },
    });
    renderStep();

    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent('The password is too common.');
    expect(screen.getByRole('button', { name: 'Create the root administrator' })).toBeEnabled();
  });

  it('falls back to the problem title when the problem has no detail', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'invalid', problem: { title: 'Bad request' } });
    renderStep();

    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent('Bad request');
  });

  it('falls back to a general message when the problem has neither detail nor title', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'invalid', problem: {} });
    renderStep();

    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The root administrator was not created.',
    );
  });

  it('asks to wait when the instance rate-limits the request', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'rateLimited', problem: {} });
    renderStep();

    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Too many attempts. Wait a minute, then try again.',
    );
  });

  it('shows a general error when the request fails', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'failed' });
    renderStep();

    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The instance could not create the root administrator. Try again.',
    );
  });

  it('maps 403 to the token-rejected callback', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'forbidden', problem: {} });
    const { onTokenRejected } = renderStep();

    submit();

    await waitFor(() => {
      expect(onTokenRejected).toHaveBeenCalledOnce();
    });
  });

  it('reports an instance that is already set up', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'notFound' });
    const { onNotFound } = renderStep();

    submit();

    await waitFor(() => {
      expect(onNotFound).toHaveBeenCalledOnce();
    });
  });

  it('disables the button while the request is pending', async () => {
    let resolve: (result: CreateRootResult) => void = () => undefined;
    vi.mocked(createRoot).mockReturnValue(
      new Promise<CreateRootResult>((r) => {
        resolve = r;
      }),
    );
    const { onCreated } = renderStep();

    submit();

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Create the root administrator' })).toBeDisabled();
    });
    resolve(CREATED);
    await waitFor(() => {
      expect(onCreated).toHaveBeenCalled();
    });
  });
});
