import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { TenantTable, type TenantRow } from './TenantTable.js';

const tenants: TenantRow[] = [
  { tenantId: 'root', displayName: 'Root', domainName: 'id.example', isDisabled: false },
  { tenantId: 'old', displayName: 'Legacy', domainName: null, isDisabled: true },
];

describe('TenantTable', () => {
  it('renders a row per tenant', () => {
    render(<TenantTable tenants={tenants} />);
    expect(screen.getByText('Root')).toBeInTheDocument();
    expect(screen.getByText('Legacy')).toBeInTheDocument();
  });

  it('shows an em dash when the domain is null', () => {
    render(<TenantTable tenants={tenants} />);
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  it('invokes onSelect with the tenant id when a row is clicked', () => {
    const onSelect = vi.fn();
    render(<TenantTable tenants={tenants} onSelect={onSelect} />);
    fireEvent.click(screen.getByTestId('tenant-row-root'));
    expect(onSelect).toHaveBeenCalledWith('root');
  });
});
