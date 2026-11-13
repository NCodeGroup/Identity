import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { TokenStep, type TokenStepProps } from './TokenStep.js';

function renderStep(props: Partial<TokenStepProps> = {}) {
  const onContinue = vi.fn<TokenStepProps['onContinue']>().mockResolvedValue(undefined);
  render(
    <AppThemeProvider>
      <TokenStep tokenSource="configured" initialToken="" onContinue={onContinue} {...props} />
    </AppThemeProvider>,
  );
  return { onContinue };
}

describe('TokenStep', () => {
  it('asks for the token as a password field and disables continuing while it is blank', () => {
    renderStep();

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Set up NCode.Identity');
    const field = screen.getByLabelText(/^Setup token/);
    expect(field).toHaveAttribute('type', 'password');
    expect(field).toHaveAttribute('autocomplete', 'off');
    expect(field).toBeRequired();
    expect(field).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('explains a configured token', () => {
    renderStep({ tokenSource: 'configured' });

    expect(screen.getByText(/Setup__Token/)).toBeInTheDocument();
  });

  it('explains a generated token', () => {
    renderStep({ tokenSource: 'generated' });

    expect(screen.getByText(/startup log of one replica/)).toBeInTheDocument();
    expect(screen.getByText(/expires one hour/)).toBeInTheDocument();
  });

  it('prefills the token the URL fragment carried', () => {
    renderStep({ initialToken: 'from-fragment' });

    expect(screen.getByLabelText(/^Setup token/)).toHaveValue('from-fragment');
    expect(screen.getByRole('button', { name: 'Continue' })).toBeEnabled();
  });

  it('continues with the trimmed token the operator typed', async () => {
    const { onContinue } = renderStep();

    fireEvent.change(screen.getByLabelText(/^Setup token/), {
      target: { value: '  typed-token  ' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(onContinue).toHaveBeenCalledExactlyOnceWith('typed-token');
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Continue' })).toBeEnabled();
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('disables continuing and shows progress while the token is checked', async () => {
    let resolve: (message: string | undefined) => void = () => undefined;
    const onContinue = vi.fn(
      () =>
        new Promise<string | undefined>((r) => {
          resolve = r;
        }),
    );
    renderStep({ initialToken: 'a-token', onContinue });

    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(onContinue).toHaveBeenCalledOnce();
    expect(screen.getByRole('button', { name: /Continue/ })).toBeDisabled();
    expect(screen.getByRole('progressbar', { name: 'Checking the token' })).toBeInTheDocument();

    resolve(undefined);
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Continue' })).toBeEnabled();
    });
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('shows the message of a rejected token and stays on the step', async () => {
    renderStep({
      initialToken: 'wrong-token',
      onContinue: vi.fn().mockResolvedValue('The instance did not accept the setup token.'),
    });

    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The instance did not accept the setup token.',
    );
    expect(screen.getByLabelText(/^Setup token/)).toHaveValue('wrong-token');
    expect(screen.getByRole('button', { name: 'Continue' })).toBeEnabled();
  });

  it('clears the message when the next check is accepted', async () => {
    renderStep({
      initialToken: 'a-token',
      onContinue: vi
        .fn()
        .mockResolvedValueOnce('The instance did not accept the setup token.')
        .mockResolvedValueOnce(undefined),
    });

    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    await screen.findByRole('alert');
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    await waitFor(() => {
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });

  it('keeps continue disabled for a token of only blanks', () => {
    renderStep();

    fireEvent.change(screen.getByLabelText(/^Setup token/), { target: { value: '   ' } });

    expect(screen.getByRole('button', { name: 'Continue' })).toBeDisabled();
  });

  it('shows the error message in an alert', () => {
    renderStep({ errorMessage: 'The instance did not accept the setup token.' });

    expect(screen.getByRole('alert')).toHaveTextContent(
      'The instance did not accept the setup token.',
    );
  });
});
