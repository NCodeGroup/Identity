import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Plugin, PluginOption } from 'vite';
import {
  defineBackendServedSurfaceConfig,
  defineStandaloneSurfaceConfig,
  defineTestConfig,
  resolveServiceOrigin,
  testGates,
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
    expect(test.testTimeout).toBe(15_000);
    expect(test.globals).toBe(true);
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

describe('defineStandaloneSurfaceConfig', () => {
  it('serves the surface on its pinned port and proxies the bootstrap document to the host', () => {
    const config = defineStandaloneSurfaceConfig({
      port: 5174,
      bootstrapHost: api,
      coverage: floors,
      env: { services__api__https__0: 'https://localhost:7001' },
    });

    expect(config.base).toBe('/');
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
    expect(config.build).toStrictEqual({ outDir: 'dist', sourcemap: true });
    expect(config.test).toStrictEqual(testGates({ environment: 'jsdom', coverage: floors }));
    expect(pluginNames(config.plugins ?? [])).toContain('vite:react-babel');
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
  it('builds the bundle under the base the host serves it from, with no dev server', () => {
    const config = defineBackendServedSurfaceConfig({ base: '/account/', coverage: floors });

    expect(config.base).toBe('/account/');
    expect(config).not.toHaveProperty('server');
    expect(config.build).toStrictEqual({ outDir: 'dist', sourcemap: true });
    expect(config.test).toStrictEqual(testGates({ environment: 'jsdom', coverage: floors }));
    expect(pluginNames(config.plugins ?? [])).toContain('vite:react-babel');
  });

  it.each(['account/', '/account', ''])('rejects base %j', (base) => {
    expect(() => defineBackendServedSurfaceConfig({ base, coverage: floors })).toThrow(
      new RangeError(`Base '${base}' must start and end with '/'.`),
    );
  });
});
