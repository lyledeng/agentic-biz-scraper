/** Tracks the loading / display / error state of a single inline PDF viewer. */
export interface DocumentViewerState {
  /** Unique key combining section type, document index, and download label. */
  key: string;
  /** Current fetch status. */
  status: 'idle' | 'loading' | 'loaded' | 'error';
  /** Blob URL created via URL.createObjectURL() — null until loaded. */
  blobUrl: string | null;
  /** User-facing error message — null unless status is 'error'. */
  errorMessage: string | null;
}
