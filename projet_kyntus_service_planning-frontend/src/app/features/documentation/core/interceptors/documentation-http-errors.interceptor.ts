import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { DocumentationNotificationService } from '../services/documentation-notification.service';
import { formatDocumentationUxMessage } from '../../shared/utils/documentation-ux-messages';

const DataApiSegment = '/api/documentation/data';
const GenerateDocumentAiSegment = '/api/generate-document-ai';

function isDocumentationHttpUrl(url: string): boolean {
  return url.includes(DataApiSegment) || url.includes(GenerateDocumentAiSegment);
}

/**
 * Messages utilisateur pour les erreurs HTTP du microservice Documentation (sans altérer le corps des erreurs).
 */
@Injectable()
export class DocumentationHttpErrorsInterceptor implements HttpInterceptor {
  constructor(private readonly notify: DocumentationNotificationService) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      tap({
        error: (err: unknown) => {
          if (!(err instanceof HttpErrorResponse)) {
            return;
          }
          if (!isDocumentationHttpUrl(req.url)) {
            return;
          }
          if (err.status === 401) {
            const msg = formatDocumentationUxMessage(
              err,
              'Votre session n’a pas pu être vérifiée. Rechargez la page puis réessayez.',
            );
            this.notify.showError(msg);
            return;
          }
          if (req.url.includes(GenerateDocumentAiSegment)) {
            if (err.status === 400) {
              this.notify.showError(
                formatDocumentationUxMessage(
                  err,
                  'Certaines informations envoyées à la génération IA sont invalides. Vérifiez le modèle et les champs.',
                ),
              );
              return;
            }
            if (err.status === 422) {
              this.notify.showError(
                formatDocumentationUxMessage(
                  err,
                  'Données insuffisantes ou document IA encore incomplet. Complétez les champs puis réessayez.',
                ),
              );
              return;
            }
            if (err.status === 503) {
              this.notify.showError(
                formatDocumentationUxMessage(
                  err,
                  'Le service de génération IA est momentanément indisponible (clé API ou fournisseur). Réessayez plus tard.',
                ),
              );
              return;
            }
          }
        },
      }),
    );
  }
}
