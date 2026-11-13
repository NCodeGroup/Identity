import { describe, expect, it } from 'vitest';
import { mergeConsoleHint } from './consoleHint.js';

describe('mergeConsoleHint', () => {
  const hint = { baseUrl: 'https://server.example.test', clientId: 'server-client' };
  const fragment = {
    consoleUrl: 'https://fragment.example.test',
    consoleClientId: 'fragment-client',
  };

  it('lets the server’s hint win over the fragment', () => {
    expect(mergeConsoleHint(hint, fragment)).toEqual(hint);
  });

  it('fills from the fragment only what the server’s hint leaves empty', () => {
    expect(mergeConsoleHint(undefined, fragment)).toEqual({
      baseUrl: 'https://fragment.example.test',
      clientId: 'fragment-client',
    });
    expect(mergeConsoleHint({ baseUrl: hint.baseUrl, clientId: '' }, fragment)).toEqual({
      baseUrl: 'https://server.example.test',
      clientId: 'fragment-client',
    });
  });

  it('returns an empty hint when neither names a console', () => {
    expect(mergeConsoleHint(undefined, {})).toEqual({});
  });
});
