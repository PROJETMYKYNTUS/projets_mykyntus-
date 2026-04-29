import { merge, Observable, of } from 'rxjs';
import { switchMap } from 'rxjs/operators';

import { DocumentationIdentityService } from '../../../core/services/documentation-identity.service';

/** RÃ©-exÃ©cute un observable Ã  chaque changement dâ€™utilisateur dev (en-tÃªtes + profil). */
export function switchMapOnDocumentationContext<T>(
  identity: DocumentationIdentityService,
  project: () => Observable<T>,
): Observable<T> {
  return merge(of(null), identity.contextRevision$).pipe(switchMap(() => project()));
}
