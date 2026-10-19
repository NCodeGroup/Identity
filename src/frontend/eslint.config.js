// The single lint configuration for every workspace package. ESLint 9+ reads only this flat format and resolves it by
// walking up from each linted file, so each package's `eslint "src/**/*.{ts,tsx}"` script finds this root file.
import eslintReact from '@eslint-react/eslint-plugin';
import js from '@eslint/js';
import prettier from 'eslint-config-prettier/flat';
import reactHooks from 'eslint-plugin-react-hooks';
import { defineConfig, globalIgnores } from 'eslint/config';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default defineConfig([
  globalIgnores([
    '**/dist',
    '**/storybook-static',
    '**/coverage',
    '**/test-results',
    '**/*.config.{ts,js}',
  ]),
  js.configs.recommended,
  tseslint.configs.recommended,
  reactHooks.configs.flat.recommended,
  eslintReact.configs['recommended-typescript'],
  // @eslint-react ships its own Rules-of-Hooks family; this preset turns off the react-hooks duplicates so each finding
  // is reported once, leaving react-hooks for the React Compiler checks @eslint-react does not cover.
  eslintReact.configs['disable-conflict-eslint-plugin-react-hooks'],
  {
    languageOptions: {
      globals: { ...globals.browser, ...globals.node },
    },
    rules: {
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/consistent-type-imports': 'error',
    },
  },
  // Must stay last: turns off every stylistic rule that would fight Prettier.
  prettier,
]);
