import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PrimeLayoutComponent } from './prime/components/prime-layout.component';

@Component({
  selector: 'app-root',
  imports: [PrimeLayoutComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  title = 'prime-angular';
}
