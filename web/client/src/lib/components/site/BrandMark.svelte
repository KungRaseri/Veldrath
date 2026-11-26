<script lang="ts">
	/**
	 * The one place that knows which derived export each surface renders.
	 *
	 * Rendered sizes are the ones brand.css enforces (hero <= 520px, auth 76px, account 44px,
	 * roster 48px, creation 40px, header wordmark 30px tall, footer lockup 64px tall). Every
	 * candidate is a derivation of the brand master — a 1.65 MB crest must never be downloaded
	 * to draw a 76px mark.
	 */
	type Variant = 'hero' | 'auth' | 'account' | 'roster' | 'creation' | 'wordmark' | 'lockup';

	type ExportSet = { src: string; srcset?: string; sizes: string; width: number; height: number };

	const sets: Record<Variant, ExportSet> = {
		hero: {
			src: '/images/crest-primary-228.webp',
			srcset: '/images/crest-primary-228.webp 228w, /images/crest-hero-1040.webp 1040w',
			sizes: '(max-width: 650px) 80vw, 520px',
			width: 520,
			height: 520
		},
		auth: {
			src: '/images/crest-primary-152.webp',
			srcset: '/images/crest-primary-152.webp 152w',
			sizes: '76px',
			width: 76,
			height: 76
		},
		account: { src: '/images/crest-primary-96.webp', sizes: '44px', width: 44, height: 44 },
		roster: { src: '/images/crest-primary-96.webp', sizes: '48px', width: 48, height: 48 },
		creation: { src: '/images/crest-primary-88.webp', sizes: '40px', width: 40, height: 40 },
		wordmark: {
			src: '/images/wordmark-header-60.webp',
			srcset: '/images/wordmark-header-60.webp 240w, /images/wordmark-header-90.webp 360w',
			sizes: '120px',
			width: 240,
			height: 60
		},
		lockup: {
			src: '/images/lockup-horizontal-128.webp',
			srcset: '/images/lockup-horizontal-128.webp 384w, /images/lockup-horizontal-192.webp 576w',
			sizes: '192px',
			width: 384,
			height: 128
		}
	};

	let {
		variant,
		class: className = '',
		label = '',
		eager = false,
		...rest
	}: {
		variant: Variant;
		class?: string;
		label?: string;
		eager?: boolean;
		[key: string]: unknown;
	} = $props();

	const set = $derived(sets[variant]);
</script>

<img
	src={set.src}
	srcset={set.srcset}
	sizes={set.sizes}
	width={set.width}
	height={set.height}
	class={className}
	alt={label}
	aria-hidden={label ? undefined : 'true'}
	loading={eager ? 'eager' : 'lazy'}
	decoding="async"
	{...rest}
/>
