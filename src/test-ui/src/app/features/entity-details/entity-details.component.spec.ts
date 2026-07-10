import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EntityDetailsComponent } from './entity-details.component';
import { ExecuteScriptService } from '../../core/services/execute-script.service';
import { DocumentProxyService } from '../../core/services/document-proxy.service';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { UnifiedEntityDetailResponse } from '../../shared/models/unified-entity.model';
import { ExecuteScriptResponse } from '../../shared/models/execute-script.model';

describe('EntityDetailsComponent', () => {
  let component: EntityDetailsComponent;
  let fixture: ComponentFixture<EntityDetailsComponent>;

  const mockDetailWY: UnifiedEntityDetailResponse = {
    details: {
      name: 'Test LLC', identifier: '2020-001', status: 'Active', formationDate: '01/01/2020',
      entityType: 'LLC', jurisdiction: 'WY',
      principalAddress: null, mailingAddress: null, registeredOffice: null,
      periodicReportMonth: null, subStatus: 'Current',
      standingTax: 'Good', standingRA: 'Good', standingOther: 'Good',
      inactiveDate: null, termOfDuration: 'Perpetual', formedIn: 'Wyoming',
      latestAnnualReportYear: '2025', annualReportExempt: 'No', licenseTaxPaid: '$60.00'
    },
    registeredAgent: { name: 'Agent Inc.', streetAddress: '123 Main St', mailingAddress: null },
    certificate: null,
    parties: [],
    documents: [
      { title: '2025 Annual Report', date: '05/20/2025', downloads: [{ label: 'PDF', proxyUrl: 'https://blob.example.com/report.pdf', fileName: 'report.pdf', error: null }] },
      { title: '2024 Failed Report', date: '05/20/2024', downloads: [{ label: 'PDF', proxyUrl: null, fileName: 'report.pdf', error: 'Download failed' }] }
    ]
  };

  const mockResponse: ExecuteScriptResponse = {
    definition: 'us-wy-entity-details',
    correlationId: 'abc-123',
    truncated: false,
    data: mockDetailWY
  };

  beforeEach(async () => {
    const executeServiceSpy = jasmine.createSpyObj('ExecuteScriptService', ['execute']);
    executeServiceSpy.execute.and.returnValue(of(mockResponse));

    const documentProxySpy = jasmine.createSpyObj('DocumentProxyService', ['fetchDocument']);

    window.history.pushState({ uniqueKey: 'WY-2020-001', state: 'WY', results: [] }, '', '');

    await TestBed.configureTestingModule({
      imports: [EntityDetailsComponent],
      providers: [
        { provide: ExecuteScriptService, useValue: executeServiceSpy },
        { provide: DocumentProxyService, useValue: documentProxySpy },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigate', 'getCurrentNavigation']) }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(EntityDetailsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should render WY-specific fields only when non-null', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Good'); // standingTax
    expect(el.textContent).toContain('Perpetual'); // termOfDuration
    expect(el.textContent).toContain('Current'); // subStatus
  });

  it('should not render certificate section when certificate is null', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).not.toContain('Certificate of Good Standing');
  });

  it('should render document view button when proxyUrl exists', () => {
    const buttons = fixture.nativeElement.querySelectorAll('button-field-pds3[type="secondary"] button');
    expect(buttons.length).toBeGreaterThan(0);
    expect(buttons[0].textContent.trim()).toContain('PDF');
  });

  it('should render error text for documents with errors and no view button', () => {
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Download failed');
    const viewButtons: Element[] = Array.from(fixture.nativeElement.querySelectorAll('button-field-pds3[type="secondary"] button'));
    const failedButtons = viewButtons.filter(b => b.textContent?.includes('2024'));
    expect(failedButtons.length).toBe(0);
  });

  it('should show "Not available" when certificate.available is false', () => {
    component.detail.set({
      ...mockDetailWY,
      certificate: { available: false, downloads: null, error: null }
    });
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Not available');
  });

  it('should call Iowa detail definition when state is IA', () => {
    const executeService = TestBed.inject(ExecuteScriptService) as jasmine.SpyObj<ExecuteScriptService>;
    window.history.pushState({ uniqueKey: 'IA-457975', state: 'IA', results: [] }, '', '');

    const iaFixture = TestBed.createComponent(EntityDetailsComponent);
    iaFixture.detectChanges();

    const latestCall = executeService.execute.calls.mostRecent();
    expect(latestCall).toBeDefined();
    expect(latestCall.args[0].definition).toBe('us-ia-entity-details');
  });

  describe('inline document viewer', () => {
    const proxyUrl = 'https://blob.example.com/report.pdf';

    it('should set loading state when viewDocument is called', () => {
      const docProxy = TestBed.inject(DocumentProxyService) as jasmine.SpyObj<DocumentProxyService>;
      docProxy.fetchDocument.and.returnValue(of(new Blob(['%PDF'], { type: 'application/pdf' })));

      component.viewDocument(proxyUrl, proxyUrl);

      const state = component.viewerStates().get(proxyUrl);
      expect(state).toBeDefined();
      expect(state!.status).toBe('loaded');
      expect(state!.blobUrl).toBeTruthy();
    });

    it('should set error state with categorised message on 401', () => {
      const docProxy = TestBed.inject(DocumentProxyService) as jasmine.SpyObj<DocumentProxyService>;
      docProxy.fetchDocument.and.returnValue(throwError(() => ({ status: 401 })));

      component.viewDocument(proxyUrl, proxyUrl);

      const state = component.viewerStates().get(proxyUrl);
      expect(state).toBeDefined();
      expect(state!.status).toBe('error');
      expect(state!.errorMessage).toContain('Authentication expired');
    });

    it('should set error state with "not found" on 404', () => {
      const docProxy = TestBed.inject(DocumentProxyService) as jasmine.SpyObj<DocumentProxyService>;
      docProxy.fetchDocument.and.returnValue(throwError(() => ({ status: 404 })));

      component.viewDocument(proxyUrl, proxyUrl);

      const state = component.viewerStates().get(proxyUrl);
      expect(state!.status).toBe('error');
      expect(state!.errorMessage).toContain('not found');
    });

    it('should set error state with "unavailable" on 502', () => {
      const docProxy = TestBed.inject(DocumentProxyService) as jasmine.SpyObj<DocumentProxyService>;
      docProxy.fetchDocument.and.returnValue(throwError(() => ({ status: 502 })));

      component.viewDocument(proxyUrl, proxyUrl);

      const state = component.viewerStates().get(proxyUrl);
      expect(state!.status).toBe('error');
      expect(state!.errorMessage).toContain('unavailable');
    });

    it('should re-fetch when retryDocument is called', () => {
      const docProxy = TestBed.inject(DocumentProxyService) as jasmine.SpyObj<DocumentProxyService>;
      docProxy.fetchDocument.and.returnValue(throwError(() => ({ status: 500 })));
      component.viewDocument(proxyUrl, proxyUrl);

      docProxy.fetchDocument.and.returnValue(of(new Blob(['%PDF'], { type: 'application/pdf' })));
      component.retryDocument();

      expect(docProxy.fetchDocument).toHaveBeenCalledTimes(2);
      const state = component.viewerStates().get(proxyUrl);
      expect(state!.status).toBe('loaded');
    });

    it('should close previous viewer when a new one is opened (single-viewer-at-a-time)', () => {
      const docProxy = TestBed.inject(DocumentProxyService) as jasmine.SpyObj<DocumentProxyService>;
      docProxy.fetchDocument.and.returnValue(of(new Blob(['%PDF'], { type: 'application/pdf' })));

      component.viewDocument('/api/v1/documents/a.pdf', 'key-a');
      expect(component.activeViewerKey()).toBe('key-a');
      expect(component.viewerStates().has('key-a')).toBeTrue();

      component.viewDocument('/api/v1/documents/b.pdf', 'key-b');
      expect(component.activeViewerKey()).toBe('key-b');
      expect(component.viewerStates().has('key-a')).toBeFalse();
    });

    it('should revoke all blob URLs on ngOnDestroy', () => {
      const revokeObjectURLSpy = spyOn(URL, 'revokeObjectURL');
      const docProxy = TestBed.inject(DocumentProxyService) as jasmine.SpyObj<DocumentProxyService>;
      docProxy.fetchDocument.and.returnValue(of(new Blob(['%PDF'], { type: 'application/pdf' })));

      component.viewDocument('/api/v1/documents/a.pdf', 'key-a');
      component.viewDocument('/api/v1/documents/b.pdf', 'key-b');

      component.ngOnDestroy();

      // revokeObjectURL called once for the close of key-a (single-viewer) + once in ngOnDestroy for key-b's blob
      expect(revokeObjectURLSpy).toHaveBeenCalled();
    });

    it('should remove viewer state when closeViewer is called', () => {
      const docProxy = TestBed.inject(DocumentProxyService) as jasmine.SpyObj<DocumentProxyService>;
      docProxy.fetchDocument.and.returnValue(of(new Blob(['%PDF'], { type: 'application/pdf' })));

      component.viewDocument(proxyUrl, proxyUrl);
      expect(component.viewerStates().has(proxyUrl)).toBeTrue();

      component.closeViewer(proxyUrl);
      expect(component.viewerStates().has(proxyUrl)).toBeFalse();
      expect(component.activeViewerKey()).toBeNull();
    });
  });
});
