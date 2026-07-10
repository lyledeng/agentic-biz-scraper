/** Request payload for the execute-script endpoint. */
export interface ExecuteScriptRequest {
  definition: string;
  parameters?: Record<string, unknown>;
}

/** Response from the execute-script endpoint. */
export interface ExecuteScriptResponse {
  definition: string;
  correlationId: string;
  truncated: boolean;
  /** Number of search results returned. Present only for business-search definitions. */
  resultCount?: number;
  data: unknown;
}
