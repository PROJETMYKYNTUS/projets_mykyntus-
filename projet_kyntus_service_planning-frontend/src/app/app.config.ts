import { APP_INITIALIZER, ApplicationConfig } from '@angular/core';
import {
  KyntusThemeService,
  kyntusThemeInitFactory,
} from './core/theme/kyntus-theme.service';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { provideHttpClient, withInterceptors, withInterceptorsFromDi } from '@angular/common/http';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { DocumentationGatewayHeadersInterceptor } from './core/interceptors/documentation-gateway-headers.interceptor';
import { DocumentationHttpErrorsInterceptor } from './core/interceptors/documentation-http-errors.interceptor';
import { DocumentationUserContextInterceptor } from './core/interceptors/documentation-user-context.interceptor';
import {
  DocumentationIdentityService,
  documentationIdentityInitFactory,
} from './core/services/documentation-identity.service';
import { parrainageDemoInterceptor } from './features/parrainage/interceptors/parrainage-demo.interceptor';
import { parrainageIdentityInterceptor } from './features/parrainage/interceptors/parrainage-identity.interceptor';
import { primeDemoInterceptor } from './features/prime/interceptors/prime-demo.interceptor';
import { primeIdentityInterceptor } from './features/prime/interceptors/prime-identity.interceptor';
import {
  KyntusNotificationInitService,
  kyntusNotificationInitFactory,
} from './core/notifications/kyntus-notification-init.service';
import { EMPLOYEE_IMPORT_HOST } from './features/users/pages/employee-import-guided/employee-import-host.context';
import { KyntusSessionService } from './core/session/kyntus-session.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(
      withInterceptorsFromDi(),
      withInterceptors([
        primeIdentityInterceptor,
        parrainageIdentityInterceptor,
        primeDemoInterceptor,
        parrainageDemoInterceptor,
      ]),
    ),
    {
      provide: APP_INITIALIZER,
      useFactory: kyntusThemeInitFactory,
      deps: [KyntusThemeService],
      multi: true,
    },
    {
      provide: APP_INITIALIZER,
      useFactory: kyntusNotificationInitFactory,
      deps: [KyntusNotificationInitService],
      multi: true,
    },
    {
      provide: APP_INITIALIZER,
      useFactory: documentationIdentityInitFactory,
      deps: [DocumentationIdentityService],
      multi: true,
    },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: DocumentationGatewayHeadersInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: DocumentationUserContextInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: DocumentationHttpErrorsInterceptor, multi: true },
    {
      provide: EMPLOYEE_IMPORT_HOST,
      useFactory: (session: KyntusSessionService) => ({
        getRole: () => session.getRole(),
        onImportCompleted: () => undefined,
      }),
      deps: [KyntusSessionService],
    },
  ],
};