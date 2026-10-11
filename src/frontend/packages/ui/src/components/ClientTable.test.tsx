import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ClientTable, type ClientRow } from './ClientTable.js';

const clients: ClientRow[] = [
  { clientId: 'web-spa', isDisabled: false },
  { clientId: 'worker', isDisabled: true },
];

describe('ClientTable', () => {
  it('renders a row per client identified by id', () => {
    render(<ClientTable clients={clients} />);
    expect(screen.getByText('web-spa')).toBeInTheDocument();
    expect(screen.getByText('worker')).toBeInTheDocument();
  });

  it('renders no actions column without callbacks', () => {
    render(<ClientTable clients={clients} />);
    expect(screen.queryByTestId('client-edit-web-spa')).not.toBeInTheDocument();
  });

  it('invokes onEdit and onDelete with the client id', () => {
    const onEdit = vi.fn();
    const onDelete = vi.fn();
    render(<ClientTable clients={clients} onEdit={onEdit} onDelete={onDelete} />);
    fireEvent.click(screen.getByTestId('client-edit-web-spa'));
    fireEvent.click(screen.getByTestId('client-delete-worker'));
    expect(onEdit).toHaveBeenCalledWith('web-spa');
    expect(onDelete).toHaveBeenCalledWith('worker');
  });
});
