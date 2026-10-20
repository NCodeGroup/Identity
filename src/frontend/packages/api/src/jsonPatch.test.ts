import { describe, expect, it } from 'vitest';
import { diffPatch, replacePatch } from './jsonPatch.js';

describe('replacePatch', () => {
  it('maps each field to a replace operation at its JSON Pointer path', () => {
    const patch = replacePatch({ displayName: 'Acme', isDisabled: true });
    expect(patch).toEqual([
      { op: 'replace', path: '/displayName', value: 'Acme' },
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('preserves null values (e.g. clearing an optional domain)', () => {
    expect(replacePatch({ issuer: null })).toEqual([
      { op: 'replace', path: '/issuer', value: null },
    ]);
  });

  it('produces an empty document for no fields', () => {
    expect(replacePatch({})).toEqual([]);
  });
});

describe('diffPatch', () => {
  it('emits add for new keys, remove for dropped keys, and replace for changed keys', () => {
    const patch = diffPatch(
      { keep: 1, drop: 2, change: 'old' },
      { keep: 1, change: 'new', added: true },
    );
    expect(patch).toEqual([
      { op: 'remove', path: '/drop' },
      { op: 'replace', path: '/change', value: 'new' },
      { op: 'add', path: '/added', value: true },
    ]);
  });

  it('produces an empty document when nothing changed (deep-equal values)', () => {
    expect(diffPatch({ nested: { a: 1 } }, { nested: { a: 1 } })).toEqual([]);
  });

  it('escapes JSON Pointer reference tokens in keys', () => {
    expect(diffPatch({}, { 'a/b~c': 1 })).toEqual([{ op: 'add', path: '/a~1b~0c', value: 1 }]);
  });
});
