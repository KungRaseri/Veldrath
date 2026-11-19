import { fireEvent, render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';
import ListEditor from './ListEditor.svelte';
import RowEditor from './RowEditor.svelte';
import type { ContentField } from '#lib/foundry/content-editor';

/** A row descriptor, defaulted the way the server sends it. */
const field = (name: string, fieldType: string, overrides: Record<string, unknown> = {}) =>
	({
		name,
		label: name,
		fieldType,
		required: false,
		default: null,
		min: null,
		max: null,
		enumValues: null,
		hint: null,
		...overrides
	}) as ContentField;

describe('ListEditor', () => {
	it('renders each entry and hands back the whole list on edit', async () => {
		const onchange = vi.fn();
		render(ListEditor, { name: 'tags', values: ['fire', 'ice'], onchange } as never);

		expect((screen.getByLabelText('tags 1') as HTMLInputElement).value).toBe('fire');

		await fireEvent.input(screen.getByLabelText('tags 2'), { target: { value: 'frost' } });

		expect(onchange).toHaveBeenCalledWith(['fire', 'frost']);
	});

	it('appends, removes and reorders', async () => {
		const onchange = vi.fn();
		render(ListEditor, { name: 'tags', values: ['a', 'b'], onchange } as never);

		await fireEvent.click(screen.getByRole('button', { name: 'Add entry' }));
		expect(onchange).toHaveBeenLastCalledWith(['a', 'b', '']);

		await fireEvent.click(screen.getByRole('button', { name: 'Remove tags 1' }));
		expect(onchange).toHaveBeenLastCalledWith(['b']);

		await fireEvent.click(screen.getByRole('button', { name: 'Move tags 1 down' }));
		expect(onchange).toHaveBeenLastCalledWith(['b', 'a']);
	});

	it('says so when the list is empty', () => {
		render(ListEditor, { name: 'tags', values: [], onchange: vi.fn() } as never);

		expect(screen.getByText('No entries yet.')).toBeTruthy();
	});

	it('disables the moves that would fall off either end', () => {
		render(ListEditor, { name: 'tags', values: ['only'], onchange: vi.fn() } as never);

		expect(
			(screen.getByRole('button', { name: 'Move tags 1 up' }) as HTMLButtonElement).disabled
		).toBe(true);
		expect(
			(screen.getByRole('button', { name: 'Move tags 1 down' }) as HTMLButtonElement).disabled
		).toBe(true);
	});
});

describe('RowEditor', () => {
	const items: ContentField[] = [
		field('type', 'Text', { required: true }),
		field('quantity', 'Integer', { min: 1 }),
		field('isGuaranteed', 'Boolean')
	];

	it('renders one fieldset per row, shaped by the items', () => {
		render(RowEditor, {
			name: 'objectives',
			items,
			rows: [{ type: 'kill', quantity: '5' }],
			onchange: vi.fn()
		} as never);

		expect(screen.getByText('Entry 1')).toBeTruthy();
		expect((screen.getByLabelText(/^type/) as HTMLInputElement).value).toBe('kill');
		expect((screen.getByLabelText('quantity') as HTMLInputElement).value).toBe('5');
	});

	it('adds a row seeded from the item defaults', async () => {
		const onchange = vi.fn();
		render(RowEditor, { name: 'objectives', items, rows: [], onchange } as never);

		expect(screen.getByText('No entries yet.')).toBeTruthy();

		await fireEvent.click(screen.getByRole('button', { name: 'Add entry' }));

		expect(onchange).toHaveBeenCalledWith([{ type: '', quantity: '', isGuaranteed: false }]);
	});

	it('edits one property of one row without touching the others', async () => {
		const onchange = vi.fn();
		render(RowEditor, {
			name: 'objectives',
			items,
			rows: [{ type: 'kill' }, { type: 'collect' }],
			onchange
		} as never);

		await fireEvent.input(screen.getAllByLabelText(/^type/)[1], { target: { value: 'visit' } });

		expect(onchange).toHaveBeenCalledWith([{ type: 'kill' }, { type: 'visit' }]);
	});

	it('toggles a boolean cell and removes a row', async () => {
		const onchange = vi.fn();
		render(RowEditor, {
			name: 'entries',
			items,
			rows: [{ isGuaranteed: false }],
			onchange
		} as never);

		await fireEvent.click(screen.getByLabelText('isGuaranteed'));
		expect(onchange).toHaveBeenLastCalledWith([{ isGuaranteed: true }]);

		await fireEvent.click(screen.getByRole('button', { name: 'Remove entry 1' }));
		expect(onchange).toHaveBeenLastCalledWith([]);
	});

	it('offers the slugs a Reference row field may name', () => {
		render(RowEditor, {
			name: 'entries',
			items: [field('itemSlug', 'Reference', { refType: 'item' })],
			rows: [{}],
			onchange: vi.fn(),
			refOptions: { item: [{ value: 'iron-sword', label: 'Iron Sword' }] }
		} as never);

		expect(screen.getByText('Iron Sword')).toBeTruthy();
	});

	it('falls back to an empty option list when a reference type has no options loaded', () => {
		render(RowEditor, {
			name: 'rows',
			items: [field('itemSlug', 'Reference', { refType: 'quest' })],
			rows: [{}],
			onchange: vi.fn()
		} as never);

		expect(screen.queryAllByRole('option')).toEqual([]);
	});

	it('renders an enum cell, a bounded decimal cell and an item hint', async () => {
		const onchange = vi.fn();
		render(RowEditor, {
			name: 'rows',
			items: [
				field('rarity', 'EnumString', { enumValues: ['Common', 'Rare'] }),
				field('weight', 'Decimal', { min: 0, max: 9 }),
				field('note', 'Text', { hint: 'Optional note' })
			],
			rows: [{ rarity: 'Common' }],
			onchange
		} as never);

		expect(screen.getByText('Optional note')).toBeTruthy();
		expect((screen.getByLabelText('weight') as HTMLInputElement).getAttribute('max')).toBe('9');

		await fireEvent.change(screen.getByLabelText('rarity'), { target: { value: 'Rare' } });

		expect(onchange).toHaveBeenCalledWith([{ rarity: 'Rare' }]);
	});

	it('moves a row, and disables the moves that would fall off either end', async () => {
		const onchange = vi.fn();
		render(RowEditor, {
			name: 'rows',
			items: [field('a', 'Text')],
			rows: [{ a: '1' }, { a: '2' }],
			onchange
		} as never);

		expect(
			(screen.getByRole('button', { name: 'Move entry 1 up' }) as HTMLButtonElement).disabled
		).toBe(true);
		expect(
			(screen.getByRole('button', { name: 'Move entry 2 down' }) as HTMLButtonElement).disabled
		).toBe(true);

		await fireEvent.click(screen.getByRole('button', { name: 'Move entry 1 down' }));

		expect(onchange).toHaveBeenCalledWith([{ a: '2' }, { a: '1' }]);
	});

	it('labels an empty enum option rather than showing a blank one', () => {
		render(RowEditor, {
			name: 'rows',
			items: [field('rarity', 'EnumString', { enumValues: ['', 'Rare'] })],
			rows: [{}],
			onchange: vi.fn()
		} as never);

		expect(screen.getByRole('option', { name: '(empty)' })).toBeTruthy();
	});

	it('treats a Reference without a target and an empty enum as plain text', () => {
		render(RowEditor, {
			name: 'rows',
			items: [field('mystery', 'Reference'), field('empty', 'EnumString', { enumValues: [] })],
			rows: [{}],
			onchange: vi.fn()
		} as never);

		expect(screen.getByLabelText('mystery')).toBeTruthy();
		expect(screen.queryByRole('combobox')).toBeNull();
	});
});
