/**
 * How long a Testing Library wait (`findBy…`, `waitFor`) polls before it fails. A wait settles in milliseconds in a
 * healthy run, but one MUI render takes seconds on a loaded CI runner under coverage, and the library's 1 s default
 * failed tests at random. This is a hang guard, not a performance assertion. It stays well under the preset's browser
 * test timeout, so a stuck wait fails with Testing Library's DOM report rather than a bare test timeout.
 */
export const ASYNC_UTIL_TIMEOUT_MS = 10_000;

/**
 * Testing Library's `configure`, injected so this package never loads Testing Library. It is the only slice of the
 * library's configuration that {@link configureAsyncUtilTimeout} sets.
 */
export type TestingLibraryConfigure = (options: { readonly asyncUtilTimeout: number }) => void;

/**
 * Gives every Testing Library wait the one deliberate timeout, {@link ASYNC_UTIL_TIMEOUT_MS}. Call it once from a
 * Vitest setup file of a suite that renders into a simulated browser, passing Testing Library's `configure`.
 */
export function configureAsyncUtilTimeout(configure: TestingLibraryConfigure): void {
  configure({ asyncUtilTimeout: ASYNC_UTIL_TIMEOUT_MS });
}
