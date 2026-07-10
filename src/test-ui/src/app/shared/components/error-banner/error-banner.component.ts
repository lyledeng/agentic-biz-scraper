import { Component, Input } from '@angular/core';
import { JumpstartComponentsModule } from '@wk/components-v3-angular17';

@Component({
  selector: 'app-error-banner',
  standalone: true,
  imports: [JumpstartComponentsModule],
  template: `
    @if (errorMessage) {
      <notification-pds3 [type]="notificationType" notificationRole="alert">
        <h4 *notificationHeadingPds3>{{ notificationType === 'warning' ? 'Warning' : 'Error' }}</h4>
        <span *notificationContentPds3>{{ errorMessage }}</span>
      </notification-pds3>
    }
  `
})
/** Dismissable notification banner for displaying error or warning messages. */
export class ErrorBannerComponent {
  @Input() errorMessage: string | null = null;
  @Input() notificationType: 'error' | 'warning' = 'error';
}
