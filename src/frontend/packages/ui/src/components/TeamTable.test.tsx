import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { TeamTable, type TeamRow } from './TeamTable.js';

const teams: TeamRow[] = [
  { teamId: 'support', displayName: 'Support', isDisabled: false },
  { teamId: 'billing', displayName: 'Billing', isDisabled: true },
];

describe('TeamTable', () => {
  it('renders a row per team', () => {
    render(<TeamTable teams={teams} />);
    expect(screen.getByRole('table', { name: 'Teams' })).toBeInTheDocument();
    expect(screen.getByText('Support')).toBeInTheDocument();
    expect(screen.getByText('Billing')).toBeInTheDocument();
  });

  it('shows each team as active or disabled', () => {
    render(<TeamTable teams={teams} />);
    expect(screen.getByRole('columnheader', { name: 'Status' })).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Disabled')).toBeInTheDocument();
  });

  it('invokes onSelect with the team id when a row is clicked', () => {
    const onSelect = vi.fn();
    render(<TeamTable teams={teams} onSelect={onSelect} />);
    fireEvent.click(screen.getByTestId('team-row-support'));
    expect(onSelect).toHaveBeenCalledWith('support');
  });

  it('shows no action column when no action callbacks are supplied', () => {
    render(<TeamTable teams={teams} />);
    expect(screen.queryByTestId('team-edit-support')).not.toBeInTheDocument();
    expect(screen.queryByTestId('team-delete-support')).not.toBeInTheDocument();
  });

  it('invokes onEdit without selecting the row', () => {
    const onSelect = vi.fn();
    const onEdit = vi.fn();
    render(<TeamTable teams={teams} onSelect={onSelect} onEdit={onEdit} />);
    fireEvent.click(screen.getByTestId('team-edit-support'));
    expect(onEdit).toHaveBeenCalledWith('support');
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('invokes onDelete without selecting the row', () => {
    const onSelect = vi.fn();
    const onDelete = vi.fn();
    render(<TeamTable teams={teams} onSelect={onSelect} onDelete={onDelete} />);
    fireEvent.click(screen.getByTestId('team-delete-billing'));
    expect(onDelete).toHaveBeenCalledWith('billing');
    expect(onSelect).not.toHaveBeenCalled();
  });
});
