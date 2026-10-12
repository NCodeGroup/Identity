import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { AppThemeProvider } from '@ncode/ui';
import { resolveRoute, type RouteContext } from './routes.js';

const baseContext: RouteContext = {
  pathname: '/account/login',
  returnUrl: '/authorize',
  antiforgeryToken: 'tok',
};

function renderRoute(context: RouteContext) {
  return render(<AppThemeProvider>{resolveRoute(context)}</AppThemeProvider>);
}

describe('resolveRoute', () => {
  it('renders the account surface for the account path', () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/account' });
    expect(container.querySelector('#logout')).not.toBeNull();
  });

  it('renders the consent surface for the consent path', () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/account/consent' });
    expect(container.querySelector('#consent-allow')).not.toBeNull();
  });

  it('falls back to the login surface for any unmatched path', () => {
    const { container } = renderRoute({ ...baseContext, pathname: '/anything/else' });
    expect(container.querySelector('#login')).not.toBeNull();
  });
});
