<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { Check, ChevronLeft, ChevronRight, Sparkles, X } from '@lucide/svelte';
	import { onMount } from 'svelte';
	import { validateCharacterName } from '$lib/game/character-name';
	import type {
		BeginCreationSessionResponse,
		CharacterPreview,
		EquipmentTypeCatalog,
		PointBuyConfig
	} from '$lib/game/creation-types';
	import {
		STAT_NAMES as statNames,
		decreaseAllocation,
		increaseAllocation,
		initialAllocation,
		pointsRemaining,
		type StatAllocation,
		type StatName
	} from '$lib/game/point-buy';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const steps = ['Name', 'Class', 'Species', 'Background', 'Attributes', 'Equipment', 'Review'];

	let step = $state(0);
	let sessionId = $state<string | null>(null);
	let starting = $state(true);
	let startError = $state('');
	let busy = $state(false);
	let stepError = $state('');

	// Name step
	let name = $state('');
	let nameStatus = $state<'idle' | 'checking' | 'available' | 'unavailable'>('idle');
	let nameError = $state('');
	let nameCheckId = 0;
	let nameTimer: ReturnType<typeof setTimeout> | undefined;

	// Selection steps
	let selectedClass = $state('');
	let selectedSpecies = $state('');
	let selectedBackground = $state('');

	// Attributes
	let pointBuy = $state<PointBuyConfig | null>(null);
	let stats = $state<StatAllocation>(initialAllocation(null));

	// Equipment
	let equipmentCatalog = $state<EquipmentTypeCatalog | null>(null);
	let armor = $state('');
	let weapon = $state('');
	let includeShield = $state(false);

	// Review
	let difficultyMode = $state<'normal' | 'hardcore'>('normal');
	let preview = $state<CharacterPreview | null>(null);
	let finalizing = $state(false);

	const remainingPoints = $derived(pointsRemaining(pointBuy, stats));

	onMount(() => {
		void begin();

		return () => clearTimeout(nameTimer);
	});

	async function begin() {
		starting = true;
		startError = '';

		try {
			const response = await fetch('/api/characters/creation', { method: 'POST' });
			const body = (await response.json().catch(() => null)) as
				BeginCreationSessionResponse | { error?: string } | null;

			if (!response.ok || !body || !('sessionId' in body)) {
				startError =
					(body && 'error' in body ? body.error : null) ??
					'Could not start a character creation session.';
				return;
			}

			sessionId = body.sessionId;
			pointBuy = body.pointBuyConfig;
			equipmentCatalog = body.equipmentTypeCatalog;

			stats = initialAllocation(pointBuy);

			selectedClass = data.catalog.classes[0]?.slug ?? '';
			selectedSpecies = data.catalog.species[0]?.slug ?? '';
			selectedBackground = data.catalog.backgrounds[0]?.slug ?? '';
			armor = equipmentCatalog?.armorTypes[0]?.slug ?? '';
			weapon = equipmentCatalog?.weaponTypes[0]?.slug ?? '';
		} catch {
			startError = 'Could not reach the game server. Try again in a moment.';
		} finally {
			starting = false;
		}
	}

	function onNameInput() {
		clearTimeout(nameTimer);
		nameStatus = 'idle';
		nameError = '';

		const value = name.trim();
		const validation = validateCharacterName(value);
		if (!validation.valid) {
			nameStatus = 'unavailable';
			nameError = validation.error ?? 'That name is not valid.';
			return;
		}

		const checkId = ++nameCheckId;
		nameStatus = 'checking';

		nameTimer = setTimeout(async () => {
			try {
				const response = await fetch(
					`/api/characters/creation/check-name?name=${encodeURIComponent(value)}`
				);
				const body = (await response.json().catch(() => null)) as {
					available?: boolean;
					error?: string;
				} | null;

				if (checkId !== nameCheckId) {
					return;
				}

				if (response.ok && body?.available) {
					nameStatus = 'available';
				} else {
					nameStatus = 'unavailable';
					nameError = body?.error ?? 'That name is not available.';
				}
			} catch {
				if (checkId === nameCheckId) {
					nameStatus = 'unavailable';
					nameError = 'Could not check that name. Try again.';
				}
			}
		}, 350);
	}

	function increase(stat: StatName) {
		stats = increaseAllocation(pointBuy, stats, stat);
	}

	function decrease(stat: StatName) {
		stats = decreaseAllocation(pointBuy, stats, stat);
	}

	async function patchStep(
		stepName: string,
		payload: unknown
	): Promise<{ ok: boolean; error: string | null }> {
		if (!sessionId) {
			return { ok: false, error: 'No active creation session.' };
		}

		const response = await fetch(`/api/characters/creation/${sessionId}/${stepName}`, {
			method: 'PATCH',
			headers: { 'content-type': 'application/json' },
			body: JSON.stringify(payload)
		});
		const body = (await response.json().catch(() => null)) as { error?: string } | null;

		return {
			ok: response.ok,
			error: response.ok ? null : (body?.error ?? 'The server rejected that choice.')
		};
	}

	async function loadPreview() {
		if (!sessionId) {
			return;
		}

		const response = await fetch(`/api/characters/creation/${sessionId}/preview`);
		const body = (await response.json().catch(() => null)) as
			CharacterPreview | { error?: string } | null;

		if (!response.ok) {
			stepError = (body && 'error' in body ? body.error : null) ?? 'Could not load the preview.';
			return;
		}

		preview = body as CharacterPreview;
	}

	async function next() {
		stepError = '';

		if (!sessionId) {
			stepError = 'No active creation session. Cancel and start again.';
			return;
		}

		busy = true;
		try {
			switch (step) {
				case 0: {
					const value = name.trim();
					if (nameStatus !== 'available') {
						stepError = nameError || 'Choose an available character name.';
						return;
					}

					const result = await patchStep('name', { characterName: value });
					if (!result.ok) {
						stepError = result.error ?? 'Could not save the name.';
						return;
					}
					break;
				}
				case 1: {
					const result = await patchStep('class', { className: selectedClass });
					if (!result.ok) {
						stepError = result.error ?? 'Could not save the class.';
						return;
					}
					break;
				}
				case 2: {
					const result = await patchStep('species', { speciesSlug: selectedSpecies });
					if (!result.ok) {
						stepError = result.error ?? 'Could not save the species.';
						return;
					}
					break;
				}
				case 3: {
					const result = await patchStep('background', { backgroundId: selectedBackground });
					if (!result.ok) {
						stepError = result.error ?? 'Could not save the background.';
						return;
					}
					break;
				}
				case 4: {
					const result = await patchStep('attributes', { allocations: stats });
					if (!result.ok) {
						stepError = result.error ?? 'Could not save the attributes.';
						return;
					}
					break;
				}
				case 5: {
					const result = await patchStep('equipment', {
						preferredArmorType: armor || null,
						preferredWeaponType: weapon || null,
						includeShield
					});
					if (!result.ok) {
						stepError = result.error ?? 'Could not save the equipment.';
						return;
					}
					break;
				}
				case 6:
					await finalize();
					return;
			}

			step += 1;
			if (step === 6) {
				await loadPreview();
			}
		} finally {
			busy = false;
		}
	}

	async function finalize() {
		if (!sessionId) {
			stepError = 'No active creation session. Cancel and start again.';
			return;
		}

		finalizing = true;
		try {
			const response = await fetch(`/api/characters/creation/${sessionId}/finalize`, {
				method: 'POST',
				headers: { 'content-type': 'application/json' },
				body: JSON.stringify({
					characterName: name.trim(),
					difficultyMode
				})
			});
			const body = (await response.json().catch(() => null)) as {
				id?: string;
				error?: string;
			} | null;

			if (!response.ok || !body?.id) {
				stepError = body?.error ?? 'Could not create the character.';
				return;
			}

			sessionId = null;
			await goto(resolve('/play/[id]', { id: body.id }));
		} finally {
			finalizing = false;
		}
	}

	function previous() {
		if (step > 0) {
			step -= 1;
			stepError = '';
		}
	}

	async function cancel() {
		const active = sessionId;
		sessionId = null;

		if (active) {
			await fetch(`/api/characters/creation/${active}`, { method: 'DELETE' }).catch(
				() => undefined
			);
		}

		await goto(resolve('/characters'));
	}
