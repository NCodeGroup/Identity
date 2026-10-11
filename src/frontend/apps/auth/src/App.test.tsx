import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { App } from './App.js';

function renderApp(props: Parameters<typeof App>[0]) {
  return render(
    <AppThemeProvider>
      <App {...props} />
    </AppThemeProvider>,
  );
}

describe('App login surface', () => {
  it('renders the login form with the stable locators and hidden fields', () => {
    const { container } = renderApp({
      pathname: '/account/login',
      search: '?ReturnUrl=%2Fauthorize%3Fx%3D1',
      cookie: 'XSRF-TOKEN=the-token',
    });

    expect(container.querySelector('#username')).not.toBeNull();
    expect(container.querySelector('#password')).not.toBeNull();
    expect(container.querySelector('#login')).not.toBeNull();

    const form = container.querySelector('form');
    expect(form?.getAttribute('action')).toBe('/account/login');
    expect(form?.getAttribute('method')).toBe('post');

    const returnUrl = container.querySelector('input[name="ReturnUrl"]') as HTMLInputElement | null;
    expect(returnUrl?.value).toBe('/authorize?x=1');
    const token = container.querySelector(
      'input[name="__RequestVerificationToken"]',
    ) as HTMLInputElement | null;
    expect(token?.value).toBe('the-token');
  });

  it('shows an error message when the error query is present', () => {
    const { getByText } = renderApp({
      pathname: '/account/login',
      search: '?error=invalid_credentials',
      cookie: '',
    });
    expect(getByText(/incorrect/i)).toBeInTheDocument();
  });
});

describe('App consent surface', () => {
  it('renders the consent form with the stable locators', () => {
    const { container } = renderApp({
      pathname: '/account/consent',
      search: '?ReturnUrl=%2Fauthorize',
      cookie: 'XSRF-TOKEN=tok',
    });

    expect(container.querySelector('#consent-allow')).not.toBeNull();
    expect(container.querySelector('#consent-deny')).not.toBeNull();
    expect(container.querySelector('form')?.getAttribute('action')).toBe('/account/consent');
  });
});
