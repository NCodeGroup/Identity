import '@testing-library/jest-dom/vitest';
import { afterEach, beforeEach } from 'vitest';
import { failOnConsole } from '@ncode/test-support';

// Warnings-as-errors for tests: any console.error/console.warn fails the test that logged it (ADR-0151).
failOnConsole({ beforeEach, afterEach });
