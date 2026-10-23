<script lang="ts">
	import { resolve } from '$app/paths';
</script>

<svelte:head>
	<title>Veldrath</title>
	<meta name="description" content="Veldrath — a multiplayer role-playing world." />
</svelte:head>

<div class="flex min-h-dvh flex-col items-center justify-center gap-3 p-8">
	<h1 class="text-3xl font-bold text-primary-500">Veldrath</h1>
	<p class="text-surface-600">The SvelteKit frontend is running.</p>
	<div class="flex gap-3">
		<a class="btn preset-filled-primary-500" href={resolve('/login')}>Sign in</a>
		<a class="btn preset-tonal-surface" href={resolve('/register')}>Create account</a>
	</div>
</div>
