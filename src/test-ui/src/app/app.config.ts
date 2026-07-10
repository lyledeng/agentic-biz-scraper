import { ApplicationConfig, ErrorHandler, Injectable, importProvidersFrom } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';
import { Title } from '@angular/platform-browser';
import { MsalModule, MsalGuard, MsalInterceptor, MsalRedirectComponent, MSAL_INSTANCE, MSAL_GUARD_CONFIG, MSAL_INTERCEPTOR_CONFIG } from '@azure/msal-angular';

import { routes } from './app.routes';
import { msalInstance, msalGuardConfig, msalInterceptorConfig } from './core/auth/auth.config';

@Injectable()
class GlobalErrorHandler implements ErrorHandler {
  handleError(error: unknown): void {
    console.error('[BizScraper UI] Unhandled error:', error);
  }
}

/** Application-level Angular configuration with routing, HTTP, MSAL authentication, and error handling providers. */
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptorsFromDi()),
    importProvidersFrom(MsalModule.forRoot(msalInstance, msalGuardConfig, msalInterceptorConfig)),
    { provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true },
    MsalGuard,
    MsalRedirectComponent,
    Title,
    { provide: ErrorHandler, useClass: GlobalErrorHandler }
  ]
};
