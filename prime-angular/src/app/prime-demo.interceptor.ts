import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { isPrimeDemoMockEnabled } from './prime/mock-data/prime-demo-config';
import { isPrimeDemoEmptyPayload, resolvePrimeDemoGet } from './prime/mock-data/prime-demo-resolver';

function shouldDemo(url: string): boolean {
  return url.includes('/api/prime') || url.includes('/api/rp');
}

function mockFor(url: string, method: string): unknown | undefined {
  return resolvePrimeDemoGet(url, method);
}

function withMockBody<T>(url: string, method: string, body: T): T {
  if (!isPrimeDemoMockEnabled() || !shouldDemo(url) || method !== 'GET') return body;
  const mock = mockFor(url, method);
  if (mock === undefined) return body;
  if (isPrimeDemoEmptyPayload(body)) return mock as T;
  return body;
}

/** Repli données démo marocaines si l’API PRIME est vide ou indisponible (soutenance). */
export const primeDemoInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isPrimeDemoMockEnabled() || !shouldDemo(req.url) || req.method !== 'GET') {
    return next(req);
  }

  const mock = mockFor(req.url, req.method);
  if (mock === undefined) return next(req);

  return next(req).pipe(
    map((event) => {
      if (event instanceof HttpResponse && event.body !== undefined) {
        const nextBody = withMockBody(req.url, req.method, event.body);
        if (nextBody !== event.body) {
          return event.clone({ body: nextBody });
        }
      }
      return event;
    }),
    catchError((err) => {
      if (mock !== undefined) {
        return of(new HttpResponse({ body: mock, status: 200, url: req.url }));
      }
      return throwError(() => err);
    }),
  );
};
