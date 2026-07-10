import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-loading-indicator',
  standalone: true,
  template: `
    @if (loading) {
      <div class="loading-overlay" role="status" aria-label="Loading">
        <div class="spinner"></div>
        <span>Loading...</span>
      </div>
    }
  `,
  styles: [`
    .loading-overlay {
      display: flex;
      align-items: center;
      gap: var(--cg3-spacing-half);
      padding: var(--cg3-spacing);
    }
    .spinner {
      width: 1.5rem;
      height: 1.5rem;
      border: 3px solid var(--cg3-color-gray-100);
      border-top-color: var(--cg3-color-blue-500);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `]
})
/** Inline spinner overlay displayed while async operations are in progress. */
export class LoadingIndicatorComponent {
  @Input() loading = false;
}
