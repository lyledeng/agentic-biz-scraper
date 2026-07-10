/** Development environment configuration (localhost). */
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:8443',
  msalConfig: {
    auth: {
      clientId: '7c184001-2736-4579-b715-210b5a7cc75a',
      authority: 'https://login.microsoftonline.com/8ac76c91-e7f1-41ff-a89c-3553b2da2c17',
      redirectUri: 'http://localhost:4200',
      postLogoutRedirectUri: 'http://localhost:4200'
    },
    scopes: ['api://3480eb5f-eca7-4a9c-81d2-5f65c1f77ceb/access_as_user']
  }
};
