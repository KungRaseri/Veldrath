import { expect, type Page } from '@playwright/test';

/**
 * Containers a page is expected to hold its content inside.
 *
 * Cards, the hero, feature cards, auth cards and the game panels are the boxes the design system
 * promises are the visible edge of a page; anything painting outside them is the bug this helper is
 * for. A caller that knows better can pass its own selector (or a comma-separated list).
 */
export const CONTENT_CONTAINERS = '.card, .hero, .feature-card, .auth-card, .game-panel';

/** What the page reported: how far it scrolls sideways, and which elements escaped their box. */
export interface OverflowReport {
	/** `scrollWidth - clientWidth` of the document, in pixels (0 or less when nothing scrolls). */
	documentOverflow: number;
	/** One readable line per offender, capped so a broken layout cannot flood the report. */
	offenders: string[];
}

/**
 * Asserts a page does not overflow horizontally and that no container paints outside itself.
 *
 * Two different failures are checked because they look alike and are not the same bug:
 *
 * 1. **The document scrolls sideways.** A card has no clipping, so the row that is too wide widens the
 *    document and *every* page gains a horizontal scrollbar — this is how the creation wizard's
 *    species steps reached a person.
 * 2. **A descendant paints past its container's edge.** This names the element, which is the half a
 *    screenshot cannot give you.
 *
 * A descendant inside an ancestor that clips or scrolls horizontally (`overflow-x` hidden/clip/
 * auto/scroll) is skipped: it is contained by something, so it cannot be painting over the page —
 * that is how the map node cells truncate a long place name.
 *
 * @param page The page to inspect, with its content rendered.
 * @param selector Containers to check; defaults to {@link CONTENT_CONTAINERS}.
 */
export async function expectNoHorizontalOverflow(
	page: Page,
	selector: string = CONTENT_CONTAINERS
): Promise<void> {
	const report = await page.evaluate((containerSelector): OverflowReport => {
		const doc = document.scrollingElement as HTMLElement;
		const offenders: string[] = [];

		const describe = (element: Element): string => {
			const classes = (element.getAttribute('class') ?? '')
				.trim()
				.split(/\s+/)
				.filter(Boolean)
				.slice(0, 2)
				.join('.');
			const text = (element.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 40);
			return `<${element.tagName.toLowerCase()}${classes ? `.${classes}` : ''}> ${text}`;
		};

		const clipsHorizontally = (element: Element): boolean => {
			const overflow = getComputedStyle(element).overflowX;
			return (
				overflow === 'hidden' || overflow === 'clip' || overflow === 'auto' || overflow === 'scroll'
			);
		};

		for (const container of Array.from(document.querySelectorAll(containerSelector))) {
			const box = container.getBoundingClientRect();

			for (const child of Array.from(container.querySelectorAll('*'))) {
				const childBox = child.getBoundingClientRect();
				// Hidden elements report a zero box, and a zero box is not an overflow.
				if (childBox.width === 0 && childBox.height === 0) continue;

				const contained = (() => {
					for (
						let node = child.parentElement;
						node && node !== container;
						node = node.parentElement
					) {
						if (clipsHorizontally(node)) return true;
					}
					return false;
				})();
				if (contained) continue;

				// 1px of slack: sub-pixel layout rounds, and a border is not an overflow.
				if (childBox.right > box.right + 1 || childBox.left < box.left - 1) {
					offenders.push(
						`${describe(container)} contains ${describe(child)} at ` +
							`${Math.round(childBox.left)}–${Math.round(childBox.right)}, ` +
							`container is ${Math.round(box.left)}–${Math.round(box.right)}`
					);
				}
			}
		}

		return {
			documentOverflow: doc.scrollWidth - doc.clientWidth,
			offenders: offenders.slice(0, 5)
		};
	}, selector);

	expect(
		report.documentOverflow,
		`the page scrolls sideways by ${report.documentOverflow}px`
	).toBeLessThanOrEqual(0);
	expect(report.offenders, 'a container paints past its own edge').toEqual([]);
}
