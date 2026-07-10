import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { DocumentProxyService } from './document-proxy.service';
import { ApiConfigService } from './api-config.service';

describe('DocumentProxyService', () => {
  let service: DocumentProxyService;
  let httpMock: HttpTestingController;
  const baseUrl = 'https://api.example.com';

  beforeEach(() => {
    const apiConfigStub = { baseUrl: signal(baseUrl) };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        DocumentProxyService,
        { provide: ApiConfigService, useValue: apiConfigStub },
      ],
    });

    service = TestBed.inject(DocumentProxyService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should fetch a document as a Blob', () => {
    const fakeBlob = new Blob(['%PDF-1.4'], { type: 'application/pdf' });
    let result: Blob | undefined;

    service.fetchDocument('/api/v1/documents/test.pdf').subscribe((b) => (result = b));

    const req = httpMock.expectOne(`${baseUrl}/api/v1/documents/test.pdf`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(fakeBlob);

    expect(result).toBeDefined();
    expect(result!.size).toBeGreaterThan(0);
  });

  it('should propagate a 401 error', () => {
    let error: { status: number } | undefined;

    service.fetchDocument('/api/v1/documents/secure.pdf').subscribe({
      error: (e) => (error = e),
    });

    httpMock
      .expectOne(`${baseUrl}/api/v1/documents/secure.pdf`)
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(error).toBeDefined();
    expect(error!.status).toBe(401);
  });

  it('should propagate a 404 error', () => {
    let error: { status: number } | undefined;

    service.fetchDocument('/api/v1/documents/missing.pdf').subscribe({
      error: (e) => (error = e),
    });

    httpMock
      .expectOne(`${baseUrl}/api/v1/documents/missing.pdf`)
      .flush(null, { status: 404, statusText: 'Not Found' });

    expect(error).toBeDefined();
    expect(error!.status).toBe(404);
  });

  it('should propagate a 502 error', () => {
    let error: { status: number } | undefined;

    service.fetchDocument('/api/v1/documents/timeout.pdf').subscribe({
      error: (e) => (error = e),
    });

    httpMock
      .expectOne(`${baseUrl}/api/v1/documents/timeout.pdf`)
      .flush(null, { status: 502, statusText: 'Bad Gateway' });

    expect(error).toBeDefined();
    expect(error!.status).toBe(502);
  });

  it('should propagate a 504 error', () => {
    let error: { status: number } | undefined;

    service.fetchDocument('/api/v1/documents/gateway.pdf').subscribe({
      error: (e) => (error = e),
    });

    httpMock
      .expectOne(`${baseUrl}/api/v1/documents/gateway.pdf`)
      .flush(null, { status: 504, statusText: 'Gateway Timeout' });

    expect(error).toBeDefined();
    expect(error!.status).toBe(504);
  });
});
