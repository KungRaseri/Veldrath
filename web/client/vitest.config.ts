import { mergeConfig } from 'vitest/config';
import viteConfig from './vite.config';

// Dedicated Vitest config so test-only module resolution does not leak into the
// app build (vite dev/build still use vite.config.ts, where the SSR bundle needs
// the `node` condition).
//
// Vitest runs the module graph in SSR mode and forwards `ssr.resolve.conditions`
// to test workers as Node `--conditions`. Svelte's package exports map `browser`
// → client build and `default` → server build, so without the `browser` condition
// `@testing-library/svelte`'s `import * as Svelte from 'svelte'` resolves to the
// server build, where `mount(...)` throws `lifecycle_function_unavailable`.
export default mergeConfig(viteConfig, {
	resolve: {
		conditions: ['browser', 'module', 'development|production'],
		ssr: {
			conditions: ['browser', 'module', 'development|production']
		}
	}
});
