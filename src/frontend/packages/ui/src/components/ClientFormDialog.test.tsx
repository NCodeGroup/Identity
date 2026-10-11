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
    expect(screen.getByRole('checkbox', { name: 'Disabled' })).toBeChecked();
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
    fireEvent.click(screen.getByRole('checkbox', { name: 'Disabled' }));
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
    expect(screen.getByTestId('client-form-cancel')).toBeDisabled();
  });
});
