import { mdsvex } from 'mdsvex';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';
import nodeAdapter from '@sveltejs/adapter-node';
import staticAdapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';

export default defineConfig(({ mode }) => {
	// `vite build --mode desktop` produces a static SPA hosted by the Avalonia
	// desktop client's local DesktopUiServer. The default build remains adapter-node.
	const desktop = mode === 'desktop';

	return {
		plugins: [
			tailwindcss(),
			sveltekit({
				compilerOptions: {
					// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
					runes: ({ filename }) =>
						filename.split(/[/\\]/).includes('node_modules') ? undefined : true
				},
				adapter: desktop
					? // Pure SPA fallback: every route is resolved client-side by the desktop
						// WebView, so no server-side prerender pass is required.
						staticAdapter({ pages: 'build-desktop', fallback: 'index.html' })
					: nodeAdapter(),
				preprocess: [mdsvex({ extensions: ['.svx', '.md'] })],
				extensions: ['.svelte', '.svx', '.md']
			})
		],
		test: {
			expect: { requireAssertions: true },
			environment: 'jsdom',
			passWithNoTests: true,
			include: ['src/**/*.{test,spec}.{js,ts}'],
			setupFiles: ['./src/test-setup.ts']
		}
	};
});
