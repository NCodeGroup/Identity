import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import {
  TenantEnvironmentFilter,
  allTenantEnvironments,
  matchesTenantEnvironment,
  tenantEnvironmentTypes,
} from './TenantEnvironmentFilter.js';
import type { TenantRow } from './TenantTable.js';

function tenant(tenantId: string, type?: string): TenantRow {
  return {
    tenantId,
    displayName: tenantId,
    issuer: null,
    mtlsEndpointAliasAuthority: null,
    isDisabled: false,
    ...(type ? { environment: { type, name: null, isProduction: type === 'production' } } : {}),
  };
}

describe('tenantEnvironmentTypes', () => {
  it('returns each distinct type once, sorted, skipping tenants without an environment', () => {
    const tenants = [
      tenant('a', 'staging'),
      tenant('b', 'development'),
      tenant('c'),
      tenant('d', 'staging'),
    ];
    expect(tenantEnvironmentTypes(tenants)).toEqual(['development', 'staging']);
  });
});

describe('matchesTenantEnvironment', () => {
  it('matches every tenant when all environments are shown', () => {
    expect(matchesTenantEnvironment(tenant('a'), allTenantEnvironments)).toBe(true);
    expect(matchesTenantEnvironment(tenant('b', 'staging'), allTenantEnvironments)).toBe(true);
  });

  it('matches only the tenants of the selected type', () => {
    expect(matchesTenantEnvironment(tenant('a', 'staging'), 'staging')).toBe(true);
    expect(matchesTenantEnvironment(tenant('b', 'production'), 'staging')).toBe(false);
    expect(matchesTenantEnvironment(tenant('c'), 'staging')).toBe(false);
  });
});

describe('TenantEnvironmentFilter', () => {
  it('reports the selected type', () => {
    const onChange = vi.fn();
    render(
      <TenantEnvironmentFilter
        types={['development', 'staging']}
        value={allTenantEnvironments}
        onChange={onChange}
      />,
    );
    fireEvent.change(screen.getByTestId('tenant-environment-filter'), {
      target: { value: 'staging' },
    });
    expect(onChange).toHaveBeenCalledWith('staging');
  });

  it('shows every environment when the selected type is not offered', () => {
    render(<TenantEnvironmentFilter types={['development']} value="retired" onChange={vi.fn()} />);
    expect(screen.getByTestId('tenant-environment-filter')).toHaveValue(allTenantEnvironments);
  });
});
