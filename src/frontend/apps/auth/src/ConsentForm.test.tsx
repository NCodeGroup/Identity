import { render, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ConsentForm } from './ConsentForm.js';

describe('ConsentForm', () => {
  it('renders the requested scopes with per-scope checkboxes from the injected loader', async () => {
    const { container, findByText } = render(
      <ConsentForm
        returnUrl="/continue?state=abc"
        antiforgeryToken="tok"
        loadDetails={async () => ({
          clientId: 'test-client',
          clientName: 'Test App',
          scopes: [
            { value: 'openid', description: 'Sign you in.', granted: true },
            { value: 'profile', description: null, granted: false },
          ],
        })}
      />,
    );

    expect(await findByText('Sign you in.')).toBeInTheDocument();
    expect(await findByText('profile')).toBeInTheDocument();
    await waitFor(() =>
      expect(container.querySelector('[data-testid="consent-scopes"]')).not.toBeNull(),
    );

    const openidCheckbox = container.querySelector<HTMLInputElement>(
      'input[name="scope"][value="openid"]',
    );
    const profileCheckbox = container.querySelector<HTMLInputElement>(
      'input[name="scope"][value="profile"]',
    );
    expect(openidCheckbox).not.toBeNull();
    expect(profileCheckbox).not.toBeNull();
    // A consent screen pre-selects every requested scope so Allow authorizes the request.
    expect(openidCheckbox?.checked).toBe(true);
    expect(profileCheckbox?.checked).toBe(true);

    expect(container.querySelector('#consent-allow')).not.toBeNull();
    expect(container.querySelector('#consent-deny')).not.toBeNull();
  });

  it('shows the client display name when no product name is provided', async () => {
    const { findByText } = render(
      <ConsentForm
        returnUrl="/continue?state=abc"
        antiforgeryToken="tok"
        loadDetails={async () => ({ clientId: 'test-client', clientName: 'Contoso', scopes: [] })}
      />,
    );

    expect(await findByText(/Contoso is requesting access/)).toBeInTheDocument();
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
