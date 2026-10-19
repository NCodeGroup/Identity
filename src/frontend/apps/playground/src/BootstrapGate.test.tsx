import { StrictMode, type ReactNode } from 'react';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { useTheme } from '@mui/material/styles';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createAppStore, runtimeConfigLoaded, type AppStore } from '@ncode/store';
import type { RuntimeConfig } from '@ncode/config';
import { BootstrapGate } from './BootstrapGate.js';
import { bootstrapRuntimeConfig } from './bootstrap.js';

vi.mock('./bootstrap.js', () => ({ bootstrapRuntimeConfig: vi.fn() }));

const bootstrapMock = vi.mocked(bootstrapRuntimeConfig);

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://op.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  clientId: 'playground-spa',
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
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

/** Surfaces the theme the gate applied so a test can observe whether branding reached it. */
function ThemeProbe() {
  const theme = useTheme();
  return (
    <div
      data-testid="theme-probe"
      data-mode={theme.palette.mode}
      data-primary={theme.palette.primary.main}
    >
      explorer
    </div>
  );
}

function renderGate(store: AppStore = createAppStore(), wrap = (node: ReactNode) => node) {
  const view = render(
    wrap(
      <Provider store={store}>
        <BootstrapGate>
          <ThemeProbe />
        </BootstrapGate>
      </Provider>,
    ),
  );
  return { ...view, store };
}

afterEach(() => {
  vi.resetAllMocks();
});

describe('BootstrapGate', () => {
  it('shows a spinner until the config loads, then stores it and renders the children', async () => {
    const load = deferred<RuntimeConfig>();
    bootstrapMock.mockReturnValue(load.promise);

    const { store } = renderGate();

    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(screen.queryByTestId('theme-probe')).not.toBeInTheDocument();
    expect(bootstrapMock).toHaveBeenCalledOnce();
    expect(bootstrapMock.mock.calls[0]![0]).toBeInstanceOf(AbortSignal);

    load.resolve(runtime);

    expect(await screen.findByTestId('theme-probe')).toHaveTextContent('explorer');
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(store.getState().config.runtime).toEqual(runtime);
  });

  it('themes the children with the discovered branding', async () => {
    bootstrapMock.mockResolvedValue({
      ...runtime,
      branding: { primaryColor: '#123456', mode: 'dark' },
    });

    renderGate();

    const probe = await screen.findByTestId('theme-probe');
    expect(probe).toHaveAttribute('data-mode', 'dark');
    expect(probe).toHaveAttribute('data-primary', '#123456');
  });

  it('applies the default theme when no branding is discovered', async () => {
    bootstrapMock.mockResolvedValue(runtime);

    renderGate();

    const probe = await screen.findByTestId('theme-probe');
    expect(probe).toHaveAttribute('data-mode', 'light');
    expect(probe).not.toHaveAttribute('data-primary', '#123456');
  });

  it('renders the children without loading when the config is already present', () => {
    const store = createAppStore();
    store.dispatch(runtimeConfigLoaded(runtime));

    renderGate(store);

    expect(screen.getByTestId('theme-probe')).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(bootstrapMock).not.toHaveBeenCalled();
  });

  it('shows the failure message when the config cannot be loaded', async () => {
    bootstrapMock.mockRejectedValue(new Error('bootstrap.json returned HTTP 404'));

    renderGate();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Configuration error: bootstrap.json returned HTTP 404',
    );
    expect(screen.queryByTestId('theme-probe')).not.toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('shows a generic message when the load rejects with a non-Error value', async () => {
    bootstrapMock.mockRejectedValue('offline');

    renderGate();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Configuration error: Failed to load configuration.',
    );
  });

  it('aborts the in-flight load when unmounted', () => {
    bootstrapMock.mockReturnValue(deferred<RuntimeConfig>().promise);

    const { unmount } = renderGate();
    const signal = bootstrapMock.mock.calls[0]![0]!;
    expect(signal.aborted).toBe(false);

    unmount();

    expect(signal.aborted).toBe(true);
  });

  it('ignores the failure of a load aborted by a StrictMode remount', async () => {
    // A fetch rejects once its signal aborts; the gate must not surface that as a configuration error.
    bootstrapMock
      .mockImplementationOnce(
        (signal) =>
          new Promise((_resolve, reject) => {
            signal?.addEventListener('abort', () =>
              reject(new Error('The operation was aborted.')),
            );
          }),
      )
      .mockResolvedValueOnce(runtime);

    renderGate(createAppStore(), (node) => <StrictMode>{node}</StrictMode>);

    expect(await screen.findByTestId('theme-probe')).toBeInTheDocument();
    expect(bootstrapMock).toHaveBeenCalledTimes(2);
    expect(bootstrapMock.mock.calls[0]![0]!.aborted).toBe(true);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
