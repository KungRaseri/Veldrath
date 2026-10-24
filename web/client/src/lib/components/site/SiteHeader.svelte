<script lang="ts">
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import {
		BookOpen,
		Crown,
		Gamepad2,
		Hammer,
		LogIn,
		PenLine,
		ScrollText,
		Shield,
		User,
		UserPlus,
		Users
	} from '@lucide/svelte';

	let {
		user = null,
		staff = null
	}: {
		user?: AuthUser | null;
		staff?: {
			foundry?: boolean;
			editorial?: boolean;
			mod?: boolean;
			admin?: boolean;
		} | null;
	} = $props();

	function active(path: string): 'page' | undefined {
		if (path === '/') {
			return page.url.pathname === '/' ? 'page' : undefined;
		}

		return page.url.pathname === path || page.url.pathname.startsWith(`${path}/`)
			? 'page'
			: undefined;
	}
</script>

<header class="site-header">
	<div class="site-header-inner">
		<a class="site-brand" href={resolve('/')} aria-label="Veldrath home">
			<img src="/images/veldrath-text-flat-true-transparent.png" alt="Veldrath" />
		</a>

		<nav class="site-nav" aria-label="Primary">
			<a class="site-nav-link" aria-current={active('/lore')} href={resolve('/lore')}>
				<BookOpen size={16} aria-hidden="true" />
				<span>Lore</span>
			</a>
			<a class="site-nav-link" aria-current={active('/patch-notes')} href={resolve('/patch-notes')}>
				<ScrollText size={16} aria-hidden="true" />
				<span>Patch notes</span>
			</a>
			<a class="site-nav-link" aria-current={active('/community')} href={resolve('/community')}>
				<Users size={16} aria-hidden="true" />
				<span>Community</span>
			</a>
			{#if staff?.foundry}
				<a
					class="site-nav-link"
					aria-current={active('/submissions')}
					href={resolve('/(foundry)/submissions')}
				>
					<Hammer size={16} aria-hidden="true" />
					<span>Foundry</span>
				</a>
			{/if}
			{#if staff?.editorial}
				<a
					class="site-nav-link"
					aria-current={active('/editorial')}
					href={resolve('/(editorial)/editorial')}
				>
					<PenLine size={16} aria-hidden="true" />
					<span>Editorial</span>
				</a>
			{/if}
			{#if staff?.mod}
				<a class="site-nav-link" aria-current={active('/mod')} href={resolve('/(mod)/mod')}>
					<Shield size={16} aria-hidden="true" />
					<span>Mod</span>
				</a>
			{/if}
			{#if staff?.admin}
				<a class="site-nav-link" aria-current={active('/admin')} href={resolve('/(admin)/admin')}>
					<Crown size={16} aria-hidden="true" />
					<span>Admin</span>
				</a>
			{/if}
		</nav>

		<nav class="site-nav" aria-label="Account">
			{#if user}
				<a class="btn preset-tonal-surface btn-sm" href={resolve('/account')}>
					<User size={15} aria-hidden="true" />
					<span>{user.username ?? user.name}</span>
				</a>
				<a class="btn preset-filled-primary-500 btn-sm" href={resolve('/characters')}>
					<Gamepad2 size={15} aria-hidden="true" />
					<span>Play</span>
				</a>
			{:else}
				<a class="btn preset-tonal-surface btn-sm" href={resolve('/login')}>
					<LogIn size={15} aria-hidden="true" />
					<span>Sign in</span>
				</a>
				<a class="btn preset-filled-primary-500 btn-sm" href={resolve('/register')}>
					<UserPlus size={15} aria-hidden="true" />
					<span>Create account</span>
				</a>
			{/if}
		</nav>
	</div>
</header>
