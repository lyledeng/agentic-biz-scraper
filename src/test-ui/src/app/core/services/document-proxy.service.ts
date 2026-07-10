import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiConfigService } from './api-config.service';

@Injectable({ providedIn: 'root' })
/** Fetches PDF documents from the document proxy endpoint via HttpClient (MSAL-intercepted). */
export class DocumentProxyService {
  private readonly http = inject(HttpClient);
  private readonly apiConfig = inject(ApiConfigService);

  /** Fetch a PDF document as a Blob. proxyUrl may be relative (/api/v1/documents/...) or absolute. */
  fetchDocument(proxyUrl: string): Observable<Blob> {
    const url = proxyUrl.startsWith('http://') || proxyUrl.startsWith('https://')
      ? proxyUrl
      : `${this.apiConfig.baseUrl()}${proxyUrl}`;
    return this.http.get(url, { responseType: 'blob' });
  }
}
