import { mountSetup } from './mount.js';

const container = document.getElementById('root');
if (!container) {
  throw new Error('Root container #root not found.');
}

// The bundle mounts as soon as its module runs, over the page the host rendered.
mountSetup(container);
