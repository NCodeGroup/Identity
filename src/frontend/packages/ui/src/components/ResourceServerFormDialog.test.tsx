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
    fireEvent.change(screen.getByTestId('resource-server-name'), { target: { value: 'API' } });
    expect(screen.getByTestId('resource-server-form-submit')).toBeEnabled();
  });

  it('locks the identifier when it is not editable', () => {
    render(
      <ResourceServerFormDialog
        open
        title="Edit resource server"
        submitLabel="Save"
        identifierEditable={false}
        initialValues={{ identifier: 'urn:api', name: 'API', isDisabled: false }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('resource-server-identifier')).toBeDisabled();
    expect(screen.getByTestId('resource-server-form-submit')).toBeEnabled();
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
    expect(screen.getByTestId('resource-server-form-cancel')).toBeDisabled();
  });
});
