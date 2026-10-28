/**
 * Minimal JSON Patch (RFC 6902) support for the Management write API. The server binds update
 * endpoints as `JsonPatchDocument<T>` (SystemTextJsonPatch) and hydrates the current resource before
 * applying the document, so the UI expresses a full desired state as a set of `replace` operations —
 * one per mutable field. This module is UI-free so the wire shape is unit-tested in isolation
 * (see docs/decisions/0064).
 */

/** A single JSON Patch operation. The Management update endpoints only ever need `replace`. */
export type JsonPatchOperation = {
  readonly op: 'replace';
  readonly path: string;
  readonly value: unknown;
};

/** A JSON Patch document: an ordered list of operations (RFC 6902). */
export type JsonPatchDocument = readonly JsonPatchOperation[];

/**
 * Builds a `replace`-only JSON Patch document from a flat map of wire property names to new values.
 * Keys are the JSON property names the server deserializes (e.g. `displayName`), which become the
 * JSON Pointer path `/displayName`. Property names are emitted verbatim; callers pass the exact
 * contract names.
 */
export function replacePatch(fields: Readonly<Record<string, unknown>>): JsonPatchDocument {
  return Object.entries(fields).map(([name, value]) => ({
    op: 'replace' as const,
    path: `/${name}`,
    value,
  }));
}

/**
 * A full JSON Patch operation for the opaque settings object, which — unlike the fixed-shape metadata
 * endpoints — may add or remove keys, so `add` and `remove` are needed alongside `replace` (RFC 6902).
 */
export type SettingsPatchOperation =
  | { readonly op: 'add'; readonly path: string; readonly value: unknown }
  | { readonly op: 'replace'; readonly path: string; readonly value: unknown }
  | { readonly op: 'remove'; readonly path: string };

/** A JSON Patch document over the settings object (an ordered list of {@link SettingsPatchOperation}). */
export type SettingsPatchDocument = readonly SettingsPatchOperation[];

/** Escapes a JSON object key into a JSON Pointer reference token (RFC 6901): `~` → `~0`, `/` → `~1`. */
function encodePointerToken(key: string): string {
  return key.replace(/~/g, '~0').replace(/\//g, '~1');
}

/**
 * Builds a top-level JSON Patch diff between the original and desired settings objects: `remove` for
 * dropped keys, `add` for new keys, and `replace` for keys whose value changed (deep-equal by JSON
 * serialization). Only the top level is diffed — a changed nested value replaces the whole key — which
 * matches how the settings object is edited as a whole. UI-free so the wire shape is unit-tested.
 */
export function diffPatch(
  original: Readonly<Record<string, unknown>>,
  desired: Readonly<Record<string, unknown>>,
): SettingsPatchDocument {
  const operations: SettingsPatchOperation[] = [];

  for (const key of Object.keys(original)) {
    if (!(key in desired)) {
      operations.push({ op: 'remove', path: `/${encodePointerToken(key)}` });
    }
  }

  for (const [key, value] of Object.entries(desired)) {
    const path = `/${encodePointerToken(key)}`;
    if (!(key in original)) {
      operations.push({ op: 'add', path, value });
    } else if (JSON.stringify(original[key]) !== JSON.stringify(value)) {
      operations.push({ op: 'replace', path, value });
    }
  }

  return operations;
}
