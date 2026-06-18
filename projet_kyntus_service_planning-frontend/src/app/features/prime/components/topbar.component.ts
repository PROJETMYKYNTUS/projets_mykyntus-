import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { I18nService } from '../state/i18n.service';
import { LucideIconComponent } from '@/shared/lucide-icon.component';
import { Search } from 'lucide';

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <header class="h-16 glass flex items-center justify-between px-6 z-10 sticky top-0">
      <div class="flex items-center gap-4 flex-1">
        <div class="relative w-64 hidden md:block">
          <span class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
            <app-lucide-icon [icon]="icons.search" className="w-4 h-4" />
          </span>
          <input
            type="text"
            [placeholder]="i18n.t('topbar.search.placeholder')"
            class="w-full pl-9 pr-4 py-2 bg-navy-900/50 border border-navy-800 rounded-full text-sm text-slate-300 focus:outline-none focus:ring-1 focus:ring-blue-500/50 focus:border-blue-500/50 transition-all placeholder:text-slate-600"
          />
        </div>
      </div>
    </header>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TopbarComponent {
  readonly i18n = inject(I18nService);
  readonly icons = { search: Search };
}
