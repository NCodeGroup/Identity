import { useEffect, useState, type ReactNode } from 'react';
import type { RuntimeConfigLoader } from '@ncode/config';
import { runtimeConfigLoaded, useAppDispatch, useAppSelector } from '@ncode/store';
import { AppThemeProvider, BootstrapScreen } from '@ncode/ui';

export type BootstrapGateProps = {
  /**
   * Loads this bundle's runtime configuration. Pass a stable value (created once, in the composition root): a new
   * loader abandons an unfinished load and starts over.
   */
  readonly load: RuntimeConfigLoader;
  readonly children: ReactNode;
};

/** A load failure, kept with the loader that produced it so a new loader starts a fresh attempt. */
type Failure = {
  readonly load: RuntimeConfigLoader;
  readonly message: string;
};

/**
 * Stateful boot gate shared by every surface that discovers its runtime configuration. Until the configuration is in
 * the store there is no API base URL or issuer, so nothing downstream can render meaningfully: the gate loads it once,
 * stores it, and renders the children themed by its branding. The loading and error states render before any branding
 * is known, so they use the default theme. Configuration that reaches the store any other way wins over a load in
 * flight or a failed one.
 */
export function BootstrapGate({ load, children }: BootstrapGateProps) {
  const dispatch = useAppDispatch();
  const runtime = useAppSelector((state) => state.config.runtime);
  const [failure, setFailure] = useState<Failure | null>(null);

  useEffect(() => {
    if (runtime) {
      return;
    }
    const controller = new AbortController();
    // A load that ignores its signal can still settle after it was abandoned; only the current one may report.
    load(controller.signal)
      .then((config) => {
        if (!controller.signal.aborted) {
          dispatch(runtimeConfigLoaded(config));
        }
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) {
          setFailure({
            load,
            message:
              cause instanceof Error && cause.message
                ? cause.message
                : 'Failed to load configuration.',
          });
        }
      });
    return () => controller.abort();
  }, [dispatch, load, runtime]);

  if (runtime) {
    return (
      <AppThemeProvider {...(runtime.branding ? { branding: runtime.branding } : {})}>
        {children}
      </AppThemeProvider>
    );
  }

  const error = failure?.load === load ? failure.message : undefined;
  return (
    <AppThemeProvider>
      <BootstrapScreen error={error} />
    </AppThemeProvider>
  );
}
