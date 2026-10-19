import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SettingsEditor } from './SettingsEditor.js';

const initialJson = JSON.stringify({ accessTokenLifetime: 3600 }, null, 2);
const effectiveJson = JSON.stringify(
  { accessTokenLifetime: 3600, issuer: 'https://op.test' },
  null,
  2,
);

describe('SettingsEditor', () => {
  it('disables save until the text is edited', () => {
    render(
      <SettingsEditor initialJson={initialJson} effectiveJson={effectiveJson} onSave={vi.fn()} />,
    );
    expect(screen.getByTestId('settings-save')).toBeDisabled();
    fireEvent.change(screen.getByTestId('settings-json'), { target: { value: '{}' } });
    expect(screen.getByTestId('settings-save')).toBeEnabled();
    fireEvent.change(screen.getByTestId('settings-json'), { target: { value: initialJson } });
    expect(screen.getByTestId('settings-save')).toBeDisabled();
  });

  it('saves the parsed object after a valid edit', () => {
    const onSave = vi.fn();
    render(
      <SettingsEditor initialJson={initialJson} effectiveJson={effectiveJson} onSave={onSave} />,
    );
    fireEvent.change(screen.getByTestId('settings-json'), {
      target: { value: '{"accessTokenLifetime": 7200}' },
    });
    fireEvent.click(screen.getByTestId('settings-save'));
    expect(onSave).toHaveBeenCalledWith({ accessTokenLifetime: 7200 });
  });

  it('blocks save and shows a helper error for invalid JSON', () => {
    const onSave = vi.fn();
    render(
      <SettingsEditor initialJson={initialJson} effectiveJson={effectiveJson} onSave={onSave} />,
    );
    fireEvent.change(screen.getByTestId('settings-json'), { target: { value: '{ not json' } });
    expect(screen.getByTestId('settings-save')).toBeDisabled();
    expect(screen.getByText('Settings must be valid JSON.')).toBeInTheDocument();
  });

  it('rejects a non-object JSON value', () => {
    render(
      <SettingsEditor initialJson={initialJson} effectiveJson={effectiveJson} onSave={vi.fn()} />,
    );
    fireEvent.change(screen.getByTestId('settings-json'), { target: { value: '[1, 2, 3]' } });
    expect(screen.getByText('Settings must be a JSON object.')).toBeInTheDocument();
  });

  it('surfaces a server error', () => {
    render(
      <SettingsEditor
        initialJson={initialJson}
        effectiveJson={effectiveJson}
        error="Conflict"
        onSave={vi.fn()}
      />,
    );
    expect(screen.getByTestId('settings-error')).toHaveTextContent('Conflict');
  });

  it('shows the persisted settings for editing and the effective settings read-only', () => {
    render(
      <SettingsEditor initialJson={initialJson} effectiveJson={effectiveJson} onSave={vi.fn()} />,
    );
    expect(screen.getByTestId('settings-json')).toHaveValue(initialJson);
    expect(screen.getByTestId('effective-settings-json')).toHaveValue(effectiveJson);
    expect(screen.getByTestId('effective-settings-json')).toHaveAttribute('readonly');
    expect(screen.queryByTestId('settings-error')).not.toBeInTheDocument();
    expect(screen.getByTestId('settings-save')).toHaveTextContent('Save settings');
  });

  it.each(['null', '42', '"text"'])('rejects the non-object JSON value %s', (value) => {
    render(
      <SettingsEditor initialJson={initialJson} effectiveJson={effectiveJson} onSave={vi.fn()} />,
    );
    fireEvent.change(screen.getByTestId('settings-json'), { target: { value } });
    expect(screen.getByText('Settings must be a JSON object.')).toBeInTheDocument();
    expect(screen.getByTestId('settings-save')).toBeDisabled();
  });

  it('disables save while saving a valid edit', () => {
    render(
      <SettingsEditor
        initialJson={initialJson}
        effectiveJson={effectiveJson}
        isSaving
        onSave={vi.fn()}
      />,
    );
    fireEvent.change(screen.getByTestId('settings-json'), {
      target: { value: '{"accessTokenLifetime": 7200}' },
    });
    expect(screen.getByTestId('settings-save')).toBeDisabled();
    expect(screen.getByTestId('settings-save')).toHaveTextContent('Saving…');
  });

  it('keeps in-progress edits until the persisted settings change, then reseeds', () => {
    const onSave = vi.fn();
    const { rerender } = render(
      <SettingsEditor initialJson={initialJson} effectiveJson={effectiveJson} onSave={onSave} />,
    );
    const edited = '{"accessTokenLifetime": 7200}';
    fireEvent.change(screen.getByTestId('settings-json'), { target: { value: edited } });

    rerender(
      <SettingsEditor initialJson={initialJson} effectiveJson={effectiveJson} onSave={onSave} />,
    );
    expect(screen.getByTestId('settings-json')).toHaveValue(edited);
    expect(screen.getByTestId('settings-save')).toBeEnabled();

    const savedJson = JSON.stringify({ accessTokenLifetime: 7200 }, null, 2);
    rerender(
      <SettingsEditor initialJson={savedJson} effectiveJson={effectiveJson} onSave={onSave} />,
    );
    expect(screen.getByTestId('settings-json')).toHaveValue(savedJson);
    expect(screen.getByTestId('settings-save')).toBeDisabled();
  });
});
