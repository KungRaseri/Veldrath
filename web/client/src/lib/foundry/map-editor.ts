/** A map node as the editor reads and writes it, mirroring `Veldrath.Contracts.Map.MapNodeDto`. */
export interface MapNodeDto {
	id: string;
	mapId: string;
	nodeType: string;
	refId: string | null;
	displayName: string;
	description: string;
	iconKey: string | null;
	layoutX: number | null;
	layoutY: number | null;
	isEntryPoint: boolean;
	isExitPoint: boolean;
	targetMapId: string | null;
	targetNodeId: string | null;
	isHidden: boolean;
	unlockType: string | null;
	unlockKey: string | null;
	discoverThreshold: number | null;
	sortOrder: number;
}

/** A map edge, mirroring `Veldrath.Contracts.Map.MapEdgeDto`. */
export interface MapEdgeDto {
	id: string;
	mapId: string;
	fromNodeId: string;
	toNodeId: string;
	direction: string;
	edgeType: string;
	label: string | null;
	isTraversable: boolean;
	isHidden: boolean;
}

/** A complete map graph, mirroring `Veldrath.Contracts.Map.MapGraphDto`. */
export interface MapGraphDto {
	id: string;
	mapType: string;
	ownerId: string;
	name: string;
	description: string;
	entryNodeId: string | null;
	nodes: MapNodeDto[];
	edges: MapEdgeDto[];
	version: number;
}

/** A map list row, mirroring `Veldrath.Contracts.Map.MapSummaryDto`. */
export interface MapSummaryDto {
	id: string;
	mapType: string;
	ownerId: string;
	name: string;
	description: string;
	entryNodeId: string | null;
	nodeCount: number;
	edgeCount: number;
	version: number;
}

/** One validation issue reported by a save, mirroring `Veldrath.Contracts.Map.MapGraphIssueDto`. */
export interface MapGraphIssue {
	code: string;
	message: string;
	severity: string;
	mapId: string | null;
	nodeId: string | null;
}

/** The outcome of a save attempt, mirroring `Veldrath.Contracts.Map.MapGraphSaveResponse`. */
export interface MapGraphSaveResult {
	success: boolean;
	status: 'ok' | 'conflict' | 'invalid' | 'not_found';
	version: number;
	issues: MapGraphIssue[];
	message: string | null;
}

/** The fields the inspector can change. Everything else is authored elsewhere or derived. */
export interface NodeEdit {
	displayName: string;
	isHidden: boolean;
	unlockKey: string;
	targetMapId: string;
}

/** A node row as the editor lists it, with the edge count that explains its role at a glance. */
export interface MapNodeRow extends MapNodeDto {
	outgoing: number;
	incoming: number;
}

/** Orders the nodes the way the map shows them and counts the edges that touch each one. */
export function nodeRows(graph: MapGraphDto): MapNodeRow[] {
	return [...graph.nodes]
		.sort((left, right) => left.sortOrder - right.sortOrder || left.id.localeCompare(right.id))
		.map((node) => ({
			...node,
			outgoing: graph.edges.filter((edge) => edge.fromNodeId === node.id).length,
			incoming: graph.edges.filter((edge) => edge.toNodeId === node.id).length
		}));
}

/** Reads the inspector's values for one node into the shape the edit map expects. */
export function readNodeEdit(node: MapNodeDto, form: FormData): NodeEdit {
	const value = (name: string, fallback: string) => {
		const raw = form.get(`node:${node.id}:${name}`);
		return raw === null ? fallback : String(raw);
	};

	return {
		displayName: value('displayName', node.displayName),
		isHidden: value('isHidden', String(node.isHidden)) === 'true',
		unlockKey: value('unlockKey', node.unlockKey ?? ''),
		targetMapId: value('targetMapId', node.targetMapId ?? '')
	};
}

/**
 * Applies inspector edits to a graph, leaving edges and every untouched field exactly as they were.
 *
 * Edges are deliberately not editable here: moving a room is a graph change that needs the canvas and the
 * validator's ear, not a text field.
 */
export function applyNodeEdits(
	graph: MapGraphDto,
	edits: Readonly<Record<string, NodeEdit>>
): MapGraphDto {
	return {
		...graph,
		nodes: graph.nodes.map((node) => {
			const edit = edits[node.id];
			if (!edit) return node;

			return {
				...node,
				displayName: edit.displayName,
				isHidden: edit.isHidden,
				unlockKey: edit.unlockKey.trim() === '' ? null : edit.unlockKey.trim(),
				targetMapId: edit.targetMapId.trim() === '' ? null : edit.targetMapId.trim()
			};
		})
	};
}

/** The outcome of a validate-only pass, mirroring `Veldrath.Contracts.Map.MapGraphValidationResponse`. */
export interface MapGraphValidationResult {
	isValid: boolean;
	issues: MapGraphIssue[];
}

/** The issues a validate pass reported, split by what they are about. */
export interface GroupedIssues {
	/** Issues attributed to a node, keyed by node id. */
	nodes: Record<string, MapGraphIssue[]>;
	/** Issues about the graph itself, which have no node to sit beside. */
	map: MapGraphIssue[];
}

/**
 * Splits issues into the ones a node row can show and the ones the page has to summarise.
 *
 * Exported and unit tested because this decides where a badge appears: an issue with no `nodeId` can
 * only be reported at the top of the page, and dropping it there would hide the reason a save fails.
 *
 * @param issues Issues from the server, in any order.
 * @returns The issues grouped by node id, plus the map-level ones.
 */
export function issuesByNode(issues: readonly MapGraphIssue[]): GroupedIssues {
	const nodes: Record<string, MapGraphIssue[]> = {};
	const map: MapGraphIssue[] = [];

	for (const issue of issues) {
		if (!issue.nodeId) {
			map.push(issue);
			continue;
		}

		(nodes[issue.nodeId] ??= []).push(issue);
	}

	return { nodes, map };
}

/** A one-line description of an issue, with the node it belongs to when the server names one. */
export function issueLabel(issue: MapGraphIssue): string {
	const where = issue.nodeId ?? issue.mapId;
	return where ? `${issue.code}: ${issue.message} (${where})` : `${issue.code}: ${issue.message}`;
}
