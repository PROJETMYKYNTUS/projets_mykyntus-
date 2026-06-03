import { HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { isParrainageDemoMockEnabled } from '../mock-data/parrainage-demo-config';
import { isParrainageDemoEmptyPayload, resolveParrainageDemoGet } from '../mock-data/parrainage-demo-resolver';

function shouldDemo(url: string): boolean {
  return url.includes('/api/parrainage');
}

function mockFor(url: string, method: string): unknown | undefined {
  return resolveParrainageDemoGet(url, method);
}

function withMockBody<T>(url: string, method: string, body: T): T {
  if (!isParrainageDemoMockEnabled() || !shouldDemo(url) || method !== 'GET') return body;
  const mock = mockFor(url, method);
  if (mock === undefined) return body;
  if (isParrainageDemoEmptyPayload(body)) return mock as T;
  return body;
}

export const parrainageDemoInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isParrainageDemoMockEnabled() || !shouldDemo(req.url) || req.method !== 'GET') {
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
