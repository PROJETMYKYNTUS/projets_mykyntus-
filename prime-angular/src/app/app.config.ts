import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { primeDemoInterceptor } from './prime-demo.interceptor';
import { primeIdentityInterceptor } from './prime-identity.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withInterceptors([primeIdentityInterceptor, primeDemoInterceptor])),
  ],
};
