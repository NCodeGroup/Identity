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
});
