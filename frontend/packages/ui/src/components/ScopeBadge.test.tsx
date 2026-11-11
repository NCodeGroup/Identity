import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { chipClasses } from '@mui/material/Chip';
import { ScopeBadge } from './ScopeBadge.js';

describe('ScopeBadge', () => {
  it('marks the platform scope as a filled warning chip with a globe icon', () => {
    render(<ScopeBadge kind="platform" />);
    const badge = screen.getByTestId('scope-badge');

    expect(badge).toHaveTextContent('Platform');
    expect(badge).toHaveClass(chipClasses.colorWarning, chipClasses.filled);
    expect(badge).not.toHaveClass(chipClasses.outlined);
    expect(within(badge).getByTestId('PublicIcon')).toBeInTheDocument();
    expect(within(badge).queryByTestId('BusinessIcon')).not.toBeInTheDocument();
  });

  it('marks a team scope as an outlined secondary chip with a groups icon', () => {
    render(<ScopeBadge kind="team" />);
    const badge = screen.getByTestId('scope-badge');

    expect(badge).toHaveTextContent('Team');
    expect(badge).toHaveClass(chipClasses.colorSecondary, chipClasses.outlined);
    expect(badge).not.toHaveClass(chipClasses.filled);
    expect(within(badge).getByTestId('GroupsIcon')).toBeInTheDocument();
    expect(within(badge).queryByTestId('PublicIcon')).not.toBeInTheDocument();
  });

  it('marks a tenant scope as an outlined primary chip with a business icon', () => {
    render(<ScopeBadge kind="tenant" />);
    const badge = screen.getByTestId('scope-badge');

    expect(badge).toHaveTextContent('Tenant');
    expect(badge).toHaveClass(chipClasses.colorPrimary, chipClasses.outlined);
    expect(badge).not.toHaveClass(chipClasses.filled);
    expect(within(badge).getByTestId('BusinessIcon')).toBeInTheDocument();
    expect(within(badge).queryByTestId('PublicIcon')).not.toBeInTheDocument();
  });

  it.each([
    ['platform', 'Platform', 'Acme Platform'],
    ['team', 'Team', 'Support'],
    ['tenant', 'Tenant', 'Acme Corp'],
  ] as const)('shows a custom label for the %s scope', (kind, defaultLabel, label) => {
    render(<ScopeBadge kind={kind} label={label} />);

    expect(screen.getByTestId('scope-badge')).toHaveTextContent(label);
    expect(screen.queryByText(defaultLabel)).not.toBeInTheDocument();
  });
});
