import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Plugin, PluginOption } from 'vite';
import { ASYNC_UTIL_TIMEOUT_MS } from '@ncode/test-support';
import {
  BROWSER_TEST_TIMEOUT_MS,
  CHUNK_SIZE_LIMIT_KB,
  CSP_NONCE_PLACEHOLDER,
  defineBackendServedSurfaceConfig,
  defineStandaloneSurfaceConfig,
  defineTestConfig,
  devCertificateDirectory,
  failOnOversizedChunks,
  oversizedChunks,
  readDevCertificate,
  resolveServiceOrigin,
  serveOverHttps,
  testGates,
  VENDOR_CHUNKS,
  type BundleOutput,
  type CoverageFloors,
} from './preset.js';

const floors: CoverageFloors = { statements: 97, branches: 95, functions: 100, lines: 96 };

const api = { service: 'api', fallbackOrigin: 'https://localhost:5001' };

function pluginNames(option: PluginOption): string[] {
  if (Array.isArray(option)) {
    return option.flatMap(pluginNames);
  }
  return option && typeof option === 'object' && 'name' in option ? [(option as Plugin).name] : [];
}

afterEach(() => {
  vi.unstubAllEnvs();
});

const expectedBuild = {
  outDir: 'dist',
  assetsDir: 'assets',
  sourcemap: true,
  chunkSizeWarningLimit: CHUNK_SIZE_LIMIT_KB,
  rolldownOptions: {
    output: {
      codeSplitting: {
        groups: VENDOR_CHUNKS.map(({ name, test }, index) => ({
          name,
          test,
          priority: VENDOR_CHUNKS.length - index,
        })),
      },
    },
  },
};

function chunk(fileName: string, sizeKb: number): BundleOutput {
  return { type: 'chunk', fileName, code: 'x'.repeat(sizeKb * 1000) };
}

describe('VENDOR_CHUNKS', () => {
  function vendorChunkOf(moduleId: string): string | undefined {
    return VENDOR_CHUNKS.find(({ test }) => test.test(moduleId))?.name;
  }

  it.each([
    ['/w/node_modules/.pnpm/react@19/node_modules/react/index.js', 'react'],
    ['/w/node_modules/.pnpm/react-dom@19/node_modules/react-dom/client.js', 'react'],
    ['/w/node_modules/.pnpm/scheduler@0/node_modules/scheduler/index.js', 'react'],
    ['/w/node_modules/.pnpm/@mui+material@9/node_modules/@mui/material/Tab/Tab.js', 'mui'],
    ['/w/node_modules/.pnpm/@emotion+react@11/node_modules/@emotion/react/dist/x.js', 'mui'],
    ['/w/node_modules/.pnpm/react-redux@9/node_modules/react-redux/dist/x.js', 'redux'],
    ['/w/node_modules/.pnpm/@reduxjs+toolkit@2/node_modules/@reduxjs/toolkit/dist/x.js', 'redux'],
    ['/w/node_modules/.pnpm/clsx@2/node_modules/clsx/dist/clsx.mjs', 'vendor'],
    ['C:\\w\\node_modules\\react\\index.js', 'react'],
    ['/w/packages/ui/src/index.ts', undefined],
  ])('puts %s in the %s chunk', (moduleId, expected) => {
    expect(vendorChunkOf(moduleId)).toBe(expected);
  });
});

describe('oversizedChunks', () => {
  it('lists only the code chunks above the limit', () => {
    const bundle: Record<string, BundleOutput> = {
      'big.js': chunk('assets/big.js', 501),
      'edge.js': chunk('assets/edge.js', 500),
      'big.css': { type: 'asset', fileName: 'assets/big.css' },
    };

    expect(oversizedChunks(bundle, 500)).toStrictEqual([
      { fileName: 'assets/big.js', sizeKb: 501 },
    ]);
  });
});

describe('failOnOversizedChunks', () => {
  type GenerateBundle = (
    this: { error(message: string): never },
    options: unknown,
    bundle: Record<string, BundleOutput>,
  ) => void;

  function generate(plugin: Plugin, bundle: Record<string, BundleOutput>): void {
    const context = {
      error(message: string): never {
        throw new Error(message);
      },
    };
    (plugin.generateBundle as unknown as GenerateBundle).call(context, {}, bundle);
  }

  it('runs only in builds', () => {
    expect(failOnOversizedChunks().apply).toBe('build');
  });

  it('fails the build on a chunk above the default limit', () => {
    const plugin = failOnOversizedChunks();

    expect(() => generate(plugin, { 'a.js': chunk('assets/a.js', 501) })).toThrow(
      /Chunks larger than 500 kB after minification: assets\/a\.js \(501\.00 kB\)/,
    );
    expect(() => generate(plugin, { 'a.js': chunk('assets/a.js', 500) })).not.toThrow();
  });

  it('holds the build to its configured chunk-size limit', () => {
    const plugin = failOnOversizedChunks();
    (plugin.configResolved as unknown as (config: unknown) => void)({
      build: { chunkSizeWarningLimit: 100 },
    });

    expect(() =>
      generate(plugin, { 'a.js': chunk('assets/a.js', 101), 'b.js': chunk('assets/b.js', 99) }),
    ).toThrow(/larger than 100 kB after minification: assets\/a\.js \(101\.00 kB\)\. Split/);
  });
});

