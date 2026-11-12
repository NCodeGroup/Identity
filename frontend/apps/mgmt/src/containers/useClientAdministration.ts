import { useState } from 'react';
import { emptyClientFormValues, type ClientFormValues } from '@ncode/ui';
import type { ClientResource } from '@ncode/api';
import {
  apiErrorMessage,
  useDeleteClientMutation,
  useUpdateClientMutation,
  type ApiError,
} from '@ncode/store';

type DialogState =
  | { readonly kind: 'none' }
  | { readonly kind: 'create' }
  | { readonly kind: 'edit'; readonly client: ClientResource }
  | { readonly kind: 'delete'; readonly client: ClientResource };

const closed: DialogState = { kind: 'none' };

export type UseClientAdministrationOptions = {
  /** The clients the page lists; a row's id resolves to one of these. */
  readonly clients: readonly ClientResource[];
  /** Creates a client from the submitted form values; the one call that differs between pages. */
  readonly createClient: (values: ClientFormValues) => Promise<unknown>;
};

/**
 * Hook: the administration both client pages share — the create/edit form dialog, the delete
 * confirmation, their error state, and the drill-in selection. A client's edit and delete address
 * the client through its own `tenantId` (ADR-1257), so a machine client reaches the control-plane
 * tenant's client routes whichever scope lists it (ADR-1466). Only the create call differs between
 * pages, so the page passes it in.
 */
export function useClientAdministration({ clients, createClient }: UseClientAdministrationOptions) {
  const [updateClient] = useUpdateClientMutation();
  const [deleteClient, { isLoading: isDeleting }] = useDeleteClientMutation();

  const [dialog, setDialog] = useState<DialogState>(closed);
  const [formError, setFormError] = useState<string>();
  const [deleteError, setDeleteError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [selectedId, setSelectedId] = useState<string>();
  const selected = clients.find((c) => c.clientId === selectedId);

  const openCreate = () => {
    setFormError(undefined);
    setDialog({ kind: 'create' });
  };
  const openEdit = (clientId: string) => {
    const client = clients.find((c) => c.clientId === clientId);
    if (!client) {
      return;
    }
    setFormError(undefined);
    setDialog({ kind: 'edit', client });
  };
  const openDelete = (clientId: string) => {
    const client = clients.find((c) => c.clientId === clientId);
    if (!client) {
      return;
    }
    setDeleteError(undefined);
    setDialog({ kind: 'delete', client });
  };
  const close = () => setDialog(closed);

  const formOpen = dialog.kind === 'create' || dialog.kind === 'edit';
  const formInitial: ClientFormValues =
    dialog.kind === 'edit' ? { isDisabled: dialog.client.isDisabled } : emptyClientFormValues;
  const isEditing = dialog.kind === 'edit';
  const deleteTarget = dialog.kind === 'delete' ? dialog.client : undefined;

  const handleSubmit = async (values: ClientFormValues) => {
    setFormError(undefined);
    setIsSubmitting(true);
    try {
      if (dialog.kind === 'create') {
        await createClient(values);
      } else if (dialog.kind === 'edit') {
        await updateClient({
          tenantId: dialog.client.tenantId,
          clientId: dialog.client.clientId,
          concurrencyToken: dialog.client.concurrencyToken,
          fields: { isDisabled: values.isDisabled },
        }).unwrap();
      }
      setDialog(closed);
    } catch (submitError) {
      setFormError(apiErrorMessage(submitError as ApiError));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async () => {
    if (dialog.kind !== 'delete') {
      return;
    }
    setDeleteError(undefined);
    try {
      await deleteClient({
        tenantId: dialog.client.tenantId,
        clientId: dialog.client.clientId,
      }).unwrap();
      setDialog(closed);
    } catch (removeError) {
      setDeleteError(apiErrorMessage(removeError as ApiError));
    }
  };

  return {
    selected,
    select: setSelectedId,
    clearSelection: () => setSelectedId(undefined),
    openCreate,
    openEdit,
    openDelete,
    close,
    formOpen,
    formInitial,
    isEditing,
    deleteTarget,
    formError,
    deleteError,
    handleSubmit,
    handleDelete,
    isSubmitting,
    isDeleting,
  };
}
