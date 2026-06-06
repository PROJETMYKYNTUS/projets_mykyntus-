import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  Input,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { Subscription } from 'rxjs';

import { AppContextService } from '../../services/app-context.service';
import { DocumentationHeaderUiService } from '../../services/documentation-header-ui.service';
import { NotificationDataService } from '../../services/notification-data.service';
import { DocIconComponent } from '../doc-icon/doc-icon.component';
import { DocumentationNotificationFlyoutComponent } from '../documentation-notification-flyout/documentation-notification-flyout.component';
import { DocumentationSettingsFlyoutComponent } from '../documentation-settings-flyout/documentation-settings-flyout.component';

@Component({
  selector: 'app-documentation-header',
  standalone: true,
  imports: [
    DocIconComponent,
    DocumentationNotificationFlyoutComponent,
    DocumentationSettingsFlyoutComponent,
  ],
  templateUrl: './documentation-header.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentationHeaderComponent implements OnInit, OnDestroy {
  @Input({ required: true }) title!: string;

  readonly app = inject(AppContextService);
  readonly ui = inject(DocumentationHeaderUiService);
  private readonly notifData = inject(NotificationDataService);

  private readonly tick = signal(0);
  private sub?: Subscription;

  readonly unreadCount = computed(() => {
    this.tick();
    return this.notifData.unreadCount();
  });

  ngOnInit(): void {
    this.sub = this.notifData.updated$.subscribe(() => this.tick.update((v) => v + 1));
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  toggleNotif(): void {
    this.ui.toggleNotif();
  }

  toggleSettings(): void {
    this.ui.toggleSettings();
  }
}
