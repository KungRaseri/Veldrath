import { gameApiFetch } from '#lib/server/game-request';
import type { GameIdentity } from '#lib/server/game-request';
import type {
	MapGraphDto,
	MapGraphSaveResult,
	MapGraphValidationResult,
	MapLifecycleResult,
	MapSummaryDto,
	MapTargets
} from '#lib/foundry/map-editor';

/** Lists every authored map with its version and state. Requires `content.view`. */
export function listMaps(identity: GameIdentity) {
	return gameApiFetch<MapSummaryDto[]>(identity, '/api/content/maps');
}

/** Reads one map graph in full. Requires `content.view`. */
export function getMap(identity: GameIdentity, mapId: string) {
	return gameApiFetch<MapGraphDto>(identity, `/api/content/maps/${encodeURIComponent(mapId)}`);
}

/**
 * Lists the references node pickers offer. Requires `content.view`.
 *
 * A picker cannot produce the typo the world validator exists to catch, which is why the editor asks for
 * this instead of letting an author type a slug.
 */
export function getMapTargets(identity: GameIdentity) {
	return gameApiFetch<MapTargets>(identity, '/api/content/maps/targets');
}

/**
 * Saves one map graph. Requires `content.manage`.
 *
 * `expectedVersion` is what makes the save honest: the server refuses the write when somebody else has moved
 * the graph on, instead of letting this edit erase theirs.
 */
export function saveMap(
	identity: GameIdentity,
	mapId: string,
	map: MapGraphDto,
	expectedVersion: number
) {
	return gameApiFetch<MapGraphSaveResult>(
		identity,
		`/api/content/maps/${encodeURIComponent(mapId)}`,
		{ method: 'PUT', json: { map, expectedVersion } }
	);
}

/**
 * Runs the world validator over a candidate graph without storing it. Requires `content.manage`.
 *
 * This is what lets an author iterate: the same issues a save would refuse on, seen while editing.
 */
export function validateMap(identity: GameIdentity, mapId: string, map: MapGraphDto) {
	return gameApiFetch<MapGraphValidationResult>(
		identity,
		`/api/content/maps/${encodeURIComponent(mapId)}/validate`,
		{ method: 'POST', json: { map } }
	);
}

/** Creates an inactive draft with a single entry node. Requires `content.manage`. */
export function createMap(
	identity: GameIdentity,
	request: { mapType: string; ownerId: string; name: string }
) {
	return gameApiFetch<MapGraphDto>(identity, '/api/content/maps', {
		method: 'POST',
		json: request
	});
}

/** Puts a draft forward for review. Requires `content.manage`. */
export function submitMap(identity: GameIdentity, mapId: string) {
	return gameApiFetch<MapLifecycleResult>(
		identity,
		`/api/content/maps/${encodeURIComponent(mapId)}/submit`,
		{ method: 'POST', json: {} }
	);
}

/** Records a reviewer's decision — the only way a map enters the game. Requires the Curator role. */
export function reviewMap(
	identity: GameIdentity,
	mapId: string,
	request: { approved: boolean; notes?: string | null }
) {
	return gameApiFetch<MapLifecycleResult>(
		identity,
		`/api/content/maps/${encodeURIComponent(mapId)}/review`,
		{ method: 'POST', json: request }
	);
}

/** Retires a live map. Requires the Curator role. */
export function deactivateMap(identity: GameIdentity, mapId: string) {
	return gameApiFetch<MapLifecycleResult>(
		identity,
		`/api/content/maps/${encodeURIComponent(mapId)}/deactivate`,
		{ method: 'POST', json: {} }
	);
}

/** Deletes a draft. A live map is retired by deactivating instead. */
export function deleteMap(identity: GameIdentity, mapId: string) {
	return gameApiFetch<MapLifecycleResult>(
		identity,
		`/api/content/maps/${encodeURIComponent(mapId)}`,
		{ method: 'DELETE' }
	);
}
