import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { InitialAccessTokenFormDialog } from './InitialAccessTokenFormDialog.js';

const resourceServers = [
  { resourceServerId: 'rs-orders', name: 'Orders', identifier: 'https://orders.example' },
  { resourceServerId: 'rs-billing', name: 'Billing', identifier: 'https://billing.example' },
];

describe('InitialAccessTokenFormDialog', () => {
  it('submits an empty description and blank lifetime by default', () => {
    const onSubmit = vi.fn();
    render(<InitialAccessTokenFormDialog open onSubmit={onSubmit} onCancel={vi.fn()} />);
    fireEvent.click(screen.getByTestId('iat-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      description: '',
      lifetimeSeconds: '',
      resourceServerId: '',
    });
    expect(screen.queryByTestId('iat-resource-server')).not.toBeInTheDocument();
  });

  it('scopes the token to a chosen resource server', () => {
    const onSubmit = vi.fn();
    render(
      <InitialAccessTokenFormDialog
        open
        resourceServers={resourceServers}
        onSubmit={onSubmit}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByText('Leave unscoped to register an ordinary client.')).toBeInTheDocument();

    fireEvent.change(screen.getByTestId('iat-resource-server'), { target: { value: 'rs-orders' } });

    expect(
      screen.getByText(/bound to this resource server, must be confidential/),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('iat-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      description: '',
      lifetimeSeconds: '',
      resourceServerId: 'rs-orders',
    });
  });

  it('lists each resource server by name and identifier', () => {
    render(
      <InitialAccessTokenFormDialog
        open
        resourceServers={resourceServers}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    fireEvent.mouseDown(screen.getByRole('combobox', { name: /Resource server/ }));

    expect(
      within(screen.getByRole('listbox'))
        .getAllByRole('option')
        .map((option) => option.textContent),
    ).toEqual(['None', 'Orders (https://orders.example)', 'Billing (https://billing.example)']);
  });

  it('trims the description and forwards a positive lifetime', () => {
    const onSubmit = vi.fn();
    render(<InitialAccessTokenFormDialog open onSubmit={onSubmit} onCancel={vi.fn()} />);
    fireEvent.change(screen.getByTestId('iat-description'), { target: { value: '  Partner X  ' } });
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: '3600' } });
    fireEvent.click(screen.getByTestId('iat-form-submit'));
    expect(onSubmit).toHaveBeenCalledWith({
      description: 'Partner X',
      lifetimeSeconds: '3600',
      resourceServerId: '',
    });
  });

  it('blocks submit on a non-positive lifetime', () => {
    const onSubmit = vi.fn();
    render(<InitialAccessTokenFormDialog open onSubmit={onSubmit} onCancel={vi.fn()} />);
    expect(screen.getByText('Leave blank for a token that never expires.')).toBeInTheDocument();
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: '0' } });
    expect(screen.getByTestId('iat-form-submit')).toBeDisabled();
    expect(
      screen.getByText('Lifetime must be a positive whole number of seconds.'),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('iat-form-submit'));
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it.each(['-5', '1.5', '1e3'])('rejects the non-integer lifetime %s', (lifetime) => {
    render(<InitialAccessTokenFormDialog open onSubmit={vi.fn()} onCancel={vi.fn()} />);
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: lifetime } });
    expect(screen.getByTestId('iat-lifetime')).toHaveValue(Number(lifetime));
    expect(screen.getByTestId('iat-form-submit')).toBeDisabled();
    expect(
      screen.getByText('Lifetime must be a positive whole number of seconds.'),
    ).toBeInTheDocument();
  });

  it('invokes onCancel from the cancel button', () => {
    const onCancel = vi.fn();
    const onSubmit = vi.fn();
    render(<InitialAccessTokenFormDialog open onSubmit={onSubmit} onCancel={onCancel} />);
    fireEvent.click(screen.getByTestId('iat-form-cancel'));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('keeps in-progress edits while open and resets the fields on reopen', () => {
    const props = { resourceServers, onSubmit: vi.fn(), onCancel: vi.fn() };
    const { rerender } = render(<InitialAccessTokenFormDialog {...props} open />);
    fireEvent.change(screen.getByTestId('iat-description'), { target: { value: 'Partner X' } });
    fireEvent.change(screen.getByTestId('iat-lifetime'), { target: { value: '3600' } });
    fireEvent.change(screen.getByTestId('iat-resource-server'), { target: { value: 'rs-orders' } });

    rerender(<InitialAccessTokenFormDialog {...props} open />);
    expect(screen.getByTestId('iat-description')).toHaveValue('Partner X');

    rerender(<InitialAccessTokenFormDialog {...props} open={false} />);
    rerender(<InitialAccessTokenFormDialog {...props} open />);
    expect(screen.getByTestId('iat-description')).toHaveValue('');
    expect(screen.getByTestId('iat-lifetime')).toHaveValue(null);
    expect(screen.getByTestId('iat-resource-server')).toHaveValue('');
  });

  it('surfaces a server error and disables actions while submitting', () => {
    render(
      <InitialAccessTokenFormDialog
        open
        isSubmitting
        error="Boom"
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />,
    );
    expect(screen.getByTestId('iat-form-error')).toHaveTextContent('Boom');
    expect(screen.getByTestId('iat-form-submit')).toBeDisabled();
    expect(screen.getByTestId('iat-form-submit')).toHaveTextContent('Minting…');
    expect(screen.getByTestId('iat-form-cancel')).toBeDisabled();
  });
});
