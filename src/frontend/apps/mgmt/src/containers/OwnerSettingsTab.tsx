import type { ManagementOwner } from '@ncode/api';
import {
  useGetEffectiveSettingsQuery,
  useGetSettingsQuery,
  useUpdateSettingsMutation,
} from '@ncode/store';
import { SettingsManager } from './SettingsManager.js';

export type OwnerSettingsTabProps = {
  readonly owner: ManagementOwner;
};

/**
 * Container: the settings of any owner (a server, a tenant, or a client or resource server in a scope; ADR-0257),
 * wiring the owner-generic settings endpoints to the shared {@link SettingsManager}. Settings are a top-level
 * JSON Patch diff.
 */
export function OwnerSettingsTab({ owner }: OwnerSettingsTabProps) {
  const { data: settings, isLoading, error } = useGetSettingsQuery(owner);
  const { data: effective } = useGetEffectiveSettingsQuery(owner);
  const [updateSettings, { isLoading: isSaving }] = useUpdateSettingsMutation();

  return (
    <SettingsManager
      settings={settings}
      effective={effective}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isSaving={isSaving}
      onSave={({ concurrencyToken, original, desired }) =>
        updateSettings({ owner, concurrencyToken, original, desired }).unwrap()
      }
    />
  );
}
