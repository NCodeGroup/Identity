import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { LogoutConfirmPage } from './LogoutConfirmPage.js';

function renderPage(props: Parameters<typeof LogoutConfirmPage>[0]) {
  return render(
    <AppThemeProvider>
      <LogoutConfirmPage {...props} />
    </AppThemeProvider>,
  );
}

describe('LogoutConfirmPage', () => {
  it('renders a native POST form carrying the return URL and the antiforgery token', () => {
    const { container } = renderPage({
      returnUrl: '/oauth2/continue?state=k',
      antiforgeryToken: 'tok',
    });

    const form = container.querySelector('form');
    expect(form?.getAttribute('action')).toBe('/account/logout/confirm');
    expect(form?.getAttribute('method')).toBe('post');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Sign out');
    expect(screen.getByText('Do you want to sign out?')).toBeInTheDocument();

    const returnUrl = container.querySelector('input[name="ReturnUrl"]') as HTMLInputElement | null;
    expect(returnUrl?.type).toBe('hidden');
    expect(returnUrl?.value).toBe('/oauth2/continue?state=k');

    const token = container.querySelector(
      'input[name="__RequestVerificationToken"]',
    ) as HTMLInputElement | null;
    expect(token?.type).toBe('hidden');
    expect(token?.value).toBe('tok');
  });

  it('renders the stable confirm and cancel buttons with their action values', () => {
    const { container } = renderPage({ returnUrl: '/continue', antiforgeryToken: 'tok' });

    const confirm = container.querySelector('#logout-confirm');
    expect(confirm).toBe(screen.getByRole('button', { name: 'Sign out' }));
    expect(confirm).toHaveAttribute('name', 'action');
    expect(confirm).toHaveAttribute('value', 'logout');
    expect(confirm).toHaveAttribute('type', 'submit');

    const cancel = container.querySelector('#logout-cancel');
    expect(cancel).toBe(screen.getByRole('button', { name: 'Stay signed in' }));
    expect(cancel).toHaveAttribute('name', 'action');
    expect(cancel).toHaveAttribute('value', 'cancel');
    expect(cancel).toHaveAttribute('type', 'submit');
  });

  it('names the product in the question when one is provided', () => {
    renderPage({ returnUrl: '/continue', antiforgeryToken: 'tok', productName: 'Acme' });

    expect(screen.getByText('Do you want to sign out of Acme?')).toBeInTheDocument();
  });
});
