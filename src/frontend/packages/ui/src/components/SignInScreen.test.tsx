import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SignInScreen } from './SignInScreen.js';

describe('SignInScreen', () => {
  it('invokes onSignIn when the button is clicked', () => {
    const onSignIn = vi.fn();
    render(<SignInScreen onSignIn={onSignIn} />);
    fireEvent.click(screen.getByTestId('signin-button'));
    expect(onSignIn).toHaveBeenCalledOnce();
  });

  it('disables the button and shows busy text when busy', () => {
    render(<SignInScreen onSignIn={vi.fn()} isBusy />);
    expect(screen.getByTestId('signin-button')).toBeDisabled();
    expect(screen.getByText('Signing in…')).toBeInTheDocument();
  });

  it('renders an error when provided', () => {
    render(<SignInScreen onSignIn={vi.fn()} error="nope" />);
    expect(screen.getByTestId('signin-error')).toHaveTextContent('nope');
  });
});
