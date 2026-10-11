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
    fireEvent.change(screen.getByTestId('scope-value'), { target: { value: 'read:orders' } });
    expect(screen.getByTestId('scope-form-submit')).toBeEnabled();
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
    expect(screen.getByTestId('scope-form-cancel')).toBeDisabled();
  });
});
