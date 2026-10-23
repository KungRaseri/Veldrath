import type { HubConnection } from '@microsoft/signalr';
import { createGameHub } from '$lib/hub';
import type {
	CharacterSelectedPayload,
	InventoryItem,
	InventoryLoadedPayload,
	KickedPayload,
	MapGraph,
	MapGraphReceivedPayload,
	NodeTraveledPayload,
	ServerInfoPayload,
	ZoneEnteredPayload
} from './types';

export type HubPhase =
	'idle' | 'connecting' | 'connected' | 'reconnecting' | 'failed' | 'disconnected';

/**
 * Reactive game session for the Phase 1 vertical slice.
 *
 * Owns the SignalR connection and reduces hub events into runes so Svelte
 * components can render without direct SignalR knowledge. Phase 2 extends this
 * with combat, chat, shop, and reconnect stores.
 */
export class GameSession {
	phase = $state<HubPhase>('idle');
	serverInfo = $state<ServerInfoPayload | null>(null);
	character = $state<CharacterSelectedPayload | null>(null);
	zone = $state<ZoneEnteredPayload | null>(null);
	map = $state<MapGraph | null>(null);
	currentNodeId = $state<string | null>(null);
	selectedNodeId = $state<string | null>(null);
	inventory = $state<InventoryItem[]>([]);
	equipped = $state<Record<string, InventoryItem>>({});
	message = $state<string | null>(null);
	error = $state<string | null>(null);
	showInventory = $state(false);
	loadingMap = $state(false);

	private hub: HubConnection | null = null;
	private characterId: string | null = null;

	get selectedNode() {
		return this.map?.nodes.find((node) => node.id === this.selectedNodeId) ?? null;
	}

	get currentNode() {
		return this.map?.nodes.find((node) => node.id === this.currentNodeId) ?? null;
	}
	get adjacentNodeIds(): string[] {
		if (!this.map || !this.currentNodeId) {
			return [];
		}

		return this.map.edges
			.filter(
				(edge) => edge.fromNodeId === this.currentNodeId && edge.isTraversable && !edge.isHidden
			)
			.map((edge) => edge.toNodeId)
			.filter((nodeId, index, all) => all.indexOf(nodeId) === index);
	}

	get zoneLabel(): string {
		if (this.zone) return this.zone.name;
		if (this.map?.mapType === 'Zone') return this.map.name;
		return 'Region map';
	}

	get equippedEntries(): [string, InventoryItem][] {
		return Object.entries(this.equipped).filter(([, item]) => Boolean(item));
	}

	async connect(characterId: string): Promise<void> {
		this.characterId = characterId;
		this.phase = 'connecting';
		this.error = null;
		this.message = null;

		try {
			this.hub = createGameHub();
			this.registerHandlers();

			this.hub.onreconnecting(() => {
				this.phase = 'reconnecting';
			});

			this.hub.onreconnected(async () => {
				this.phase = 'connected';
				this.message = 'Reconnected. Restoring character...';
				await this.selectCharacter();
			});

			this.hub.onclose(() => {
				if (this.phase !== 'disconnected') {
					this.phase = 'failed';
				}
			});

			await this.hub.start();
			this.phase = 'connected';
			await this.selectCharacter();
		} catch (error) {
			this.phase = 'failed';
			this.error = error instanceof Error ? error.message : 'Failed to connect to the game server.';
		}
	}

	async disconnect(): Promise<void> {
		this.phase = 'disconnected';
		const hub = this.hub;
		this.hub = null;
		if (hub) {
			await hub.stop().catch(() => undefined);
		}
	}

	async selectCharacter(): Promise<void> {
		if (!this.hub || !this.characterId) return;
		await this.hub.invoke('SelectCharacter', this.characterId);
	}

	async requestMapGraph(mapId: string | null = null): Promise<void> {
		if (!this.hub) return;
		this.loadingMap = true;
		await this.hub.invoke('GetMapGraph', mapId);
	}

	async travel(nodeId: string): Promise<void> {
		if (!this.hub || !this.map) return;
		this.error = null;
		await this.hub.invoke('TravelToNode', { mapId: this.map.id, nodeId });
	}

	async enterZone(zoneId: string): Promise<void> {
		if (!this.hub) return;
		await this.hub.invoke('EnterZone', zoneId);
	}

	async openInventory(): Promise<void> {
		if (!this.hub) return;
		this.error = null;
		await this.hub.invoke('GetInventory');
	}

	selectNode(nodeId: string): void {
		this.selectedNodeId = nodeId;
	}

	private registerHandlers(): void {
		const hub = this.hub;
		if (!hub) return;

		hub.on('ServerInfo', (payload: ServerInfoPayload) => {
			this.serverInfo = payload;
		});

		hub.on('CharacterSelected', (payload: CharacterSelectedPayload) => {
			void this.handleCharacterSelected(payload);
		});

		hub.on('CharacterAlreadyActive', () => {
			this.error = 'This character is already active in another session.';
		});

		hub.on('ZoneEntered', (payload: ZoneEnteredPayload) => {
			this.zone = payload;
		});

		hub.on('MapGraphReceived', (payload: MapGraphReceivedPayload) => {
			this.loadingMap = false;
			if (!payload.success || !payload.map) {
				this.error = payload.errorMessage ?? 'Failed to load the map.';
				return;
			}

			this.map = payload.map;
			this.currentNodeId =
				payload.currentNodeId ?? payload.map.entryNodeId ?? payload.map.nodes[0]?.id ?? null;
			this.selectedNodeId = this.currentNodeId;
		});

		hub.on('NodeTraveled', (payload: NodeTraveledPayload) => {
			void this.handleNodeTraveled(payload);
		});

		hub.on('InventoryLoaded', (payload: InventoryLoadedPayload) => {
			this.inventory = payload.itemDetails ?? [];
			this.equipped = payload.equippedItems ?? {};
			if (payload.openOverlay !== false) {
				this.showInventory = true;
			}
		});

		hub.on('SystemMessage', (message: string) => {
			this.message = message;
		});

		hub.on('Error', (message: string) => {
			this.error = message;
		});

		hub.on('OnKicked', (payload: KickedPayload) => {
			this.error = payload.reason ?? 'You were disconnected by a moderator.';
			this.phase = 'disconnected';
		});
	}

	private async handleCharacterSelected(payload: CharacterSelectedPayload): Promise<void> {
		this.character = payload;
		this.currentNodeId = payload.currentNodeId ?? null;

		if (payload.currentZoneId) {
			await this.enterZone(payload.currentZoneId);
		}

		await this.requestMapGraph(null);
	}

	private async handleNodeTraveled(payload: NodeTraveledPayload): Promise<void> {
		if (!payload.success) {
			this.error = payload.errorMessage ?? 'Travel failed.';
			return;
		}

		this.message = payload.message ?? null;

		if (payload.nodeId) {
			this.currentNodeId = payload.nodeId;
			this.selectedNodeId = payload.nodeId;
		}

		if (payload.mapChanged) {
			await this.requestMapGraph(payload.mapId ?? null);
		}
	}
}
