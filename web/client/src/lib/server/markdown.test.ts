import { describe, expect, it } from 'vitest';
import { renderMarkdown } from '#lib/server/markdown';

/**
 * Editorial content is rendered with `{@html …}`, so the sanitiser is the last line of defence.
 * The cases below are the ones an author could plausibly paste in by accident.
 */
describe('renderMarkdown', () => {
	it('renders headings, emphasis, lists, and code', () => {
		const html = renderMarkdown('## Chapter\n\n**bold** and *italic*\n\n- one\n- two\n\n`code`');

		expect(html).toContain('<h2>Chapter</h2>');
		expect(html).toContain('<strong>bold</strong>');
		expect(html).toContain('<em>italic</em>');
		expect(html).toContain('<li>one</li>');
		expect(html).toContain('<code>code</code>');
	});

	it('strips script tags and inline handlers', () => {
		const html = renderMarkdown('<script>alert(1)</script>\n\n<img src="x" onerror="alert(1)">');

		expect(html).not.toContain('<script');
		expect(html).not.toContain('alert(1)');
		expect(html).not.toContain('onerror');
	});

	it('drops javascript: links but keeps the text', () => {
		const html = renderMarkdown('[click me](javascript:alert(1))');

		expect(html).not.toContain('javascript:');
		expect(html).toContain('click me');
	});

	it('marks external links noopener noreferrer', () => {
		const html = renderMarkdown('[Veldrath](https://example.com)');

		expect(html).toContain('href="https://example.com"');
		expect(html).toContain('rel="noopener noreferrer"');
	});

	it('keeps tables and strips disallowed attributes', () => {
		const html = renderMarkdown(
			'| a | b |\n| --- | --- |\n| 1 | 2 |\n\n<div style="color:red">raw</div>'
		);

		expect(html).toContain('<table>');
		expect(html).toContain('<td>1</td>');
		expect(html).not.toContain('style=');
		expect(html).not.toContain('<div');
	});

	it('renders an empty document for empty input', () => {
		expect(renderMarkdown('')).toBe('');
	});

	it('tolerates a null body from the API', () => {
		expect(renderMarkdown(null as unknown as string)).toBe('');
	});
});
