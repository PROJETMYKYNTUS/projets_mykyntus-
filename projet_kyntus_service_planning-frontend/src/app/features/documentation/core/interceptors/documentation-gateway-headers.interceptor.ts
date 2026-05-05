import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { DocumentationGatewayDefaultHeaders } from '../constants/documentation-gateway-default-headers';

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
  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!shouldApplyDocumentationGatewayDefaults(req.url)) {
      return next.handle(req);
    }

    let headers = req.headers;
    for (const [key, value] of Object.entries(DocumentationGatewayDefaultHeaders)) {
      if (!headers.has(key)) {
        headers = headers.set(key, value);
      }
    }

    return next.handle(req.clone({ headers }));
  }
}
