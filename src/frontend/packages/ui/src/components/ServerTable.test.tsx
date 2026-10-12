import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ServerTable, type ServerRow } from './ServerTable.js';

const servers: ServerRow[] = [
  { serverId: 'root', displayName: 'Root server' },
  { serverId: 'edge', displayName: 'Edge server' },
];

describe('ServerTable', () => {
  it('renders a row per server', () => {
    render(<ServerTable servers={servers} />);
    expect(screen.getByText('Root server')).toBeInTheDocument();
    expect(screen.getByText('Edge server')).toBeInTheDocument();
  });

  it('invokes onSelect with the server id when a row is clicked', () => {
    const onSelect = vi.fn();
    render(<ServerTable servers={servers} onSelect={onSelect} />);
    fireEvent.click(screen.getByTestId('server-row-root'));
    expect(onSelect).toHaveBeenCalledWith('root');
  });

  it('shows no action column when no action callbacks are supplied', () => {
    render(<ServerTable servers={servers} />);
    expect(screen.queryByTestId('server-edit-root')).not.toBeInTheDocument();
    expect(screen.queryByTestId('server-delete-root')).not.toBeInTheDocument();
  });

  it('invokes onEdit without selecting the row', () => {
    const onSelect = vi.fn();
    const onEdit = vi.fn();
    render(<ServerTable servers={servers} onSelect={onSelect} onEdit={onEdit} />);
    fireEvent.click(screen.getByTestId('server-edit-root'));
    expect(onEdit).toHaveBeenCalledWith('root');
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('invokes onDelete without selecting the row', () => {
    const onSelect = vi.fn();
    const onDelete = vi.fn();
    render(<ServerTable servers={servers} onSelect={onSelect} onDelete={onDelete} />);
    fireEvent.click(screen.getByTestId('server-delete-edge'));
    expect(onDelete).toHaveBeenCalledWith('edge');
    expect(onSelect).not.toHaveBeenCalled();
  });
});
