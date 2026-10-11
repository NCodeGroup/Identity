/**
 * Minimal JSON Patch (RFC 6902) support for the Management write API. The server binds update
 * endpoints as `JsonPatchDocument<T>` (SystemTextJsonPatch) and hydrates the current resource before
 * applying the document, so the UI expresses a full desired state as a set of `replace` operations —
 * one per mutable field. This module is UI-free so the wire shape is unit-tested in isolation
 * (see docs/adr/0064).
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
