import { describe, expect, it } from 'vitest';
import { FAMILY_ICON_FILE_NAMES } from './familyIcons.js';

describe('FAMILY_ICON_FILE_NAMES', () => {
  it('names the file each family icon link points at', () => {
    expect(FAMILY_ICON_FILE_NAMES).toEqual(['favicon.ico', 'favicon.svg', 'apple-touch-icon.png']);
  });
});
