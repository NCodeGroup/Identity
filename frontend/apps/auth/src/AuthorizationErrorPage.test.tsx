import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { AuthorizationErrorPage } from './AuthorizationErrorPage.js';

function renderPage(props: Parameters<typeof AuthorizationErrorPage>[0]) {
  return render(
    <AppThemeProvider>
      <AuthorizationErrorPage {...props} />
    </AppThemeProvider>,
  );
}

describe('AuthorizationErrorPage', () => {
  it('shows the error code with the stable automation contract', () => {
    const { container } = renderPage({
      code: 'invalid_client',
      description: 'The client is not registered.',
    });

    const error = container.querySelector('#authorization-error');
    expect(error).toHaveAttribute('data-error-code', 'invalid_client');
    expect(error).toHaveAttribute('role', 'alert');
    expect(error).toHaveTextContent('invalid_client');
    expect(container.querySelector('#authorization-error-description')).toHaveTextContent(
      'The client is not registered.',
    );
  });

  it('omits the description element when the host gave none', () => {
    const { container } = renderPage({ code: 'invalid_client', description: null });

    expect(container.querySelector('#authorization-error')).toBeInTheDocument();
    expect(container.querySelector('#authorization-error-description')).toBeNull();
  });
});
