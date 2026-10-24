<script lang="ts">
	import { resolve } from '$app/paths';
	import { BookOpen, Gamepad2, Globe2, Swords, UserPlus, Users } from '@lucide/svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>Veldrath — a multiplayer role-playing world</title>
	<meta
		name="description"
		content="Veldrath is a persistent multiplayer RPG of warring regions, ancient lore, and dangerous frontiers."
	/>
</svelte:head>

<section class="hero">
	<img
		class="hero-crest"
		src="/images/veldrath-final-crest-transparent.png"
		alt=""
		aria-hidden="true"
	/>
	<div class="hero-content">
		<p class="hero-kicker">An online RPG of myth and conflict</p>
		<h1 class="text-hero mt-3">Veldrath</h1>
		<p class="text-lore mx-auto mt-5 max-w-2xl text-lg text-surface-600-400">
			A persistent multiplayer role-playing world of warring regions, ancient lore, and dangerous
			frontiers. Your legend begins here.
		</p>
		<div class="mt-8 flex flex-wrap justify-center gap-3">
			<a class="btn preset-filled-primary-500" href={resolve('/characters')}>
				<Gamepad2 size={18} aria-hidden="true" />
				Play the game
			</a>
			<a class="btn preset-tonal-surface" href={resolve('/register')}>
				<UserPlus size={18} aria-hidden="true" />
				Create account
			</a>
			<a class="btn preset-tonal-surface" href={resolve('/lore')}>
				<BookOpen size={18} aria-hidden="true" />
				Explore the lore
			</a>
		</div>
	</div>
</section>

<div class="mx-auto max-w-6xl space-y-10 px-5 py-10">
	<section class="grid gap-4 md:grid-cols-3">
		<article class="feature-card">
			<Swords size={26} aria-hidden="true" />
			<h2 class="mt-3 h3">Deep character building</h2>
			<p class="mt-2 text-sm leading-relaxed text-surface-600-400">
				Choose your class, shape your background, and forge a character that is uniquely yours.
			</p>
		</article>
		<article class="feature-card">
			<Globe2 size={26} aria-hidden="true" />
			<h2 class="mt-3 h3">A living world</h2>
			<p class="mt-2 text-sm leading-relaxed text-surface-600-400">
				Explore Varenmark and beyond — a world that breathes, changing with your actions.
			</p>
		</article>
		<article class="feature-card">
			<Users size={26} aria-hidden="true" />
			<h2 class="mt-3 h3">Crafted by the community</h2>
			<p class="mt-2 text-sm leading-relaxed text-surface-600-400">
				Content curated and expanded by players through the Foundry and editorial tools.
			</p>
		</article>
	</section>

	{#if data.announcements.length > 0}
		<section class="space-y-3">
			<h2 class="h2">Announcements</h2>
			<div class="grid gap-3 sm:grid-cols-2">
				{#each data.announcements as announcement (announcement.id)}
					<article class="card border border-primary-500/30 p-4">
						<h3 class="h3">{announcement.title}</h3>
						<p class="mt-1 text-sm text-surface-600-400">{announcement.body}</p>
					</article>
				{/each}
			</div>
		</section>
	{/if}

	<div class="grid gap-6 lg:grid-cols-2">
		<section class="space-y-3">
			<div class="flex items-center justify-between gap-3">
				<h2 class="h2">Latest lore</h2>
				<a class="text-sm" href={resolve('/lore')}>All lore</a>
			</div>
			{#if data.lore.length === 0}
				<p class="card p-4 text-sm text-surface-600-400">Lore articles will appear here.</p>
			{:else}
				<ul class="space-y-2">
					{#each data.lore as article (article.id)}
						<li class="card p-4 transition hover:border-primary-500/40">
							<a class="block no-underline" href={resolve('/lore/[slug]', { slug: article.slug })}>
								<p class="text-title">{article.title}</p>
								<p class="text-lore mt-1 line-clamp-2 text-sm text-surface-600-400">
									{article.summary}
								</p>
							</a>
						</li>
					{/each}
				</ul>
			{/if}
		</section>

		<section class="space-y-3">
			<div class="flex items-center justify-between gap-3">
				<h2 class="h2">Latest patch notes</h2>
				<a class="text-sm" href={resolve('/patch-notes')}>All patch notes</a>
			</div>
			{#if data.patchNotes.length === 0}
				<p class="card p-4 text-sm text-surface-600-400">Patch notes will appear here.</p>
			{:else}
				<ul class="space-y-2">
					{#each data.patchNotes as note (note.id)}
						<li class="card p-4 transition hover:border-primary-500/40">
							<a
								class="block no-underline"
								href={resolve('/patch-notes/[slug]', { slug: note.slug })}
							>
								<p class="text-title">
									{note.title}
									<span class="preset-tonal-secondary-500 ml-2 chip text-xs">{note.version}</span>
								</p>
								<p class="mt-1 text-sm text-surface-600-400">{note.summary}</p>
							</a>
						</li>
					{/each}
				</ul>
			{/if}
		</section>
	</div>
</div>
