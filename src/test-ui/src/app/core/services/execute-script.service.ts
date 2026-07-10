import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ExecuteScriptRequest, ExecuteScriptResponse } from '../../shared/models/execute-script.model';
import { DefinitionInfo } from '../../shared/models/definition-info.model';
import { ApiConfigService } from './api-config.service';

@Injectable({ providedIn: 'root' })
/** HTTP client for the execute-script and definitions endpoints. */
export class ExecuteScriptService {
  private readonly http = inject(HttpClient);
  private readonly apiConfig = inject(ApiConfigService);

  execute(request: ExecuteScriptRequest): Observable<ExecuteScriptResponse> {
    const url = `${this.apiConfig.baseUrl()}/api/v2/execute-script`;
    return this.http.post<ExecuteScriptResponse>(url, request);
  }

  listDefinitions(): Observable<DefinitionInfo[]> {
    const url = `${this.apiConfig.baseUrl()}/api/v2/definitions`;
    return this.http.get<DefinitionInfo[]>(url);
  }
}
