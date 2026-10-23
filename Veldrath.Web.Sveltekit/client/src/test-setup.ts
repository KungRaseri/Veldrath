import { afterEach, vi } from 'vitest';
import { cleanup } from '@testing-library/svelte';
import '@testing-library/jest-dom/vitest';

// jsdom (used by the client node test project) does not implement
// ResizeObserver, which @floating-ui/@zag-js rely on to keep popover/menu/modal
// content positioned. Without a stub, positioning updates throw unhandled
// rejections after a test completes and fail the unit-test run. Provide a
// no-op implementation.
if (!('ResizeObserver' in globalThis)) {
	class ResizeObserverMock implements ResizeObserver {
		observe(): void {}
		unobserve(): void {}
		disconnect(): void {}
	}
	vi.stubGlobal('ResizeObserver', ResizeObserverMock);
}

// @testing-library/svelte only auto-cleans up when vitest globals are enabled,
// so register cleanup explicitly for the non-global (explicit imports) setup.
afterEach(() => cleanup());

// Bits UI's `BodyScrollLock` (used by modal Dialog/Menu content) schedules a
// ~24ms cleanup timeout when the component unmounts. When a test ends with that
// timer still pending, it fires after the jsdom environment is torn down and
// throws `ReferenceError: document is not defined` as an unhandled error —
// failing the whole unit-test run. Flush it while the environment is still
// alive, handling both real and fake timer modes.
afterEach(async () => {
	if (vi.isFakeTimers()) {
		vi.runOnlyPendingTimers();
	} else {
		// One macrotask past Bits UI's default 24ms body-scroll-lock restore delay.
		await new Promise((resolve) => setTimeout(resolve, 30));
	}
});
