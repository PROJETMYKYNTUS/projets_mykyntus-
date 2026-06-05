import { Component, Input } from '@angular/core';

import { AppContextService } from '../../services/app-context.service';
import { DocIconComponent } from '../doc-icon/doc-icon.component';

@Component({
  selector: 'app-documentation-header',
  standalone: true,
  imports: [DocIconComponent],
  templateUrl: './documentation-header.component.html',
})
export class DocumentationHeaderComponent {
  @Input({ required: true }) title!: string;

  constructor(readonly app: AppContextService) {}
}
