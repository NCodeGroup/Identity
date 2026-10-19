import { StrictMode, type ReactNode } from 'react';
import { act, render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { useTheme } from '@mui/material/styles';
import { describe, expect, it, vi } from 'vitest';
import type { RuntimeConfig, RuntimeConfigLoader } from '@ncode/config';
import { createAppStore, runtimeConfigLoaded, type AppStore } from '@ncode/store';
import { createAppTheme } from '@ncode/ui';
import { BootstrapGate } from './BootstrapGate.js';

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

type Deferred<T> = {
  readonly promise: Promise<T>;
  readonly resolve: (value: T) => void;
  readonly reject: (reason: unknown) => void;
};

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((onResolve, onReject) => {
    resolve = onResolve;
    reject = onReject;
  });
  return { promise, resolve, reject };
}

/** A load that never settles on its own and rejects as soon as it is aborted, like a real fetch. */
function abortableLoad(): RuntimeConfigLoader {
  return (signal) =>
    new Promise<RuntimeConfig>((_resolve, reject) => {
      signal.addEventListener('abort', () =>
        reject(new DOMException('The operation was aborted.', 'AbortError')),
      );
    });
}

/** Surfaces the theme the gate applied, so a test can observe whether the branding reached the children. */
function ThemeProbe() {
  const theme = useTheme();
  return (
    <div
      data-testid="theme-probe"
      data-mode={theme.palette.mode}
      data-primary={theme.palette.primary.main}
    >
      app
    </div>
  );
}

function gate(store: AppStore, load: RuntimeConfigLoader) {
  return (
    <Provider store={store}>
      <BootstrapGate load={load}>
        <ThemeProbe />
      </BootstrapGate>
    </Provider>
  );
}

function renderGate(
  load: RuntimeConfigLoader,
  store: AppStore = createAppStore(),
  wrap = (node: ReactNode) => node,
) {
  const view = render(wrap(gate(store, load)));
  return {
    ...view,
    store,
    rerenderWith: (next: RuntimeConfigLoader) => view.rerender(gate(store, next)),
  };
}

