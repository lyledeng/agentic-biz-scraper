import { Component, signal, inject, NgZone, OnInit, OnDestroy } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { JumpstartComponentsModule } from '@wk/components-v3-angular17';
import { finalize } from 'rxjs';
import { UnifiedEntityDetailResponse, UnifiedSearchResult } from '../../shared/models/unified-entity.model';
import { ExecuteScriptService } from '../../core/services/execute-script.service';
import { DocumentProxyService } from '../../core/services/document-proxy.service';
import { DocumentViewerState } from '../../shared/models/document-viewer.model';
import { LoadingIndicatorComponent } from '../../shared/components/loading-indicator/loading-indicator.component';
import { ErrorBannerComponent } from '../../shared/components/error-banner/error-banner.component';

const STATE_DETAIL_SLUG: Record<string, string> = {
  CO: 'us-co-entity-details',
  WY: 'us-wy-entity-details',
  IA: 'us-ia-entity-details',
  DE: 'de-de-entity-details',
  MO: 'us-mo-entity-details',
  WA: 'us-wa-entity-details',
};

@Component({
  selector: 'app-entity-details',
  standalone: true,
  imports: [JumpstartComponentsModule, LoadingIndicatorComponent, ErrorBannerComponent],
  templateUrl: './entity-details.component.html',
  styleUrl: './entity-details.component.css'
})
/** Entity detail viewer showing registration info, agent, parties, and document downloads. */
export class EntityDetailsComponent implements OnInit, OnDestroy {
  private readonly executeScriptService = inject(ExecuteScriptService);
  private readonly documentProxyService = inject(DocumentProxyService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly router = inject(Router);
  private readonly ngZone = inject(NgZone);

  loading = signal(true);
  detail = signal<UnifiedEntityDetailResponse | null>(null);
  error = signal<string | null>(null);

  /** Per-document viewer states keyed by a unique identifier. */
  viewerStates = signal<Map<string, DocumentViewerState>>(new Map());
  /** Key of the currently active viewer (single-viewer-at-a-time). */
  activeViewerKey = signal<string | null>(null);
  /** Label shown in the side-panel header for the active document. */
  activeViewerLabel = signal<string>('Document');
  /** ProxyUrl of the active document — kept for retry from the panel. */
  activeViewerProxyUrl = signal<string | null>(null);

  private readonly activeBlobUrls: string[] = [];
  private previousResults: UnifiedSearchResult[] = [];
  private readonly navState: Record<string, unknown>;

  constructor() {
    // Read navigation state in the constructor — getCurrentNavigation() is only
    // available before the navigation finishes (i.e. during construction).
    const nav = this.router.getCurrentNavigation();
    const navExtrasState = nav?.extras?.state as Record<string, unknown> | undefined;
    const browserState = history.state as Record<string, unknown> | undefined;

    // Prefer router extras if it contains our expected key, otherwise fall back to history.state
    this.navState = navExtrasState?.['uniqueKey'] ? navExtrasState : (browserState ?? {});
  }

  ngOnInit(): void {
    const uniqueKey = this.navState['uniqueKey'] as string | undefined;
    const entityState = this.navState['state'] as string | undefined;
    this.previousResults = (this.navState['results'] as UnifiedSearchResult[]) ?? [];

    if (!uniqueKey || !entityState) {
      this.error.set('No entity selected. Please go back to search.');
      this.loading.set(false);
      return;
    }

    const stateKey = entityState.length === 2 ? entityState : entityState.slice(-2);
    const slug = STATE_DETAIL_SLUG[stateKey];
    if (!slug) {
      this.error.set(`Unsupported state: ${entityState}`);
      this.loading.set(false);
      return;
    }

    this.executeScriptService.execute({
      definition: slug,
      parameters: { uniqueKey }
    }).pipe(
      finalize(() => this.ngZone.run(() => this.loading.set(false)))
    ).subscribe({
      next: (response) => {
        this.ngZone.run(() => this.detail.set(response.data as UnifiedEntityDetailResponse));
      },
      error: (err) => {
        this.ngZone.run(() => {
          if (err.status === 503) {
            const retryAfterHeader = err.headers?.get?.('Retry-After');
            const retryAfterSuffix = retryAfterHeader ? ` Retry after ${retryAfterHeader} seconds.` : '';
            this.error.set(`Service is currently busy.${retryAfterSuffix}`.trim());
          } else {
            this.error.set(err.message || 'Failed to load entity details');
          }
        });
      }
    });
  }

  onBackToResults(): void {
    this.router.navigate(['/search'], {
      state: { results: this.previousResults }
    });
  }

  onNewSearch(): void {
    this.router.navigate(['/search']);
  }

  /** Handle close requests from the side-modal (Escape key / overlay click). */
  onPanelCloseRequest(): void {
    const key = this.activeViewerKey();
    if (key) this.closeViewer(key);
  }

  /** Open (or re-open) the side-panel PDF viewer for a given document. */
  viewDocument(proxyUrl: string, key: string, label?: string): void {
    // Single-viewer-at-a-time: close any previously active viewer.
    const currentKey = this.activeViewerKey();
    if (currentKey && currentKey !== key) {
      this.closeViewer(currentKey);
    }

    this.updateViewerState(key, { key, status: 'loading', blobUrl: null, errorMessage: null });
    this.activeViewerKey.set(key);
    this.activeViewerLabel.set(label ?? 'Document');
    this.activeViewerProxyUrl.set(proxyUrl);

    this.documentProxyService.fetchDocument(proxyUrl).subscribe({
      next: (blob) => {
        this.ngZone.run(() => {
          // Ensure the blob has the correct PDF MIME type so the browser
          // renders it inline instead of triggering a download.
          const pdfBlob = blob.type === 'application/pdf'
            ? blob
            : new Blob([blob], { type: 'application/pdf' });
          const blobUrl = URL.createObjectURL(pdfBlob);
          this.activeBlobUrls.push(blobUrl);
          this.updateViewerState(key, { key, status: 'loaded', blobUrl, errorMessage: null });
        });
      },
      error: (err) => {
        this.ngZone.run(() => {
          this.updateViewerState(key, {
            key,
            status: 'error',
            blobUrl: null,
            errorMessage: this.mapErrorMessage(err),
          });
        });
      },
    });
  }

  /** Retry a failed document load (uses stored proxyUrl and label). */
  retryDocument(): void {
    const key = this.activeViewerKey();
    const proxyUrl = this.activeViewerProxyUrl();
    if (key && proxyUrl) {
      this.viewDocument(proxyUrl, key, this.activeViewerLabel());
    }
  }

  /** Close the inline viewer for a document and revoke its blob URL. */
  closeViewer(key: string): void {
    const states = new Map(this.viewerStates());
    const state = states.get(key);
    if (state?.blobUrl) {
      URL.revokeObjectURL(state.blobUrl);
      const idx = this.activeBlobUrls.indexOf(state.blobUrl);
      if (idx !== -1) this.activeBlobUrls.splice(idx, 1);
    }
    states.delete(key);
    this.viewerStates.set(states);
    if (this.activeViewerKey() === key) {
      this.activeViewerKey.set(null);
      this.activeViewerProxyUrl.set(null);
    }
  }

  ngOnDestroy(): void {
    for (const url of this.activeBlobUrls) {
      URL.revokeObjectURL(url);
    }
    this.activeBlobUrls.length = 0;
  }

  /** Sanitize a blob URL so Angular allows it in iframe [src] bindings. */
  getSafeBlobUrl(blobUrl: string): SafeResourceUrl {
    return this.sanitizer.bypassSecurityTrustResourceUrl(blobUrl);
  }

  private updateViewerState(key: string, state: DocumentViewerState): void {
    const states = new Map(this.viewerStates());
    states.set(key, state);
    this.viewerStates.set(states);
  }

  private mapErrorMessage(err: unknown): string {
    const status = (err as { status?: number })?.status;
    switch (status) {
      case 401:
      case 403:
        return 'Authentication expired. Please sign in again.';
      case 404:
        return 'Document not found.';
      case 502:
      case 504:
        return 'Document storage is temporarily unavailable. Please try again.';
      default:
        return 'Failed to load document.';
    }
  }
}
