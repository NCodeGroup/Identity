import react from '@vitejs/plugin-react';
import type { ViteUserConfig } from 'vitest/config';

// Every workspace's vite.config.ts / vitest.config.ts imports this module, and Vite's config bundler externalizes bare
// imports, so Node loads it natively by stripping its types. Keep it one module of erasable TypeScript with no relative
// or workspace imports (ADR-0157). The configs that call it are not type-checked, so it validates its own options.

/** The coverage floors a workspace holds: the level it has reached, rounded down (ADR-0151). */
export type CoverageFloors = {
  readonly statements: number;
  readonly branches: number;
  readonly functions: number;
  readonly lines: number;
};

type TestConfig = NonNullable<ViteUserConfig['test']>;

export type TestConfigOptions = {
  /** The Vitest environment. Any environment other than `node` simulates a browser and gets the DOM settings. */
  readonly environment: NonNullable<TestConfig['environment']>;
  readonly coverage: CoverageFloors;
};

/** Environment variables, read for the Aspire service-discovery origins. */
export type ServiceEnvironment = Readonly<Record<string, string | undefined>>;

/** The OpenID Provider host that publishes a standalone surface's bootstrap document during local development. */
export type BootstrapHost = {
  /** The Aspire resource the AppHost references the surface to (`WithReference`), for example `api`. */
  readonly service: string;
  /** The host's origin outside Aspire, where no service-discovery variable is set. */
  readonly fallbackOrigin: string;
};

export type StandaloneSurfaceConfigOptions = {
  /**
   * The dev server's port. The surface's redirect URIs, CORS origin, and AppHost endpoint all assume it, so the server
   * refuses to start on any other.
   */
  readonly port: number;
  readonly bootstrapHost: BootstrapHost;
  readonly coverage: CoverageFloors;
  /** Where the service-discovery variables are read from; defaults to `process.env`. */
  readonly env?: ServiceEnvironment;
};

export type BackendServedSurfaceConfigOptions = {
  /** The path the serving host publishes the bundle under; assets resolve from it. */
  readonly base: string;
  readonly coverage: CoverageFloors;
};

/** The well-known path of the bootstrap document every bundle fetches from its serving host (`@ncode/config`). */
const BOOTSTRAP_PATH = '/config/bootstrap.json';

const COVERAGE_METRICS = ['statements', 'branches', 'functions', 'lines'] as const;

function assertCoverageFloors(coverage: CoverageFloors): void {
  for (const metric of COVERAGE_METRICS) {
    const floor: unknown = coverage[metric];
    if (typeof floor !== 'number' || !(floor >= 0 && floor <= 100)) {
      throw new RangeError(`Coverage floor '${metric}' must be a number from 0 to 100.`);
    }
  }
}

function assertPort(port: number): void {
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new RangeError(`Port '${String(port)}' must be an integer from 1 to 65535.`);
  }
}

/** The test gates every workspace holds (ADR-0151), for a workspace's own `test` configuration. */
export function testGates({ environment, coverage }: TestConfigOptions): TestConfig {
  assertCoverageFloors(coverage);
  const simulatesBrowser = environment !== 'node';
  return {
    environment,
    // jsdom + MUI renders run for seconds under v8 coverage on shared CI runners, and React Testing Library cleans up
    // after each test through the global hooks; see "jsdom test timeout" in the frontend README.
    ...(simulatesBrowser ? { testTimeout: 15_000, globals: true } : {}),
    setupFiles: ['./vitest.setup.ts'],
    // A test that makes no assertion fails.
    expect: { requireAssertions: true },
    include: ['src/**/*.test.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      // Every source file counts except the tests, the stories, the entry point, and the re-export barrel.
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/**/*.stories.{ts,tsx}',
        'src/main.{ts,tsx}',
        'src/index.ts',
      ],
      thresholds: { ...coverage },
    },
  };
}

/** The Vitest configuration of a workspace that only tests: a package, or the headless runner. */
export function defineTestConfig(options: TestConfigOptions): ViteUserConfig {
  return { test: testGates(options) };
}

/**
 * Resolves the bootstrap host's origin. Under the Aspire local-dev orchestrator (ADR-0071) the surface's resource is
 * `WithReference` the host, so Aspire injects its service-discovery variables; outside Aspire none is set.
 */
export function resolveServiceOrigin(
  { service, fallbackOrigin }: BootstrapHost,
  env: ServiceEnvironment,
): string {
  if (!service) {
    throw new RangeError('The bootstrap host must name its Aspire service.');
  }
  if (!/^https?:$/.test(URL.parse(fallbackOrigin)?.protocol ?? '')) {
    throw new RangeError(`Fallback origin '${fallbackOrigin}' must be an absolute HTTP(S) URL.`);
  }
  return (
    env[`services__${service}__https__0`] ?? env[`services__${service}__http__0`] ?? fallbackOrigin
  );
}

/**
 * The configuration of a standalone surface: a single-page app served by its own Vite dev server. That server is the
 * bundle's serving host, so it must publish the bootstrap document; rather than ship a static copy that drifts, it
 * proxies the well-known path to the OpenID Provider host, and local development reads the real document (ADR-0064).
 */
export function defineStandaloneSurfaceConfig({
  port,
  bootstrapHost,
  coverage,
  env = process.env,
}: StandaloneSurfaceConfigOptions): ViteUserConfig {
  assertPort(port);
  return {
    base: '/',
    plugins: [react()],
    server: {
      port,
      strictPort: true,
      proxy: {
        [BOOTSTRAP_PATH]: {
          target: resolveServiceOrigin(bootstrapHost, env),
          changeOrigin: true,
          // The local OpenID Provider host serves a development certificate.
          secure: false,
        },
      },
    },
    build: { outDir: 'dist', sourcemap: true },
    test: testGates({ environment: 'jsdom', coverage }),
  };
}

/**
 * The configuration of a backend-served surface: a bundle the OpenID Provider host serves from its `dist` folder. It has
 * no dev server; its `dev` script rebuilds `dist` on change instead.
 */
export function defineBackendServedSurfaceConfig({
  base,
  coverage,
}: BackendServedSurfaceConfigOptions): ViteUserConfig {
  if (!base.startsWith('/') || !base.endsWith('/')) {
    throw new RangeError(`Base '${base}' must start and end with '/'.`);
  }
  return {
    base,
    plugins: [react()],
    build: { outDir: 'dist', sourcemap: true },
    test: testGates({ environment: 'jsdom', coverage }),
  };
}
