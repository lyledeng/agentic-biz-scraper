/** Production environment configuration (assembly). */
export const environment = {
  production: true,
  apiBaseUrl: '/mvpoc/bizscraper-api',
  msalConfig: {
    auth: {
      clientId: '7c184001-2736-4579-b715-210b5a7cc75a',
      authority: 'https://login.microsoftonline.com/8ac76c91-e7f1-41ff-a89c-3553b2da2c17',
      redirectUri: 'https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui',
      postLogoutRedirectUri: 'https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui'
    },
    scopes: ['api://3480eb5f-eca7-4a9c-81d2-5f65c1f77ceb/access_as_user']
  }
};
