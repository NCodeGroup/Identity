import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Fragment, StrictMode, type ComponentType, type ReactNode } from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { createAppStore, sessionEnded, sessionStarted } from '@ncode/store';
import type { BffRuntimeConfig, OpenIdRuntimeConfig } from '@ncode/config';
import { SessionGate } from './SessionGate.js';
import { SESSION_CHANNEL_NAME, SESSION_ENDED_MESSAGE } from './session.js';

const runtime: BffRuntimeConfig = {
  kind: 'bff',
  apiBaseUrl: '/management',
  bff: { basePath: '/bff', antiforgeryHeader: { name: 'X-CSRF', value: '1' } },
  features: {},
};

const openIdRuntime: OpenIdRuntimeConfig = {
  kind: 'openid',
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

const NOW = new Date('2030-01-01T00:00:00Z');
const NOW_SECONDS = NOW.getTime() / 1000;

function userBody(secondsUntilExpiry = 3600) {
  return {
    sub: 'admin',
    name: 'Ada Admin',
    claims: { email: 'ada@example.test' },
    session: {
      expiresAt: NOW_SECONDS + secondsUntilExpiry,
      idleExpiresAt: NOW_SECONDS + secondsUntilExpiry,
    },
    logoutUrl: '/bff/logout',
    logoutSid: 'guard',
  };
}

function answer(status: number, body?: unknown): Response {
  return {
    status,
    ok: status >= 200 && status < 300,
    json: () => Promise.resolve(body),
  } as Response;
}

// An in-process stand-in for BroadcastChannel: `postMessage` records the message and delivers it to the other open
// channels of the same name, and `receive` delivers a message that another tab posted without recording it.
class FakeBroadcastChannel {
  static instances: FakeBroadcastChannel[] = [];
  static posted: unknown[] = [];

  static reset() {
    FakeBroadcastChannel.instances = [];
    FakeBroadcastChannel.posted = [];
  }

  static receive(name: string, data: unknown) {
    for (const channel of FakeBroadcastChannel.instances) {
      if (channel.name === name && !channel.closed) {
        channel.onmessage?.({ data } as MessageEvent<unknown>);
      }
    }
  }

  onmessage: ((event: MessageEvent<unknown>) => void) | null = null;
  closed = false;

  constructor(readonly name: string) {
    FakeBroadcastChannel.instances.push(this);
  }

  postMessage(data: unknown) {
    FakeBroadcastChannel.posted.push(data);
  }

  close() {
    this.closed = true;
  }
}

function openChannels() {
  return FakeBroadcastChannel.instances.filter(
    (channel) => channel.name === SESSION_CHANNEL_NAME && !channel.closed,
  );
}

function renderGate(
  preloadedRuntime: BffRuntimeConfig | OpenIdRuntimeConfig | null = runtime,
  Wrapper: ComponentType<{ children: ReactNode }> = Fragment,
) {
  const store = createAppStore(preloadedRuntime ? { config: { runtime: preloadedRuntime } } : {});
  const view = render(
    <Wrapper>
      <Provider store={store}>
        <SessionGate>
          <div data-testid="protected">protected</div>
        </SessionGate>
      </Provider>
    </Wrapper>,
  );
  return { store, ...view };
}

async function settle() {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });
}

