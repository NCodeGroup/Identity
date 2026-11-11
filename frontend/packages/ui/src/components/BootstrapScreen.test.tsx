import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { BootstrapScreen } from './BootstrapScreen.js';

describe('BootstrapScreen', () => {
  it('shows a labeled progress indicator while the configuration loads', () => {
    render(<BootstrapScreen />);

    expect(screen.getByRole('progressbar', { name: 'Loading configuration' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows why the configuration could not be loaded instead of the progress indicator', () => {
    render(<BootstrapScreen error="The bootstrap document is invalid." />);

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Configuration error: The bootstrap document is invalid.',
    );
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('treats an empty error as still loading', () => {
    render(<BootstrapScreen error="" />);

    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
