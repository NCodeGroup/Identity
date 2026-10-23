import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { PasswordPage } from './PasswordPage.js';

function renderPage(props: Parameters<typeof PasswordPage>[0]) {
  return render(
    <AppThemeProvider>
      <PasswordPage {...props} />
    </AppThemeProvider>,
  );
}

describe('PasswordPage', () => {
  it('renders a native POST form with the three password inputs and the antiforgery token', () => {
    const { container } = renderPage({ antiforgeryToken: 'tok' });

    const form = container.querySelector('form');
    expect(form?.getAttribute('action')).toBe('/account/password');
    expect(form?.getAttribute('method')).toBe('post');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Change password');

    const current = screen.getByLabelText(/Current password/);
    expect(current).toHaveAttribute('id', 'current_password');
    expect(current).toHaveAttribute('name', 'current_password');
    expect(current).toHaveAttribute('type', 'password');
    expect(current).toHaveAttribute('autocomplete', 'current-password');
    expect(current).toBeRequired();

    const next = screen.getByLabelText(/^New password/);
    expect(next).toHaveAttribute('id', 'new_password');
    expect(next).toHaveAttribute('name', 'new_password');
    expect(next).toHaveAttribute('type', 'password');
    expect(next).toHaveAttribute('autocomplete', 'new-password');
    expect(next).toBeRequired();

    const confirm = screen.getByLabelText(/Confirm new password/);
    expect(confirm).toHaveAttribute('id', 'confirm_password');
    expect(confirm).toHaveAttribute('name', 'confirm_password');
    expect(confirm).toHaveAttribute('type', 'password');
    expect(confirm).toHaveAttribute('autocomplete', 'new-password');
    expect(confirm).toBeRequired();

    const token = container.querySelector(
      'input[name="__RequestVerificationToken"]',
    ) as HTMLInputElement | null;
    expect(token?.type).toBe('hidden');
    expect(token?.value).toBe('tok');

    expect(container.querySelector('#change-password')).toBe(
      screen.getByRole('button', { name: 'Change password' }),
    );
    expect(screen.getByRole('link', { name: 'Back to your account' })).toHaveAttribute(
      'href',
      '/account/',
    );
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByTestId('password-changed')).not.toBeInTheDocument();
  });

  it('confirms the change when the password was changed', () => {
    renderPage({ antiforgeryToken: 'tok', changed: true });

    expect(screen.getByTestId('password-changed')).toHaveTextContent(
      'Your password was changed. Your other sessions were signed out.',
    );
  });

  it('shows the error message in an alert when one is provided', () => {
    renderPage({ antiforgeryToken: 'tok', errorMessage: 'The current password is incorrect.' });

    expect(screen.getByRole('alert')).toHaveTextContent('The current password is incorrect.');
    expect(screen.queryByTestId('password-changed')).not.toBeInTheDocument();
  });

  it('titles the page with the product name when one is provided', () => {
    renderPage({ antiforgeryToken: 'tok', productName: 'Acme' });

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Acme — change password');
  });
});
