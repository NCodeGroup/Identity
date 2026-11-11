import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ClientFormDialog, emptyClientFormValues } from './ClientFormDialog.js';

describe('ClientFormDialog', () => {
  it('seeds the disabled toggle from initialValues when opened', () => {
    render(
      <ClientFormDialog
        open
        title="Edit client"
        submitLabel="Save"
        initialValues={{ isDisabled: true }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();
  });

  it('submits the toggled values', () => {
    const onSubmit = vi.fn();
    render(
      <ClientFormDialog
        open
        title="Create client"
        submitLabel="Create"
        initialValues={emptyClientFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByRole('switch', { name: 'Disabled' }));
    fireEvent.click(screen.getByTestId('client-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({ isDisabled: true });
  });

  it('surfaces a server error and blocks actions while submitting', () => {
    render(
      <ClientFormDialog
        open
        title="Create client"
        submitLabel="Create"
        initialValues={emptyClientFormValues}
        isSubmitting
        error="Boom"
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('client-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('client-form-submit')).toBeDisabled();
    expect(screen.getByTestId('client-form-submit')).toHaveTextContent('Saving…');
    expect(screen.getByTestId('client-form-cancel')).toBeDisabled();
  });

  it('submits the untouched values with the submit label', () => {
    const onSubmit = vi.fn();
    render(
      <ClientFormDialog
        open
        title="Edit client"
        submitLabel="Save"
        initialValues={{ isDisabled: true }}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('client-form-submit')).toHaveTextContent('Save');
    expect(screen.queryByTestId('client-form-error')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('client-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({ isDisabled: true });
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    const onSubmit = vi.fn();
    render(
      <ClientFormDialog
        open
        title="Create client"
        submitLabel="Create"
        initialValues={emptyClientFormValues}
        onSubmit={onSubmit}
        onCancel={onCancel}
      />,
    );
    fireEvent.click(screen.getByTestId('client-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = {
      title: 'Edit client',
      submitLabel: 'Save',
      onSubmit: vi.fn(),
      onCancel: vi.fn(),
    };
    const { rerender } = render(
      <ClientFormDialog {...props} open initialValues={{ isDisabled: false }} />,
    );
    fireEvent.click(screen.getByRole('switch', { name: 'Disabled' }));

    rerender(<ClientFormDialog {...props} open initialValues={{ isDisabled: false }} />);
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();

    rerender(<ClientFormDialog {...props} open={false} initialValues={{ isDisabled: false }} />);
    rerender(<ClientFormDialog {...props} open initialValues={{ isDisabled: false }} />);
    expect(screen.getByRole('switch', { name: 'Disabled' })).not.toBeChecked();
  });
});
