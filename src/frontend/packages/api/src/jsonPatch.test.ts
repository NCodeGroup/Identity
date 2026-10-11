import { describe, expect, it } from 'vitest';
import { replacePatch } from './jsonPatch.js';

describe('replacePatch', () => {
  it('maps each field to a replace operation at its JSON Pointer path', () => {
    const patch = replacePatch({ displayName: 'Acme', isDisabled: true });
    expect(patch).toEqual([
      { op: 'replace', path: '/displayName', value: 'Acme' },
      { op: 'replace', path: '/isDisabled', value: true },
    ]);
  });

  it('preserves null values (e.g. clearing an optional domain)', () => {
    expect(replacePatch({ domainName: null })).toEqual([
      { op: 'replace', path: '/domainName', value: null },
    ]);
  });

  it('produces an empty document for no fields', () => {
    expect(replacePatch({})).toEqual([]);
  });
});
