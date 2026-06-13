import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { DocumentationGatewayDefaultHeaders } from '../models/documentation-gateway-default-headers';
import { DocumentationHeaders } from '../models/documentation-headers';
import { DocumentationIdentityService } from '../services/documentation-identity.service';
import { KyntusSessionService } from '../session/kyntus-session.service';

const DocumentationApiPrefix = '/api/documentation';
const GenerateDocumentAiApiPrefix = '/api/generate-document-ai';

function shouldApplyDocumentationGatewayDefaults(url: string): boolean {
  return url.includes(GenerateDocumentAiApiPrefix) || url.includes(DocumentationApiPrefix);
}

/**
 * En-têtes de repli (tenant / utilisateur / rôle) uniquement pour les appels Documentation,
 * afin de ne pas mélanger avec les autres microservices du planning sous `/api/`.
 */
@Injectable()
export class DocumentationGatewayHeadersInterceptor implements HttpInterceptor {
  private readonly identity = inject(DocumentationIdentityService);
  private readonly session = inject(KyntusSessionService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!shouldApplyDocumentationGatewayDefaults(req.url)) {
      return next.handle(req);
    }

    let headers = req.headers;
    const profileHeaders = this.identity.getHeaderMap();
    for (const [key, value] of Object.entries(profileHeaders)) {
      if (value && !headers.has(key)) {
        headers = headers.set(key, value);
      }
    }
    if (!this.session.isAuthenticated()) {
      for (const [key, value] of Object.entries(this.resolveGatewayDefaultHeaders())) {
        if (!headers.has(key)) {
          headers = headers.set(key, value);
        }
      }
    }

    return next.handle(req.clone({ headers }));
  }

  /** En-têtes de repli : profil RH démo si le rôle stocké indique RH, sinon pilote. */
  private resolveGatewayDefaultHeaders(): Record<string, string> {
    const role = (this.identity.getCurrentRole() || this.session.getRole() || '').trim().toLowerCase();
    if (role === 'rh' || role === 'admin') {
      return {
        ...DocumentationGatewayDefaultHeaders,
        [DocumentationHeaders.userId]: '11111111-1111-4111-8111-111111111102',
        [DocumentationHeaders.userRole]: 'rh',
      };
    }
    return { ...DocumentationGatewayDefaultHeaders };
  }
}
