import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { TeamFormDialog, emptyTeamFormValues } from './TeamFormDialog.js';

const support = { teamId: 'support', displayName: 'Support', isDisabled: false };

describe('TeamFormDialog', () => {
  it('seeds the fields from initialValues when opened', () => {
    render(
      <TeamFormDialog
        open
        title="Edit team"
        submitLabel="Save"
        initialValues={{ ...support, isDisabled: true }}
        isTeamIdLocked
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('team-id')).toHaveValue('support');
    expect(screen.getByTestId('team-display-name')).toHaveValue('Support');
    expect(screen.getByTestId('team-disabled')).toBeChecked();
  });

  it('locks the team ID of an existing team', () => {
    render(
      <TeamFormDialog
        open
        title="Edit team"
        submitLabel="Save"
        initialValues={support}
        isTeamIdLocked
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('team-id')).toBeDisabled();
    expect(screen.getByText('A team ID cannot be changed.')).toBeInTheDocument();
    expect(screen.getByTestId('team-form-submit')).toBeEnabled();
  });

  it('disables submit until a valid team ID and a display name are entered', () => {
    render(
      <TeamFormDialog
        open
        title="Create team"
        submitLabel="Create"
        initialValues={emptyTeamFormValues}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('team-form-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('team-id'), { target: { value: 'Not Valid' } });
    fireEvent.change(screen.getByTestId('team-display-name'), { target: { value: 'New' } });
    expect(screen.getByTestId('team-id')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByTestId('team-form-submit')).toBeDisabled();

    fireEvent.change(screen.getByTestId('team-id'), { target: { value: 'new-team' } });
    expect(screen.getByTestId('team-id')).toHaveAttribute('aria-invalid', 'false');
    expect(screen.getByTestId('team-form-submit')).toBeEnabled();

    fireEvent.change(screen.getByTestId('team-display-name'), { target: { value: '   ' } });
    expect(screen.getByTestId('team-form-submit')).toBeDisabled();
  });

  it('submits trimmed values including the disabled flag', () => {
    const onSubmit = vi.fn();
    render(
      <TeamFormDialog
        open
        title="Create team"
        submitLabel="Create"
        initialValues={emptyTeamFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('team-id'), { target: { value: ' support ' } });
    fireEvent.change(screen.getByTestId('team-display-name'), { target: { value: '  Support  ' } });
    fireEvent.click(screen.getByTestId('team-disabled'));
    fireEvent.click(screen.getByTestId('team-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      teamId: 'support',
      displayName: 'Support',
      isDisabled: true,
    });
  });

  it('surfaces a server error and blocks submit while submitting', () => {
    render(
      <TeamFormDialog
        open
        title="Create team"
        submitLabel="Create"
        initialValues={support}
        isSubmitting
        error="Boom"
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('team-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('team-form-submit')).toBeDisabled();
    expect(screen.getByTestId('team-form-submit')).toHaveTextContent('Saving…');
    expect(screen.getByTestId('team-form-cancel')).toBeDisabled();
  });

  it('does not submit when the form is invalid', () => {
    const onSubmit = vi.fn();
    render(
      <TeamFormDialog
        open
        title="Create team"
        submitLabel="Create"
        initialValues={emptyTeamFormValues}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByTestId('team-form-submit'));
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('keeps in-progress edits while open and reseeds from initialValues on reopen', () => {
    const props = {
      title: 'Edit team',
      submitLabel: 'Save',
      isTeamIdLocked: true,
      onSubmit: vi.fn(),
      onCancel: vi.fn(),
    };
    const { rerender } = render(<TeamFormDialog {...props} open initialValues={support} />);
    fireEvent.change(screen.getByTestId('team-display-name'), {
      target: { value: 'Unsaved edit' },
    });

    rerender(<TeamFormDialog {...props} open initialValues={{ ...support }} />);
    expect(screen.getByTestId('team-display-name')).toHaveValue('Unsaved edit');

    rerender(<TeamFormDialog {...props} open={false} initialValues={support} />);
    rerender(
      <TeamFormDialog {...props} open initialValues={{ ...support, displayName: 'Billing' }} />,
    );
    expect(screen.getByTestId('team-display-name')).toHaveValue('Billing');
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    render(
      <TeamFormDialog
        open
        title="Create team"
        submitLabel="Create"
        initialValues={emptyTeamFormValues}
        onSubmit={vi.fn()}
        onCancel={onCancel}
      />,
    );
    fireEvent.click(screen.getByTestId('team-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
  });
});
