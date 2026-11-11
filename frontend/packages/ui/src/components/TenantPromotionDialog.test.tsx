import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import {
  TenantPromotionDialog,
  type TenantPromotionDialogProps,
  type TenantPromotionDialogReport,
} from './TenantPromotionDialog.js';

const tenantIds = ['dev', 'staging', 'prod'];

const baseReport: TenantPromotionDialogReport = {
  sourceTenantId: 'dev',
  targetTenantId: 'staging',
  isApplied: false,
  changes: [
    { resourceType: 'client', resourceId: 'web', action: 'created' },
    { resourceType: 'resource_server', resourceId: 'api', action: 'updated' },
  ],
  notes: [],
  errors: [],
};

function renderDialog(overrides: Partial<TenantPromotionDialogProps> = {}) {
  const props: TenantPromotionDialogProps = {
    open: true,
    tenantIds,
    onPreview: vi.fn(),
    onPromote: vi.fn(),
    onCancel: vi.fn(),
    ...overrides,
  };
  const view = render(<TenantPromotionDialog {...props} />);
  return { props, ...view };
}

function choose(source: string, target: string) {
  fireEvent.change(screen.getByTestId('promotion-source'), { target: { value: source } });
  fireEvent.change(screen.getByTestId('promotion-target'), { target: { value: target } });
}

