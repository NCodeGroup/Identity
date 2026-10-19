import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { TenantFormDialog, emptyTenantFormValues } from './TenantFormDialog.js';

describe('TenantFormDialog', () => {
  it('seeds the fields from initialValues when opened', () => {
    render(
      <TenantFormDialog
        open
        title="Edit tenant"
        submitLabel="Save"
        initialValues={{ displayName: 'Acme', domainName: 'acme.example', isDisabled: true }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Acme');
    expect(screen.getByTestId('tenant-domain-name')).toHaveValue('acme.example');
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();
  });

  it('disables submit until a display name is entered', () => {
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={emptyTenantFormValues}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: '   ' } });
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: 'New' } });
    expect(screen.getByTestId('tenant-form-submit')).toBeEnabled();
  });

  it('submits trimmed values', () => {
    const onSubmit = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={emptyTenantFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('tenant-display-name'), { target: { value: '  Acme  ' } });
    fireEvent.change(screen.getByTestId('tenant-domain-name'), {
      target: { value: ' acme.example ' },
    });
    fireEvent.click(screen.getByTestId('tenant-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      displayName: 'Acme',
      domainName: 'acme.example',
      isDisabled: false,
    });
  });

  it('surfaces a server error and blocks submit while submitting', () => {
    const onSubmit = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={{ displayName: 'Acme', domainName: '', isDisabled: false }}
        isSubmitting
        error="Boom"
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('tenant-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('tenant-form-submit')).toBeDisabled();
    expect(screen.getByTestId('tenant-form-submit')).toHaveTextContent('Saving…');
    expect(screen.getByTestId('tenant-form-cancel')).toBeDisabled();
  });

  it('submits the toggled disabled state', () => {
    const onSubmit = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Edit tenant"
        submitLabel="Save"
        initialValues={{ displayName: 'Acme', domainName: '', isDisabled: false }}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    const toggle = screen.getByRole('switch', { name: 'Disabled' });
    fireEvent.click(toggle);
    expect(toggle).toBeChecked();
    fireEvent.click(screen.getByTestId('tenant-form-submit'));
    expect(onSubmit).toHaveBeenCalledExactlyOnceWith({
      displayName: 'Acme',
      domainName: '',
      isDisabled: true,
    });
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = {
      title: 'Edit tenant',
      submitLabel: 'Save',
      onSubmit: vi.fn(),
      onCancel: vi.fn(),
    };
    const { rerender } = render(
      <TenantFormDialog
        {...props}
        open
        initialValues={{ displayName: 'Acme', domainName: 'acme.example', isDisabled: false }}
      />,
    );
    fireEvent.change(screen.getByTestId('tenant-display-name'), {
      target: { value: 'Unsaved edit' },
    });

    rerender(
      <TenantFormDialog
        {...props}
        open
        initialValues={{ displayName: 'Acme', domainName: 'acme.example', isDisabled: false }}
      />,
    );
    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Unsaved edit');

    rerender(
      <TenantFormDialog
        {...props}
        open={false}
        initialValues={{ displayName: 'Acme', domainName: 'acme.example', isDisabled: false }}
      />,
    );
    rerender(
      <TenantFormDialog
        {...props}
        open
        initialValues={{ displayName: 'Globex', domainName: 'globex.example', isDisabled: true }}
      />,
    );
    expect(screen.getByTestId('tenant-display-name')).toHaveValue('Globex');
    expect(screen.getByTestId('tenant-domain-name')).toHaveValue('globex.example');
    expect(screen.getByRole('switch', { name: 'Disabled' })).toBeChecked();
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    render(
      <TenantFormDialog
        open
        title="Create tenant"
        submitLabel="Create"
        initialValues={emptyTenantFormValues}
        onSubmit={vi.fn()}
        onCancel={onCancel}
      />,
    );
    fireEvent.click(screen.getByTestId('tenant-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
  });
});
