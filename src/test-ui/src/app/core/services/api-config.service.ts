import { Injectable, signal, WritableSignal } from '@angular/core';
import { environment } from '../../../environments/environment.development';

const STORAGE_KEY = 'bizscraper.apiBaseUrl';
const DEFAULT_URL = environment.apiBaseUrl;

function createStorageSignal(key: string, defaultValue: string): WritableSignal<string> {
  const sig = signal(localStorage.getItem(key) ?? defaultValue);
  const originalSet = sig.set.bind(sig);
  const originalUpdate = sig.update.bind(sig);
  sig.set = (value: string) => {
    originalSet(value);
    localStorage.setItem(key, value);
  };
  sig.update = (fn: (value: string) => string) => {
    originalUpdate((current) => {
      const next = fn(current);
      localStorage.setItem(key, next);
      return next;
    });
  };
  return sig;
}

@Injectable({ providedIn: 'root' })
/** Manages the configurable API base URL persisted to localStorage. */
export class ApiConfigService {
  readonly baseUrl: WritableSignal<string> = createStorageSignal(STORAGE_KEY, DEFAULT_URL);
}
