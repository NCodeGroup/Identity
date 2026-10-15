import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { ProfileReadiness } from '@ncode/conformance-core';
import { ProfileCard } from './ProfileCard.js';

describe('ProfileCard', () => {
  it('renders the profile name and status', () => {
    const profile: ProfileReadiness = {
      id: 'basic-op',
      name: 'Basic OP',
      status: 'fail',
      findingIds: ['pkce'],
    };
    const { getByTestId, getByText } = render(<ProfileCard profile={profile} />);
    const card = getByTestId('profile-basic-op');
    expect(card).toHaveAttribute('data-status', 'fail');
    expect(getByText('Basic OP')).toBeInTheDocument();
    expect(getByText(/Blocked/)).toBeInTheDocument();
  });
});
