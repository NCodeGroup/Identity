import '@testing-library/jest-dom/vitest';
import { afterEach, beforeEach } from 'vitest';
import { failOnConsole } from '@ncode/test-support';

// Warnings-as-errors for tests: any console.error/console.warn fails the test that logged it (ADR-0151).
failOnConsole({ beforeEach, afterEach });

// The host serves every page with its base set to the surface beneath the tenant (ADR-0193); the tests run at the
// surface of a tenant served at the root.
const base = document.createElement('base');
base.setAttribute('href', '/account/');
document.head.append(base);
