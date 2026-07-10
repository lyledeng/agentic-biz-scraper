import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  thresholds: {
    http_req_duration: ['p(95)<30000', 'p(99)<120000'],
    http_req_failed: ['rate<0.05'],
  },
  scenarios: {
    single_page: {
      executor: 'constant-vus',
      vus: 1,
      duration: '30s',
      exec: 'singlePage',
    },
    multi_page: {
      executor: 'constant-vus',
      vus: 1,
      duration: '30s',
      exec: 'multiPage',
    },
    concurrent_rejection: {
      executor: 'constant-vus',
      vus: 2,
      duration: '15s',
      exec: 'busyScenario',
    },
  },
};

const baseUrl = __ENV.BASE_URL || 'https://localhost:8443';
const headers = { 'Content-Type': 'application/json' };

export function singlePage() {
  const payload = JSON.stringify({
    definition: 'us-co-business-search',
    parameters: { searchTerm: 'Acme Construction Colorado' },
  });
  const response = http.post(`${baseUrl}/api/v2/execute-script`, payload, { headers, insecureSkipTLSVerify: true });
  check(response, { 'single page ok': (r) => r.status === 200 });
  sleep(1);
}

export function multiPage() {
  const payload = JSON.stringify({
    definition: 'us-co-business-search',
    parameters: { searchTerm: 'Mountain' },
  });
  const response = http.post(`${baseUrl}/api/v2/execute-script`, payload, { headers, insecureSkipTLSVerify: true });
  check(response, { 'multi page ok': (r) => r.status === 200 || r.status === 422 });
  sleep(1);
}

export function busyScenario() {
  const payload = JSON.stringify({
    definition: 'us-co-business-search',
    parameters: { searchTerm: 'Acme' },
  });
  const response = http.post(`${baseUrl}/api/v2/execute-script`, payload, { headers, insecureSkipTLSVerify: true });
  check(response, { 'busy handled': (r) => [200, 503].includes(r.status) });
}
