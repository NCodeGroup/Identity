import { readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';
import react from '@vitejs/plugin-react';
import type { Plugin } from 'vite';
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
  readonly coverage: CoverageFloors;
};

type BuildConfig = NonNullable<ViteUserConfig['build']>;

/**
 * The vendor chunks every bundle splits its third-party code into, highest priority first: React, then MUI with its
 * emotion styling engine, then Redux, then every other package. A vendor chunk changes only when its packages are
 * upgraded, so it stays cached across application releases, and no chunk carries the whole dependency graph (ADR-0175).
 */
export const VENDOR_CHUNKS: readonly { readonly name: string; readonly test: RegExp }[] = [
  { name: 'react', test: /[\\/]node_modules[\\/](react|react-dom|scheduler)[\\/]/ },
  { name: 'mui', test: /[\\/]node_modules[\\/](@mui|@emotion)[\\/]/ },
  {
    name: 'redux',
    test: /[\\/]node_modules[\\/](@reduxjs|redux|react-redux|redux-thunk|reselect|immer)[\\/]/,
  },
  { name: 'vendor', test: /[\\/]node_modules[\\/]/ },
];

/** The size, in kB of minified code, above which a chunk fails the build: Vite's chunk-size warning threshold. */
export const CHUNK_SIZE_LIMIT_KB = 500;

/** An output of a generated bundle, as far as the chunk-size gate reads it: a code chunk or an asset. */
export type BundleOutput =
  | { readonly type: 'chunk'; readonly fileName: string; readonly code: string }
  | { readonly type: 'asset'; readonly fileName: string };

/** The code chunks of a bundle whose minified code exceeds the limit, each with its size in kB. */
export function oversizedChunks(
  bundle: Readonly<Record<string, BundleOutput>>,
  limitKb: number,
): { readonly fileName: string; readonly sizeKb: number }[] {
  const oversized: { fileName: string; sizeKb: number }[] = [];
  for (const output of Object.values(bundle)) {
    const sizeKb = output.type === 'chunk' ? output.code.length / 1000 : 0;
    if (sizeKb > limitKb) {
      oversized.push({ fileName: output.fileName, sizeKb });
    }
  }
  return oversized;
}

/**
 * Fails the build when a chunk exceeds the build's `chunkSizeWarningLimit`, the build-time counterpart of the test
 * gates that treat warnings as errors (ADR-0151). Vite only warns, and a warning in a passing build goes unread.
 */
export function failOnOversizedChunks(): Plugin {
  let limitKb = CHUNK_SIZE_LIMIT_KB;
  return {
    name: 'ncode:fail-on-oversized-chunks',
    apply: 'build',
    configResolved(config) {
      limitKb = config.build.chunkSizeWarningLimit;
    },
    generateBundle(_options, bundle) {
      const oversized = oversizedChunks(bundle as Readonly<Record<string, BundleOutput>>, limitKb);
      if (oversized.length > 0) {
        const list = oversized.map((c) => `${c.fileName} (${c.sizeKb.toFixed(2)} kB)`).join(', ');
        this.error(
          `Chunks larger than ${String(limitKb)} kB after minification: ${list}. Split them — lazy-load the ` +
            'route-level containers or add a vendor chunk — rather than raising the limit (ADR-0175).',
        );
      }
    },
  };
}

/**
 * The build options every bundle shares: the output and assets directories, source maps, the vendor chunks, and the
 * chunk-size limit (ADR-0175).
 */
