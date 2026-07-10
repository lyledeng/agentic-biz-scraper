import { MsalGuardConfiguration, MsalInterceptorConfiguration } from '@azure/msal-angular';
import { BrowserCacheLocation, InteractionType, PublicClientApplication } from '@azure/msal-browser';
import { environment } from '../../../environments/environment.development';

/** Singleton MSAL PublicClientApplication instance configured from environment. */
export const msalInstance = new PublicClientApplication({
  auth: {
    clientId: environment.msalConfig.auth.clientId,
    authority: environment.msalConfig.auth.authority,
    redirectUri: environment.msalConfig.auth.redirectUri,
    postLogoutRedirectUri: environment.msalConfig.auth.postLogoutRedirectUri
  },
  cache: {
    cacheLocation: BrowserCacheLocation.SessionStorage
  }
});

/** MsalGuard configuration — uses redirect for login interaction. */
export const msalGuardConfig: MsalGuardConfiguration = {
  interactionType: InteractionType.Redirect,
  authRequest: {
    scopes: environment.msalConfig.scopes
  }
};

/** MsalInterceptor configuration — maps API base URL to required scopes. */
export const msalInterceptorConfig: MsalInterceptorConfiguration = {
  interactionType: InteractionType.Redirect,
  protectedResourceMap: new Map<string, string[]>([
    [`${environment.apiBaseUrl}/*`, environment.msalConfig.scopes]
  ])
};
