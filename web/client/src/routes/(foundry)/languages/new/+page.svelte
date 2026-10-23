<script lang="ts">
	import { resolve } from '$app/paths';
	import type { ActionData } from './$types';

	let { form }: { form: ActionData } = $props();
</script>

<svelte:head>
	<title>Build a language — Foundry</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-4 p-6">
	<a class="text-sm text-primary-500" href={resolve('/languages')}>← All languages</a>

	<header class="space-y-1">
		<h1 class="h1">Build a language</h1>
		<p class="text-surface-600-400">
			Submit a language for curation. The full phonology and lexicon can be refined after approval.
		</p>
	</header>

	{#if form?.error}
		<p class="text-sm text-error-500">{form.error}</p>
	{/if}

	<form class="space-y-4 card p-6" method="POST">
		<div class="grid gap-3 sm:grid-cols-2">
			<label class="label">
				<span>Display name</span>
				<input class="input" type="text" name="displayName" required placeholder="Calethic" />
			</label>
			<label class="label">
				<span>Slug</span>
				<input class="input" type="text" name="slug" required placeholder="calethic" />
			</label>
			<label class="label">
				<span>Language family</span>
				<input class="input" type="text" name="typeKey" placeholder="imperial" />
			</label>
			<label class="label">
				<span>Tonal character</span>
				<input class="input" type="text" name="tonalCharacter" placeholder="Hard + Formal" />
			</label>
		</div>

		<label class="label">
			<span>Description</span>
			<textarea class="textarea" name="description" rows="4"></textarea>
		</label>

		<div class="grid gap-3 sm:grid-cols-2">
			<label class="label">
				<span>Syllable patterns (comma-separated)</span>
				<input class="input" type="text" name="syllablePatterns" placeholder="CVC, CV, CVCC" />
			</label>
			<label class="label">
				<span>Initial clusters</span>
				<input class="input" type="text" name="initialClusters" placeholder="KR-, VR-, DR-" />
			</label>
			<label class="label">
				<span>Forbidden clusters</span>
				<input class="input" type="text" name="forbiddenClusters" />
			</label>
			<label class="label">
				<span>Final clusters</span>
				<input class="input" type="text" name="finalClusters" placeholder="-LD, -RN" />
			</label>
		</div>

		<label class="label">
			<span>Sample text</span>
			<textarea class="textarea font-mono text-xs" name="sampleText" rows="4"></textarea>
		</label>

		<button class="btn preset-filled-primary-500" type="submit">Submit for review</button>
	</form>
</div>