async function advance(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe('SessionGate', () => {
  const originalLocation = window.location;
  let assign: ReturnType<typeof vi.fn>;
  let fetchMock: ReturnType<typeof vi.fn>;

  function stubLocation(search = '') {
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: {
        origin: 'https://ui.test',
        href: `https://ui.test/tenants/acme/clients${search}`,
        pathname: '/tenants/acme/clients',
        search,
        assign,
      },
    });
  }

  function stubVisibility(state: DocumentVisibilityState) {
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      get: () => state,
    });
  }

  beforeEach(() => {
    vi.useFakeTimers({
      toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date'],
    });
    vi.setSystemTime(NOW);
    assign = vi.fn();
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    vi.stubGlobal('BroadcastChannel', FakeBroadcastChannel);
    FakeBroadcastChannel.reset();
    stubLocation();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    Reflect.deleteProperty(document, 'visibilityState');
    Object.defineProperty(window, 'location', { configurable: true, value: originalLocation });
  });

  describe('probing', () => {
    it('shows the busy sign-in screen while the probe is pending', () => {
      fetchMock.mockReturnValue(new Promise(() => {}));
      renderGate();

      const button = screen.getByTestId('signin-button');
      expect(button).toBeDisabled();
      expect(button).toHaveTextContent('Signing in…');
      expect(screen.queryByTestId('protected')).not.toBeInTheDocument();
    });

    it('probes the BFF user endpoint with the session cookie and the anti-forgery header', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      renderGate();
      await settle();

      expect(fetchMock).toHaveBeenCalledWith('/bff/user', {
        credentials: 'same-origin',
        cache: 'no-store',
        headers: { accept: 'application/json', 'X-CSRF': '1' },
      });
    });

    it('renders the children once the probe answers a user', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      const { store } = renderGate();
      await settle();

      expect(screen.getByTestId('protected')).toBeInTheDocument();
      expect(store.getState().session.status).toBe('signed-in');
      expect(store.getState().session.user?.sub).toBe('admin');
    });

    it('renders the children under StrictMode, whose effects run twice', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      const { store } = renderGate(runtime, StrictMode);
      await settle();

      expect(screen.getByTestId('protected')).toBeInTheDocument();
      expect(store.getState().session.status).toBe('signed-in');
      expect(openChannels()).toHaveLength(1);
    });

    it('shows the sign-in screen when the probe answers 401', async () => {
      fetchMock.mockResolvedValue(answer(401));
      const { store } = renderGate();
      await settle();

      expect(store.getState().session.status).toBe('signed-out');
      expect(screen.getByTestId('signin-button')).toBeEnabled();
      expect(screen.queryByTestId('signin-error')).not.toBeInTheDocument();
      expect(screen.queryByTestId('protected')).not.toBeInTheDocument();
      expect(FakeBroadcastChannel.posted).toEqual([]);
    });

    it('shows the probe failure message when the probe fails', async () => {
      fetchMock.mockResolvedValue(answer(500));
      renderGate();
      await settle();

      expect(screen.getByTestId('signin-error')).toHaveTextContent('Unable to check your session.');
      expect(screen.getByTestId('signin-button')).toBeEnabled();
    });

    it('names the product on the sign-in screen from the branding', async () => {
      fetchMock.mockResolvedValue(answer(401));
      renderGate({ ...runtime, branding: { displayName: 'Acme Console' } });
      await settle();

      expect(screen.getByRole('heading', { name: 'Acme Console' })).toBeInTheDocument();
    });

    it('ignores a user that answers after the gate unmounted', async () => {
      let resolve: (response: Response) => void = () => {};
      fetchMock.mockReturnValue(new Promise<Response>((r) => (resolve = r)));
      const { store, unmount } = renderGate();
      unmount();

      resolve(answer(200, userBody()));
      await settle();

      expect(store.getState().session.status).toBe('unknown');
    });

    it('ignores a failure that answers after the gate unmounted', async () => {
      let reject: (reason: unknown) => void = () => {};
      fetchMock.mockReturnValue(new Promise<Response>((_, r) => (reject = r)));
      const { store, unmount } = renderGate();
      unmount();

      reject(new Error('offline'));
      await settle();

      expect(store.getState().session.status).toBe('unknown');
    });
  });

  describe('the BFF host', () => {
    it('reports an error for an OpenID runtime and never probes', () => {
      renderGate(openIdRuntime);

      expect(
        screen.getByText(/The management console must be served by its BFF host\./),
      ).toBeInTheDocument();
      expect(fetchMock).not.toHaveBeenCalled();
    });

    it('reports an error before any runtime is loaded', () => {
      renderGate(null);

      expect(
        screen.getByText(/The management console must be served by its BFF host\./),
      ).toBeInTheDocument();
      expect(fetchMock).not.toHaveBeenCalled();
    });
  });

  describe('signing in', () => {
    it('navigates to the BFF login with the current path as the return URL', async () => {
      fetchMock.mockResolvedValue(answer(401));
      renderGate();
      await settle();

      fireEvent.click(screen.getByTestId('signin-button'));

      expect(assign).toHaveBeenCalledWith('/bff/login?returnUrl=%2Ftenants%2Facme%2Fclients');
      expect(screen.getByTestId('signin-button')).toBeDisabled();
    });

    it('shows the BFF error code and removes it from the address bar', async () => {
      stubLocation('?error=access_denied');
      const replaceState = vi.spyOn(window.history, 'replaceState');
      fetchMock.mockResolvedValue(answer(401));
      renderGate();
      await settle();

      expect(screen.getByTestId('signin-error')).toHaveTextContent(
        'Sign-in failed (access_denied).',
      );
      expect(replaceState).toHaveBeenCalledWith(null, '', '/tenants/acme/clients');
    });

    it('leaves the address bar alone without an error code', async () => {
      const replaceState = vi.spyOn(window.history, 'replaceState');
      fetchMock.mockResolvedValue(answer(401));
      renderGate();
      await settle();

      expect(replaceState).not.toHaveBeenCalled();
    });
  });

  describe('while signed in', () => {
    it('probes on the poll interval', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      renderGate();
      await settle();
      expect(fetchMock).toHaveBeenCalledTimes(1);

      await advance(59_999);
      expect(fetchMock).toHaveBeenCalledTimes(1);

      await advance(1);
      expect(fetchMock).toHaveBeenCalledTimes(2);
      expect(screen.getByTestId('protected')).toBeInTheDocument();
    });

    it('probes one second after the session expires and returns to sign-in when it ended', async () => {
      fetchMock.mockResolvedValueOnce(answer(200, userBody(30))).mockResolvedValue(answer(401));
      renderGate();
      await settle();

      await advance(30_999);
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(screen.getByTestId('protected')).toBeInTheDocument();

      await advance(1);
      expect(fetchMock).toHaveBeenCalledTimes(2);
      expect(screen.queryByTestId('protected')).not.toBeInTheDocument();
      expect(screen.getByTestId('signin-error')).toHaveTextContent(
        'Your session ended. Sign in to continue.',
      );
    });

    it('probes when a hidden tab becomes visible', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      renderGate();
      await settle();

      stubVisibility('hidden');
      await act(async () => {
        document.dispatchEvent(new Event('visibilitychange'));
      });
      expect(fetchMock).toHaveBeenCalledTimes(1);

      stubVisibility('visible');
      await act(async () => {
        document.dispatchEvent(new Event('visibilitychange'));
      });
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('stops probing and closes its channel when it unmounts', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      const { unmount } = renderGate();
      await settle();
      expect(openChannels()).toHaveLength(1);

      unmount();
      await advance(120_000);
      stubVisibility('visible');
      document.dispatchEvent(new Event('visibilitychange'));

      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(openChannels()).toHaveLength(0);
    });

    it('ends the session when another tab announces it, without announcing it again', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      const { store } = renderGate();
      await settle();

      await act(async () => {
        FakeBroadcastChannel.receive(SESSION_CHANNEL_NAME, SESSION_ENDED_MESSAGE);
      });

      expect(store.getState().session.status).toBe('signed-out');
      expect(screen.getByTestId('signin-error')).toHaveTextContent(
        'Your session ended. Sign in to continue.',
      );
      expect(FakeBroadcastChannel.posted).toEqual([]);
    });

    it('announces a session that the store ended, once', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      const { store } = renderGate();
      await settle();

      await act(async () => {
        store.dispatch(sessionEnded());
      });
      await act(async () => {
        store.dispatch(sessionEnded());
      });

      expect(FakeBroadcastChannel.posted).toEqual([SESSION_ENDED_MESSAGE]);
      expect(screen.getByTestId('signin-error')).toHaveTextContent(
        'Your session ended. Sign in to continue.',
      );
    });

    it('announces again when a later session ends in this tab after a broadcast', async () => {
      fetchMock.mockResolvedValue(answer(200, userBody()));
      const { store } = renderGate();
      await settle();

      await act(async () => {
        FakeBroadcastChannel.receive(SESSION_CHANNEL_NAME, SESSION_ENDED_MESSAGE);
      });
      expect(FakeBroadcastChannel.posted).toEqual([]);

      await act(async () => {
        store.dispatch(sessionStarted(userBody()));
      });
      await act(async () => {
        store.dispatch(sessionEnded());
      });

      expect(FakeBroadcastChannel.posted).toEqual([SESSION_ENDED_MESSAGE]);
    });

    it('prefers the sign-in error over the ended notice', async () => {
      stubLocation('?error=access_denied');
      fetchMock.mockResolvedValue(answer(200, userBody()));
      const { store } = renderGate();
      await settle();

      await act(async () => {
        store.dispatch(sessionEnded());
      });

      expect(screen.getByTestId('signin-error')).toHaveTextContent(
        'Sign-in failed (access_denied).',
      );
    });
  });
});
