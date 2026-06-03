import { APP_INITIALIZER, ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { provideHttpClient, withInterceptors, withInterceptorsFromDi } from '@angular/common/http';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { DocumentationHttpErrorsInterceptor } from './features/documentation/core/interceptors/documentation-http-errors.interceptor';
import {
  DocumentationIdentityService,
  documentationIdentityInitFactory,
} from './features/documentation/core/services/documentation-identity.service';
import { primeDemoInterceptor } from './features/prime/interceptors/prime-demo.interceptor';
import { parrainageDemoInterceptor } from './features/parrainage/interceptors/parrainage-demo.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(
      withInterceptorsFromDi(),
      withInterceptors([primeDemoInterceptor, parrainageDemoInterceptor]),
    ),
    {
      provide: APP_INITIALIZER,
      useFactory: documentationIdentityInitFactory,
      deps: [DocumentationIdentityService],
      multi: true,
    },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: DocumentationHttpErrorsInterceptor, multi: true },
  ],
};