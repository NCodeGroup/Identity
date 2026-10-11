import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ResourceServerTable, type ResourceServerRow } from './ResourceServerTable.js';

const resourceServers: ResourceServerRow[] = [
  {
    resourceServerId: 'rs-orders',
    name: 'Orders API',
    identifier: 'https://orders.example.com',
    isDisabled: false,
    isSystem: false,
    scopeCount: 2,
  },
  {
    resourceServerId: 'rs-management',
    name: 'Management API',
    identifier: 'urn:ncode:management',
    isDisabled: true,
    isSystem: true,
    scopeCount: 1,
  },
];

describe('ResourceServerTable', () => {
  it('renders a row per resource server with its name, identifier, and scope count', () => {
    render(<ResourceServerTable resourceServers={resourceServers} />);
    expect(screen.getByText('Orders API')).toBeInTheDocument();
    expect(screen.getByText('https://orders.example.com')).toBeInTheDocument();
    expect(screen.getByTestId('resource-server-row-rs-orders')).toHaveTextContent('2');
  });

  it('renders no actions column without callbacks', () => {
    render(<ResourceServerTable resourceServers={resourceServers} />);
    expect(screen.queryByTestId('resource-server-edit-rs-orders')).not.toBeInTheDocument();
  });

  it('invokes onSelect, onEdit, and onDelete with the resource-server id', () => {
    const onSelect = vi.fn();
    const onEdit = vi.fn();
    const onDelete = vi.fn();
    render(
      <ResourceServerTable
        resourceServers={resourceServers}
        onSelect={onSelect}
        onEdit={onEdit}
        onDelete={onDelete}
      />,
    );
    fireEvent.click(screen.getByTestId('resource-server-row-rs-orders'));
    fireEvent.click(screen.getByTestId('resource-server-edit-rs-orders'));
    fireEvent.click(screen.getByTestId('resource-server-delete-rs-orders'));
    expect(onSelect).toHaveBeenCalledWith('rs-orders');
    expect(onEdit).toHaveBeenCalledWith('rs-orders');
    expect(onDelete).toHaveBeenCalledWith('rs-orders');
  });

  it('exposes no delete control for a system resource server', () => {
    const onDelete = vi.fn();
    render(<ResourceServerTable resourceServers={resourceServers} onDelete={onDelete} />);
    expect(screen.queryByTestId('resource-server-delete-rs-management')).not.toBeInTheDocument();
    expect(screen.getByTestId('resource-server-delete-rs-orders')).toBeInTheDocument();
  });
});
