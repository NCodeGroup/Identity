import { describe, expect, expectTypeOf, it } from 'vitest';
import { useDispatch, useSelector, useStore } from 'react-redux';
import { useAppDispatch, useAppSelector, useAppStore } from './hooks.js';
import type { AppDispatch, AppStore, RootState } from './store.js';

/** The state type that a selector passed to a `useSelector`-style hook receives. */
type SelectorState<Hook extends (selector: never) => unknown> = Parameters<Parameters<Hook>[0]>[0];

// The typed hooks are react-redux's own hooks pre-bound to this store's types, so they read the
// default react-redux context that each app shell's <Provider store={createAppStore()}> supplies.
// The type assertions are enforced by `tsc --noEmit` (test files are type-checked).
describe('typed hooks', () => {
  it('useAppDispatch is react-redux useDispatch returning AppDispatch', () => {
    expect(useAppDispatch).toBe(useDispatch);
    expectTypeOf<ReturnType<typeof useAppDispatch>>().toEqualTypeOf<AppDispatch>();
  });

  it('useAppSelector is react-redux useSelector whose selectors receive RootState', () => {
    expect(useAppSelector).toBe(useSelector);
    expectTypeOf<SelectorState<typeof useAppSelector>>().toEqualTypeOf<RootState>();
  });

  it('useAppStore is react-redux useStore returning AppStore', () => {
    expect(useAppStore).toBe(useStore);
    expectTypeOf(useAppStore).toExtend<() => AppStore>();
  });
});
