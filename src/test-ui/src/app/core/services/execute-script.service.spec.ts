import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ExecuteScriptService } from './execute-script.service';
import { ApiConfigService } from './api-config.service';
import { signal } from '@angular/core';

describe('ExecuteScriptService', () => {
  let service: ExecuteScriptService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        {
          provide: ApiConfigService,
          useValue: { baseUrl: signal('https://localhost:8443') }
        }
      ]
    });
    service = TestBed.inject(ExecuteScriptService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should POST to execute-script and return the response', () => {
    const mockResponse = {
      definition: 'us-wy-business-search',
      correlationId: 'abc-123',
      truncated: false,
      data: [{ name: 'Wendys', identifier: '123', status: 'Active', entityType: 'LLC', formationDate: null, state: 'WY', event: null, uniqueKey: 'WY-123', standingTax: null, standingRA: null, registeredOffice: null }]
    };

    service.execute({ definition: 'us-wy-business-search', parameters: { searchTerm: 'Wendy' } }).subscribe(response => {
      expect(response.definition).toBe('us-wy-business-search');
      expect(response.correlationId).toBe('abc-123');
    });

    const req = httpMock.expectOne('https://localhost:8443/api/v2/execute-script');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ definition: 'us-wy-business-search', parameters: { searchTerm: 'Wendy' } });
    req.flush(mockResponse);
  });

  it('should propagate HTTP errors as Observable errors', () => {
    service.execute({ definition: 'us-co-business-search', parameters: { searchTerm: 'fail' } }).subscribe({
      next: () => fail('expected error'),
      error: (err: { status: number }) => {
        expect(err.status).toBe(500);
      }
    });

    const req = httpMock.expectOne('https://localhost:8443/api/v2/execute-script');
    req.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });
  });

  it('should GET definitions list', () => {
    const mockDefs = [
      { definitionSlug: 'us-co-business-search', name: 'CO Search', description: null, state: 'CO', requiredParameters: [{ name: 'searchTerm', description: null }] }
    ];

    service.listDefinitions().subscribe(defs => {
      expect(defs.length).toBe(1);
      expect(defs[0].definitionSlug).toBe('us-co-business-search');
    });

    const req = httpMock.expectOne('https://localhost:8443/api/v2/definitions');
    expect(req.request.method).toBe('GET');
    req.flush(mockDefs);
  });
});
