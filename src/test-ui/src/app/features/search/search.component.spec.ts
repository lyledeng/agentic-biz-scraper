import { ComponentFixture, TestBed, fakeAsync, tick, flush } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { SearchComponent } from './search.component';
import { ExecuteScriptService } from '../../core/services/execute-script.service';
import { Router } from '@angular/router';

describe('SearchComponent', () => {
  let component: SearchComponent;
  let fixture: ComponentFixture<SearchComponent>;
  let executeService: jasmine.SpyObj<ExecuteScriptService>;

  beforeEach(async () => {
    executeService = jasmine.createSpyObj('ExecuteScriptService', ['execute']);
    executeService.execute.and.returnValue(throwError(() => ({
      status: 503,
      headers: { get: () => '30' }
    })));

    await TestBed.configureTestingModule({
      imports: [SearchComponent],
      providers: [
        { provide: ExecuteScriptService, useValue: executeService },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate']) }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SearchComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should include IA definition slug mapping', () => {
    component.onSearch('Acme', 'IA');

    const request = executeService.execute.calls.mostRecent().args[0];
    expect(request.definition).toBe('us-ia-business-search');
    expect(request.parameters).toEqual({ searchTerm: 'Acme' });
  });

  it('should show retry guidance when service returns 503 with Retry-After', () => {
    component.onSearch('Acme', 'IA');
    fixture.detectChanges();

    expect(component.error()).toContain('Retry after 30 seconds');
  });

  it('should show initial prompt when no search performed', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const emptyState = compiled.querySelector('.empty-state');
    expect(emptyState).toBeTruthy();
    expect(emptyState?.textContent).toContain('Enter a business name above');
  });

  it('should show no-records-found notification after empty search', fakeAsync(() => {
    executeService.execute.and.returnValue(of({
      definition: 'us-co-business-search',
      correlationId: 'test',
      truncated: false,
      resultCount: 0,
      data: []
    }));

    component.onSearch('ZZZZNONEXISTENT', 'CO');
    tick();
    fixture.detectChanges();

    // Verify component state captures the search context
    expect(component.hasSearched()).toBeTrue();
    expect(component.lastSearchTerm).toBe('ZZZZNONEXISTENT');
    expect(component.lastSearchState).toBe('CO');
    expect(component.results().length).toBe(0);

    // Verify notification is rendered and initial prompt is gone
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('notification-pds3[type="info"]')).toBeTruthy();
    expect(compiled.querySelector('.empty-state')).toBeFalsy();
    flush();
  }));

  it('should show no-records-found notification when data is null', fakeAsync(() => {
    executeService.execute.and.returnValue(of({
      definition: 'us-co-business-search',
      correlationId: 'test',
      truncated: false,
      data: null
    }));

    component.onSearch('NullTest', 'WY');
    tick();
    fixture.detectChanges();

    expect(component.hasSearched()).toBeTrue();
    expect(component.lastSearchTerm).toBe('NullTest');
    expect(component.lastSearchState).toBe('WY');
    expect(component.results().length).toBe(0);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('notification-pds3[type="info"]')).toBeTruthy();
    flush();
  }));

  it('should show truncation warning when truncated with zero results', fakeAsync(() => {
    executeService.execute.and.returnValue(of({
      definition: 'us-co-business-search',
      correlationId: 'test',
      truncated: true,
      resultCount: 0,
      data: []
    }));

    component.onSearch('BroadSearch', 'DE');
    tick();
    fixture.detectChanges();

    expect(component.warning()).toContain('incomplete results');
    const compiled = fixture.nativeElement as HTMLElement;
    const notification = compiled.querySelector('notification-pds3[type="info"]');
    expect(notification).toBeFalsy();
    flush();
  }));

  it('should replace no-records with results table on successful search', fakeAsync(() => {
    // First: empty search
    executeService.execute.and.returnValue(of({
      definition: 'us-co-business-search',
      correlationId: 'test1',
      truncated: false,
      resultCount: 0,
      data: []
    }));
    component.onSearch('Empty', 'CO');
    tick();
    fixture.detectChanges();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('notification-pds3[type="info"]')).toBeTruthy();
    expect(compiled.querySelector('.results-section')).toBeFalsy();
    flush();

    // Second: populated search
    executeService.execute.and.returnValue(of({
      definition: 'us-co-business-search',
      correlationId: 'test2',
      truncated: false,
      resultCount: 1,
      data: [{
        name: 'Acme Corp', identifier: '123', status: 'Active',
        entityType: 'LLC', formationDate: '01/01/2020', state: 'CO',
        uniqueKey: 'abc', event: null
      }]
    }));
    component.onSearch('Acme', 'CO');
    tick();
    fixture.detectChanges();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.results-section')).toBeTruthy();
    expect(compiled.querySelector('notification-pds3[type="info"]')).toBeFalsy();
    flush();
  }));
});
