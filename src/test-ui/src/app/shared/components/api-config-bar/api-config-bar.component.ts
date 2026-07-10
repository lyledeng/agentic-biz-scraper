import { Component, inject } from '@angular/core';
import { JumpstartComponentsModule } from '@wk/components-v3-angular17';
import { ApiConfigService } from '../../../core/services/api-config.service';

@Component({
  selector: 'app-api-config-bar',
  standalone: true,
  imports: [JumpstartComponentsModule],
  template: `
    <text-field-pds3 label="API Base URL" [labelFor]="'api-url-input'" [labelId]="'api-url-label'">
      <input
        [id]="'api-url-input'"
        type="text"
        [value]="apiConfig.baseUrl()"
        (blur)="onUrlChange($event)"
        (keydown.enter)="onUrlChange($event)"
      />
    </text-field-pds3>
  `,
  styles: [`
    :host {
      display: block;
    }
  `]
})
/** Toolbar input for configuring the API base URL at runtime. */
export class ApiConfigBarComponent {
  readonly apiConfig = inject(ApiConfigService);

  onUrlChange(event: Event): void {
    const value = (event.target as HTMLInputElement).value.trim();
    if (value) {
      this.apiConfig.baseUrl.set(value);
    }
  }
}
