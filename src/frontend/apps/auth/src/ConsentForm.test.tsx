import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ConsentForm, type ConsentDetails } from './ConsentForm.js';

const details: ConsentDetails = {
  clientId: 'test-client',
  clientName: 'Test App',
  scopes: [
    { value: 'openid', description: 'Sign you in.', granted: true },
    { value: 'profile', description: null, granted: false },
  ],
};

/** Records each submission (with its submitter) and cancels it, since jsdom does not implement form navigation. */
function captureSubmissions(form: HTMLFormElement): FormData[] {
  const submissions: FormData[] = [];
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    submissions.push(new FormData(form, (event as SubmitEvent).submitter));
  });
  return submissions;
}

function deferred<T>() {
  let resolve: (value: T) => void = () => {};
  const promise = new Promise<T>((settle) => {
    resolve = settle;
  });
  return { promise, resolve };
}

describe('ConsentForm', () => {
  it('renders the requested scopes with per-scope checkboxes from the injected loader', async () => {
    const { container, findByText } = render(
      <ConsentForm
        returnUrl="/continue?state=abc"
        antiforgeryToken="tok"
        loadDetails={async () => details}
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

  it('captions a described scope with its raw value and shows an undescribed scope once', async () => {
    render(
      <ConsentForm
        returnUrl="/continue"
        antiforgeryToken="tok"
        loadDetails={async () => details}
      />,
    );

    // The described scope shows its description as the label and its raw value as a caption.
    expect(await screen.findByText('Sign you in.')).toBeInTheDocument();
    expect(screen.getByText('openid')).toBeInTheDocument();
    // The undescribed scope falls back to its raw value, without a duplicate caption.
    expect(screen.getAllByText('profile')).toHaveLength(1);
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

  it('prefers the product name over the client display name', async () => {
    const loadDetails = vi.fn(async () => details);
    render(
      <ConsentForm
        returnUrl="/continue"
        antiforgeryToken="tok"
        productName="Fabrikam"
        loadDetails={loadDetails}
      />,
    );

    expect(await screen.findByText('Sign you in.')).toBeInTheDocument();
    expect(screen.getByText(/Fabrikam is requesting access/)).toBeInTheDocument();
    expect(screen.queryByText(/Test App/)).not.toBeInTheDocument();
    expect(loadDetails).toHaveBeenCalledWith('/continue');
  });

  it('falls back to the client id when the client has no display name', async () => {
    render(
      <ConsentForm
        returnUrl="/continue"
        antiforgeryToken="tok"
        loadDetails={async () => ({ clientId: 'test-client', clientName: null, scopes: [] })}
      />,
    );

    expect(await screen.findByText(/test-client is requesting access/)).toBeInTheDocument();
    expect(screen.queryByTestId('consent-scopes')).not.toBeInTheDocument();
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
    expect(screen.getByText(/An application is requesting access/)).toBeInTheDocument();
  });

  it('keeps the decision controls without a scope list when the loader fails', async () => {
    const loadDetails = vi.fn(() => Promise.reject(new Error('network down')));
    render(<ConsentForm returnUrl="/continue" antiforgeryToken="tok" loadDetails={loadDetails} />);

    await waitFor(() => expect(loadDetails).toHaveBeenCalledTimes(1));
    await act(async () => {});
    expect(screen.queryByTestId('consent-scopes')).not.toBeInTheDocument();
    expect(screen.getByText(/An application is requesting access/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Allow' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Deny' })).toBeEnabled();
  });

  it('ignores a stale load when the return URL changes before it resolves', async () => {
    const stale = deferred<ConsentDetails | null>();
    const loadDetails = vi.fn((returnUrl: string) =>
      returnUrl === '/first'
        ? stale.promise
        : Promise.resolve<ConsentDetails>({
            clientId: 'second-client',
            clientName: 'Second App',
            scopes: [{ value: 'email', description: 'Read your email.', granted: false }],
          }),
    );
    const { rerender } = render(
      <ConsentForm returnUrl="/first" antiforgeryToken="tok" loadDetails={loadDetails} />,
    );
    rerender(<ConsentForm returnUrl="/second" antiforgeryToken="tok" loadDetails={loadDetails} />);

    expect(await screen.findByText('Read your email.')).toBeInTheDocument();
    await act(async () => {
      stale.resolve({
        clientId: 'first-client',
        clientName: 'First App',
        scopes: [{ value: 'phone', description: 'Read your phone.', granted: false }],
      });
    });

    expect(loadDetails.mock.calls.map(([returnUrl]) => returnUrl)).toEqual(['/first', '/second']);
    expect(screen.getByText(/Second App is requesting access/)).toBeInTheDocument();
    expect(screen.queryByText('Read your phone.')).not.toBeInTheDocument();
  });

  describe('decision submission', () => {
    async function renderLoaded() {
      const { container } = render(
        <ConsentForm
          returnUrl="/continue?state=abc"
          antiforgeryToken="tok"
          loadDetails={async () => details}
        />,
      );
      await screen.findByTestId('consent-scopes');
      const form = container.querySelector('form');
      expect(form?.getAttribute('action')).toBe('/account/consent');
      expect(form?.getAttribute('method')).toBe('post');
      return captureSubmissions(form!);
    }

    it('allows with every requested scope by default', async () => {
      const submissions = await renderLoaded();

      fireEvent.click(screen.getByRole('button', { name: 'Allow' }));

      expect(submissions).toHaveLength(1);
      const submitted = submissions[0]!;
      expect(submitted.get('action')).toBe('allow');
      expect(submitted.getAll('scope')).toEqual(['openid', 'profile']);
      expect(submitted.get('ReturnUrl')).toBe('/continue?state=abc');
      expect(submitted.get('__RequestVerificationToken')).toBe('tok');
    });

    it('allows with only the scopes left selected after the end-user down-scopes', async () => {
      const submissions = await renderLoaded();
      const profile = screen.getByRole('checkbox', { name: 'profile' });

      fireEvent.click(profile);
      expect(profile).not.toBeChecked();
      fireEvent.click(screen.getByRole('button', { name: 'Allow' }));

      expect(submissions).toHaveLength(1);
      expect(submissions[0]!.get('action')).toBe('allow');
      expect(submissions[0]!.getAll('scope')).toEqual(['openid']);
    });

    it('denies with the deny action, the return URL, and the antiforgery token', async () => {
      const submissions = await renderLoaded();

      fireEvent.click(screen.getByRole('button', { name: 'Deny' }));

      expect(submissions).toHaveLength(1);
      const submitted = submissions[0]!;
      expect(submitted.get('action')).toBe('deny');
      expect(submitted.get('ReturnUrl')).toBe('/continue?state=abc');
      expect(submitted.get('__RequestVerificationToken')).toBe('tok');
    });
  });

  describe('default loader', () => {
    afterEach(() => {
      vi.unstubAllGlobals();
    });

    it('fetches the consent details for the encoded return URL and renders its scopes', async () => {
      const fetchMock = vi.fn(() =>
        Promise.resolve(
          new Response(JSON.stringify(details), {
            status: 200,
            headers: { 'content-type': 'application/json' },
          }),
        ),
      );
      vi.stubGlobal('fetch', fetchMock);

      render(<ConsentForm returnUrl="/continue?state=abc" antiforgeryToken="tok" />);

      expect(await screen.findByText('Sign you in.')).toBeInTheDocument();
      expect(screen.getByText(/Test App is requesting access/)).toBeInTheDocument();
      expect(fetchMock).toHaveBeenCalledWith(
        '/account/consent/details?ReturnUrl=%2Fcontinue%3Fstate%3Dabc',
        { headers: { accept: 'application/json' } },
      );
    });

    it('renders no scope list when the details request fails', async () => {
      const response = new Response('not found', { status: 404 });
      const json = vi.spyOn(response, 'json');
      const fetchMock = vi.fn(() => Promise.resolve(response));
      vi.stubGlobal('fetch', fetchMock);

      render(<ConsentForm returnUrl="/continue" antiforgeryToken="tok" />);

      await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
      await act(async () => {});
      expect(json).not.toHaveBeenCalled();
      expect(screen.queryByTestId('consent-scopes')).not.toBeInTheDocument();
      expect(screen.getByText(/An application is requesting access/)).toBeInTheDocument();
    });

    it('renders the decision controls without details when fetch is unavailable', async () => {
      vi.stubGlobal('fetch', undefined);

      render(<ConsentForm returnUrl="/continue" antiforgeryToken="tok" />);
      await act(async () => {});

      expect(screen.queryByTestId('consent-scopes')).not.toBeInTheDocument();
      expect(screen.getByText(/An application is requesting access/)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Allow' })).toBeInTheDocument();
    });
  });
});
