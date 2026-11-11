import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ScopeFormDialog, emptyScopeFormValues } from './ScopeFormDialog.js';

describe('ScopeFormDialog', () => {
  it('submits the trimmed value and description', () => {
    const onSubmit = vi.fn();
    render(
      <ScopeFormDialog
        open
        title="Add scope"
        submitLabel="Add"
        initialValues={emptyScopeFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('scope-value'), { target: { value: '  read:orders  ' } });
    fireEvent.change(screen.getByTestId('scope-description'), {
      target: { value: '  Read orders  ' },
    });
    fireEvent.click(screen.getByTestId('scope-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({ value: 'read:orders', description: 'Read orders' });
  });

  it('blocks submit until the value is provided', () => {
    render(
      <ScopeFormDialog
        open
        title="Add scope"
        submitLabel="Add"
        initialValues={emptyScopeFormValues}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('scope-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('scope-value'), { target: { value: '   ' } });
    expect(screen.getByTestId('scope-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('scope-value'), { target: { value: 'read:orders' } });
    expect(screen.getByTestId('scope-form-submit')).toBeEnabled();
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = {
      title: 'Edit scope',
      submitLabel: 'Save',
      valueEditable: false,
      onSubmit: vi.fn(),
      onCancel: vi.fn(),
    };
    const { rerender } = render(
      <ScopeFormDialog
        {...props}
        open
        initialValues={{ value: 'read:orders', description: 'Read orders' }}
      />,
    );
    fireEvent.change(screen.getByTestId('scope-description'), {
      target: { value: 'Unsaved edit' },
    });

    rerender(
      <ScopeFormDialog
        {...props}
        open
        initialValues={{ value: 'read:orders', description: 'Read orders' }}
      />,
    );
    expect(screen.getByTestId('scope-description')).toHaveValue('Unsaved edit');

    rerender(
      <ScopeFormDialog
        {...props}
        open={false}
        initialValues={{ value: 'read:orders', description: 'Read orders' }}
      />,
    );
    rerender(
      <ScopeFormDialog
        {...props}
        open
        initialValues={{ value: 'write:orders', description: 'Write orders' }}
      />,
    );
    expect(screen.getByTestId('scope-value')).toHaveValue('write:orders');
    expect(screen.getByTestId('scope-description')).toHaveValue('Write orders');
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    const onSubmit = vi.fn();
    render(
      <ScopeFormDialog
        open
        title="Add scope"
        submitLabel="Add"
        initialValues={emptyScopeFormValues}
        onSubmit={onSubmit}
        onCancel={onCancel}
      />,
    );
    fireEvent.click(screen.getByTestId('scope-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('locks the value and allows a description-only edit when the value is not editable', () => {
    const onSubmit = vi.fn();
    render(
      <ScopeFormDialog
        open
        title="Edit scope"
        submitLabel="Save"
        valueEditable={false}
        initialValues={{ value: 'read:orders', description: 'Read orders' }}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('scope-value')).toBeDisabled();
    expect(screen.getByText('The scope value is immutable after creation.')).toBeInTheDocument();
    fireEvent.change(screen.getByTestId('scope-description'), {
      target: { value: 'Read all orders' },
    });
    fireEvent.click(screen.getByTestId('scope-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({ value: 'read:orders', description: 'Read all orders' });
  });

  it('surfaces a server error and blocks actions while submitting', () => {
    render(
      <ScopeFormDialog
        open
        title="Add scope"
        submitLabel="Add"
        initialValues={emptyScopeFormValues}
        isSubmitting
        error="Boom"
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('scope-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('scope-form-submit')).toBeDisabled();
    expect(screen.getByTestId('scope-form-submit')).toHaveTextContent('Saving…');
    expect(screen.getByTestId('scope-form-cancel')).toBeDisabled();
  });
});
