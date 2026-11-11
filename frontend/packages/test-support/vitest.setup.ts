import { afterEach, beforeEach } from 'vitest';
import { failOnConsole } from './src/consoleGuard.js';

failOnConsole({ beforeEach, afterEach });
