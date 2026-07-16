import fs from 'fs';
import path from 'path';

const FE = 'src/app/features';

const templateReplacements = [
  ['text-slate-100', 'text-primary'],
  ['text-slate-200', 'text-primary'],
  ['text-slate-50', 'text-primary'],
  ['text-slate-300', 'text-muted'],
  ['text-slate-400', 'text-muted'],
  ['text-slate-500', 'text-muted'],
  ['text-slate-600', 'text-muted'],
  ['text-slate-700', 'text-muted'],
  ['placeholder:text-slate-500', 'placeholder:text-muted'],
  ['text-gray-400', 'text-muted'],
  ['text-gray-500', 'text-muted'],
  ['text-gray-600', 'text-muted'],
  ['text-muted-foreground', 'text-muted'],
  ['bg-navy-950/80', 'bg-input'],
  ['bg-navy-950/60', 'bg-input'],
  ['bg-navy-950/55', 'bg-input'],
  ['bg-navy-950/50', 'bg-input'],
  ['bg-navy-950/40', 'bg-input'],
  ['bg-navy-950/25', 'bg-input'],
  ['bg-navy-950', 'bg-input'],
  ['bg-navy-900/80', 'bg-card'],
  ['bg-navy-900/60', 'bg-card'],
  ['bg-navy-900/50', 'bg-card'],
  ['bg-navy-900/40', 'bg-card'],
  ['bg-navy-900', 'bg-card'],
  ['border-navy-800/55', 'border-default'],
  ['border-navy-800', 'border-default'],
  ['border-navy-700', 'border-default'],
  ['border-navy-600/80', 'border-default'],
  ['border-navy-600', 'border-default'],
  ['divide-navy-800', 'divide-default'],
  ['border-white/20', 'border-default'],
  ['border-white/15', 'border-default'],
  ['border-white/10', 'border-default'],
  ['hover:bg-navy-800/60', 'hover:bg-input'],
  ['hover:bg-navy-800/50', 'hover:bg-input'],
  ['hover:bg-navy-800/45', 'hover:bg-input'],
  ['hover:bg-navy-900/40', 'hover:bg-input'],
  ['hover:bg-navy-800', 'hover:bg-input'],
  ['border-emerald-500/45', 'border-[var(--success-border)]'],
  ['bg-emerald-500/15', 'bg-[var(--success-bg)]'],
  ['text-emerald-100', 'text-[var(--success-text)]'],
  ['text-emerald-400', 'text-[var(--success-text)]'],
  ['border-amber-500/40', 'border-[var(--warning-border)]'],
  ['bg-amber-500/10', 'bg-[var(--warning-bg)]'],
  ['text-amber-100', 'text-[var(--warning-text)]'],
  ['text-amber-400', 'text-[var(--warning-text)]'],
  ['text-red-400', 'text-[var(--danger-text)]'],
  ['text-indigo-400', 'text-[var(--electric-blue)]'],
  ['text-cyan-400', 'text-[var(--info-text)]'],
  ['bg-input/50', 'bg-input'],
  ['bg-input/55', 'bg-input'],
  ['bg-input/60', 'bg-input'],
  ['bg-card/50', 'bg-card'],
  ['bg-card/40', 'bg-card'],
  ['bg-card/80', 'bg-card'],
];

const cssReplacements = [
  [/font-family:\s*['"]?(Inter|DM Sans|Segoe UI)['"]?[^;]*/gi, 'font-family: var(--font-sans)'],
  [/border-radius:\s*4px/g, 'border-radius: var(--radius-md, 0.5rem)'],
  [/border-radius:\s*6px/g, 'border-radius: var(--radius-sm, 0.375rem)'],
  [/border-radius:\s*8px/g, 'border-radius: var(--radius-md, 0.5rem)'],
  [/border-radius:\s*10px/g, 'border-radius: var(--radius-md, 0.5rem)'],
  [/border-radius:\s*12px/g, 'border-radius: var(--radius-md, 0.5rem)'],
  [/border-radius:\s*14px/g, 'border-radius: var(--radius-card, 0.875rem)'],
  [/border-radius:\s*16px/g, 'border-radius: var(--radius-card, 0.875rem)'],
  [/border-radius:\s*18px/g, 'border-radius: var(--radius-card, 0.875rem)'],
  [/border-radius:\s*20px/g, 'border-radius: var(--radius-card, 0.875rem)'],
  [/border-radius:\s*99px/g, 'border-radius: var(--radius-pill, 999px)'],
  [/border-radius:\s*999px/g, 'border-radius: var(--radius-pill, 999px)'],
  [/#10b981\b/g, 'var(--success)'],
  [/#16a34a\b/g, 'var(--success)'],
  [/#22c55e\b/g, 'var(--success)'],
  [/#059669\b/g, 'var(--success-text)'],
  [/#f59e0b\b/g, 'var(--warning)'],
  [/#f97316\b/g, 'var(--warning)'],
  [/#d97706\b/g, 'var(--warning-text)'],
  [/#ef4444\b/g, 'var(--danger)'],
  [/#dc2626\b/g, 'var(--danger)'],
  [/#f87171\b/g, 'var(--danger-text)'],
  [/#3b82f6\b/g, 'var(--soft-blue)'],
  [/#2563eb\b/g, 'var(--blue-600)'],
  [/#6366f1\b/g, 'var(--electric-blue)'],
  [/#64748b\b/g, 'var(--text-muted)'],
  [/#94a3b8\b/g, 'var(--text-muted)'],
  [/#6b7280\b/g, 'var(--text-muted)'],
  [/#0f172a\b/g, 'var(--navy-950, #0f172a)'],
  [/#f8fafc\b/g, 'var(--bg-input)'],
  [/#f3f4f6\b/g, 'var(--bg-input)'],
  [/#e2e8f0\b/g, 'var(--border-color)'],
  [/#e5e7eb\b/g, 'var(--border-color)'],
  [/#cbd5e1\b/g, 'var(--border-color)'],
];

function walk(dir, exts, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) walk(p, exts, acc);
    else if (exts.some((e) => ent.name.endsWith(e))) acc.push(p);
  }
  return acc;
}

const base = path.resolve('.');
let tsChanged = 0;
let cssChanged = 0;

for (const file of walk(path.join(base, FE), ['.ts'])) {
  let c = fs.readFileSync(file, 'utf8');
  const orig = c;
  for (const [from, to] of templateReplacements) c = c.split(from).join(to);
  if (c !== orig) {
    fs.writeFileSync(file, c, 'utf8');
    tsChanged++;
  }
}

for (const file of walk(path.join(base, FE), ['.css', '.scss'])) {
  let c = fs.readFileSync(file, 'utf8');
  const orig = c;
  for (const [re, to] of cssReplacements) c = c.replace(re, to);
  if (c !== orig) {
    fs.writeFileSync(file, c, 'utf8');
    cssChanged++;
  }
}

console.log(`TS: ${tsChanged}, CSS: ${cssChanged}`);