describe('TenantPromotionDialog', () => {
  it('disables Preview and Promote until two distinct tenants are chosen', () => {
    renderDialog();
    expect(screen.getByTestId('promotion-preview')).toBeDisabled();
    expect(screen.getByTestId('promotion-promote')).toBeDisabled();

    choose('dev', 'dev');
    expect(screen.getByTestId('promotion-preview')).toBeDisabled();

    choose('dev', 'staging');
    expect(screen.getByTestId('promotion-preview')).toBeEnabled();
    expect(screen.getByTestId('promotion-promote')).toBeDisabled();
  });

  it('previews the chosen pair', () => {
    const { props } = renderDialog();
    choose('dev', 'staging');
    fireEvent.click(screen.getByTestId('promotion-preview'));
    expect(props.onPreview).toHaveBeenCalledWith({
      sourceTenantId: 'dev',
      targetTenantId: 'staging',
      environmentSettings: [],
    });
  });

  it('lists the changes and enables Promote for a clean preview of the chosen pair', () => {
    const { props } = renderDialog({ report: baseReport });
    expect(screen.queryByTestId('promotion-changes')).not.toBeInTheDocument();
    expect(screen.getByTestId('promotion-promote')).toBeDisabled();

    choose('dev', 'staging');
    expect(screen.getByTestId('promotion-changes')).toHaveTextContent('web');
    expect(screen.getByTestId('promotion-changes')).toHaveTextContent('resource_server');
    expect(screen.getByTestId('promotion-changes')).toHaveTextContent('updated');
    expect(screen.getByTestId('promotion-promote')).toBeEnabled();

    fireEvent.click(screen.getByTestId('promotion-promote'));
    expect(props.onPromote).toHaveBeenCalledWith({
      sourceTenantId: 'dev',
      targetTenantId: 'staging',
      environmentSettings: [],
    });
  });

  it('ignores a report for another pair', () => {
    renderDialog({ report: baseReport });
    choose('dev', 'prod');
    expect(screen.queryByTestId('promotion-changes')).not.toBeInTheDocument();
    expect(screen.getByTestId('promotion-promote')).toBeDisabled();
  });

  it('shows an error alert per error and disables Promote', () => {
    renderDialog({
      report: {
        ...baseReport,
        errors: [
          { resourceType: 'client', resourceId: 'web', detail: 'The client is host managed.' },
          { resourceType: 'resource_server', detail: 'The audience is taken.' },
        ],
      },
    });
    choose('dev', 'staging');
    const alerts = screen.getAllByTestId('promotion-report-error');
    expect(alerts).toHaveLength(2);
    expect(alerts[0]).toHaveTextContent('client web: The client is host managed.');
    expect(alerts[1]).toHaveTextContent('resource_server: The audience is taken.');
    expect(screen.getByTestId('promotion-promote')).toBeDisabled();
  });

  it('reads a host_managed note as a client the promotion leaves', () => {
    renderDialog({
      report: {
        ...baseReport,
        notes: [
          {
            resourceType: 'client',
            resourceId: 'bootstrap',
            code: 'host_managed',
            detail: 'The host configures the client.',
          },
        ],
      },
    });
    choose('dev', 'staging');
    expect(screen.getByTestId('promotion-notes')).toHaveTextContent(
      'The promotion leaves client bootstrap. The host configures the client.',
    );
  });

  it('shows the error prop in an alert', () => {
    renderDialog({ error: 'Changing the limit needs more authority.' });
    expect(screen.getByTestId('promotion-error')).toHaveTextContent(
      'Changing the limit needs more authority.',
    );
  });

  it('sends the environment-bound values the preview asks for, grouped by resource', () => {
    const { props } = renderDialog({
      report: {
        ...baseReport,
        notes: [
          {
            resourceType: 'client',
            resourceId: 'web',
            code: 'environment_setting_unset',
            detail: 'Set redirect_uris.',
            settingName: 'redirect_uris',
          },
          {
            resourceType: 'client',
            resourceId: 'web',
            code: 'environment_setting_unset',
            detail: 'Set jwks_uri.',
            settingName: 'jwks_uri',
          },
          {
            resourceType: 'tenant',
            resourceId: 'staging',
            code: 'environment_setting_unset',
            detail: 'Set environment_name.',
            settingName: 'environment_name',
          },
        ],
      },
    });
    choose('dev', 'staging');
    fireEvent.change(screen.getByTestId('environment-setting-client-web-redirect_uris'), {
      target: { value: '["https://staging.example/cb"]' },
    });
    fireEvent.change(screen.getByTestId('environment-setting-client-web-jwks_uri'), {
      target: { value: '"https://staging.example/jwks"' },
    });
    fireEvent.click(screen.getByTestId('promotion-promote'));
    expect(props.onPromote).toHaveBeenCalledWith({
      sourceTenantId: 'dev',
      targetTenantId: 'staging',
      environmentSettings: [
        {
          resourceType: 'client',
          resourceId: 'web',
          settings: {
            redirect_uris: ['https://staging.example/cb'],
            jwks_uri: 'https://staging.example/jwks',
          },
        },
      ],
    });
  });

  it('flags invalid JSON and disables Promote until it is fixed', () => {
    renderDialog({
      report: {
        ...baseReport,
        notes: [
          {
            resourceType: 'client',
            resourceId: 'web',
            code: 'environment_setting_unset',
            detail: 'Set redirect_uris.',
            settingName: 'redirect_uris',
          },
        ],
      },
    });
    choose('dev', 'staging');
    const field = screen.getByTestId('environment-setting-client-web-redirect_uris');
    fireEvent.change(field, { target: { value: '[oops' } });
    expect(field).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Enter a valid JSON value.')).toBeInTheDocument();
    expect(screen.getByTestId('promotion-promote')).toBeDisabled();

    fireEvent.change(field, { target: { value: '[]' } });
    expect(field).toHaveAttribute('aria-invalid', 'false');
    expect(screen.getByTestId('promotion-promote')).toBeEnabled();
  });

  it('shows a success alert and a Close button for an applied report', () => {
    const { props } = renderDialog({ report: { ...baseReport, isApplied: true } });
    choose('dev', 'staging');
    expect(screen.getByTestId('promotion-applied')).toHaveTextContent('Promoted.');
    expect(screen.queryByTestId('promotion-promote')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('promotion-close'));
    expect(props.onCancel).toHaveBeenCalled();
  });

  it('disables the actions while busy', () => {
    renderDialog({ report: baseReport, isBusy: true });
    choose('dev', 'staging');
    expect(screen.getByTestId('promotion-preview')).toBeDisabled();
    expect(screen.getByTestId('promotion-promote')).toBeDisabled();
    expect(screen.getByTestId('promotion-cancel')).toBeDisabled();
  });

  it('cancels', () => {
    const { props } = renderDialog();
    fireEvent.click(screen.getByTestId('promotion-cancel'));
    expect(props.onCancel).toHaveBeenCalled();
  });

  it('resets the selections each time it opens', () => {
    const { props, rerender } = renderDialog();
    choose('dev', 'staging');
    rerender(<TenantPromotionDialog {...props} open />);
    expect(screen.getByTestId('promotion-source')).toHaveValue('dev');

    rerender(<TenantPromotionDialog {...props} open={false} />);
    rerender(<TenantPromotionDialog {...props} open />);
    expect(screen.getByTestId('promotion-source')).toHaveValue('');
    expect(screen.getByTestId('promotion-target')).toHaveValue('');
  });
});