describe('testGates', () => {
  it('applies the gates every workspace holds', () => {
    const test = testGates({ environment: 'node', coverage: floors });

    expect(test).toStrictEqual({
      environment: 'node',
      setupFiles: ['./vitest.setup.ts'],
      expect: { requireAssertions: true },
      include: ['src/**/*.test.{ts,tsx}'],
      coverage: {
        provider: 'v8',
        include: ['src/**/*.{ts,tsx}'],
        exclude: [
          'src/**/*.test.{ts,tsx}',
          'src/**/*.stories.{ts,tsx}',
          'src/main.{ts,tsx}',
          'src/index.ts',
        ],
        thresholds: floors,
      },
    });
  });

  it('adds the DOM settings for an environment that simulates a browser', () => {
    const test = testGates({ environment: 'jsdom', coverage: floors });

    expect(test.environment).toBe('jsdom');
    expect(test.testTimeout).toBe(BROWSER_TEST_TIMEOUT_MS);
    expect(test.globals).toBe(true);
  });

  it('leaves room for three Testing Library waits before a browser test times out', () => {
    expect(BROWSER_TEST_TIMEOUT_MS).toBeGreaterThanOrEqual(3 * ASYNC_UTIL_TIMEOUT_MS);
  });

  it('copies the floors rather than sharing the caller object', () => {
    const test = testGates({ environment: 'node', coverage: floors });

    expect(test.coverage).toHaveProperty('thresholds');
    expect((test.coverage as { thresholds: unknown }).thresholds).not.toBe(floors);
  });

  it.each([
    ['above 100', { ...floors, lines: 101 }, 'lines'],
    ['below 0', { ...floors, statements: -1 }, 'statements'],
    ['not a number', { ...floors, branches: Number.NaN }, 'branches'],
    ['missing', { statements: 1, branches: 1, lines: 1 } as unknown as CoverageFloors, 'functions'],
  ])('rejects a floor %s', (_case, coverage, metric) => {
    expect(() => testGates({ environment: 'node', coverage })).toThrow(
      new RangeError(`Coverage floor '${metric}' must be a number from 0 to 100.`),
    );
  });
});

describe('defineTestConfig', () => {
  it('holds only the test gates', () => {
    expect(defineTestConfig({ environment: 'node', coverage: floors })).toStrictEqual({
      test: testGates({ environment: 'node', coverage: floors }),
    });
  });
});

describe('resolveServiceOrigin', () => {
  it('prefers the HTTPS service-discovery origin', () => {
    const env = {
      services__api__https__0: 'https://localhost:7001',
      services__api__http__0: 'http://localhost:7000',
    };

    expect(resolveServiceOrigin(api, env)).toBe('https://localhost:7001');
  });

  it('falls back to the HTTP service-discovery origin', () => {
    expect(resolveServiceOrigin(api, { services__api__http__0: 'http://localhost:7000' })).toBe(
      'http://localhost:7000',
    );
  });

  it('reads the variables of the named service only', () => {
    const env = { services__conformanceapi__https__0: 'https://localhost:7003' };

    expect(resolveServiceOrigin(api, env)).toBe('https://localhost:5001');
    expect(
      resolveServiceOrigin({ service: 'conformanceapi', fallbackOrigin: 'https://x.test' }, env),
    ).toBe('https://localhost:7003');
  });

  it('rejects an unnamed service', () => {
    expect(() => resolveServiceOrigin({ ...api, service: '' }, {})).toThrow(
      new RangeError('The bootstrap host must name its Aspire service.'),
    );
  });

  it.each(['localhost:5001', 'not a url', 'ftp://localhost:5001'])(
    'rejects fallback origin %j, which is not an absolute HTTP(S) URL',
    (fallbackOrigin) => {
      expect(() => resolveServiceOrigin({ ...api, fallbackOrigin }, {})).toThrow(
        new RangeError(`Fallback origin '${fallbackOrigin}' must be an absolute HTTP(S) URL.`),
      );
    },
  );
});

describe('devCertificateDirectory', () => {
  it('defaults to the .ncode directory of the home directory', () => {
    expect(devCertificateDirectory({})).toBe(join(homedir(), '.ncode', 'identity', 'dev-cert'));
  });

  it('is overridden by NCODE_DEV_CERT_DIR', () => {
    expect(devCertificateDirectory({ NCODE_DEV_CERT_DIR: 'x' })).toBe('x');
  });
});

