import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { RootAccountStep } from './RootAccountStep.js';
import { createRoot, type CreateRootResult } from './setupApi.js';

vi.mock('./setupApi.js', () => ({ createRoot: vi.fn(), getStatus: vi.fn() }));

function renderStep() {
  const onCreated = vi.fn();
  const onTokenRejected = vi.fn();
  const onNotFound = vi.fn();
  render(
    <AppThemeProvider>
      <RootAccountStep
        token="the-token"
        minimumPasswordLength={15}
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

function fillAccount(password = 'a long passphrase!', confirmation = password): void {
  fill(/^User name/, '  root  ');
  fill(/^Password/, password);
  fill(/^Confirm password/, confirmation);
}

function submit(): void {
  fireEvent.click(screen.getByRole('button', { name: 'Create the root administrator' }));
}

const CREATED: CreateRootResult = {
  kind: 'created',
  root: { tenantId: 't', principalId: 'p', userName: 'root' },
};

describe('RootAccountStep', () => {
  beforeEach(() => {
    vi.mocked(createRoot).mockReset();
  });

  it('renders the account fields with their autocomplete hints', () => {
    renderStep();

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(
      'Create the root administrator',
    );
    const userName = screen.getByLabelText(/^User name/);
    expect(userName).toHaveAttribute('autocomplete', 'username');
    expect(userName).toBeRequired();
    expect(screen.getByLabelText(/^Email/)).not.toBeRequired();
    const password = screen.getByLabelText(/^Password/);
    expect(password).toHaveAttribute('type', 'password');
    expect(password).toHaveAttribute('autocomplete', 'new-password');
    expect(password).toHaveAccessibleDescription('Use at least 15 characters.');
    const confirm = screen.getByLabelText(/^Confirm password/);
    expect(confirm).toHaveAttribute('type', 'password');
    expect(confirm).toHaveAttribute('autocomplete', 'new-password');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('refuses a password shorter than the minimum without calling the instance', () => {
    renderStep();

    fillAccount('too short', 'too short');
    submit();

    expect(screen.getByRole('alert')).toHaveTextContent(
      'The password must have at least 15 characters.',
    );
    expect(createRoot).not.toHaveBeenCalled();
  });

  it('refuses a confirmation that differs without calling the instance', () => {
    renderStep();

    fillAccount('a long passphrase!', 'another long passphrase!');
    submit();

    expect(screen.getByRole('alert')).toHaveTextContent(
      'The password and its confirmation do not match.',
    );
    expect(createRoot).not.toHaveBeenCalled();
  });

  it('creates the root with the token and the trimmed user name, and omits a blank email', async () => {
    vi.mocked(createRoot).mockResolvedValue(CREATED);
    const { onCreated } = renderStep();

    fillAccount();
    submit();

    await waitFor(() => {
      expect(onCreated).toHaveBeenCalledExactlyOnceWith('root');
    });
    expect(createRoot).toHaveBeenCalledExactlyOnceWith({
      token: 'the-token',
      userName: 'root',
      password: 'a long passphrase!',
    });
  });

  it('sends the trimmed email when one is given', async () => {
    vi.mocked(createRoot).mockResolvedValue(CREATED);
    const { onCreated } = renderStep();

    fillAccount();
    fill(/^Email/, '  root@example.com ');
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

  it('shows the problem detail the instance gives for an invalid account and enables another try', async () => {
    vi.mocked(createRoot).mockResolvedValue({
      kind: 'invalid',
      problem: { title: 'Bad', detail: 'The password is too common.' },
    });
    renderStep();

    fillAccount();
    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent('The password is too common.');
    expect(screen.getByRole('button', { name: 'Create the root administrator' })).toBeEnabled();
  });

  it('falls back to the problem title when the problem has no detail', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'invalid', problem: { title: 'Bad request' } });
    renderStep();

    fillAccount();
    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent('Bad request');
  });

  it('falls back to a general message when the problem has neither detail nor title', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'invalid', problem: {} });
    renderStep();

    fillAccount();
    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The root administrator was not created.',
    );
  });

  it('asks to wait when the instance rate-limits the request', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'rateLimited', problem: {} });
    renderStep();

    fillAccount();
    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Too many attempts. Wait a minute, then try again.',
    );
  });

  it('shows a general error when the request fails', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'failed' });
    renderStep();

    fillAccount();
    submit();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The instance could not create the root administrator. Try again.',
    );
  });

  it('reports a refused token', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'forbidden', problem: {} });
    const { onTokenRejected } = renderStep();

    fillAccount();
    submit();

    await waitFor(() => {
      expect(onTokenRejected).toHaveBeenCalledOnce();
    });
  });

  it('reports an instance that is already set up', async () => {
    vi.mocked(createRoot).mockResolvedValue({ kind: 'notFound' });
    const { onNotFound } = renderStep();

    fillAccount();
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

    fillAccount();
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
