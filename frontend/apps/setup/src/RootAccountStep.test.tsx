import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { RootAccountStep } from './RootAccountStep.js';

function renderStep() {
  const onContinue = vi.fn();
  render(
    <AppThemeProvider>
      <RootAccountStep minimumPasswordLength={15} onContinue={onContinue} />
    </AppThemeProvider>,
  );
  return { onContinue };
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
  fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
}

describe('RootAccountStep', () => {
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

  it('refuses a password shorter than the minimum without continuing', () => {
    const { onContinue } = renderStep();

    fillAccount('too short', 'too short');
    submit();

    expect(screen.getByRole('alert')).toHaveTextContent(
      'The password must have at least 15 characters.',
    );
    expect(onContinue).not.toHaveBeenCalled();
  });

  it('refuses a confirmation that differs without continuing', () => {
    const { onContinue } = renderStep();

    fillAccount('a long passphrase!', 'another long passphrase!');
    submit();

    expect(screen.getByRole('alert')).toHaveTextContent(
      'The password and its confirmation do not match.',
    );
    expect(onContinue).not.toHaveBeenCalled();
  });

  it('continues with the trimmed user name and omits a blank email', () => {
    const { onContinue } = renderStep();

    fillAccount();
    submit();

    expect(onContinue).toHaveBeenCalledExactlyOnceWith({
      userName: 'root',
      password: 'a long passphrase!',
    });
  });

  it('continues with the trimmed email when one is given and clears an earlier error', () => {
    const { onContinue } = renderStep();

    fillAccount('too short', 'too short');
    submit();
    expect(screen.getByRole('alert')).toBeInTheDocument();

    fillAccount();
    fill(/^Email/, '  root@example.com ');
    submit();

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(onContinue).toHaveBeenCalledExactlyOnceWith({
      userName: 'root',
      email: 'root@example.com',
      password: 'a long passphrase!',
    });
  });
});
