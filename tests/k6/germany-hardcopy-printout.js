import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  thresholds: {
    http_req_duration: ['p(95)<90000'],
    http_req_failed: ['rate<0.10'],
  },
  scenarios: {
    hardcopy_printout: {
      executor: 'constant-vus',
      vus: 1,
      duration: '30s',
      exec: 'hardcopyPrintout',
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

export function hardcopyPrintout() {
  const payload = JSON.stringify({
    searchTerm: 'Claes und Rohde',
    registrationId: 'Paderborn HRA 3059',
  });

  const response = http.post(
    `${baseUrl}/api/v1/germany-search/current-hardcopy-printout`,
    payload,
    Object.assign({ headers: { 'Content-Type': 'application/json' } }, tlsOpts)
  );

  check(response, {
    'hardcopy status 200': (r) => r.status === 200,
    'hardcopy content-type is PDF': (r) =>
      r.headers['Content-Type'] && r.headers['Content-Type'].includes('application/pdf'),
    'hardcopy has X-Correlation-Id header': (r) =>
      r.headers['X-Correlation-Id'] !== undefined && r.headers['X-Correlation-Id'] !== '',
    'hardcopy has X-Document-Url header': (r) =>
      r.headers['X-Document-Url'] !== undefined && r.headers['X-Document-Url'] !== '',
    'hardcopy has X-Original-Document-Url header': (r) =>
      r.headers['X-Original-Document-Url'] !== undefined && r.headers['X-Original-Document-Url'] !== '',
    'hardcopy has Content-Disposition header': (r) =>
      r.headers['Content-Disposition'] && r.headers['Content-Disposition'].includes('attachment'),
    'hardcopy body is non-empty': (r) => r.body && r.body.length > 0,
    'hardcopy duration < 90s': (r) => r.timings.duration < 90000,
  });

  sleep(5);
}

export function validationError() {
  const payload = JSON.stringify({
    searchTerm: '',
    registrationId: '',
  });

  const response = http.post(
    `${baseUrl}/api/v1/germany-search/current-hardcopy-printout`,
    payload,
    Object.assign({ headers: { 'Content-Type': 'application/json' } }, tlsOpts)
  );

  check(response, {
    'empty fields returns 400': (r) => r.status === 400,
  });

  sleep(1);
}
