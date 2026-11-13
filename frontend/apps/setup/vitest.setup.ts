import '@testing-library/jest-dom/vitest';
import { configure } from '@testing-library/react';
import { afterEach, beforeEach } from 'vitest';
import { configureAsyncUtilTimeout, failOnConsole } from '@ncode/test-support';

// Warnings-as-errors for tests: any console.error/console.warn fails the test that logged it (ADR-1151).
failOnConsole({ beforeEach, afterEach });

// Every Testing Library wait gets the one deliberate timeout (see "Testing Library and test timeouts" in the frontend
// README).
configureAsyncUtilTimeout(configure);

// The host serves the page with its base set to the setup surface, so the bundle's relative URLs resolve beneath it; the
// tests run with the host served at the root.
const base = document.createElement('base');
base.setAttribute('href', '/setup/');
document.head.append(base);
