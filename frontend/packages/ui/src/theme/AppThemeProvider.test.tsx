import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { createTheme, useTheme } from '@mui/material/styles';
import { AppThemeProvider } from './AppThemeProvider.js';

const muiDefaults = createTheme();

/** Renders the theme values its descendants receive, so the test can observe what the provider supplies. */
function ThemeProbe() {
  const theme = useTheme();
  return (
    <dl>
      <dt>mode</dt>
      <dd data-testid="probe-mode">{theme.palette.mode}</dd>
      <dt>primary</dt>
      <dd data-testid="probe-primary">{theme.palette.primary.main}</dd>
      <dt>radius</dt>
      <dd data-testid="probe-radius">{String(theme.shape.borderRadius)}</dd>
    </dl>
  );
}

describe('AppThemeProvider', () => {
  it('renders its children under the default application theme', () => {
    render(
      <AppThemeProvider>
        <ThemeProbe />
      </AppThemeProvider>,
    );

    expect(screen.getByTestId('probe-mode')).toHaveTextContent('light');
    expect(screen.getByTestId('probe-primary')).toHaveTextContent(muiDefaults.palette.primary.main);
    expect(screen.getByTestId('probe-radius')).toHaveTextContent('8');
  });

  it('derives the theme from the runtime branding', () => {
    render(
      <AppThemeProvider branding={{ mode: 'dark', primaryColor: '#123456' }}>
        <ThemeProbe />
      </AppThemeProvider>,
    );

    expect(screen.getByTestId('probe-mode')).toHaveTextContent('dark');
    expect(screen.getByTestId('probe-primary')).toHaveTextContent('#123456');
  });

  it('applies the CssBaseline global styles from the branded theme', () => {
    render(
      <AppThemeProvider branding={{ mode: 'dark' }}>
        <ThemeProbe />
      </AppThemeProvider>,
    );

    // The dark palette's default background (#121212), applied to <body> by CssBaseline.
    expect(getComputedStyle(document.body).backgroundColor).toBe('rgb(18, 18, 18)');
  });
});
