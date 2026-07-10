import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { JumpstartComponentsModule } from '@wk/components-v3-angular17';
import { MsalService, MsalBroadcastService } from '@azure/msal-angular';
import { InteractionStatus, AccountInfo } from '@azure/msal-browser';
import { Subject, filter, takeUntil } from 'rxjs';
import { ApiConfigBarComponent } from './shared/components/api-config-bar/api-config-bar.component';
import { environment } from '../environments/environment.development';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, JumpstartComponentsModule, ApiConfigBarComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
/** Root application shell with navigation, authentication display, and API config bar. */
export class AppComponent implements OnInit, OnDestroy {
  displayName: string | null = null;

  private readonly destroying$ = new Subject<void>();

  constructor(
    private readonly msalService: MsalService,
    private readonly msalBroadcastService: MsalBroadcastService
  ) {}

  ngOnInit(): void {
    this.msalBroadcastService.inProgress$
      .pipe(
        filter((status: InteractionStatus) => status === InteractionStatus.None),
        takeUntil(this.destroying$)
      )
      .subscribe(() => {
        this.setActiveAccount();
      });
  }

  ngOnDestroy(): void {
    this.destroying$.next();
    this.destroying$.complete();
  }

  signOut(): void {
    this.msalService.logoutRedirect({
      postLogoutRedirectUri: environment.msalConfig.auth.postLogoutRedirectUri
    });
  }

  private setActiveAccount(): void {
    const activeAccount = this.msalService.instance.getActiveAccount();
    if (!activeAccount) {
      const accounts = this.msalService.instance.getAllAccounts();
      if (accounts.length > 0) {
        this.msalService.instance.setActiveAccount(accounts[0]);
      }
    }
    const account: AccountInfo | null = this.msalService.instance.getActiveAccount();
    this.displayName = account?.name ?? account?.username ?? null;
  }
}
