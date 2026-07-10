import { Routes } from '@angular/router';
import { MsalGuard, MsalRedirectComponent } from '@azure/msal-angular';
import { SearchComponent } from './features/search/search.component';
import { EntityDetailsComponent } from './features/entity-details/entity-details.component';

/** Application route definitions. */
export const routes: Routes = [
  { path: '', redirectTo: 'search', pathMatch: 'full' },
  { path: 'search', component: SearchComponent, title: 'BizScraper — Search', canActivate: [MsalGuard] },
  { path: 'entity-details', component: EntityDetailsComponent, title: 'BizScraper — Entity Details', canActivate: [MsalGuard] },
  { path: 'auth', component: MsalRedirectComponent }
];
