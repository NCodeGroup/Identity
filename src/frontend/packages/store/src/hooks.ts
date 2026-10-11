import { useDispatch, useSelector, useStore } from 'react-redux';
import type { AppDispatch, AppStore, RootState } from './store.js';

/** Typed `useDispatch` for app and container components. */
export const useAppDispatch = useDispatch.withTypes<AppDispatch>();
/** Typed `useSelector` for app and container components. */
export const useAppSelector = useSelector.withTypes<RootState>();
/** Typed `useStore` for rare imperative access. */
export const useAppStore = useStore.withTypes<AppStore>();
