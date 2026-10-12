import { render, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ConsentForm } from './ConsentForm.js';

describe('ConsentForm', () => {
  it('renders the requested scopes from the injected loader', async () => {
    const { container, findByText } = render(
      <ConsentForm
        returnUrl="/continue?state=abc"
        antiforgeryToken="tok"
        loadDetails={async () => ({ clientId: 'test-client', scopes: ['openid', 'profile'] })}
      />,
    );

    expect(await findByText('openid')).toBeInTheDocument();
    expect(await findByText('profile')).toBeInTheDocument();
    await waitFor(() =>
      expect(container.querySelector('[data-testid="consent-scopes"]')).not.toBeNull(),
    );
    expect(container.querySelector('#consent-allow')).not.toBeNull();
    expect(container.querySelector('#consent-deny')).not.toBeNull();
  });

  it('still renders the decision controls when no details resolve', async () => {
    const { container } = render(
      <ConsentForm
        returnUrl="/continue?state=abc"
        antiforgeryToken="tok"
        loadDetails={async () => null}
      />,
    );

    await waitFor(() => expect(container.querySelector('#consent-allow')).not.toBeNull());
    expect(container.querySelector('[data-testid="consent-scopes"]')).toBeNull();
  });
});
