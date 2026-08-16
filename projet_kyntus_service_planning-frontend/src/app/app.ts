import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { KyntusConfirmHostComponent } from './shared/components/kyntus-confirm/kyntus-confirm-host.component';
import { KyntusPromptHostComponent } from './shared/components/kyntus-prompt/kyntus-prompt-host.component';
import { KyntusToastHostComponent } from './shared/components/ui/kyntus-toast-host.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    CommonModule,
    KyntusConfirmHostComponent,
    KyntusPromptHostComponent,
    KyntusToastHostComponent,
  ],
  template: `
    <router-outlet></router-outlet>
    <app-kyntus-confirm-host />
    <app-kyntus-prompt-host />
    <app-kyntus-toast-host />
  `,
})
export class App {}
