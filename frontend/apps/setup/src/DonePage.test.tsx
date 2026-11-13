import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { DonePage, FIRST_INSTANCE_DOCS_URL } from './DonePage.js';

describe('DonePage', () => {
  it('names the root administrator and lists what remains', () => {
    render(
      <AppThemeProvider>
        <DonePage userName="root" />
      </AppThemeProvider>,
    );

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('The instance is set up');
    expect(screen.getByText('root')).toBeInTheDocument();
    expect(screen.getByText(/The setup token is spent\./)).toBeInTheDocument();
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Sign in to the management console as the root administrator.',
      'Register the management console client.',
      'Configure the tenants and their grants.',
      'Remove the setup token Secret.',
    ]);
  });

  it('links to the guide to the first use of a Production instance', () => {
    render(
      <AppThemeProvider>
        <DonePage userName="root" />
      </AppThemeProvider>,
    );

    const link = screen.getByRole('link', {
      name: 'Read the guide to the first use of a Production instance',
    });
    expect(link).toHaveAttribute(
      'href',
      'https://github.com/NCodeGroup/identity/blob/main/docs/first-production-instance.md',
    );
    expect(link).toHaveAttribute('href', FIRST_INSTANCE_DOCS_URL);
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });
});
