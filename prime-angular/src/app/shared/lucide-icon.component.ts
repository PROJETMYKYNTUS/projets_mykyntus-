import type { IconNode } from 'lucide';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnChanges,
  inject,
} from '@angular/core';

function stringifyAttrs(attrs: Record<string, unknown>): string {
  return Object.entries(attrs)
    .filter(([k]) => k !== 'key')
    .map(([k, v]) => `${k}="${String(v ?? '').replace(/"/g, '&quot;')}"`)
    .join(' ');
}

function iconNodeToSvgString(nodes: IconNode, className: string): string {
  const body = nodes
    .map(([tag, attrs]) => {
      const a = stringifyAttrs(attrs as Record<string, unknown>);
      return `<${tag} ${a}></${tag}>`;
    })
    .join('');
  const cls = className.replace(/"/g, '');
  return `<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="${cls}">${body}</svg>`;
}

@Component({
  selector: 'app-lucide-icon',
  standalone: true,
  template: `<span class="inline-flex shrink-0" [innerHTML]="svgHtml"></span>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LucideIconComponent implements OnChanges {
  private readonly sanitizer = inject(DomSanitizer);

  @Input({ required: true }) icon!: IconNode;
  @Input() className = '';

  svgHtml: SafeHtml | null = null;

  ngOnChanges(): void {
    this.svgHtml = this.sanitizer.bypassSecurityTrustHtml(
      iconNodeToSvgString(this.icon, this.className),
    );
  }
}