</script>

<svelte:head>
	<title>Create character — Veldrath</title>
</svelte:head>

<div class="mx-auto max-w-2xl space-y-6 p-6">
	<header class="space-y-1">
		<div class="flex flex-wrap items-center justify-between gap-2">
			<div class="flex items-center gap-3">
				<img
					class="h-10 w-10 object-contain"
					src="/images/veldrath-final-crest-transparent.png"
					alt=""
					aria-hidden="true"
				/>
				<h1 class="h2">Create your character</h1>
			</div>
			{#if data.slots}
				<span class="text-sm text-surface-600-400">
					{data.slots.used} / {data.slots.max} slots used
				</span>
			{/if}
		</div>
		<p class="text-sm text-surface-600-400">
			Step {Math.min(step + 1, steps.length)} of {steps.length} · {steps[step]}
		</p>
		<div class="h-2 overflow-hidden rounded bg-surface-800">
			<div
				class="h-full bg-gradient-to-r from-primary-600 via-primary-500 to-secondary-500 transition-all"
				style={`width: ${((step + 1) / steps.length) * 100}%`}
			></div>
		</div>
	</header>

	{#if data.catalogError}
		<div class="card border border-error-500/40 p-4 text-sm text-error-500">
			{data.catalogError}
		</div>
	{/if}

	{#if starting}
		<div class="card p-6 text-sm text-surface-600-400">Preparing your creation session…</div>
	{:else if startError}
		<div class="space-y-3 card p-6">
			<p class="text-sm text-error-500">{startError}</p>
			<div class="flex flex-wrap gap-2">
				<button class="btn preset-filled-primary-500" type="button" onclick={begin}>
					Try again
				</button>
				<a class="btn preset-tonal-surface" href={resolve('/characters')}>Back to characters</a>
			</div>
		</div>
	{:else}
		<div class="space-y-4 card p-6">
			{#if stepError}
				<p class="text-sm text-error-500">{stepError}</p>
			{/if}

			{#if step === 0}
				<div class="space-y-3">
					<label class="label">
						<span>Character name</span>
						<input
							class="input"
							type="text"
							bind:value={name}
							oninput={onNameInput}
							required
							minlength="2"
							maxlength="20"
							autocomplete="off"
						/>
						<span class="text-xs text-surface-600-400">
							2–20 characters, letters and spaces only.
						</span>
					</label>

					{#if nameStatus === 'checking'}
						<p class="text-sm text-surface-600-400">Checking availability…</p>
					{:else if nameStatus === 'available'}
						<p class="text-sm text-success-500">That name is available.</p>
					{:else if nameStatus === 'unavailable'}
						<p class="text-sm text-error-500">{nameError}</p>
					{/if}
				</div>
			{:else if step === 1}
				<div class="grid gap-2 sm:grid-cols-2">
					{#each data.catalog.classes as option (option.slug)}
						<button
							class="btn justify-start text-left {selectedClass === option.slug
								? 'preset-filled-primary-500'
								: 'preset-tonal-surface'}"
							type="button"
							aria-pressed={selectedClass === option.slug}
							onclick={() => (selectedClass = option.slug)}
						>
							{option.displayName}
						</button>
					{/each}
				</div>
			{:else if step === 2}
				<div class="grid gap-2">
					{#each data.catalog.species as option (option.slug)}
						<button
							class="btn justify-start text-left {selectedSpecies === option.slug
								? 'preset-filled-primary-500'
								: 'preset-tonal-surface'}"
							type="button"
							aria-pressed={selectedSpecies === option.slug}
							onclick={() => (selectedSpecies = option.slug)}
						>
							<span class="font-semibold">{option.displayName}</span>
							{#if option.description}
								<span class="block text-xs opacity-80">{option.description}</span>
							{/if}
						</button>
					{/each}
				</div>
			{:else if step === 3}
				<div class="grid gap-2">
					{#each data.catalog.backgrounds as option (option.slug)}
						<button
							class="btn justify-start text-left {selectedBackground === option.slug
								? 'preset-filled-primary-500'
								: 'preset-tonal-surface'}"
							type="button"
							aria-pressed={selectedBackground === option.slug}
							onclick={() => (selectedBackground = option.slug)}
						>
							<span class="font-semibold">{option.displayName}</span>
							{#if option.description}
								<span class="block text-xs opacity-80">{option.description}</span>
							{/if}
						</button>
					{/each}
				</div>
			{:else if step === 4}
				<div class="space-y-3">
					<p class="text-sm text-surface-600-400">
						Points remaining:
						<span class="font-mono font-semibold text-primary-500">{remainingPoints}</span>
						{pointBuy
							? `of ${pointBuy.totalPoints} (${pointBuy.minStatValue}–${pointBuy.maxStatValue} per stat)`
							: ''}
					</p>

					{#each statNames as stat (stat)}
						<div class="flex items-center justify-between gap-3">
							<span class="text-sm">{stat}</span>
							<div class="flex items-center gap-2">
								<button
									class="btn h-8 w-8 preset-tonal-surface p-0"
									type="button"
									aria-label={`Decrease ${stat}`}
									onclick={() => decrease(stat)}
									disabled={!pointBuy || stats[stat] <= pointBuy.minStatValue}
								>
									−
								</button>
								<span class="w-8 text-center font-mono">{stats[stat]}</span>
								<button
									class="btn h-8 w-8 preset-tonal-surface p-0"
									type="button"
									aria-label={`Increase ${stat}`}
									onclick={() => increase(stat)}
									disabled={!pointBuy || stats[stat] >= pointBuy.maxStatValue}
								>
									+
								</button>
							</div>
						</div>
					{/each}
				</div>
			{:else if step === 5}
				<div class="space-y-3">
					<p class="text-sm text-surface-600-400">
						Optional starting equipment preferences. You can change gear in game.
					</p>

					<label class="label">
						<span>Armor type</span>
						<select class="select" bind:value={armor}>
							<option value="">No preference</option>
							{#each equipmentCatalog?.armorTypes ?? [] as option (option.slug)}
								<option value={option.slug}>{option.displayName}</option>
							{/each}
						</select>
					</label>

					<label class="label">
						<span>Weapon type</span>
						<select class="select" bind:value={weapon}>
							<option value="">No preference</option>
							{#each equipmentCatalog?.weaponTypes ?? [] as option (option.slug)}
								<option value={option.slug}>{option.displayName}</option>
							{/each}
						</select>
					</label>

					<label class="flex items-center gap-2 text-sm">
						<input type="checkbox" bind:checked={includeShield} />
						<span>Include a shield</span>
					</label>
				</div>
			{:else if step === 6}
				<div class="space-y-4">
					{#if preview}
						<dl class="grid gap-2 text-sm sm:grid-cols-2">
							<div>
								<dt class="text-surface-600-400">Name</dt>
								<dd>{name.trim()}</dd>
							</div>
							<div>
								<dt class="text-surface-600-400">Class</dt>
								<dd>{preview.className ?? '—'}</dd>
							</div>
							<div>
								<dt class="text-surface-600-400">Species</dt>
								<dd>{preview.speciesName ?? '—'}</dd>
							</div>
							<div>
								<dt class="text-surface-600-400">Background</dt>
								<dd>{preview.backgroundName ?? '—'}</dd>
							</div>
							<div>
								<dt class="text-surface-600-400">Health / Mana</dt>
								<dd>{preview.health} / {preview.mana}</dd>
							</div>
							<div class="sm:col-span-2">
								<dt class="text-surface-600-400">Attributes</dt>
								<dd class="font-mono text-xs">
									STR {preview.strength} · DEX {preview.dexterity} · CON {preview.constitution} · INT
									{preview.intelligence} · WIS {preview.wisdom} · CHA {preview.charisma}
								</dd>
							</div>
						</dl>
					{:else}
						<p class="text-sm text-surface-600-400">Loading preview…</p>
					{/if}

					<fieldset class="space-y-2">
						<legend class="text-sm font-semibold">Difficulty</legend>
						<label class="flex items-center gap-2 text-sm">
							<input type="radio" bind:group={difficultyMode} value="normal" />
							<span>Normal — standard death rules</span>
						</label>
						<label class="flex items-center gap-2 text-sm">
							<input type="radio" bind:group={difficultyMode} value="hardcore" />
							<span>Hardcore — death is permanent</span>
						</label>
					</fieldset>

					{#if difficultyMode === 'hardcore'}
						<p class="text-sm text-warning-500">
							Hardcore characters cannot be restored after death.
						</p>
					{/if}
				</div>
			{/if}

			<div
				class="flex flex-wrap items-center justify-between gap-2 border-t border-surface-200-800 pt-4"
			>
				<div class="flex flex-wrap gap-2">
					<button
						class="btn preset-tonal-surface"
						type="button"
						onclick={cancel}
						disabled={busy || finalizing}
					>
						<X size={15} aria-hidden="true" />
						Cancel
					</button>
					{#if step > 0}
						<button
							class="btn preset-tonal-surface"
							type="button"
							onclick={previous}
							disabled={busy || finalizing}
						>
							<ChevronLeft size={15} aria-hidden="true" />
							Back
						</button>
					{/if}
				</div>

				<button
					class="btn preset-filled-primary-500"
					type="button"
					onclick={next}
					disabled={busy || finalizing || (step === 0 && nameStatus !== 'available')}
				>
					{#if step === 6}
						<Check size={16} aria-hidden="true" />
						{finalizing ? 'Creating…' : 'Create character'}
					{:else if busy}
						<Sparkles size={16} aria-hidden="true" />
						Saving…
					{:else}
						Continue
						<ChevronRight size={16} aria-hidden="true" />
					{/if}
				</button>
			</div>
		</div>
	{/if}
</div>
