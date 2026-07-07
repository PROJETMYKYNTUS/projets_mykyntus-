import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';

import { DocumentationEntryService } from '../services/documentation-entry.service';
import { NotificationDataService } from '../services/notification-data.service';

/** Aligne le rôle documentation avant le rendu des pages du module. */
export const documentationEntryGuard: CanActivateFn = () => {
  const entry = inject(DocumentationEntryService);
  entry.syncNavRoleFromSession();
  entry.primeProfileOnce();
  inject(NotificationDataService).ensureLoaded();
  return true;
};
