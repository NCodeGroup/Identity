import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ServerFormDialog, emptyServerFormValues } from './ServerFormDialog.js';

describe('ServerFormDialog', () => {
  it('seeds the field from initialValues when opened', () => {
    render(
      <ServerFormDialog
        open
        title="Edit server"
        submitLabel="Save"
        initialValues={{ displayName: 'Root server' }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('server-display-name')).toHaveValue('Root server');
  });

  it('disables submit until a display name is entered', () => {
    render(
      <ServerFormDialog
        open
        title="Create server"
        submitLabel="Create"
        initialValues={emptyServerFormValues}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('server-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('server-display-name'), { target: { value: '   ' } });
    expect(screen.getByTestId('server-form-submit')).toBeDisabled();
    fireEvent.change(screen.getByTestId('server-display-name'), { target: { value: 'New' } });
    expect(screen.getByTestId('server-form-submit')).toBeEnabled();
  });

  it('submits a trimmed display name', () => {
    const onSubmit = vi.fn();
    render(
      <ServerFormDialog
        open
        title="Create server"
        submitLabel="Create"
        initialValues={emptyServerFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('server-display-name'), { target: { value: '  Root  ' } });
    fireEvent.click(screen.getByTestId('server-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({ displayName: 'Root' });
  });

  it('surfaces a server error and blocks submit while submitting', () => {
    render(
      <ServerFormDialog
        open
        title="Create server"
        submitLabel="Create"
        initialValues={{ displayName: 'Root server' }}
        isSubmitting
        error="Boom"
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('server-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('server-form-submit')).toBeDisabled();
    expect(screen.getByTestId('server-form-submit')).toHaveTextContent('Saving…');
    expect(screen.getByTestId('server-form-cancel')).toBeDisabled();
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = {
      title: 'Edit server',
      submitLabel: 'Save',
      onSubmit: vi.fn(),
      onCancel: vi.fn(),
    };
    const { rerender } = render(
      <ServerFormDialog {...props} open initialValues={{ displayName: 'Root server' }} />,
    );
    fireEvent.change(screen.getByTestId('server-display-name'), {
      target: { value: 'Unsaved edit' },
    });

    rerender(<ServerFormDialog {...props} open initialValues={{ displayName: 'Root server' }} />);
    expect(screen.getByTestId('server-display-name')).toHaveValue('Unsaved edit');

    rerender(
      <ServerFormDialog {...props} open={false} initialValues={{ displayName: 'Root server' }} />,
    );
    rerender(<ServerFormDialog {...props} open initialValues={{ displayName: 'Edge server' }} />);
    expect(screen.getByTestId('server-display-name')).toHaveValue('Edge server');
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    render(
      <ServerFormDialog
        open
        title="Create server"
        submitLabel="Create"
        initialValues={emptyServerFormValues}
        onSubmit={vi.fn()}
        onCancel={onCancel}
      />,
    );
    fireEvent.click(screen.getByTestId('server-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
  });
});
