import { mdsvex } from 'mdsvex';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';
import nodeAdapter from '@sveltejs/adapter-node';
import { sveltekit } from '@sveltejs/kit/vite';

// Veldrath is served by adapter-node only: the SvelteKit app is the single client
// (browser), and the Avalonia desktop shell that needed an adapter-static SPA build
// was retired.
export default defineConfig(() => {
	return {
		plugins: [
			tailwindcss(),
			sveltekit({
				compilerOptions: {
					// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
					runes: ({ filename }) =>
						filename.split(/[/\\]/).includes('node_modules') ? undefined : true
				},
				adapter: nodeAdapter(),
				preprocess: [mdsvex({ extensions: ['.svx', '.md'] })],
				extensions: ['.svelte', '.svx', '.md']
			})
		],
		test: {
			expect: { requireAssertions: true },
			environment: 'jsdom',
			passWithNoTests: true,
			include: ['src/**/*.{test,spec}.{js,ts}'],
			setupFiles: ['./src/test-setup.ts'],
			coverage: {
				provider: 'v8' as const,
				reporter: ['text-summary', 'lcov'],
				include: ['src/**/*.{ts,svelte}'],
				exclude: ['src/**/*.{test,spec}.{js,ts}', 'src/**/*.d.ts', 'src/test-setup.ts']
			}
		}
	};
});
