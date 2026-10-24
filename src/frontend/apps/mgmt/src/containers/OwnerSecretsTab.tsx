import type { ManagementOwner, RotateSecretRequest } from '@ncode/api';
import {
  useCreateSecretMutation,
  useDeleteSecretMutation,
  useLazyGetSecretPublicKeyQuery,
  useListSecretsQuery,
  useRotateSecretMutation,
} from '@ncode/store';
import { SecretsManager } from './SecretsManager.js';

export type OwnerSecretsTabProps = {
  readonly owner: ManagementOwner;
  /** The owner noun used in headings and empty/warning copy (e.g. "client", "tenant", "server"). */
  readonly ownerLabel: string;
};

/**
 * Container: the secrets of any owner (a server, a tenant, or a client or resource server in a scope; ADR-0257),
 * wiring the owner-generic secret endpoints to the shared {@link SecretsManager}. Secrets honor the one-time
 * reveal (ADR-0050). The rotate action is offered only for a client owner.
 */
export function OwnerSecretsTab({ owner, ownerLabel }: OwnerSecretsTabProps) {
  const { data, isLoading, error } = useListSecretsQuery(owner);
  const [createSecret, { isLoading: isCreating }] = useCreateSecretMutation();
  const [deleteSecret, { isLoading: isDeleting }] = useDeleteSecretMutation();
  const [rotateSecret, { isLoading: isRotating }] = useRotateSecretMutation();
  const [getPublicKey] = useLazyGetSecretPublicKeyQuery();
  // Only a client's shared secrets rotate in one call.
  const clientOwner = owner.kind === 'client' ? owner : undefined;

  return (
    <SecretsManager
      ownerLabel={ownerLabel}
      secrets={data?.secrets ?? []}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isCreating={isCreating}
      isDeleting={isDeleting}
      onCreate={(body) => createSecret({ owner, body }).unwrap()}
      onDelete={(secretId) => deleteSecret({ owner, secretId }).unwrap()}
      onExportPublicKey={(secretId) => getPublicKey({ owner, secretId }).unwrap()}
      {...(clientOwner
        ? {
            onRotate: (secretId: string, body: RotateSecretRequest) =>
              rotateSecret({ owner: clientOwner, secretId, body }).unwrap(),
            isRotating,
          }
        : {})}
    />
  );
}
