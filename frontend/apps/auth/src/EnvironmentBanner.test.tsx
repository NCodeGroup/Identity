import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { App } from './App.js';
import { EnvironmentBanner } from './EnvironmentBanner.js';

describe('EnvironmentBanner', () => {
  it('shows the environment name when present', () => {
    const { getByText } = render(
      <EnvironmentBanner environment={{ type: 'staging', name: 'Pre-prod' }} />,
    );

    expect(getByText('Pre-prod environment — not for production use')).toBeInTheDocument();
  });

  it('falls back to the environment type without a name', () => {
    const { getByText } = render(<EnvironmentBanner environment={{ type: 'staging' }} />);

    expect(getByText('staging environment — not for production use')).toBeInTheDocument();
  });
});

describe('App environment banner', () => {
  it('shows the banner for a tenant with a non-production environment', () => {
    const { getByText } = render(
      <AppThemeProvider>
        <App
          pathname="/account/login"
          search=""
          cookie=""
          branding={{ environment: { type: 'development' } }}
        />
      </AppThemeProvider>,
    );

    expect(getByText('development environment — not for production use')).toBeInTheDocument();
  });
});
