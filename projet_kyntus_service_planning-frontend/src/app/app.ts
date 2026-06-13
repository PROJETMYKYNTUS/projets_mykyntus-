import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { KyntusConfirmHostComponent } from './shared/components/kyntus-confirm/kyntus-confirm-host.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CommonModule, KyntusConfirmHostComponent],
  template: `
    <router-outlet></router-outlet>
    <app-kyntus-confirm-host />
  `
})
export class App {}