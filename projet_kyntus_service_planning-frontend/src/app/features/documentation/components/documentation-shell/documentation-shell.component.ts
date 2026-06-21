import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-documentation-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  template: `<div class="ky-page-shell"><router-outlet></router-outlet></div>`,
})
export class DocumentationShellComponent {}
