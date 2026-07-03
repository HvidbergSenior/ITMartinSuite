/**
 * k6 load test — ITMartinSuite
 *
 * Simulates 10 concurrent users spread across the most-used apps.
 * Thresholds: 95th-percentile response < 3 seconds, error rate < 5 %.
 *
 * Run locally:  k6 run ITMartinTests/k6/load-concurrent.js
 * Full report:  k6 run --out json=TestResults/k6.json ITMartinTests/k6/load-concurrent.js
 */

import http  from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  scenarios: {
    load: {
      executor:  'constant-vus',
      vus:       10,
      duration:  '30s',
    },
  },
  thresholds: {
    // 95th-percentile response time under 3 000 ms
    http_req_duration: ['p(95)<3000'],
    // Less than 5 % of requests fail
    http_req_failed:   ['rate<0.05'],
  },
};

// Apps to load-test (always-on + the most important manual ones)
const APPS = [
  { name: 'Poll',           url: __ENV.POLL_URL           || 'https://stem.itmartin.dk'            },
  { name: 'DailyBrief',    url: __ENV.DAILYBRIEF_URL     || 'https://nyheder.itmartin.dk'         },
  { name: 'Gallery',       url: __ENV.GALLERY_URL         || 'https://gallery.itmartin.dk'         },
  { name: 'LibrarySearch', url: __ENV.LIBRARY_SEARCH_URL  || 'https://search-books.itmartin.dk'    },
  { name: 'Receipt',       url: __ENV.RECEIPT_URL         || 'https://kvittering.itmartin.dk'      },
  { name: 'Club',          url: __ENV.CLUB_URL            || 'https://lions-club.itmartin.dk'      },
  { name: 'Musik',         url: __ENV.MUSIK_URL           || 'https://musik.itmartin.dk'           },
];

export default function () {
  const app = APPS[Math.floor(Math.random() * APPS.length)];

  const res = http.get(app.url, {
    tags:    { app: app.name },
    headers: { 'User-Agent': 'k6-itmartin-loadtest/1.0' },
    timeout: '10s',
  });

  check(res, {
    'status 200':          r => r.status === 200,
    'has blazor content':  r => r.body.includes('blazor') || r.body.includes('<body'),
    'under 3 s':           r => r.timings.duration < 3000,
  });

  sleep(1);
}

export function handleSummary(data) {
  const p95  = data.metrics.http_req_duration?.values?.['p(95)'];
  const fail = data.metrics.http_req_failed?.values?.rate;
  const reqs = data.metrics.http_reqs?.values?.count;

  return {
    stdout: `\n=== Load Test Summary ===\n` +
            `  Requests:      ${reqs}\n` +
            `  p95 latency:   ${p95?.toFixed(0)} ms\n` +
            `  Error rate:    ${(fail * 100)?.toFixed(1)} %\n` +
            `  Status:        ${(p95 < 3000 && fail < 0.05) ? 'PASS ✓' : 'FAIL ✗'}\n`,
    'TestResults/k6-summary.json': JSON.stringify(data, null, 2),
  };
}
