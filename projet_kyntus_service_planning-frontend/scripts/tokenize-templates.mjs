import fs from 'fs';
import path from 'path';

const roots = [
  'src/app/features/prime',
  'src/app/features/parrainage',
];

const replacements = [
  ['text-slate-100', 'text-primary'],
  ['text-slate-200', 'text-primary'],
  ['text-slate-50', 'text-primary'],
  ['text-slate-300', 'text-muted'],
  ['text-slate-400', 'text-muted'],
  ['text-slate-500', 'text-muted'],
  ['placeholder:text-slate-500', 'placeholder:text-muted'],
  ['bg-navy-950', 'bg-input'],
  ['bg-navy-900', 'bg-card'],
  ['border-navy-800', 'border-default'],
  ['border-navy-700', 'border-default'],
  ['border-navy-600', 'border-default'],
  ['divide-navy-800', 'divide-default'],
  ['border-white/10', 'border-default'],
  ['border-white/15', 'border-default'],
  ['border-white/20', 'border-default'],
  ['hover:bg-navy-800/60', 'hover:bg-input'],
  ['hover:bg-navy-800/50', 'hover:bg-input'],
  ['hover:bg-navy-800/45', 'hover:bg-input'],
  ['hover:bg-navy-900/40', 'hover:bg-input'],
  ['border-navy-600/80', 'border-default'],
  ['border-emerald-500/45', 'border-[var(--success-border)]'],
  ['bg-emerald-500/15', 'bg-[var(--success-bg)]'],
  ['text-emerald-100', 'text-[var(--success-text)]'],
  ['border-amber-500/40', 'border-[var(--warning-border)]'],
  ['bg-amber-500/10', 'bg-[var(--warning-bg)]'],
  ['text-amber-100', 'text-[var(--warning-text)]'],
  ['bg-input/50', 'bg-input'],
  ['bg-input/55', 'bg-input'],
  ['bg-input/60', 'bg-input'],
  ['bg-card/50', 'bg-card'],
  ['bg-card/40', 'bg-card'],
  ['bg-card/80', 'bg-card'],
  ['hover:bg-navy-800', 'hover:bg-input'],
];

function walk(dir, acc = []) {
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) walk(p, acc);
    else if (ent.name.endsWith('.ts')) acc.push(p);
  }
  return acc;
}

const base = path.resolve('.');
let changed = 0;
for (const root of roots) {
  const dir = path.join(base, root);
  if (!fs.existsSync(dir)) continue;
  for (const file of walk(dir)) {
    let content = fs.readFileSync(file, 'utf8');
    const orig = content;
    for (const [from, to] of replacements) {
      content = content.split(from).join(to);
    }
    if (content !== orig) {
      fs.writeFileSync(file, content, 'utf8');
      changed++;
      console.log(path.relative(base, file));
    }
  }
}
console.log(`Updated ${changed} files`);
