import { describe, expect, it } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { LoginForm } from './LoginForm.js';

function renderForm(props: Parameters<typeof LoginForm>[0]) {
  return render(
    <AppThemeProvider>
      <LoginForm {...props} />
    </AppThemeProvider>,
  );
}

/** Records each submission (with its submitter) and cancels it, since jsdom does not implement form navigation. */
function captureSubmissions(form: HTMLFormElement): FormData[] {
  const submissions: FormData[] = [];
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    submissions.push(new FormData(form, (event as SubmitEvent).submitter));
  });
  return submissions;
}

function loginForm(): HTMLFormElement {
  const form = screen.getByRole('button', { name: 'Sign in' }).closest('form');
  expect(form).not.toBeNull();
  return form!;
}

describe('LoginForm', () => {
  it('renders the stable locators and a native POST form to the login action', () => {
    const { container } = renderForm({
      returnUrl: '/authorize?x=1',
      antiforgeryToken: 'the-token',
    });

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Sign in');
    expect(container.querySelector('#username')).toBe(screen.getByLabelText(/Username/));
    expect(container.querySelector('#password')).toBe(screen.getByLabelText(/Password/));
    expect(container.querySelector('#login')).toBe(screen.getByRole('button', { name: 'Sign in' }));

    const form = loginForm();
    expect(form.getAttribute('action')).toBe('/account/login');
    expect(form.getAttribute('method')).toBe('post');
  });

  it('marks both credentials as required, autofocuses the username, and masks the password', () => {
    renderForm({ returnUrl: '/', antiforgeryToken: 'the-token' });

    const username = screen.getByLabelText(/Username/);
    const password = screen.getByLabelText(/Password/);
    expect(username).toBeRequired();
    expect(username).toHaveAttribute('autocomplete', 'username');
    expect(username).toHaveFocus();
    expect(password).toBeRequired();
    expect(password).toHaveAttribute('type', 'password');
    expect(password).toHaveAttribute('autocomplete', 'current-password');
  });

  it('names the product in the heading when a product name is provided', () => {
    renderForm({ returnUrl: '/', antiforgeryToken: 'the-token', productName: 'Contoso' });

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Sign in to Contoso');
  });

  it('shows the error message in an alert when one is provided', () => {
    renderForm({
      returnUrl: '/',
      antiforgeryToken: 'the-token',
      errorMessage: 'The username or password is incorrect.',
    });

    expect(screen.getByRole('alert')).toHaveTextContent('The username or password is incorrect.');
  });

  it.each([
    ['omitted', {}],
    ['null', { errorMessage: null }],
    ['empty', { errorMessage: '' }],
  ] as const)('shows no alert when the error message is %s', (_label, extra) => {
    renderForm({ returnUrl: '/', antiforgeryToken: 'the-token', ...extra });

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('blocks submission while the required credentials are empty', () => {
    renderForm({ returnUrl: '/', antiforgeryToken: 'the-token' });
    const form = loginForm();
    const submissions = captureSubmissions(form);

    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(submissions).toHaveLength(0);
    expect(form.checkValidity()).toBe(false);
    expect(screen.getByLabelText(/Username/)).toBeInvalid();
    expect(screen.getByLabelText(/Password/)).toBeInvalid();
  });

  it('blocks submission when only the username is entered', () => {
    renderForm({ returnUrl: '/', antiforgeryToken: 'the-token' });
    const submissions = captureSubmissions(loginForm());

    fireEvent.change(screen.getByLabelText(/Username/), { target: { value: 'ada' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(submissions).toHaveLength(0);
    expect(screen.getByLabelText(/Username/)).toBeValid();
    expect(screen.getByLabelText(/Password/)).toBeInvalid();
  });

  it('submits the credentials with the return URL, antiforgery token, and login action', () => {
    renderForm({ returnUrl: '/authorize?x=1', antiforgeryToken: 'the-token' });
    const submissions = captureSubmissions(loginForm());

    fireEvent.change(screen.getByLabelText(/Username/), { target: { value: 'ada' } });
    fireEvent.change(screen.getByLabelText(/Password/), { target: { value: 's3cret' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(submissions).toHaveLength(1);
    const submitted = submissions[0]!;
    expect(submitted.get('username')).toBe('ada');
    expect(submitted.get('password')).toBe('s3cret');
    expect(submitted.get('ReturnUrl')).toBe('/authorize?x=1');
    expect(submitted.get('__RequestVerificationToken')).toBe('the-token');
    expect(submitted.get('login')).toBe('login');
  });
});
