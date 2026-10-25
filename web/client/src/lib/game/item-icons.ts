import {
	BookOpen,
	Boxes,
	Circle,
	FlaskConical,
	Footprints,
	Gem,
	HardHat,
	Layers,
	Package,
	Scroll,
	ScrollText,
	Shirt,
	Shield,
	Sword
} from '@lucide/svelte';
import type { InventoryItem } from './types';

/** Any Lucide icon component (all of them accept `size` and `class`). */
type IconComponent = typeof Sword;

const BY_TYPE: Record<string, IconComponent> = {
	weapon: Sword,
	shield: Shield,
	offhand: BookOpen,
	helmet: HardHat,
	shoulders: Shirt,
	chest: Shirt,
	bracers: Layers,
	gloves: Layers,
	belt: Layers,
	legs: Layers,
	boots: Footprints,
	necklace: Gem,
	ring: Circle,
	questitem: ScrollText,
	enchantmentscroll: Scroll,
	material: Boxes,
	consumable: FlaskConical
};

const BY_SLOT: Record<string, IconComponent> = {
	MainHand: Sword,
	OffHand: Shield,
	Head: HardHat,
	Chest: Shirt,
	Legs: Layers,
	Feet: Footprints,
	Ring: Circle,
	Amulet: Gem
};

const SLOT_LABELS: Record<string, string> = {
	MainHand: 'Main hand',
	OffHand: 'Off hand',
	Head: 'Head',
	Chest: 'Chest',
	Legs: 'Legs',
	Feet: 'Feet',
	Ring: 'Ring',
	Amulet: 'Amulet'
};

/** Icon for an inventory entry, derived from the catalog item type. */
export function itemIcon(item: InventoryItem): IconComponent {
	return BY_TYPE[(item.type ?? '').toLowerCase()] ?? Package;
}

/** Icon for an equipment slot, used to render empty slots in the character sheet. */
export function slotIcon(slot: string): IconComponent {
	return BY_SLOT[slot] ?? Package;
}

/** Human label for an equipment slot. */
export function slotLabel(slot: string): string {
	return SLOT_LABELS[slot] ?? slot;
}
