import { APP_INITIALIZER, ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { DocumentationGatewayHeadersInterceptor } from './features/documentation/core/interceptors/documentation-gateway-headers.interceptor';
import { DocumentationHttpErrorsInterceptor } from './features/documentation/core/interceptors/documentation-http-errors.interceptor';
import { DocumentationUserContextInterceptor } from './features/documentation/core/interceptors/documentation-user-context.interceptor';
import {
  DocumentationIdentityService,
  documentationIdentityInitFactory,
} from './features/documentation/core/services/documentation-identity.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    {
      provide: APP_INITIALIZER,
      useFactory: documentationIdentityInitFactory,
      deps: [DocumentationIdentityService],
      multi: true,
    },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: DocumentationHttpErrorsInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: DocumentationGatewayHeadersInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: DocumentationUserContextInterceptor, multi: true },
  ],
};