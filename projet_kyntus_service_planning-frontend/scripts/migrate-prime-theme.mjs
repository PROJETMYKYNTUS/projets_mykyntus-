import fs from 'fs';
import path from 'path';

const PRIME_DIR = path.resolve('src/app/features/prime');

function walk(dir, files = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, files);
    else if (entry.name.endsWith('.ts')) files.push(full);
  }
  return files;
}

const SOLID_BTN_RE =
  /bg-(?:blue|emerald|indigo|cyan|rose|violet|red|green|slate-[67]|amber)-|ky-gradient|--ky-gradient|btn-primary|ky-btn-primary/;

function migrateTextWhite(content) {
  return content.replace(/text-white/g, (match, offset) => {
    const start = Math.max(0, offset - 300);
    const end = Math.min(content.length, offset + 80);
    const ctx = content.slice(start, end);
    if (SOLID_BTN_RE.test(ctx)) return match;
    return 'text-primary';
  });
}

const REPLACEMENTS = [
  ['text-muted-foreground', 'text-muted'],
  ['text-gray-400', 'text-muted'],
  ['text-slate-50', 'text-primary'],
  ['placeholder:text-slate-500', 'placeholder:text-muted'],
  ['hover:text-slate-200', 'hover:text-primary'],
  ['hover:text-slate-300', 'hover:text-primary'],
  ['text-slate-700', 'text-muted'],
  ['text-slate-600', 'text-muted'],
  ['text-slate-500', 'text-muted'],
  ['text-slate-400', 'text-muted'],
  ['text-slate-300', 'text-muted'],
  ['text-slate-200', 'text-primary'],
  ['text-slate-100', 'text-primary'],
  ['border-white/20', ''],
  ['border-white/15', ''],
  ['border-white/10', ''],
  ['divide-navy-800/70', 'divide-default'],
  ['divide-navy-800/50', 'divide-default'],
  ['divide-navy-800/45', 'divide-default'],
  ['divide-navy-800/40', 'divide-default'],
  ['divide-y divide-navy-800', 'divide-y divide-default'],
  ['divide-navy-800', 'divide-default'],
  ['border-navy-800/80', 'border-default/80'],
  ['border-navy-800/70', 'border-default/70'],
  ['border-navy-800/55', 'border-default/55'],
  ['border-navy-800/50', 'border-default/50'],
  ['border-navy-800/45', 'border-default/45'],
  ['border-navy-800/40', 'border-default/40'],
  ['border-b border-navy-800', 'border-b border-default'],
  ['border-t border-navy-800', 'border-t border-default'],
  ['border-navy-800', 'border-default'],
  ['border-navy-700', 'border-default'],
  ['border-navy-600', 'border-default'],
  ['rounded border-navy-600', 'rounded border-default'],
  ['bg-navy-950/80', 'bg-input/80'],
  ['bg-navy-950/60', 'bg-input/60'],
  ['bg-navy-950/55', 'bg-input/55'],
  ['bg-navy-950/50', 'bg-input/50'],
  ['bg-navy-950/40', 'bg-input/40'],
  ['bg-navy-950/25', 'bg-input/25'],
  ['bg-navy-950', 'bg-input'],
  ['bg-navy-900/60', 'bg-card/60'],
  ['bg-navy-900/50', 'bg-card/50'],
  ['bg-navy-900/45', 'bg-card/45'],
  ['hover:bg-navy-800/60', 'hover:bg-input/60'],
  ['hover:bg-navy-800/50', 'hover:bg-input/50'],
  ['hover:bg-navy-800/45', 'hover:bg-input/45'],
  ['hover:bg-navy-800/40', 'hover:bg-input/40'],
  ['hover:bg-navy-800', 'hover:bg-input'],
  ['hover:bg-navy-700', 'hover:bg-input'],
  ['bg-navy-900', 'bg-card'],
  ['bg-navy-800', 'bg-input'],
  ['bg-slate-900', 'bg-card'],
  ['[class.bg-navy-900]', '[class.bg-card]'],
  ['[class.text-slate-300]', '[class.text-muted]'],
  ['bg-slate-500/15 text-slate-300', 'bg-slate-500/15 text-muted'],
  // inputs that got bg-card should use bg-input
  ['border border-default bg-card px-3 py-2', 'border border-default bg-input px-3 py-2'],
  ['border border-default bg-card pl-8', 'border border-default bg-input pl-8'],
  ['border border-default bg-card px-2', 'border border-default bg-input px-2'],
];

const STYLE_HEX_REPLACEMENTS = [
  [/var\(--text-muted,\s*#[0-9a-fA-F]+\)/g, 'var(--text-muted)'],
  [/var\(--text-primary,\s*#[0-9a-fA-F]+\)/g, 'var(--text-primary)'],
  [/var\(--bg-card,\s*#[0-9a-fA-F]+\)/g, 'var(--bg-card)'],
  [/var\(--bg-input,\s*#[0-9a-fA-F]+\)/g, 'var(--bg-input)'],
  [/var\(--border-default,\s*#[0-9a-fA-F]+\)/g, 'var(--border-color)'],
  [/border-radius:\s*0\.875rem/g, 'border-radius: var(--radius-card)'],
  [/border-radius:\s*0\.5rem/g, 'border-radius: var(--radius-md)'],
  [/border-radius:\s*9999px/g, 'border-radius: var(--radius-pill)'],
  [/border-radius:\s*999px/g, 'border-radius: var(--radius-pill)'],
];

const changed = [];
const report = { changed: [], remaining: 0, textWhite: {} };

for (const file of walk(PRIME_DIR)) {
  let content = fs.readFileSync(file, 'utf8');
  const original = content;

  for (const [from, to] of REPLACEMENTS) {
    content = content.split(from).join(to);
  }

  content = migrateTextWhite(content);

  for (const [re, rep] of STYLE_HEX_REPLACEMENTS) {
    content = content.replace(re, rep);
  }

  content = content.replace(/class="([^"]*)"/g, (_, cls) => {
    const cleaned = cls.replace(/\s{2,}/g, ' ').trim();
    return `class="${cleaned}"`;
  });

  if (content !== original) {
    fs.writeFileSync(file, content, 'utf8');
    changed.push(path.relative(PRIME_DIR, file));
  }

  const tw = (content.match(/text-white/g) || []).length;
  if (tw > 0) {
    report.textWhite[path.relative(PRIME_DIR, file)] = tw;
  }
}

// Count remaining legacy patterns
let remaining = 0;
for (const file of walk(PRIME_DIR)) {
  const content = fs.readFileSync(file, 'utf8');
  const matches = content.match(/text-slate-|bg-navy-|border-navy-|border-white\//g);
  if (matches) remaining += matches.length;
}

report.changed = changed;
report.remaining = remaining;

fs.writeFileSync('MIGRATION_AGENT_REPORT.txt', JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
