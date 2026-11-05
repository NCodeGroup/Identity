import { useState } from 'react';
import type { CreatedSecretResource, ManagementOwner } from '@ncode/api';
import {
  useGetEffectiveSettingsQuery,
  useGetSettingsQuery,
  useUpdateSettingsMutation,
} from '@ncode/store';
import { RevealSecretDialog } from '@ncode/ui';
import { SettingsManager } from './SettingsManager.js';

export type OwnerSettingsTabProps = {
  readonly owner: ManagementOwner;
};

// See ADR-1050 (secret material is revealed once), ADR-1257 (owner-generic settings) and ADR-1391 (a client's secrets
// follow its method).
/**
 * Container: the settings of any owner (the platform, a team, a tenant, or a client or resource server in a scope),
 * wiring the owner-generic settings endpoints to the shared {@link SettingsManager}. Settings are a top-level
 * JSON Patch diff. A client's settings change can issue the secret its client-authentication method needs; the
 * response carries its material once, which the tab reveals in the one-time dialog.
 */
export function OwnerSettingsTab({ owner }: OwnerSettingsTabProps) {
  const { data: settings, isLoading, error } = useGetSettingsQuery(owner);
  const { data: effective } = useGetEffectiveSettingsQuery(owner);
  const [updateSettings, { isLoading: isSaving }] = useUpdateSettingsMutation();
  const [issued, setIssued] = useState<CreatedSecretResource | null>(null);

  return (
    <>
      <SettingsManager
        settings={settings}
        effective={effective}
        isLoading={isLoading}
        loadError={Boolean(error)}
        isSaving={isSaving}
        onSave={async ({ concurrencyToken, original, desired }) => {
          const issuedSecret = await updateSettings({
            owner,
            concurrencyToken,
            original,
            desired,
          }).unwrap();
          setIssued(issuedSecret ?? null);
        }}
      />
      <RevealSecretDialog
        open={issued !== null}
        secretId={issued?.secretId ?? ''}
        secretMaterial={issued?.secretMaterial ?? null}
        onClose={() => setIssued(null)}
      />
    </>
  );
}
