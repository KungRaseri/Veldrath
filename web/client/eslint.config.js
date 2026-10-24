import prettier from 'eslint-config-prettier';
import js from '@eslint/js';
import svelte from 'eslint-plugin-svelte';
import { defineConfig } from 'eslint/config';
import globals from 'globals';
import ts from 'typescript-eslint';

export default defineConfig(
	// Git-ignored paths come from the repository-root .gitignore; ESLint needs
	// them as explicit globs scoped to this package.
	{
		ignores: [
			'node_modules/**',
			'.svelte-kit/**',
			'build/**',
			'build-desktop/**',
			'.output/**',
			'coverage/**',
			'test-results/**',
			'playwright-report/**'
		]
	},
	js.configs.recommended,
	ts.configs.recommended,
	svelte.configs.recommended,
	prettier,
	svelte.configs.prettier,
	{
		languageOptions: { globals: { ...globals.browser, ...globals.node } },
		rules: {
			// typescript-eslint strongly recommend that you do not use the no-undef lint rule on TypeScript projects.
			// see: https://typescript-eslint.io/troubleshooting/faqs/eslint/#i-get-errors-from-the-no-undef-rule-about-global-variables-not-being-defined-even-though-there-are-no-typescript-errors
			'no-undef': 'off'
		}
	},
	{
		// DB boundary: only `src/lib/server/**` may import the shared Drizzle
		// schema. Guards against the browser bundle (or any non-server
		// module) reaching into DB types — the schema stays the single source of
		// truth server-side (client driver: postgres, server driver: pg).
		files: ['**/*.{js,ts,svelte}'],
		ignores: ['src/lib/server/**'],
		rules: {
			'no-restricted-imports': [
				'error',
				{
					paths: [
						{
							name: '@veldrath/shared/db',
							message: 'Only import @veldrath/shared/db from src/lib/server/** (DB boundary).'
						}
					]
				}
			]
		}
	},
	{
		files: ['**/*.svelte', '**/*.svelte.ts', '**/*.svelte.js'],
		languageOptions: {
			parserOptions: {
				projectService: true,
				extraFileExtensions: ['.svelte'],
				parser: ts.parser
			}
		}
	},
	{
		// Override or add rule settings here, such as:
		// 'svelte/button-has-type': 'error'
		rules: {}
	}
);
