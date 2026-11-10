import { describe, expect, it } from 'vitest';
import indexHtml from '../index.html?raw';

// The kit's favicon files the app carries; build/favicon-copies.ps1 keeps them equal to brand/favicon/ (ADR-1492).
const carried = import.meta.glob('../favicon/*', { query: '?url', import: 'default', eager: true });

describe('the family icon set', () => {
  it('is linked from the index with the brand kit tags and no manifest', () => {
    const head = new DOMParser().parseFromString(indexHtml, 'text/html').head;
    const links = Array.from(head.querySelectorAll('link'), (link) => [
      link.getAttribute('rel'),
      link.getAttribute('href'),
      link.getAttribute('sizes'),
      link.getAttribute('type'),
    ]);
    expect(links).toEqual([
      ['icon', './favicon/favicon.ico', '32x32', null],
      ['icon', './favicon/favicon.svg', null, 'image/svg+xml'],
      ['apple-touch-icon', './favicon/apple-touch-icon.png', null, null],
    ]);
  });

  it('is carried beside the index', () => {
    expect(new Set(Object.keys(carried))).toEqual(
      new Set([
        '../favicon/apple-touch-icon.png',
        '../favicon/favicon.ico',
        '../favicon/favicon.svg',
      ]),
    );
  });
});
