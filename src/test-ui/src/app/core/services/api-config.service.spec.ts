import { TestBed } from '@angular/core/testing';
import { ApiConfigService } from './api-config.service';

describe('ApiConfigService', () => {
  let service: ApiConfigService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(ApiConfigService);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('should default baseUrl to https://localhost:8443 when localStorage is empty', () => {
    expect(service.baseUrl()).toBe('https://localhost:8443');
  });

  it('should write to localStorage when baseUrl signal is updated', () => {
    service.baseUrl.set('https://dev.example.com');
    expect(localStorage.getItem('bizscraper.apiBaseUrl')).toBe('https://dev.example.com');
  });

  it('should restore saved URL from localStorage on initialization', () => {
    TestBed.resetTestingModule();
    localStorage.setItem('bizscraper.apiBaseUrl', 'https://staging.example.com');
    TestBed.configureTestingModule({});
    const freshService = TestBed.inject(ApiConfigService);
    expect(freshService.baseUrl()).toBe('https://staging.example.com');
  });
});
