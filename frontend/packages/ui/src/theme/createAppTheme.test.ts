import { describe, expect, it } from 'vitest';
import { createTheme } from '@mui/material/styles';
import { createAppTheme } from './createAppTheme.js';

const muiDefaults = createTheme();

describe('createAppTheme', () => {
  it('builds a light theme with the default palette when no branding is supplied', () => {
    const theme = createAppTheme();

    expect(theme.palette.mode).toBe('light');
    expect(theme.palette.primary.main).toBe(muiDefaults.palette.primary.main);
    expect(theme.palette.secondary.main).toBe(muiDefaults.palette.secondary.main);
  });

  it('applies the shared shape and typography regardless of branding', () => {
    for (const theme of [
      createAppTheme(),
      createAppTheme({ mode: 'dark', primaryColor: '#123456' }),
    ]) {
      expect(theme.shape.borderRadius).toBe(8);
      expect(theme.typography.fontFamily).toBe('Inter,Roboto,Helvetica,Arial,sans-serif');
    }
  });

  it('applies the branded mode and colors', () => {
    const theme = createAppTheme({
      mode: 'dark',
      primaryColor: '#123456',
      secondaryColor: '#abcdef',
    });

    expect(theme.palette.mode).toBe('dark');
    expect(theme.palette.primary.main).toBe('#123456');
    expect(theme.palette.secondary.main).toBe('#abcdef');
  });

  it('keeps the default colors for branding that omits or blanks them', () => {
    const omitted = createAppTheme({ displayName: 'Acme' });
    const blank = createAppTheme({ primaryColor: '', secondaryColor: '' });

    for (const theme of [omitted, blank]) {
      expect(theme.palette.mode).toBe('light');
      expect(theme.palette.primary.main).toBe(muiDefaults.palette.primary.main);
      expect(theme.palette.secondary.main).toBe(muiDefaults.palette.secondary.main);
    }
  });

  it('brands only the primary color when no secondary color is supplied', () => {
    const theme = createAppTheme({ primaryColor: '#123456' });

    expect(theme.palette.primary.main).toBe('#123456');
    expect(theme.palette.secondary.main).toBe(muiDefaults.palette.secondary.main);
  });
});
