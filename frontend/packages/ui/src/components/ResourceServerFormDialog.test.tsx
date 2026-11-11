import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import {
  ResourceServerFormDialog,
  emptyResourceServerFormValues,
} from './ResourceServerFormDialog.js';

describe('ResourceServerFormDialog', () => {
  it('submits the trimmed identifier, name, and disabled toggle', () => {
    const onSubmit = vi.fn();
    render(
      <ResourceServerFormDialog
        open
        title="Create resource server"
        submitLabel="Create"
        initialValues={emptyResourceServerFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('resource-server-identifier'), {
      target: { value: '  https://api.example.com  ' },
    });
    fireEvent.change(screen.getByTestId('resource-server-name'), {
      target: { value: '  Orders API  ' },
    });
    fireEvent.click(screen.getByRole('switch', { name: 'Disabled' }));
    fireEvent.click(screen.getByTestId('resource-server-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      identifier: 'https://api.example.com',
      name: 'Orders API',
      isDisabled: true,
    });
  });

  it('blocks submit until both the identifier and name are provided', () => {
    const onSubmit = vi.fn();
    render(
      <ResourceServerFormDialog
        open
        title="Create resource server"
        submitLabel="Create"
        initialValues={emptyResourceServerFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('resource-server-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('resource-server-identifier'), {
      target: { value: 'urn:api' },
    });
    expect(screen.getByTestId('resource-server-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('resource-server-name'), { target: { value: 'API' } });
    expect(screen.getByTestId('resource-server-form-submit')).toBeEnabled();
    fireEvent.change(screen.getByTestId('resource-server-identifier'), {
      target: { value: '   ' },
    });
    expect(screen.getByTestId('resource-server-form-submit')).toBeDisabled();
  });

  it('locks the identifier when it is not editable', () => {
    const onSubmit = vi.fn();
    render(
      <ResourceServerFormDialog
        open
        title="Edit resource server"
        submitLabel="Save"
        identifierEditable={false}
        initialValues={{ identifier: 'urn:api', name: 'API', isDisabled: false }}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('resource-server-identifier')).toBeDisabled();
    expect(screen.getByText('The identifier is immutable after creation.')).toBeInTheDocument();
    expect(screen.getByTestId('resource-server-form-submit')).toBeEnabled();
    fireEvent.change(screen.getByTestId('resource-server-name'), { target: { value: '   ' } });
    expect(screen.getByTestId('resource-server-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('resource-server-name'), {
      target: { value: ' Renamed API ' },
    });
    fireEvent.click(screen.getByTestId('resource-server-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      identifier: 'urn:api',
      name: 'Renamed API',
      isDisabled: false,
    });
  });

  it('surfaces a server error and blocks actions while submitting', () => {
    render(
      <ResourceServerFormDialog
        open
        title="Create resource server"
        submitLabel="Create"
        initialValues={emptyResourceServerFormValues}
        isSubmitting
        error="Boom"
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('resource-server-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('resource-server-form-submit')).toBeDisabled();
    expect(screen.getByTestId('resource-server-form-submit')).toHaveTextContent('Saving…');
    expect(screen.getByTestId('resource-server-form-cancel')).toBeDisabled();
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    const onSubmit = vi.fn();
    render(
      <ResourceServerFormDialog
        open
        title="Create resource server"
        submitLabel="Create"
        initialValues={emptyResourceServerFormValues}
        onSubmit={onSubmit}
        onCancel={onCancel}
      />,
    );
    fireEvent.click(screen.getByTestId('resource-server-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = {
      title: 'Edit resource server',
      submitLabel: 'Save',
      identifierEditable: false,
      onSubmit: vi.fn(),
      onCancel: vi.fn(),
    };
    const { rerender } = render(
      <ResourceServerFormDialog
        {...props}
        open
        initialValues={{ identifier: 'urn:orders', name: 'Orders API', isDisabled: false }}
      />,
    );
    fireEvent.change(screen.getByTestId('resource-server-name'), {
      target: { value: 'Unsaved edit' },
    });

    rerender(
      <ResourceServerFormDialog
        {...props}
        open
        initialValues={{ identifier: 'urn:orders', name: 'Orders API', isDisabled: false }}
      />,
    );
    expect(screen.getByTestId('resource-server-name')).toHaveValue('Unsaved edit');

    rerender(
      <ResourceServerFormDialog
        {...props}
        open={false}
        initialValues={{ identifier: 'urn:orders', name: 'Orders API', isDisabled: false }}
      />,
    );
    rerender(
      <ResourceServerFormDialog
        {...props}
        open
        initialValues={{ identifier: 'urn:billing', name: 'Billing API', isDisabled: true }}
      />,
    );
    expect(screen.getByTestId('resource-server-identifier')).toHaveValue('urn:billing');
    expect(screen.getByTestId('resource-server-name')).toHaveValue('Billing API');
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();
  });
});
