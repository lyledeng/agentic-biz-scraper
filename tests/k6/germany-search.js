import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  thresholds: {
    http_req_duration: ['p(95)<60000', 'p(99)<120000'],
    http_req_failed: ['rate<0.10'],
  },
  scenarios: {
    single_result: {
      executor: 'constant-vus',
      vus: 1,
      duration: '30s',
      exec: 'singleResult',
    },
    multi_page: {
      executor: 'constant-vus',
      vus: 1,
      duration: '60s',
      exec: 'multiPage',
    },
    no_results: {
      executor: 'constant-vus',
      vus: 1,
      duration: '15s',
      exec: 'noResults',
    },
    warning: {
      executor: 'constant-vus',
      vus: 1,
      duration: '60s',
      exec: 'warningScenario',
    },
    validation_error: {
      executor: 'constant-vus',
      vus: 1,
      duration: '10s',
      exec: 'validationError',
    },
  },
};

const baseUrl = __ENV.BASE_URL || 'https://localhost:8443';
const tlsOpts = { insecureSkipTLSVerify: true };

// Single-result search
export function singleResult() {
  const response = http.get(
    `${baseUrl}/api/v1/germany-search?name=${encodeURIComponent('Rohde & Schwarz Group Services GmbH')}`,
    tlsOpts
  );
  check(response, {
    'single result status 200': (r) => r.status === 200,
    'single result has results': (r) => {
      const body = JSON.parse(r.body);
      return body.results && body.results.length >= 1;
    },
    'single result totalCount >= 1': (r) => JSON.parse(r.body).totalCount >= 1,
    'single result pagesScraped >= 1': (r) => JSON.parse(r.body).pagesScraped >= 1,
  });
  sleep(2);
}

// Multi-page search (broad term)
export function multiPage() {
  const response = http.get(
    `${baseUrl}/api/v1/germany-search?name=Rohde`,
    tlsOpts
  );
  check(response, {
    'multi page status 200': (r) => r.status === 200,
    'multi page has results': (r) => JSON.parse(r.body).results.length > 0,
    'multi page pagesScraped > 1': (r) => JSON.parse(r.body).pagesScraped > 1,
  });
  sleep(5);
}

// No-results search
export function noResults() {
  const response = http.get(
    `${baseUrl}/api/v1/germany-search?name=xyznonexistentcompany99999`,
    tlsOpts
  );
  check(response, {
    'no results status 200': (r) => r.status === 200,
    'no results empty array': (r) => JSON.parse(r.body).results.length === 0,
    'no results totalCount 0': (r) => JSON.parse(r.body).totalCount === 0,
  });
  sleep(1);
}

// Warning scenario (exceeded-hits)
export function warningScenario() {
  const response = http.get(
    `${baseUrl}/api/v1/germany-search?name=Rohde`,
    tlsOpts
  );
  check(response, {
    'warning status 200': (r) => r.status === 200,
    'warning field populated': (r) => {
      const body = JSON.parse(r.body);
      return body.warning !== null && body.warning.length > 0;
    },
    'warning has 100 results': (r) => JSON.parse(r.body).totalCount === 100,
  });
  sleep(5);
}

// Validation error (empty name)
export function validationError() {
  const response = http.get(
    `${baseUrl}/api/v1/germany-search?name=`,
    tlsOpts
  );
  check(response, {
    'validation error status 400': (r) => r.status === 400,
    'validation error has problem details': (r) => {
      const body = JSON.parse(r.body);
      return body.title === 'Bad Request' && body.status === 400;
    },
  });
  sleep(0.5);
}
