import { useMemo, useState } from 'react';
import { DataStateBoundary, SettingsEditor } from '@ncode/ui';
import type {
  EffectiveSettingsResource,
  SettingLimitsResource,
  SettingsResource,
} from '@ncode/api';
import { apiErrorMessage, type ApiError } from '@ncode/store';

export type SettingsManagerProps = {
  readonly settings: SettingsResource | undefined;
  readonly effective: EffectiveSettingsResource | undefined;
  /** The limits the settings may carry and whether the caller may change them; undefined until loaded. */
  readonly limits: SettingLimitsResource | undefined;
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
 * Container-agnostic settings surface shared by every settings owner (the platform, a team, a tenant, a client or a resource server). Settings are
 * an opaque JSON object edited against a read-only effective (merged) view; the save is a top-level JSON
 * Patch diff the container issues. The owner-specific RTK Query data and mutation are injected so the same
 * UI serves each owner's nested path. Owns only transient save-error state.
 */
export function SettingsManager({
  settings,
  effective,
  limits,
  isLoading,
  loadError,
  isSaving,
  onSave,
}: SettingsManagerProps) {
  const [saveError, setSaveError] = useState<string>();

  const isLocked = limits !== undefined && !limits.canChange && limits.names.length > 0;
  const { locked, editable } = useMemo(() => {
    const lockedEntries: Record<string, unknown> = {};
    const editableEntries: Record<string, unknown> = {};
    const limitNames = new Set(limits?.names ?? []);
    for (const [key, value] of Object.entries(settings?.settings ?? {})) {
      (limitNames.has(key) ? lockedEntries : editableEntries)[key] = value;
    }
    return { locked: lockedEntries, editable: editableEntries };
  }, [settings?.settings, limits?.names]);

  const initialJson = useMemo(
    () => JSON.stringify(isLocked ? editable : (settings?.settings ?? {}), null, 2),
    [isLocked, editable, settings?.settings],
  );
  const lockedJson = useMemo(() => JSON.stringify(locked, null, 2), [locked]);
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
        desired: isLocked ? { ...desired, ...locked } : desired,
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
        limitNames={limits?.names ?? []}
        lockedJson={isLocked ? lockedJson : undefined}
        isSaving={isSaving}
        error={saveError}
        onSave={handleSave}
      />
    </DataStateBoundary>
  );
}