describe('BootstrapGate', () => {
  describe('loading', () => {
    it('shows the loading screen until the config loads, then stores it and renders the children', async () => {
      const pending = deferred<RuntimeConfig>();
      const load = vi.fn<RuntimeConfigLoader>(() => pending.promise);

      const { store } = renderGate(load);

      expect(
        screen.getByRole('progressbar', { name: 'Loading configuration' }),
      ).toBeInTheDocument();
      expect(screen.queryByTestId('theme-probe')).not.toBeInTheDocument();
      expect(load).toHaveBeenCalledOnce();
      expect(load.mock.calls[0]![0]).toBeInstanceOf(AbortSignal);

      pending.resolve(runtime);

      expect(await screen.findByTestId('theme-probe')).toHaveTextContent('app');
      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
      expect(store.getState().config.runtime).toBe(runtime);
      expect(load).toHaveBeenCalledOnce();
    });

    it('renders the children without loading when the config is already in the store', () => {
      const load = vi.fn<RuntimeConfigLoader>();

      renderGate(load, createAppStore({ config: { runtime } }));

      expect(screen.getByTestId('theme-probe')).toBeInTheDocument();
      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
      expect(load).not.toHaveBeenCalled();
    });
  });

  describe('branding', () => {
    it('themes the children with the discovered branding', async () => {
      const load = vi.fn<RuntimeConfigLoader>(() =>
        Promise.resolve({ ...runtime, branding: { primaryColor: '#123456', mode: 'dark' } }),
      );

      renderGate(load);

      const probe = await screen.findByTestId('theme-probe');
      expect(probe).toHaveAttribute('data-mode', 'dark');
      expect(probe).toHaveAttribute('data-primary', '#123456');
    });

    it('applies the default theme when the config carries no branding', async () => {
      renderGate(() => Promise.resolve(runtime));

      const probe = await screen.findByTestId('theme-probe');
      expect(probe).toHaveAttribute('data-mode', 'light');
      expect(probe).toHaveAttribute('data-primary', createAppTheme().palette.primary.main);
    });
  });

  describe('failure', () => {
    it('shows why the config could not be loaded', async () => {
      renderGate(() => Promise.reject(new Error('The bootstrap document is invalid.')));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Configuration error: The bootstrap document is invalid.',
      );
      expect(screen.queryByTestId('theme-probe')).not.toBeInTheDocument();
      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });

    it.each([
      ['a non-Error value', 'offline'],
      ['an Error without a message', new Error('')],
    ])('shows a generic message when the load rejects with %s', async (_case, reason) => {
      renderGate(() => Promise.reject(reason));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Configuration error: Failed to load configuration.',
      );
    });

    it('starts a fresh attempt when given a new loader after a failure', async () => {
      const { rerenderWith, store } = renderGate(() => Promise.reject(new Error('offline')));
      expect(await screen.findByRole('alert')).toHaveTextContent('offline');

      const retry = deferred<RuntimeConfig>();
      rerenderWith(() => retry.promise);

      expect(screen.getByRole('progressbar')).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();

      retry.resolve(runtime);

      expect(await screen.findByTestId('theme-probe')).toBeInTheDocument();
      expect(store.getState().config.runtime).toBe(runtime);
    });

    it('renders config that reaches the store another way even after a failure', async () => {
      const { store } = renderGate(() => Promise.reject(new Error('offline')));
      expect(await screen.findByRole('alert')).toBeInTheDocument();

      act(() => {
        store.dispatch(runtimeConfigLoaded(runtime));
      });

      expect(screen.getByTestId('theme-probe')).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });

  describe('abandoning a load', () => {
    it('aborts the load in flight when unmounted', async () => {
      const load = vi.fn<RuntimeConfigLoader>(abortableLoad());

      const { unmount } = renderGate(load);
      const signal = load.mock.calls[0]![0];
      expect(signal.aborted).toBe(false);

      unmount();

      expect(signal.aborted).toBe(true);
      await expect(load.mock.results[0]!.value).rejects.toThrow('The operation was aborted.');
    });

    it('ignores the failure of a load aborted by a StrictMode remount', async () => {
      const second = deferred<RuntimeConfig>();
      const load = vi
        .fn<RuntimeConfigLoader>()
        .mockImplementationOnce(abortableLoad())
        .mockReturnValueOnce(second.promise);

      renderGate(load, createAppStore(), (node) => <StrictMode>{node}</StrictMode>);

      expect(load).toHaveBeenCalledTimes(2);
      expect(load.mock.calls[0]![0].aborted).toBe(true);
      await act(() =>
        expect(load.mock.results[0]!.value).rejects.toThrow('The operation was aborted.'),
      );
      expect(screen.getByRole('progressbar')).toBeInTheDocument();
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();

      second.resolve(runtime);

      expect(await screen.findByTestId('theme-probe')).toBeInTheDocument();
    });

    it('abandons the load in flight without an error when the config reaches the store another way', async () => {
      const load = vi.fn<RuntimeConfigLoader>(abortableLoad());
      const { store } = renderGate(load);
      expect(screen.getByRole('progressbar')).toBeInTheDocument();

      act(() => {
        store.dispatch(runtimeConfigLoaded(runtime));
      });

      expect(screen.getByTestId('theme-probe')).toBeInTheDocument();
      expect(load.mock.calls[0]![0].aborted).toBe(true);
      await expect(load.mock.results[0]!.value).rejects.toThrow('The operation was aborted.');
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('abandons an unfinished load when given a new loader', async () => {
      const first = vi.fn<RuntimeConfigLoader>(abortableLoad());
      const second = deferred<RuntimeConfig>();
      const { rerenderWith, store } = renderGate(first);

      rerenderWith(() => second.promise);

      expect(first.mock.calls[0]![0].aborted).toBe(true);
      second.resolve({ ...runtime, issuer: 'https://second.test' });

      expect(await screen.findByTestId('theme-probe')).toBeInTheDocument();
      expect(store.getState().config.runtime?.issuer).toBe('https://second.test');
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('drops an abandoned load that settles after its replacement', async () => {
      // A loader that ignores its signal: the first load outlives the one that replaced it.
      const stale = deferred<RuntimeConfig>();
      const { rerenderWith, store } = renderGate(() => stale.promise);
      const current = deferred<RuntimeConfig>();
      rerenderWith(() => current.promise);

      current.resolve(runtime);
      expect(await screen.findByTestId('theme-probe')).toBeInTheDocument();

      await act(async () => {
        stale.resolve({ ...runtime, issuer: 'https://stale.test' });
        await stale.promise;
      });

      expect(store.getState().config.runtime).toBe(runtime);
    });
  });
});
