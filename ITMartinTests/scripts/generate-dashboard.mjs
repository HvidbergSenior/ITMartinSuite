#!/usr/bin/env node
/**
 * Reads NUnit XML + k6 JSON output → writes TestResults/dashboard/index.html
 * Called by the GitHub Actions workflow after tests finish.
 */

import { readFileSync, mkdirSync, writeFileSync, existsSync } from 'fs';
import { parseString } from 'xml2js';
import { promisify } from 'util';

const parseXml = promisify(parseString);
mkdirSync('TestResults/dashboard', { recursive: true });

async function parseNUnit(path) {
  if (!existsSync(path)) return [];
  const xml = readFileSync(path, 'utf8');
  const doc = await parseXml(xml);
  const cases = [];
  function walk(node) {
    if (!node) return;
    (node['test-case'] || []).forEach(tc => {
      const attrs = tc.$ || {};
      cases.push({
        name:     attrs.name    || '',
        result:   attrs.result  || 'Unknown',
        duration: parseFloat(attrs.duration || '0'),
        message:  tc.failure?.[0]?.message?.[0] || tc.reason?.[0]?.message?.[0] || '',
      });
    });
    ['test-suite', 'test-run'].forEach(k => (node[k] || []).forEach(walk));
  }
  walk(doc['test-run'] || doc);
  return cases;
}

function k6Summary() {
  const path = 'TestResults/k6-summary.json';
  if (!existsSync(path)) return null;
  try {
    const d = JSON.parse(readFileSync(path, 'utf8'));
    return {
      p95:  d.metrics?.http_req_duration?.values?.['p(95)']?.toFixed(0),
      fail: (d.metrics?.http_req_failed?.values?.rate * 100)?.toFixed(1),
      reqs: d.metrics?.http_reqs?.values?.count,
    };
  } catch { return null; }
}

const smoke = await parseNUnit('TestResults/smoke-results.xml');
const flows = await parseNUnit('TestResults/flow-results.xml');
const k6    = k6Summary();
const now   = new Date().toLocaleString('da-DK', { timeZone: 'Europe/Copenhagen' });

function badge(result) {
  if (result === 'Passed')     return '<span class="badge pass">✓ OK</span>';
  if (result === 'Failed')     return '<span class="badge fail">✗ FAIL</span>';
  if (result === 'Skipped' ||
      result === 'Ignored')    return '<span class="badge skip">– OFFLINE</span>';
  return                              '<span class="badge skip">? ' + result + '</span>';
}

function rows(tests) {
  return tests.map(t => `
    <tr class="${t.result.toLowerCase()}">
      <td>${t.name.replace(/App_Loads\((.+)\)/, '$1').replace(/_/g, ' ')}</td>
      <td>${badge(t.result)}</td>
      <td>${t.duration > 0 ? t.duration.toFixed(2) + ' s' : '—'}</td>
      <td class="msg">${t.message ? `<small>${t.message.substring(0, 120)}</small>` : ''}</td>
    </tr>`).join('');
}

const totalSmoke = smoke.length;
const failSmoke  = smoke.filter(t => t.result === 'Failed').length;
const skipSmoke  = smoke.filter(t => t.result === 'Skipped' || t.result === 'Ignored').length;
const failFlows  = flows.filter(t => t.result === 'Failed').length;

const overall = (failSmoke + failFlows) === 0 ? 'pass' : 'fail';

const html = `<!DOCTYPE html>
<html lang="da">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width,initial-scale=1"/>
  <title>ITMartin Test Dashboard</title>
  <style>
    body { font-family: system-ui, sans-serif; background: #0F1117; color: #E5E7EB; margin: 0; padding: 16px; }
    h1   { font-size: 20px; margin-bottom: 4px; }
    h2   { font-size: 15px; margin: 24px 0 8px; color: #9CA3AF; text-transform: uppercase; letter-spacing: .05em; }
    .ts  { font-size: 13px; color: #6B7280; margin-bottom: 20px; }
    .overall { display: inline-block; padding: 6px 16px; border-radius: 6px; font-weight: 700; font-size: 16px; margin-bottom: 20px; }
    .overall.pass { background: #14532D; color: #86EFAC; }
    .overall.fail { background: #7F1D1D; color: #FCA5A5; }
    table { width: 100%; border-collapse: collapse; font-size: 13px; }
    th    { text-align: left; color: #6B7280; padding: 6px 8px; border-bottom: 1px solid #1F2937; }
    td    { padding: 6px 8px; border-bottom: 1px solid #1F2937; }
    tr.failed  td:first-child { color: #FCA5A5; }
    tr.skipped td, tr.ignored td { opacity: .5; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge.pass { background: #14532D; color: #86EFAC; }
    .badge.fail { background: #7F1D1D; color: #FCA5A5; }
    .badge.skip { background: #292524; color: #78716C; }
    .msg  { max-width: 300px; word-break: break-word; }
    .k6   { background: #161825; border-radius: 8px; padding: 12px 16px; display: flex; gap: 24px; }
    .k6 .metric { text-align: center; }
    .k6 .val { font-size: 24px; font-weight: 700; }
    .k6 .lbl { font-size: 11px; color: #6B7280; }
  </style>
</head>
<body>
  <h1>ITMartin Test Dashboard</h1>
  <div class="ts">Seneste kørsel: ${now}</div>

  <div class="overall ${overall}">${overall === 'pass' ? '✓ Alle tests OK' : `✗ ${failSmoke + failFlows} test(s) fejlede`}</div>

  <h2>Smoke Tests — alle apps (${totalSmoke - skipSmoke} kørte, ${skipSmoke} offline, ${failSmoke} fejl)</h2>
  <table>
    <tr><th>App</th><th>Status</th><th>Tid</th><th>Besked</th></tr>
    ${rows(smoke)}
  </table>

  <h2>Flow Tests — bruger-flows (${flows.length} tests, ${failFlows} fejl)</h2>
  <table>
    <tr><th>Test</th><th>Status</th><th>Tid</th><th>Besked</th></tr>
    ${rows(flows)}
  </table>

  ${k6 ? `
  <h2>Load Test — 10 samtidige brugere, 30 sek</h2>
  <div class="k6">
    <div class="metric"><div class="val">${k6.p95} ms</div><div class="lbl">P95 svartid</div></div>
    <div class="metric"><div class="val">${k6.fail} %</div><div class="lbl">Fejlrate</div></div>
    <div class="metric"><div class="val">${k6.reqs}</div><div class="lbl">Requests</div></div>
  </div>` : ''}

</body>
</html>`;

writeFileSync('TestResults/dashboard/index.html', html);
console.log('Dashboard written to TestResults/dashboard/index.html');
