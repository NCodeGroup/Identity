import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ConfirmDialog } from './ConfirmDialog.js';

describe('ConfirmDialog', () => {
  it('renders the title and message', () => {
    render(
      <ConfirmDialog
        open
        title="Delete tenant"
        message="Delete Acme?"
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByText('Delete tenant')).toBeInTheDocument();
    expect(screen.getByText('Delete Acme?')).toBeInTheDocument();
  });

  it('invokes onConfirm and onCancel', () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    render(
      <ConfirmDialog
        open
        title="Delete tenant"
        message="Delete Acme?"
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    );
    fireEvent.click(screen.getByTestId('confirm-accept'));
    fireEvent.click(screen.getByTestId('confirm-cancel'));
    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('disables both actions and surfaces an error while busy', () => {
    render(
      <ConfirmDialog
        open
        title="Delete tenant"
        message="Delete Acme?"
        isBusy
        error="Still has clients"
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('confirm-accept')).toBeDisabled();
    expect(screen.getByTestId('confirm-cancel')).toBeDisabled();
    expect(screen.getByTestId('confirm-error')).toHaveTextContent('Still has clients');
  });
});
