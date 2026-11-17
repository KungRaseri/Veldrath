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

/**
 * The map kinds an author may create, with the one-line explanation the wizard shows.
 *
 * `World` is reserved: the server refuses it, so it is not offered. `Region` and `Zone` maps *define* the
 * thing they are owned by, while an `Interior` belongs to a `ZoneLocation` that must already exist.
 */
export const MAP_KINDS = [
	{
		value: 'Region',
		label: 'Region',
		hint: 'A region: its nodes are the zones inside it and the exits to other regions.'
	},
	{
		value: 'Zone',
		label: 'Zone',
		hint: 'A zone: its nodes are the locations inside it and the exits back to its region.'
	},
	{
		value: 'Interior',
		label: 'Interior',
		hint: 'A dungeon or building interior. It belongs to a location that already exists.'
	}
] as const;

/** One creatable map kind. */
export type MapKind = (typeof MAP_KINDS)[number];

/** The hint per kind, so a component can look one up without a search or a fallback branch. */
export const MAP_KIND_HINTS: Record<string, string> = Object.fromEntries(
	MAP_KINDS.map((entry) => [entry.value, entry.hint])
);

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
	/**
	 * Whether the map is in the game. Optional because a partial payload (and a test fixture) predates the
	 * review gate; the contract defaults it to `true`, which is what {@link mapState} assumes.
	 */
	isActive?: boolean;
	/** The version a reviewer signed off; `0` when it has never been approved. */
	approvedVersion?: number;
	/** When the map was put forward for review. */
	submittedAt?: string | null;
	/** When a reviewer approved it. */
	approvedAt?: string | null;
	/** Notes from the last review decision, so a rejection reason is readable on the map itself. */
	lastReviewNotes?: string | null;
	/** When the last review decision was made. */
	lastReviewedAt?: string | null;
	/**
	 * Whether this account may write the map: the account that created it, or anyone holding
	 * `content.manage`. Decided per map by the server, so a reader of someone else's draft gets no controls.
	 */
	canEdit?: boolean;
}

/** One option in a reference picker, mirroring `Veldrath.Contracts.Map.MapTargetOptionDto`. */
export interface MapTargetOption {
	/** Value stored on the node — a slug, an owner slug, or a map id. */
	id: string;
	/** Human-readable label for the option. */
	label: string;
}

/** One node of an authored map, mirroring `Veldrath.Contracts.Map.MapNodeOptionDto`. */
export interface MapNodeOption extends MapTargetOption {
	/** The map that owns the node. */
	mapId: string;
	/** Whether the node is its map's entry point. */
	isEntryPoint: boolean;
}

