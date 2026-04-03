import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div style="display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;gap:1rem">
      <h1 style="font-size:4rem;margin:0">403</h1>
      <p>Vous n'avez pas accès à cette page.</p>
      <a href="http://localhost:4201/login">Retour au login</a>
    </div>
  `
})
export class UnauthorizedComponent {}