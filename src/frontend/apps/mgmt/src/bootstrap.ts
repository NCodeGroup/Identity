import { loadRuntimeConfig, type RuntimeConfig } from '@ncode/config';

/** Resolves the origin that serves this bundle and its bootstrap document. */
export function currentOrigin(): string {
  return window.location.origin;
}

/**
 * Loads the tier-agnostic runtime configuration for this bundle. Thin wrapper over
 * {@link loadRuntimeConfig} that binds the browser origin and this surface identity, kept separate from
 * React so the boot sequence is testable.
 */
export function bootstrapRuntimeConfig(signal?: AbortSignal): Promise<RuntimeConfig> {
  return loadRuntimeConfig({
    origin: currentOrigin(),
    surface: 'mgmt',
    ...(signal ? { signal } : {}),
  });
}