/** The references node pickers offer, mirroring `Veldrath.Contracts.Map.MapTargetsDto`. */
export interface MapTargets {
	/** Every authored map, for exit destinations. */
	maps: MapTargetOption[];
	/** Owners of zone and interior maps. */
	zones: MapTargetOption[];
	/** Every zone location. */
	locations: MapTargetOption[];
	/** Owners of region maps. */
	regions: MapTargetOption[];
	/** Every node of every map, so an exit's destination is chosen rather than typed. */
	nodes: MapNodeOption[];
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
	/** Whether the map is in the game; see {@link MapGraphDto.isActive}. */
	isActive?: boolean;
	/** The version a reviewer signed off. */
	approvedVersion?: number;
	/** When the map was put forward for review. */
	submittedAt?: string | null;
	/** Whether this account may write the map; see {@link MapGraphDto.canEdit}. */
	canEdit?: boolean;
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

/**
 * Reads the draft a page posted, refusing anything that is not this map's graph.
 *
 * There is exactly one writer — the draft the canvas holds — so the save action never reconstructs a graph
 * from individual form fields. A malformed or mismatched payload falls back to the stored graph, which
 * makes the save a no-op rather than a partial write of something nobody authored.
 *
 * @param raw The `graph` form field, as posted.
 * @param fallback The graph as stored, used when the payload cannot be trusted.
 * @param mapId The map the route names.
 * @returns The draft to save, or the stored graph.
 */
export function parseDraftGraph(raw: unknown, fallback: MapGraphDto, mapId: string): MapGraphDto {
	if (typeof raw !== 'string' || raw.trim() === '') return fallback;

	try {
		const parsed: unknown = JSON.parse(raw);
		if (!parsed || typeof parsed !== 'object') return fallback;

		const candidate = parsed as Partial<MapGraphDto>;
		if (candidate.id !== mapId) return fallback;
		if (!Array.isArray(candidate.nodes) || !Array.isArray(candidate.edges)) return fallback;

		return candidate as MapGraphDto;
	} catch {
		return fallback;
	}
}

/** The outcome of a lifecycle action, mirroring `Veldrath.Contracts.Map.MapLifecycleResponse`. */
export interface MapLifecycleResult {
	success: boolean;
	status: 'ok' | 'invalid' | 'not_found' | 'conflict';
	message: string | null;
	issues: MapGraphIssue[];
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

/** The four states a map can be in, named the way the editor shows them. */
export type MapState = 'Draft' | 'In review' | 'Live' | 'Changed since approval';

/**
 * Which lifecycle state a map is in.
 *
 * Derived rather than stored: `ApprovedVersion < Version` is the whole of "changed since approval", so the
 * badge cannot disagree with the numbers it comes from.
 *
 * @param map The graph or summary to read.
 * @returns The state to show.
 */
export function mapState(map: {
	isActive?: boolean;
	approvedVersion?: number;
	version: number;
	submittedAt?: string | null;
}): MapState {
	if (map.isActive === false) {
		return map.submittedAt ? 'In review' : 'Draft';
	}

	return (map.approvedVersion ?? 0) < map.version ? 'Changed since approval' : 'Live';
}

/** What the review queue can say about a submitted map before a reviewer opens it. */
export interface MapReviewVerdict {
	/** `ready` when approval would succeed, `blocked` when it would be refused, `unknown` when it could not be checked. */
	status: 'ready' | 'blocked' | 'unknown';
	/** Errors, which refuse an approval exactly as they refuse a save. */
	errors: number;
	/** Warnings, which a submitted draft may carry and which a reviewer still has to see. */
	warnings: number;
}

/**
 * Whether approving a submitted map would succeed.
 *
 * The decision re-runs the world rules, so a reviewer can see the answer before opening the graph instead of
 * discovering a refusal at the end of it. A warning alone is not a refusal — a half-made map is how a zone gets
 * built — except `map_without_exit`, which is what strands every character that arrives on the map.
 *
 * @param issues Issues from a validate pass, or `null` when the pass could not run.
 * @returns The verdict, with the two counts a queue row shows.
 */
export function reviewVerdict(issues: readonly MapGraphIssue[] | null): MapReviewVerdict {
	if (issues === null) return { status: 'unknown', errors: 0, warnings: 0 };

	const errors = issues.filter((issue) => issue.severity === 'Error').length;
	const blocking = issues.some((issue) => issue.code === 'map_without_exit');

	return {
		status: errors > 0 || blocking ? 'blocked' : 'ready',
		errors,
		warnings: issues.length - errors
	};
}

/**
 * One line a reviewer can act on, naming the count that decides the verdict.
 *
 * Kept beside `reviewVerdict` rather than in the page so the wording of a refusal has a test: zero errors with a
 * blocked verdict is not a contradiction, it is the missing exit, and the row has to say so.
 *
 * @param verdict The verdict to describe.
 * @returns The sentence the queue shows on that map's row.
 */
export function reviewVerdictLabel(verdict: MapReviewVerdict): string {
	const errors = `${verdict.errors} error${verdict.errors === 1 ? '' : 's'}`;

	if (verdict.status === 'unknown') return 'The verdict could not be checked';

	if (verdict.status === 'blocked') {
		return verdict.errors > 0
			? `Approval would be refused · ${errors}`
			: 'Approval would be refused · the map has no exit';
	}

	return verdict.warnings === 0
		? 'Approval would accept it'
		: `Approval would accept it · ${verdict.warnings} warning${verdict.warnings === 1 ? '' : 's'}`;
}

/**
 * Trims the two nullable text fields so a blank input means "no value" rather than an empty string.
 *
 * The editor keeps the raw text while the author types — trimming on each keystroke would make a space
 * untypable in the middle of a key — and normalises once, when the draft is posted.
 *
 * @param graph The draft as edited.
 * @returns The same draft with blank unlock keys and exit targets set to `null`.
 */
export function normalizeDraft(graph: MapGraphDto): MapGraphDto {
	return {
		...graph,
		nodes: graph.nodes.map((node) => ({
			...node,
			unlockKey: blankToNull(node.unlockKey),
			targetMapId: blankToNull(node.targetMapId)
		}))
	};
}

function blankToNull(value: string | null): string | null {
	if (value === null) return null;
	const trimmed = value.trim();
	return trimmed === '' ? null : trimmed;
}

/** A one-line description of an issue, with the node it belongs to when the server names one. */
export function issueLabel(issue: MapGraphIssue): string {
	const where = issue.nodeId ?? issue.mapId;
	return where ? `${issue.code}: ${issue.message} (${where})` : `${issue.code}: ${issue.message}`;
}
