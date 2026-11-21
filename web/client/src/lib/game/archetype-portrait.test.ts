import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ArchetypePortrait from './ArchetypePortrait.svelte';
import { archetypeSilhouette, archetypeTierFromTraits } from './archetype-portraits';

afterEach(() => {
	vi.restoreAllMocks();
});

describe('archetype portrait', () => {
	it('renders the 1× file a known slug resolves to, and offers the 2× alongside it', () => {
		render(ArchetypePortrait, { slug: 'giant-rat', size: 64 });

		const image = screen.getByTestId('archetype-portrait').querySelector('img');
		expect(image?.getAttribute('src')).toBe(
			'/images/archetypes/archetype-giant-rat-portrait-64.webp'
		);
		expect(image?.getAttribute('srcset')).toBe(
			'/images/archetypes/archetype-giant-rat-portrait-64.webp 64w, /images/archetypes/archetype-giant-rat-portrait-128.webp 128w'
		);
	});

	it('names the 2× file on the detail surface and keeps the 64 px box', () => {
		render(ArchetypePortrait, { slug: 'giant-rat', size: 128 });

		const tile = screen.getByTestId('archetype-portrait');
		expect(tile.querySelector('img')?.getAttribute('src')).toBe(
			'/images/archetypes/archetype-giant-rat-portrait-128.webp'
		);
		expect(tile.querySelector('img')?.getAttribute('width')).toBe('64');
		expect(tile.querySelector('img')?.getAttribute('height')).toBe('64');
	});

	it('draws the class glyph and logs nothing when the file is missing', async () => {
		const error = vi.spyOn(console, 'error').mockImplementation(() => {});
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});

		render(ArchetypePortrait, { slug: 'not-drawn-yet', size: 64, silhouette: 'beast' });
		const tile = screen.getByTestId('archetype-portrait');
		const image = tile.querySelector('img');
		expect(image).not.toBeNull();

		await fireEvent.error(image!);

		expect(tile.querySelector('img')).toBeNull();
		expect(tile.querySelector('.archetype-portrait__fallback')).not.toBeNull();
		expect(tile.querySelector('svg.lucide-paw-print')).not.toBeNull();
		expect(error).not.toHaveBeenCalled();
		expect(warn).not.toHaveBeenCalled();
	});

	it('draws the fallback straight away when there is no slug at all', () => {
		render(ArchetypePortrait, { slug: null, size: 64 });

		const tile = screen.getByTestId('archetype-portrait');
		expect(tile.querySelector('img')).toBeNull();
		expect(tile.querySelector('.archetype-portrait__fallback')).not.toBeNull();
	});

	it('picks the glyph by the class the slug and type key resolve to', async () => {
		render(ArchetypePortrait, {
			slug: 'moor-wight',
			size: 64,
			silhouette: archetypeSilhouette('moor-wight', 'undead')
		});
		const tile = screen.getByTestId('archetype-portrait');
		await fireEvent.error(tile.querySelector('img')!);

		expect(tile.querySelector('svg.lucide-ghost')).not.toBeNull();
		expect(tile.querySelector('svg.lucide-swords')).toBeNull();
	});

	it('follows the tier with a CSS frame class and never with image data', () => {
		const { unmount } = render(ArchetypePortrait, { slug: 'bandit-captain', tier: 'boss' });
		let tile = screen.getByTestId('archetype-portrait');
		expect(tile.classList.contains('archetype-portrait--boss')).toBe(true);
		expect(tile.getAttribute('data-tier')).toBe('boss');
		expect(tile.querySelector('.archetype-portrait__image')).not.toBeNull();
		unmount();

		render(ArchetypePortrait, { slug: 'storm-harrower', tier: 'elite' });
		tile = screen.getByTestId('archetype-portrait');
		expect(tile.classList.contains('archetype-portrait--elite')).toBe(true);

		render(ArchetypePortrait, { slug: 'giant-rat', tier: 'regular' });
		const regular = screen.getAllByTestId('archetype-portrait').at(-1)!;
		expect(regular.className).not.toContain('archetype-portrait--boss');
		expect(regular.className).not.toContain('archetype-portrait--elite');
	});

	it('draws the townsfolk plate for a townspeople payload, before any trait flag', () => {
		render(ArchetypePortrait, {
			slug: 'village-blacksmith',
			tier: archetypeTierFromTraits({ boss: true }, 'townspeople'),
			silhouette: archetypeSilhouette('village-blacksmith', 'townspeople')
		});

		const tile = screen.getByTestId('archetype-portrait');
		expect(tile.classList.contains('archetype-portrait--townsfolk')).toBe(true);
		expect(tile.getAttribute('data-tier')).toBe('townsfolk');
	});
});
