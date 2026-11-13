import { gameApiFetch } from '#lib/server/game-request';
import type { GameIdentity } from '#lib/server/game-request';
import type { MapGraphDto, MapGraphSaveResult, MapSummaryDto } from '#lib/foundry/map-editor';

/** Lists every authored map with its version. Requires `content.manage`. */
export function listMaps(identity: GameIdentity) {
	return gameApiFetch<MapSummaryDto[]>(identity, '/api/content/maps');
}

/** Reads one map graph in full. Requires `content.manage`. */
export function getMap(identity: GameIdentity, mapId: string) {
	return gameApiFetch<MapGraphDto>(identity, `/api/content/maps/${encodeURIComponent(mapId)}`);
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