describe('development certificate', () => {
  type ConfigHook = (config: unknown, env: { mode: string; isPreview?: boolean }) => unknown;

  let directory = '';

  function exportCertificate(): string {
    directory = mkdtempSync(join(tmpdir(), 'ncode-dev-cert-'));
    writeFileSync(join(directory, 'localhost.pem'), 'CERT');
    writeFileSync(join(directory, 'localhost.key'), 'KEY');
    return directory;
  }

  afterEach(() => {
    if (directory) {
      rmSync(directory, { recursive: true, force: true });
      directory = '';
    }
  });

  describe('readDevCertificate', () => {
    it('reads the exported certificate and key', () => {
      expect(readDevCertificate({ NCODE_DEV_CERT_DIR: exportCertificate() })).toStrictEqual({
        cert: 'CERT',
        key: 'KEY',
      });
    });

    it('fails with the way to export a missing certificate', () => {
      const missing = join(tmpdir(), 'ncode-dev-cert-missing');

      let error: unknown;
      try {
        readDevCertificate({ NCODE_DEV_CERT_DIR: missing });
      } catch (caught) {
        error = caught;
      }

      expect(error).toBeInstanceOf(Error);
      expect((error as Error).message).toMatch(/frontend-install\.ps1/);
      expect((error as Error).cause).toBeDefined();
    });
  });

  describe('serveOverHttps', () => {
    function configOf(
      env: Record<string, string | undefined>,
      hookEnv: { mode: string; isPreview?: boolean },
    ): unknown {
      return (serveOverHttps(env).config as unknown as ConfigHook)({}, hookEnv);
    }

    it('runs only in the dev server', () => {
      const plugin = serveOverHttps({});

      expect(plugin.name).toBe('ncode:serve-over-https');
      expect(plugin.apply).toBe('serve');
    });

    it('serves the dev server over HTTPS with the exported certificate', () => {
      const env = { NCODE_DEV_CERT_DIR: exportCertificate() };

      expect(configOf(env, { mode: 'development' })).toStrictEqual({
        server: { https: { cert: 'CERT', key: 'KEY' } },
      });
    });

    it.each([
      ['tests', { mode: 'test' }],
      ['a preview', { mode: 'development', isPreview: true }],
    ])('adds nothing for %s, which need no certificate', (_case, hookEnv) => {
      const env = { NCODE_DEV_CERT_DIR: join(tmpdir(), 'ncode-dev-cert-missing') };

      expect(configOf(env, hookEnv)).toBeUndefined();
    });

    it('defers to the certificate Aspire supplies', () => {
      const env = {
        NCODE_DEV_CERT_DIR: join(tmpdir(), 'ncode-dev-cert-missing'),
        TLS_CONFIG_PFX: 'x.pfx',
      };

      expect(configOf(env, { mode: 'development' })).toBeUndefined();
    });
  });
});

describe('defineStandaloneSurfaceConfig', () => {
  it('serves the surface on its pinned port and proxies the bootstrap document to the host', () => {
    const config = defineStandaloneSurfaceConfig({
      port: 5174,
      bootstrapHost: api,
      coverage: floors,
      env: { services__api__https__0: 'https://localhost:7001' },
    });

    expect(config.base).toBe('/');
    expect(config).not.toHaveProperty('html');
    expect(config.server).toStrictEqual({
      port: 5174,
      strictPort: true,
      proxy: {
        '/config/bootstrap.json': {
          target: 'https://localhost:7001',
          changeOrigin: true,
          secure: false,
        },
      },
    });
    expect(config.build).toStrictEqual(expectedBuild);
    expect(config.test).toStrictEqual(testGates({ environment: 'jsdom', coverage: floors }));
    expect(pluginNames(config.plugins ?? [])).toContain('vite:react-babel');
    expect(pluginNames(config.plugins ?? [])).toContain('ncode:fail-on-oversized-chunks');
    expect(pluginNames(config.plugins ?? [])).toContain('ncode:serve-over-https');
  });

  it('reads the service-discovery variables from the process environment by default', () => {
    vi.stubEnv('services__api__https__0', 'https://localhost:7101');

    const config = defineStandaloneSurfaceConfig({
      port: 5173,
      bootstrapHost: api,
      coverage: floors,
    });

    expect(config.server?.proxy?.['/config/bootstrap.json']).toMatchObject({
      target: 'https://localhost:7101',
    });
  });

  it.each([0, 65536, 5173.5, '5173' as unknown as number])('rejects port %s', (port) => {
    expect(() =>
      defineStandaloneSurfaceConfig({ port, bootstrapHost: api, coverage: floors, env: {} }),
    ).toThrow(new RangeError(`Port '${String(port)}' must be an integer from 1 to 65535.`));
  });
});

describe('defineBackendServedSurfaceConfig', () => {
  it('builds a bundle that resolves its assets against the document base, with no dev server', () => {
    const config = defineBackendServedSurfaceConfig({ coverage: floors });

    expect(config.base).toBe('./');
    expect(config.html).toStrictEqual({ cspNonce: '__NCODE_CSP_NONCE__' });
    expect(config).not.toHaveProperty('server');
    expect(config.build).toStrictEqual(expectedBuild);
    expect(config.test).toStrictEqual(testGates({ environment: 'jsdom', coverage: floors }));
    expect(pluginNames(config.plugins ?? [])).toContain('vite:react-babel');
    expect(pluginNames(config.plugins ?? [])).toContain('ncode:fail-on-oversized-chunks');
  });

  it("names the placeholder the host replaces with each response's nonce", () => {
    expect(CSP_NONCE_PLACEHOLDER).toBe('__NCODE_CSP_NONCE__');
  });
});
