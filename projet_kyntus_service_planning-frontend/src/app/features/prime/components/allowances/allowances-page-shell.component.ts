import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-allowances-page-shell',
  standalone: true,
  template: `
    <div class="prime-page-shell">
      <div class="flex flex-wrap justify-between items-start gap-4">
        <div>
          <h1 class="prime-page-title">{{ title() }}</h1>
          @if (subtitle()) {
            <p class="prime-page-subtitle">{{ subtitle() }}</p>
          }
        </div>
        <div class="flex flex-wrap gap-2 items-center">
          <ng-content select="[pageActions]" />
        </div>
      </div>

      @if (error()) {
        <div class="ky-alert ky-alert-error">{{ error() }}</div>
      }

      <ng-content />
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllowancesPageShellComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>('');
  readonly error = input<string>('');
}
