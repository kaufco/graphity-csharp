// Adds to the API documentation that DocFX generated, under each type and member that a page of the guide
// uses, the links to those pages ("In the guide:"). The guide publishes which of its pages use which
// part of the API as api-pages.json; this is a step of deploying the API documentation, so that the
// sources do not depend on the guide.
//
// Usage: node add-guide-links.mjs <docs-dir> <api-pages.json> [<guide-url>]
// <guide-url> replaces the address of the guide that api-pages.json names, for a local preview.
// An element of api-pages.json that the documentation does not have, as when the guide already uses
// an API newer than this release, is reported and left out.
import fs from 'node:fs';
import path from 'node:path';

const [docs, data, guideUrl] = process.argv.slice(2);
if (!docs || !data) {
	console.error('Usage: node add-guide-links.mjs <docs-dir> <api-pages.json> [<guide-url>]');
	process.exit(2);
}
const api = JSON.parse(fs.readFileSync(data, 'utf8'));
const guide = (guideUrl ?? api.guide).replace(/\/?$/, '/');
// The guide opens in the language of this documentation.
const language = 'csharp';

const escape = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** The block for an element: a link to each page, with its number and title. */
function guideLinks(pages) {
	const items = pages.map((p) => {
		const { number, title } = api.pages[p];
		return `<li><a href="${escape(`${guide}${p}?lang=${language}`)}">${escape(`${number} ${title}`)}</a></li>`;
	});
	return `<h4 class="section">In the guide</h4>\n<ul class="guide-links">\n${items.join('\n')}\n</ul>\n`;
}

// What follows the documentation of a member, or that of a page.
const NEXT_MEMBER = /<a id="|<h3 |<h2 |<\/article>/g;
const NEXT_SECTION = /<h2 class="section"|<\/article>/g;

/**
 * Adds the block as a section at the end of the documentation of a member, or of the top of a page.
 * Returns the new text, or null if the element is not there.
 */
function addTo(html, anchor, block) {
	const at = anchor === null ? html.indexOf('<article') : html.indexOf(`id="${escape(anchor)}"`);
	if (at < 0) return null;
	const pattern = anchor === null ? NEXT_SECTION : NEXT_MEMBER;
	pattern.lastIndex = anchor === null ? at : html.indexOf('</h3>', at);
	const end = pattern.exec(html)?.index ?? -1;
	if (end < 0) return null;
	if (html.slice(at, end).includes('<ul class="guide-links">')) return html;
	return html.slice(0, end) + block + html.slice(end);
}

const byFile = new Map();
for (const [element, pages] of Object.entries(api.csharp ?? {})) {
	const [file, anchor] = element.split('#');
	if (!byFile.has(file)) byFile.set(file, []);
	byFile.get(file).push({ anchor: anchor ?? null, pages });
}

let linked = 0;
const missing = [];
for (const [file, elements] of byFile) {
	const target = path.join(docs, file);
	if (!fs.existsSync(target)) {
		missing.push(...elements.map((e) => `${file}${e.anchor === null ? '' : `#${e.anchor}`}`));
		continue;
	}
	let html = fs.readFileSync(target, 'utf8');
	for (const { anchor, pages } of elements) {
		const next = addTo(html, anchor, guideLinks(pages));
		if (next === null) missing.push(`${file}${anchor === null ? '' : `#${anchor}`}`);
		else if (next !== html) {
			html = next;
			linked++;
		}
	}
	fs.writeFileSync(target, html);
}
console.log(`Linked ${linked} items and methods to the guide at ${guide}.`);
if (missing.length) console.log(`Not in this documentation, left out:\n  ${missing.join('\n  ')}`);
