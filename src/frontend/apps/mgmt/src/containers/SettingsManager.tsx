import { useMemo, useState } from 'react';
import { DataStateBoundary, SettingsEditor } from '@ncode/ui';
import type { EffectiveSettingsResource, SettingsResource } from '@ncode/api';
import { apiErrorMessage, type ApiError } from '@ncode/store';

export type SettingsManagerProps = {
  readonly settings: SettingsResource | undefined;
  readonly effective: EffectiveSettingsResource | undefined;
  readonly isLoading: boolean;
  readonly loadError: boolean;
  readonly isSaving: boolean;
  /**
   * Persists the desired settings. The manager supplies the owner-level concurrency token and the original
   * object so the container can issue a top-level add/remove/replace JSON Patch diff; it must reject with an
   * {@link ApiError}-shaped error on failure.
   */
  readonly onSave: (args: {
    readonly concurrencyToken: string;
    readonly original: Readonly<Record<string, unknown>>;
    readonly desired: Readonly<Record<string, unknown>>;
  }) => Promise<void>;
};

/**
 * Container-agnostic settings surface shared by every settings owner (client, tenant, server). Settings are
 * an opaque JSON object edited against a read-only effective (merged) view; the save is a top-level JSON
 * Patch diff the container issues. The owner-specific RTK Query data and mutation are injected so the same
 * UI serves each owner's nested path. Owns only transient save-error state.
 */
export function SettingsManager({
  settings,
  effective,
  isLoading,
  loadError,
  isSaving,
  onSave,
}: SettingsManagerProps) {
  const [saveError, setSaveError] = useState<string>();

  const initialJson = useMemo(
    () => JSON.stringify(settings?.settings ?? {}, null, 2),
    [settings?.settings],
  );
  const effectiveJson = useMemo(
    () => JSON.stringify(effective?.settings ?? {}, null, 2),
    [effective?.settings],
  );

  const handleSave = async (desired: Record<string, unknown>) => {
    if (!settings) {
      return;
    }
    setSaveError(undefined);
    try {
      await onSave({
        concurrencyToken: settings.concurrencyToken,
        original: settings.settings,
        desired,
      });
    } catch (submitError) {
      setSaveError(apiErrorMessage(submitError as ApiError));
    }
  };

  return (
    <DataStateBoundary
      isLoading={isLoading}
      error={loadError ? 'Failed to load settings.' : undefined}
    >
      <SettingsEditor
        initialJson={initialJson}
        effectiveJson={effectiveJson}
        isSaving={isSaving}
        error={saveError}
        onSave={handleSave}
      />
    </DataStateBoundary>
  );
}
