import { ApplicationConfig } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { parrainageDemoInterceptor } from './parrainage-demo.interceptor';
import { parrainageIdentityInterceptor } from './parrainage-identity.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withInterceptors([parrainageIdentityInterceptor, parrainageDemoInterceptor])),
  ],
};
