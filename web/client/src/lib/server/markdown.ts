import { marked } from 'marked';
import sanitizeHtml from 'sanitize-html';

/**
 * Renders trusted editorial Markdown to sanitized HTML for SSR.
 *
 * Editorial content is authored by staff through RealmFoundry, but it is still
 * sanitized defensively before being injected with `{@html ...}`.
 */
export function renderMarkdown(markdown: string): string {
	const html = marked.parse(markdown ?? '', { async: false }) as string;

	return sanitizeHtml(html, {
		allowedTags: [
			'h1',
			'h2',
			'h3',
			'h4',
			'h5',
			'h6',
			'p',
			'br',
			'hr',
			'strong',
			'em',
			'del',
			'blockquote',
			'ul',
			'ol',
			'li',
			'code',
			'pre',
			'a',
			'img',
			'table',
			'thead',
			'tbody',
			'tr',
			'th',
			'td'
		],
		allowedAttributes: {
			a: ['href', 'title', 'target', 'rel'],
			img: ['src', 'alt', 'title', 'width', 'height'],
			th: ['align'],
			td: ['align'],
			code: ['class']
		},
		allowedSchemes: ['http', 'https', 'mailto'],
		transformTags: {
			a: sanitizeHtml.simpleTransform('a', { rel: 'noopener noreferrer' })
		}
	});
}
