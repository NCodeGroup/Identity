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
});
