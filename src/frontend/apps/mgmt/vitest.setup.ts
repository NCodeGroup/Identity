import '@testing-library/jest-dom/vitest';

// Cross-realm fetch/AbortSignal reconciliation for the jsdom environment.
//
// Under `environment: 'jsdom'`, `fetch`/`Request`/`Response` remain Node's (undici) globals, but
// jsdom overrides `AbortController`/`AbortSignal` with its own implementations. RTK Query's
// `fetchBaseQuery` attaches an `AbortSignal` (from the global `AbortController`, i.e. jsdom's) to a
// `new Request(url, { signal })`, and undici's `Request` rejects the foreign-realm signal with
// "RequestInit: Expected signal to be an instance of AbortSignal" — which surfaces as a spurious
// query failure (e.g. "Failed to load tenants") before any mocked `fetch` is reached. Node's original
// `AbortController` is no longer reachable (jsdom shadowed the global) and `undici` is not a resolvable
// dependency, so we wrap `Request` to drop the incompatible jsdom signal during construction. This is
// a test-environment-only shim; the browser serves a single consistent realm, so production is
// unaffected. Aborting is never exercised in these unit tests, so dropping the signal is harmless.
const NativeRequest = globalThis.Request;
const JsdomAbortSignal = globalThis.AbortSignal;
class RealmSafeRequest extends NativeRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    if (init?.signal instanceof JsdomAbortSignal) {
      const { signal: _incompatibleSignal, ...rest } = init;
      super(input, rest);
    } else {
      super(input, init);
    }
  }
}
globalThis.Request = RealmSafeRequest as unknown as typeof Request;
