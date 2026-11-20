import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';
import ArchetypePortrait from './ArchetypePortrait.svelte';

afterEach(() => {
	vi.restoreAllMocks();
});

describe('archetype portrait', () => {
	it('renders the tile file a known slug resolves to', () => {
		render(ArchetypePortrait, { slug: 'giant-rat', size: 128 });

		const image = screen.getByTestId('archetype-portrait').querySelector('img');
		expect(image?.getAttribute('src')).toBe('/images/archetypes/giant-rat-128.webp');
	});

	it('renders the 256 file and the detail box for the detail surface', () => {
		render(ArchetypePortrait, { slug: 'giant-rat', size: 256 });

		const tile = screen.getByTestId('archetype-portrait');
		expect(tile.querySelector('img')?.getAttribute('src')).toBe(
			'/images/archetypes/giant-rat-256.webp'
		);
		expect(tile.classList.contains('archetype-portrait--detail')).toBe(true);
	});

	it('draws the class glyph and logs nothing when the file is missing', async () => {
		const error = vi.spyOn(console, 'error').mockImplementation(() => {});
		const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});

		render(ArchetypePortrait, { slug: 'not-drawn-yet', size: 128, silhouette: 'beast' });
		const tile = screen.getByTestId('archetype-portrait');
		const image = tile.querySelector('img');
		expect(image).not.toBeNull();

		await fireEvent.error(image!);

		expect(tile.querySelector('img')).toBeNull();
		expect(tile.querySelector('.archetype-portrait__fallback')).not.toBeNull();
		expect(error).not.toHaveBeenCalled();
		expect(warn).not.toHaveBeenCalled();
	});

	it('draws the fallback straight away when there is no slug at all', () => {
		render(ArchetypePortrait, { slug: null, size: 128 });

		const tile = screen.getByTestId('archetype-portrait');
		expect(tile.querySelector('img')).toBeNull();
		expect(tile.querySelector('.archetype-portrait__fallback')).not.toBeNull();
	});

	it('follows the tier with a CSS frame class and never with image data', () => {
		const { unmount } = render(ArchetypePortrait, { slug: 'bandit-captain', tier: 'boss' });
		let tile = screen.getByTestId('archetype-portrait');
		expect(tile.classList.contains('archetype-portrait--boss')).toBe(true);
		expect(tile.getAttribute('data-tier')).toBe('boss');
		unmount();

		render(ArchetypePortrait, { slug: 'storm-harrower', tier: 'elite' });
		tile = screen.getByTestId('archetype-portrait');
		expect(tile.classList.contains('archetype-portrait--elite')).toBe(true);

		render(ArchetypePortrait, { slug: 'giant-rat', tier: 'regular' });
		const regular = screen.getAllByTestId('archetype-portrait').at(-1)!;
		expect(regular.className).not.toContain('archetype-portrait--boss');
		expect(regular.className).not.toContain('archetype-portrait--elite');

		render(ArchetypePortrait, { slug: 'mira', tier: 'townsfolk' });
		const townsfolk = screen.getAllByTestId('archetype-portrait').at(-1)!;
		expect(townsfolk.classList.contains('archetype-portrait--townsfolk')).toBe(true);
	});
});
