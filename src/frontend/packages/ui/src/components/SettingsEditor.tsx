import { useEffect, useRef, useState } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';

export type SettingsEditorProps = {
  /** The persisted settings object as pretty-printed JSON; reseeds the editor when it changes. */
  readonly initialJson: string;
  /** The read-only effective (merged) settings as pretty-printed JSON. */
  readonly effectiveJson: string;
  readonly isSaving?: boolean;
  /** A server-side error surfaced after a failed save. */
  readonly error?: string | undefined;
  readonly onSave: (settings: Record<string, unknown>) => void;
};

/** Parses editor text into a plain JSON object, or returns an error message for the editor to show. */
function parseSettings(text: string): { object: Record<string, unknown> } | { error: string } {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    return { error: 'Settings must be valid JSON.' };
  }
  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return { error: 'Settings must be a JSON object.' };
  }
  return { object: parsed as Record<string, unknown> };
}

/**
 * Stateless settings editor. The persisted settings are an opaque JSON object, so the editor exposes
 * them as editable JSON text alongside a read-only view of the effective (merged) settings the server
 * resolves at runtime. The text and parse-error state are intrinsic to the control (they reseed when
 * {@link SettingsEditorProps.initialJson} changes after a save), so the component still renders in
 * isolation under Storybook/RTL. Persistence (the JSON Patch diff) and server-error handling live in
 * the container; {@link SettingsEditorProps.onSave} receives the parsed desired object.
 */
export function SettingsEditor({
  initialJson,
  effectiveJson,
  isSaving = false,
  error,
  onSave,
}: SettingsEditorProps) {
  const [text, setText] = useState(initialJson);
  const previousInitialRef = useRef(initialJson);

  useEffect(() => {
    if (initialJson !== previousInitialRef.current) {
      setText(initialJson);
      previousInitialRef.current = initialJson;
    }
  }, [initialJson]);

  const parsed = parseSettings(text);
  const parseError = 'error' in parsed ? parsed.error : undefined;
  const isDirty = text !== initialJson;

  const handleSave = () => {
    if (parseError || !isDirty || isSaving || 'error' in parsed) {
      return;
    }
    onSave(parsed.object);
  };

  return (
    <Stack spacing={2}>
      {error && (
        <Alert severity="error" data-testid="settings-error">
          {error}
        </Alert>
      )}
      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Settings
        </Typography>
        <TextField
          value={text}
          onChange={(event) => setText(event.target.value)}
          error={Boolean(parseError)}
          helperText={parseError}
          multiline
          minRows={6}
          fullWidth
          slotProps={{
            htmlInput: {
              'data-testid': 'settings-json',
              spellCheck: false,
              style: { fontFamily: 'monospace' },
            },
          }}
        />
      </Box>
      <Box display="flex" justifyContent="flex-end">
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={Boolean(parseError) || !isDirty || isSaving}
          data-testid="settings-save"
        >
          {isSaving ? 'Saving…' : 'Save settings'}
        </Button>
      </Box>
      <Box>
        <Typography variant="subtitle2" gutterBottom>
          Effective settings (read-only)
        </Typography>
        <TextField
          value={effectiveJson}
          multiline
          minRows={6}
          fullWidth
          slotProps={{
            input: { readOnly: true },
            htmlInput: {
              'data-testid': 'effective-settings-json',
              spellCheck: false,
              style: { fontFamily: 'monospace' },
            },
          }}
        />
      </Box>
    </Stack>
  );
}
