import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Search } from 'lucide';
import { LucideIconComponent } from '@/shared/lucide-icon.component';

@Component({
  selector: 'app-parrainage-header',
  standalone: true,
  imports: [LucideIconComponent],
  template: `
    <header class="h-16 px-8 flex items-center justify-end bg-app/80 backdrop-blur-md border-b border-default sticky top-0 z-40 transition-colors duration-300">
      <div class="flex items-center gap-6">
        <div class="relative group hidden md:block">
          <app-lucide-icon [icon]="searchIcon" className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted group-focus-within:text-blue-500 transition-colors" />
          <input
            type="text"
            placeholder="Rechercher…"
            class="bg-card/50 border border-default rounded-full py-2 pl-10 pr-4 text-sm text-primary focus:outline-none focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/50 w-64 transition-all placeholder:text-muted shadow-inner"
          />
        </div>
        <div class="flex items-center gap-3 pl-2 group">
          <div class="text-right hidden md:block">
            <p class="text-sm font-bold text-primary leading-none group-hover:text-blue-400 transition-colors">Parrainage</p>
            <p class="text-[10px] text-muted font-medium mt-1">Kyntus</p>
          </div>
          <div class="w-9 h-9 rounded-full bg-gradient-to-tr from-blue-600 to-blue-500 flex items-center justify-center text-white font-bold shadow-[0_0_10px_rgba(37,99,235,0.3)] border border-blue-500/30">P</div>
        </div>
      </div>
    </header>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ParrainageHeaderComponent {
  readonly searchIcon = Search;
}
