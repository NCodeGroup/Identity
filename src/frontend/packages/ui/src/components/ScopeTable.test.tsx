import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ScopeTable, type ScopeRow } from './ScopeTable.js';

const scopes: ScopeRow[] = [
  { value: 'read:orders', description: 'Read orders', isSystem: false },
  { value: 'openid', description: 'OpenID Connect', isSystem: true },
];

describe('ScopeTable', () => {
  it('renders a row per scope with its value and description', () => {
    render(<ScopeTable scopes={scopes} />);
    expect(screen.getByText('read:orders')).toBeInTheDocument();
    expect(screen.getByText('Read orders')).toBeInTheDocument();
  });

  it('renders no actions column without callbacks', () => {
    render(<ScopeTable scopes={scopes} />);
    expect(screen.queryByTestId('scope-edit-read:orders')).not.toBeInTheDocument();
  });

  it('invokes onEdit and onDelete with the scope value', () => {
    const onEdit = vi.fn();
    const onDelete = vi.fn();
    render(<ScopeTable scopes={scopes} onEdit={onEdit} onDelete={onDelete} />);
    fireEvent.click(screen.getByTestId('scope-edit-read:orders'));
    fireEvent.click(screen.getByTestId('scope-delete-read:orders'));
    expect(onEdit).toHaveBeenCalledWith('read:orders');
    expect(onDelete).toHaveBeenCalledWith('read:orders');
  });

  it('exposes no delete control for a system scope', () => {
    const onDelete = vi.fn();
    render(<ScopeTable scopes={scopes} onDelete={onDelete} />);
    expect(screen.queryByTestId('scope-delete-openid')).not.toBeInTheDocument();
    expect(screen.getByTestId('scope-delete-read:orders')).toBeInTheDocument();
  });
});
