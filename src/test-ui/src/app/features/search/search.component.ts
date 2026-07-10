import { Component, signal, inject, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, switchMap, catchError, of, EMPTY, map } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { JumpstartComponentsModule } from '@wk/components-v3-angular17';
import { UnifiedSearchResult } from '../../shared/models/unified-entity.model';
import { ExecuteScriptService } from '../../core/services/execute-script.service';
import { LoadingIndicatorComponent } from '../../shared/components/loading-indicator/loading-indicator.component';
import { ErrorBannerComponent } from '../../shared/components/error-banner/error-banner.component';

type StateCode = 'CO' | 'WY' | 'IA' | 'DE' | 'MO' | 'WA';

const STATE_SLUG_MAP: Record<StateCode, string> = {
  CO: 'us-co-business-search',
  WY: 'us-wy-business-search',
  IA: 'us-ia-business-search',
  DE: 'de-de-business-search',
  MO: 'us-mo-business-search',
  WA: 'us-wa-business-search',
};

const STATE_DISPLAY_LABEL: Record<string, string> = {
  CO: 'Colorado (US)',
  WY: 'Wyoming (US)',
  IA: 'Iowa (US)',
  DE: 'Germany (DE)',
  MO: 'Missouri (US)',
  WA: 'Washington (US)',
};

@Component({
  selector: 'app-search',
  standalone: true,
  imports: [JumpstartComponentsModule, LoadingIndicatorComponent, ErrorBannerComponent],
  templateUrl: './search.component.html',
  styleUrl: './search.component.css'
})
/** Multi-state business entity search page with debounced input and result grid. */
export class SearchComponent implements OnInit, OnDestroy {
  private readonly executeScriptService = inject(ExecuteScriptService);
  private readonly router = inject(Router);
  private readonly searchTrigger$ = new Subject<{ term: string; state: StateCode }>();
  private readonly destroy$ = new Subject<void>();

  loading = signal(false);
  results = signal<UnifiedSearchResult[]>([]);
  error = signal<string | null>(null);
  warning = signal<string | null>(null);
  hasSearched = signal(false);

  searchTerm = '';
  selectedState: StateCode = 'CO';
  lastSearchTerm = '';
  lastSearchState: StateCode = 'CO';

  ngOnInit(): void {
    const state = history.state;
    if (state?.results) {
      this.results.set(state.results);
    }

    this.searchTrigger$.pipe(
      switchMap(({ term, state }) => {
        this.loading.set(true);
        this.error.set(null);
        this.warning.set(null);
        this.results.set([]);
        return this.executeScriptService.execute({
          definition: STATE_SLUG_MAP[state],
          parameters: { searchTerm: term }
        }).pipe(
          map(response => {
            const mapped = (response.data as UnifiedSearchResult[]) ?? [];
            if (response.truncated && mapped.length === 0) {
              this.warning.set('Search may have incomplete results. Please try again or refine your search.');
              this.loading.set(false);
              return null;
            }
            return mapped;
          }),
          catchError((err) => {
            const retryAfterHeader = err.headers?.get?.('Retry-After');
            const retryAfterSuffix = retryAfterHeader ? ` Retry after ${retryAfterHeader} seconds.` : '';
            if (err.status === 422 && err.error?.type?.includes('exceeded-record-count')) {
              this.warning.set(err.error.detail || 'Your search term is too broad. Please refine your search.');
            } else if (err.status === 503) {
              this.error.set(`Service is currently busy.${retryAfterSuffix}`.trim());
            } else if (err.status === 400 && err.error?.detail) {
              this.error.set(`Validation error: ${err.error.detail}`);
            } else {
              this.error.set(err.message || 'An error occurred while searching');
            }
            this.loading.set(false);
            return EMPTY;
          })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe((data) => {
      if (data !== null) {
        this.results.set(data);
        this.loading.set(false);
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearch(term: string, state: StateCode): void {
    const trimmed = term.trim();
    if (!trimmed) {
      this.error.set('Search term is required');
      return;
    }
    this.hasSearched.set(true);
    this.lastSearchTerm = trimmed;
    this.lastSearchState = state;
    this.searchTrigger$.next({ term: trimmed, state });
  }

  onSelectResult(result: UnifiedSearchResult): void {
    this.router.navigate(['/entity-details'], {
      state: {
        uniqueKey: result.uniqueKey,
        state: result.state,
        results: this.results()
      }
    });
  }

  stateDisplayLabel(state: string): string {
    return STATE_DISPLAY_LABEL[state] ?? state;
  }
}
