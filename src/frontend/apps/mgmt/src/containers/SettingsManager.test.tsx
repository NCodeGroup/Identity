import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import type { EffectiveSettingsResource, SettingsResource } from '@ncode/api';
import { SettingsManager, type SettingsManagerProps } from './SettingsManager.js';

const settings: SettingsResource = { concurrencyToken: 'st1', settings: { a: 1 } };
const effective: EffectiveSettingsResource = { settings: { a: 1, issuer: 'https://op.test' } };

function renderManager(props: Partial<SettingsManagerProps> = {}) {
  const onSave = vi.fn<SettingsManagerProps['onSave']>().mockResolvedValue(undefined);
  render(
    <SettingsManager
      settings={settings}
      effective={effective}
      limits={{ names: [], canChange: true }}
      isLoading={false}
      loadError={false}
      isSaving={false}
      onSave={onSave}
      {...props}
    />,
  );
  return onSave;
}

function pretty(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

describe('SettingsManager', () => {
  it('shows a loading indicator while the settings load', () => {
    renderManager({ settings: undefined, effective: undefined, isLoading: true });

    expect(screen.getByTestId('data-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('settings-json')).not.toBeInTheDocument();
  });

  it('reports a failure to load the settings', () => {
    renderManager({ settings: undefined, effective: undefined, loadError: true });

    expect(screen.getByTestId('data-error')).toHaveTextContent('Failed to load settings.');
  });

  it('edits the persisted settings next to the read-only effective settings', () => {
    renderManager();

    expect(screen.getByTestId('settings-json')).toHaveValue(pretty({ a: 1 }));
    expect(screen.getByTestId('effective-settings-json')).toHaveValue(
      pretty({ a: 1, issuer: 'https://op.test' }),
    );
  });

  it('shows empty objects when no settings or effective settings are present', () => {
    renderManager({ settings: undefined, effective: undefined });

    expect(screen.getByTestId('settings-json')).toHaveValue('{}');
    expect(screen.getByTestId('effective-settings-json')).toHaveValue('{}');
  });

  it('saves the desired settings with the owner concurrency token and the original object', () => {
    const onSave = renderManager();

    fireEvent.change(screen.getByTestId('settings-json'), {
      target: { value: '{"a": 2, "b": 3}' },
    });
    fireEvent.click(screen.getByTestId('settings-save'));

    expect(onSave).toHaveBeenCalledWith({
      concurrencyToken: 'st1',
      original: { a: 1 },
      desired: { a: 2, b: 3 },
    });
    expect(screen.queryByTestId('settings-error')).not.toBeInTheDocument();
  });

  it('surfaces a rejected save with the server problem detail', async () => {
    const onSave = renderManager();
    onSave.mockRejectedValue({
      status: 412,
      data: { detail: 'The settings changed since you loaded them.' },
    });

    fireEvent.change(screen.getByTestId('settings-json'), { target: { value: '{"a": 2}' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    expect(await screen.findByTestId('settings-error')).toHaveTextContent(
      'The settings changed since you loaded them.',
    );
  });

  it('moves the limits out of the editor and shows them read-only when the caller cannot change them', () => {
    renderManager({
      settings: { concurrencyToken: 'st1', settings: { a: 1, max_tenants: 5 } },
      limits: { names: ['max_tenants'], canChange: false },
    });

    expect(screen.getByTestId('settings-json')).toHaveValue(pretty({ a: 1 }));
    expect(screen.getByTestId('locked-settings-json')).toHaveValue(pretty({ max_tenants: 5 }));
  });

  it('keeps the locked entries in the desired settings when the caller cannot change the limits', () => {
    const onSave = renderManager({
      settings: { concurrencyToken: 'st1', settings: { a: 1, max_tenants: 5 } },
      limits: { names: ['max_tenants'], canChange: false },
    });

    fireEvent.change(screen.getByTestId('settings-json'), { target: { value: '{"a": 2}' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    expect(onSave).toHaveBeenCalledWith({
      concurrencyToken: 'st1',
      original: { a: 1, max_tenants: 5 },
      desired: { a: 2, max_tenants: 5 },
    });
  });

  it('edits the whole document when the caller can change the limits', () => {
    const onSave = renderManager({
      settings: { concurrencyToken: 'st1', settings: { a: 1, max_tenants: 5 } },
      limits: { names: ['max_tenants'], canChange: true },
    });

    expect(screen.getByTestId('settings-json')).toHaveValue(pretty({ a: 1, max_tenants: 5 }));
    expect(screen.queryByTestId('locked-settings-json')).not.toBeInTheDocument();

    fireEvent.change(screen.getByTestId('settings-json'), {
      target: { value: '{"a": 1, "max_tenants": 9}' },
    });
    fireEvent.click(screen.getByTestId('settings-save'));

    expect(onSave).toHaveBeenCalledWith({
      concurrencyToken: 'st1',
      original: { a: 1, max_tenants: 5 },
      desired: { a: 1, max_tenants: 9 },
    });
  });

  it('does not save when no persisted settings are loaded to diff against', () => {
    const onSave = renderManager({ settings: undefined });

    fireEvent.change(screen.getByTestId('settings-json'), { target: { value: '{"a": 2}' } });
    fireEvent.click(screen.getByTestId('settings-save'));

    expect(onSave).not.toHaveBeenCalled();
  });
});