function buildOptions(): BuildConfig {
  return {
    outDir: 'dist',
    // The host serves only this directory of a backend-served bundle, as immutable content-hashed files (ADR-0254).
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
}

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

/**
 * The test timeout, in milliseconds, of a suite that simulates a browser. It leaves room for at least three Testing
 * Library waits, each bounded by `ASYNC_UTIL_TIMEOUT_MS` from `@ncode/test-support`, so a test that waits on a few
 * renders fails with the library's DOM report rather than a bare timeout. The preset names that constant in prose only:
 * it must not import a workspace package (ADR-0157).
 */
export const BROWSER_TEST_TIMEOUT_MS = 30_000;

/** The test gates every workspace holds (ADR-0151), for a workspace's own `test` configuration. */
export function testGates({ environment, coverage }: TestConfigOptions): TestConfig {
  assertCoverageFloors(coverage);
  const simulatesBrowser = environment !== 'node';
  return {
    environment,
    // jsdom + MUI renders run for seconds under v8 coverage on shared CI runners, and React Testing Library cleans up
    // after each test through the global hooks; see "Testing Library and test timeouts" in the frontend README.
    ...(simulatesBrowser ? { testTimeout: BROWSER_TEST_TIMEOUT_MS, globals: true } : {}),
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

/** The environment variable that overrides the directory holding the exported development certificate. */
export const DEV_CERTIFICATE_DIRECTORY_VARIABLE = 'NCODE_DEV_CERT_DIR';

/** The directory that holds `localhost.pem` and `localhost.key`, as exported by `build/frontend-install.ps1`. */
export function devCertificateDirectory(env: ServiceEnvironment): string {
  return (
    env[DEV_CERTIFICATE_DIRECTORY_VARIABLE] ?? join(homedir(), '.ncode', 'identity', 'dev-cert')
  );
}

/** The PEM-encoded development certificate and its private key. */
export type DevCertificate = { readonly cert: string; readonly key: string };

/** Reads the exported development certificate, failing with the way to export it when it is missing. */
export function readDevCertificate(env: ServiceEnvironment): DevCertificate {
  const directory = devCertificateDirectory(env);
  try {
    return {
      cert: readFileSync(join(directory, 'localhost.pem'), 'utf8'),
      key: readFileSync(join(directory, 'localhost.key'), 'utf8'),
    };
  } catch (cause) {
    throw new Error(
      `The development certificate was not found in '${directory}'. Run ./build/frontend-install.ps1 to export it, or set ${DEV_CERTIFICATE_DIRECTORY_VARIABLE} to the directory that holds localhost.pem and localhost.key.`,
      { cause },
    );
  }
}

/**
 * Serves the dev server over HTTPS with the exported ASP.NET Core development certificate, so a standalone surface
 * matches its HTTPS-only redirect URIs and CORS origin outside Aspire (ADR-0071). Aspire's wrapper config supplies the
 * certificate (`TLS_CONFIG_PFX`) for the AppHost, so the plugin adds nothing then; tests and previews never need one;
 * otherwise the installer exports the certificate and a missing one fails the dev server at start.
 */
export function serveOverHttps(env: ServiceEnvironment): Plugin {
  return {
    name: 'ncode:serve-over-https',
    apply: 'serve',
    config(_config, { mode, isPreview }) {
      if (mode === 'test' || isPreview || env.TLS_CONFIG_PFX) {
        return;
      }
      return { server: { https: readDevCertificate(env) } };
    },
  };
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
    plugins: [react(), failOnOversizedChunks(), serveOverHttps(env)],
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
    build: buildOptions(),
    test: testGates({ environment: 'jsdom', coverage }),
  };
}

/**
 * The value Vite writes into the `nonce` attribute of every script, modulepreload link and the
 * `<meta property="csp-nonce">` of a backend-served bundle. The host writes each response's nonce in its place, so
 * `InteractiveBundleShell.NoncePlaceholder` in the .NET host must stay equal (ADR-0256).
 */
export const CSP_NONCE_PLACEHOLDER = '__NCODE_CSP_NONCE__';

/**
 * The configuration of a backend-served surface: a bundle the OpenID Provider host serves from its `dist` folder. It has
 * no dev server; its `dev` script rebuilds `dist` on change instead. The bundle is built with a relative base, so it
 * does not assume where it is mounted: the host serves it beneath each tenant's route and sets the document's
 * `<base href>` to that location, which the bundle's asset URLs resolve against (ADR-0193). The bundle runs under the
 * page's Content-Security-Policy nonce, so the build marks its scripts with the placeholder the host replaces
 * (ADR-0256).
 */
export function defineBackendServedSurfaceConfig({
  coverage,
}: BackendServedSurfaceConfigOptions): ViteUserConfig {
  return {
    base: './',
    html: { cspNonce: CSP_NONCE_PLACEHOLDER },
    plugins: [react(), failOnOversizedChunks()],
    build: buildOptions(),
    test: testGates({ environment: 'jsdom', coverage }),
  };
}
