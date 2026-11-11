import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DataStateBoundary } from './DataStateBoundary.js';

const content = <p data-testid="content">Loaded rows</p>;

describe('DataStateBoundary', () => {
  it('renders the children once loaded without an error', () => {
    render(<DataStateBoundary isLoading={false}>{content}</DataStateBoundary>);

    expect(screen.getByTestId('content')).toHaveTextContent('Loaded rows');
    expect(screen.queryByTestId('data-loading')).not.toBeInTheDocument();
    expect(screen.queryByTestId('data-error')).not.toBeInTheDocument();
    expect(screen.queryByTestId('data-empty')).not.toBeInTheDocument();
  });

  it('shows a progress indicator instead of the children while loading', () => {
    render(<DataStateBoundary isLoading>{content}</DataStateBoundary>);

    expect(screen.getByTestId('data-loading')).toContainElement(screen.getByRole('progressbar'));
    expect(screen.queryByTestId('content')).not.toBeInTheDocument();
  });

  it('prefers the loading state over an error and an empty state', () => {
    render(
      <DataStateBoundary isLoading error="Boom" isEmpty>
        {content}
      </DataStateBoundary>,
    );

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('data-error')).not.toBeInTheDocument();
    expect(screen.queryByTestId('data-empty')).not.toBeInTheDocument();
  });

  it('shows the error instead of the children, preferring it over an empty state', () => {
    render(
      <DataStateBoundary isLoading={false} error="Failed to load tenants" isEmpty>
        {content}
      </DataStateBoundary>,
    );

    expect(screen.getByRole('alert')).toHaveTextContent('Failed to load tenants');
    expect(screen.getByTestId('data-error')).toHaveTextContent('Failed to load tenants');
    expect(screen.queryByTestId('data-empty')).not.toBeInTheDocument();
    expect(screen.queryByTestId('content')).not.toBeInTheDocument();
  });

  it('treats an empty error string as no error', () => {
    render(
      <DataStateBoundary isLoading={false} error="">
        {content}
      </DataStateBoundary>,
    );

    expect(screen.queryByTestId('data-error')).not.toBeInTheDocument();
    expect(screen.getByTestId('content')).toBeInTheDocument();
  });

  it('shows the default empty message instead of the children when empty', () => {
    render(
      <DataStateBoundary isLoading={false} isEmpty>
        {content}
      </DataStateBoundary>,
    );

    expect(screen.getByTestId('data-empty')).toHaveTextContent('Nothing to show yet.');
    expect(screen.queryByTestId('content')).not.toBeInTheDocument();
  });

  it('shows a custom empty message', () => {
    render(
      <DataStateBoundary isLoading={false} isEmpty emptyMessage="No tenants yet.">
        {content}
      </DataStateBoundary>,
    );

    expect(screen.getByTestId('data-empty')).toHaveTextContent('No tenants yet.');
    expect(screen.queryByText('Nothing to show yet.')).not.toBeInTheDocument();
  });
});
