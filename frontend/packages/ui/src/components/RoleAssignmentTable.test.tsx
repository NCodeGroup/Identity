import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { RoleAssignmentTable, type RoleAssignmentRow } from './RoleAssignmentTable.js';

const assignments: RoleAssignmentRow[] = [
  { assignmentId: 'a-1', principalId: 'alice' },
  { assignmentId: 'a-2', principalId: 'bob' },
];

describe('RoleAssignmentTable', () => {
  it('renders a row per assignment with its principal id', () => {
    render(<RoleAssignmentTable label="Owners" testIdPrefix="owners" assignments={assignments} />);
    expect(screen.getByRole('table', { name: 'Owners' })).toBeInTheDocument();
    expect(screen.getByTestId('owners-row-alice')).toHaveTextContent('alice');
    expect(screen.getByTestId('owners-row-bob')).toHaveTextContent('bob');
  });

  it('renders no actions column without onRevoke', () => {
    render(<RoleAssignmentTable label="Owners" testIdPrefix="owners" assignments={assignments} />);
    expect(screen.queryByRole('columnheader', { name: 'Actions' })).not.toBeInTheDocument();
    expect(screen.queryByTestId('owners-revoke-alice')).not.toBeInTheDocument();
  });

  it('invokes onRevoke with the principal id', () => {
    const onRevoke = vi.fn();
    render(
      <RoleAssignmentTable
        label="Owners"
        testIdPrefix="owners"
        assignments={assignments}
        onRevoke={onRevoke}
      />,
    );
    expect(screen.getByRole('columnheader', { name: 'Actions' })).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('owners-revoke-bob'));
    expect(onRevoke).toHaveBeenCalledExactlyOnceWith('bob');
  });
});
