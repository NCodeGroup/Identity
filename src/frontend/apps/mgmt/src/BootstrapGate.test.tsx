import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { useTheme } from '@mui/material/styles';
import { createAppStore, runtimeConfigLoaded } from '@ncode/store';
import { createAppTheme } from '@ncode/ui';
import type { RuntimeConfig } from '@ncode/config';
import { bootstrapRuntimeConfig } from './bootstrap.js';
import { BootstrapGate } from './BootstrapGate.js';

vi.mock('./bootstrap.js', () => ({ bootstrapRuntimeConfig: vi.fn() }));

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
};

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((onResolve) => {
    resolve = onResolve;
  });
  return { promise, resolve };
}

/** Renders the active MUI theme's mode and primary color, so the test can observe the applied branding. */
function ThemeProbe() {
  const theme = useTheme();
  return (
    <div data-testid="theme-probe">{`${theme.palette.mode} ${theme.palette.primary.main}`}</div>
  );
}

function renderGate(store = createAppStore()) {
  render(
    <Provider store={store}>
      <BootstrapGate>
        <ThemeProbe />
      </BootstrapGate>
    </Provider>,
  );
  return store;
}

describe('BootstrapGate', () => {
  beforeEach(() => {
    vi.mocked(bootstrapRuntimeConfig).mockReset();
  });

  it('shows a spinner until discovery resolves, then renders the children themed by the branding', async () => {
    const discovery = deferred<RuntimeConfig>();
    vi.mocked(bootstrapRuntimeConfig).mockReturnValue(discovery.promise);

    const store = renderGate();

    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(screen.queryByTestId('theme-probe')).not.toBeInTheDocument();
    expect(vi.mocked(bootstrapRuntimeConfig).mock.calls[0]![0]).toBeInstanceOf(AbortSignal);

    discovery.resolve({ ...runtime, branding: { primaryColor: '#123456', mode: 'dark' } });

    expect(await screen.findByTestId('theme-probe')).toHaveTextContent('dark #123456');
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(store.getState().config.runtime?.branding).toEqual({
      primaryColor: '#123456',
      mode: 'dark',
    });
    expect(bootstrapRuntimeConfig).toHaveBeenCalledTimes(1);
  });

  it('renders the children with the default theme when the discovered config has no branding', async () => {
    vi.mocked(bootstrapRuntimeConfig).mockResolvedValue(runtime);

    renderGate();

    expect(await screen.findByTestId('theme-probe')).toHaveTextContent(
      `light ${createAppTheme().palette.primary.main}`,
    );
  });

  it('skips discovery when the runtime config is already loaded', () => {
    renderGate(createAppStore({ config: { runtime } }));

    expect(screen.getByTestId('theme-probe')).toBeInTheDocument();
    expect(bootstrapRuntimeConfig).not.toHaveBeenCalled();
  });

  it('surfaces the discovery error message when discovery fails', async () => {
    vi.mocked(bootstrapRuntimeConfig).mockRejectedValue(
      new Error('The bootstrap document is invalid.'),
    );

    renderGate();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Configuration error: The bootstrap document is invalid.',
    );
    expect(screen.queryByTestId('theme-probe')).not.toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('falls back to a generic message when discovery rejects with a non-Error value', async () => {
    vi.mocked(bootstrapRuntimeConfig).mockRejectedValue('offline');

    renderGate();

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Configuration error: Failed to load configuration.',
    );
  });

  describe('abort', () => {
    let signal: AbortSignal | undefined;
    let rejected: boolean;

    beforeEach(() => {
      signal = undefined;
      rejected = false;
      // A discovery that never resolves on its own and rejects as soon as it is aborted, like a real fetch.
      vi.mocked(bootstrapRuntimeConfig).mockImplementation((given) => {
        signal = given;
        return new Promise<RuntimeConfig>((_resolve, reject) => {
          given?.addEventListener('abort', () => {
            rejected = true;
            reject(new DOMException('The operation was aborted.', 'AbortError'));
          });
        });
      });
    });

    it('aborts the in-flight discovery on unmount', async () => {
      const { unmount } = render(
        <Provider store={createAppStore()}>
          <BootstrapGate>
            <ThemeProbe />
          </BootstrapGate>
        </Provider>,
      );
      expect(signal?.aborted).toBe(false);

      unmount();

      expect(signal?.aborted).toBe(true);
      await waitFor(() => expect(rejected).toBe(true));
    });

    it('does not surface the abort as an error when the config arrives through another path', async () => {
      const store = renderGate();
      expect(screen.getByRole('progressbar')).toBeInTheDocument();

      act(() => {
        store.dispatch(runtimeConfigLoaded(runtime));
      });

      expect(screen.getByTestId('theme-probe')).toBeInTheDocument();
      expect(signal?.aborted).toBe(true);
      await waitFor(() => expect(rejected).toBe(true));
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(screen.getByTestId('theme-probe')).toBeInTheDocument();
    });
  });
});
