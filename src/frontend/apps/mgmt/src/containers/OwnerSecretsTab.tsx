import type {
  CreateRemoteSecretRequest,
  ImportRemoteSecretRequest,
  SecretOwner,
  RotateSecretRequest,
} from '@ncode/api';
import {
  useCreateRemoteSecretMutation,
  useCreateSecretMutation,
  useDeleteSecretMutation,
  useImportRemoteSecretMutation,
  useLazyGetSecretPublicKeyQuery,
  useListSecretsQuery,
  useRotateSecretMutation,
} from '@ncode/store';
import { SecretsManager } from './SecretsManager.js';

export type OwnerSecretsTabProps = {
  readonly owner: SecretOwner;
  /** The owner noun used in headings and empty/warning copy (e.g. "client", "tenant", "resource server"). */
  readonly ownerLabel: string;
};

/**
 * Container: the secrets of any secret owner (a tenant, or a client or resource server in a scope; ADR-1257). The platform and a
 * team hold settings only,
 * wiring the owner-generic secret endpoints to the shared {@link SecretsManager}. Secrets honor the one-time
 * reveal (ADR-1050). The rotate action is offered only for a client owner, and creating and binding a KMS-held key only
 * for a tenant owner.
 */
export function OwnerSecretsTab({ owner, ownerLabel }: OwnerSecretsTabProps) {
  const { data, isLoading, error } = useListSecretsQuery(owner);
  const [createSecret, { isLoading: isCreating }] = useCreateSecretMutation();
  const [deleteSecret, { isLoading: isDeleting }] = useDeleteSecretMutation();
  const [rotateSecret, { isLoading: isRotating }] = useRotateSecretMutation();
  const [createRemoteSecret, { isLoading: isCreatingRemote }] = useCreateRemoteSecretMutation();
  const [importRemoteSecret, { isLoading: isBinding }] = useImportRemoteSecretMutation();
  const [getPublicKey] = useLazyGetSecretPublicKeyQuery();
  // Only a client's shared secrets rotate in one call.
  const clientOwner = owner.kind === 'client' ? owner : undefined;
  // Only a tenant's signing keys are held in a key management service.
  const tenantOwner = owner.kind === 'tenant' ? owner : undefined;

  return (
    <SecretsManager
      ownerLabel={ownerLabel}
      secrets={data?.secrets ?? []}
      isLoading={isLoading}
      loadError={Boolean(error)}
      isCreating={isCreating || isCreatingRemote}
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
      {...(tenantOwner
        ? {
            onCreateRemote: (body: CreateRemoteSecretRequest) =>
              createRemoteSecret({ tenantId: tenantOwner.tenantId, body }).unwrap(),
            onImportRemote: (body: ImportRemoteSecretRequest) =>
              importRemoteSecret({ tenantId: tenantOwner.tenantId, body }).unwrap(),
            isBinding,
          }
        : {})}
    />
  );
}
