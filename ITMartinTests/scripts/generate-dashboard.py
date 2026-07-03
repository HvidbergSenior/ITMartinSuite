#!/usr/bin/env python3
"""
Reads NUnit XML + k6 JSON → writes TestResults/dashboard/index.html
Called by the GitHub Actions workflow after all tests finish.
"""

import xml.etree.ElementTree as ET
import json, os
from datetime import datetime

def parse_nunit(path):
    if not os.path.exists(path):
        return []
    root = ET.parse(path).getroot()
    cases = []
    def walk(node):
        for child in node:
            if child.tag == 'test-case':
                msg_el = child.find('.//message')
                cases.append({
                    'name':     child.get('name', ''),
                    'result':   child.get('result', 'Unknown'),
                    'duration': float(child.get('duration') or 0),
                    'message':  (msg_el.text or '') if msg_el is not None else '',
                })
            else:
                walk(child)
    walk(root)
    return cases

def k6_summary():
    path = 'TestResults/k6-summary.json'
    if not os.path.exists(path):
        return None
    try:
        d = json.load(open(path))
        m = d.get('metrics', {})
        return {
            'p95':  round(m.get('http_req_duration', {}).get('values', {}).get('p(95)', 0)),
            'fail': round(m.get('http_req_failed',   {}).get('values', {}).get('rate',  0) * 100, 1),
            'reqs': int(  m.get('http_reqs',          {}).get('values', {}).get('count', 0)),
        }
    except Exception:
        return None

smoke = parse_nunit('TestResults/smoke-results.xml')
flows = parse_nunit('TestResults/flow-results.xml')
k6    = k6_summary()
now   = datetime.now().strftime('%d/%m/%Y %H:%M')

def badge(r):
    if r == 'Passed':               return '<span class="badge pass">✓ OK</span>'
    if r == 'Failed':               return '<span class="badge fail">✗ FAIL</span>'
    if r in ('Skipped', 'Ignored'): return '<span class="badge skip">– OFFLINE</span>'
    return f'<span class="badge skip">? {r}</span>'

def rows(tests):
    out = []
    for t in tests:
        name = t['name'].replace('App_Loads(','').rstrip(')')
        msg  = (t['message'] or '')[:150].replace('<','&lt;')
        dur  = ('%.2f s' % t['duration']) if t['duration'] > 0 else '—'
        out.append(f"""<tr class="{t['result'].lower()}">
      <td>{name}</td><td>{badge(t['result'])}</td><td>{dur}</td>
      <td class="msg">{'<small>'+msg+'</small>' if msg else ''}</td></tr>""")
    return '\n'.join(out)

fail_s  = sum(1 for t in smoke if t['result'] == 'Failed')
skip_s  = sum(1 for t in smoke if t['result'] in ('Skipped','Ignored'))
fail_f  = sum(1 for t in flows if t['result'] == 'Failed')
total   = fail_s + fail_f
status  = 'pass' if total == 0 else 'fail'
txt     = '✓ Alle tests OK' if total == 0 else f'✗ {total} test(s) fejlede'

k6_html = ''
if k6:
    color = '#86EFAC' if k6['p95'] < 3000 and k6['fail'] < 5 else '#FCA5A5'
    k6_html = f"""
  <h2>Load Test — 10 samtidige brugere i 30 sek</h2>
  <div class="k6">
    <div class="metric"><div class="val" style="color:{color}">{k6['p95']} ms</div><div class="lbl">P95 svartid</div></div>
    <div class="metric"><div class="val">{k6['fail']} %</div><div class="lbl">Fejlrate</div></div>
    <div class="metric"><div class="val">{k6['reqs']}</div><div class="lbl">Requests total</div></div>
  </div>"""

html = f"""<!DOCTYPE html>
<html lang="da">
<head>
  <meta charset="utf-8"/><meta name="viewport" content="width=device-width,initial-scale=1"/>
  <title>ITMartin Test Dashboard</title>
  <style>
    body{{font-family:system-ui,sans-serif;background:#0F1117;color:#E5E7EB;margin:0;padding:16px}}
    h1{{font-size:20px;margin-bottom:4px}}
    h2{{font-size:13px;margin:24px 0 8px;color:#6B7280;text-transform:uppercase;letter-spacing:.08em}}
    .ts{{font-size:13px;color:#6B7280;margin-bottom:16px}}
    .overall{{display:inline-block;padding:6px 16px;border-radius:6px;font-weight:700;font-size:15px;margin-bottom:20px}}
    .overall.pass{{background:#14532D;color:#86EFAC}}.overall.fail{{background:#7F1D1D;color:#FCA5A5}}
    table{{width:100%;border-collapse:collapse;font-size:13px}}
    th{{text-align:left;color:#6B7280;padding:5px 8px;border-bottom:1px solid #1F2937}}
    td{{padding:5px 8px;border-bottom:1px solid #1F2937}}
    tr.failed td:first-child{{color:#FCA5A5}}
    tr.skipped td,tr.ignored td{{opacity:.45}}
    .badge{{padding:2px 7px;border-radius:4px;font-size:11px;font-weight:600}}
    .badge.pass{{background:#14532D;color:#86EFAC}}.badge.fail{{background:#7F1D1D;color:#FCA5A5}}
    .badge.skip{{background:#292524;color:#78716C}}
    .msg{{max-width:260px;word-break:break-word}}
    .k6{{background:#161825;border-radius:8px;padding:12px 16px;display:flex;gap:32px;margin-top:8px}}
    .k6 .val{{font-size:22px;font-weight:700}}.k6 .lbl{{font-size:11px;color:#6B7280}}
  </style>
</head>
<body>
  <h1>ITMartin · Test Dashboard</h1>
  <div class="ts">Seneste kørsel: {now}</div>
  <div class="overall {status}">{txt}</div>

  <h2>Smoke · alle apps ({len(smoke)-skip_s} kørte &nbsp;·&nbsp; {skip_s} offline &nbsp;·&nbsp; {fail_s} fejl)</h2>
  <table><tr><th>App</th><th>Status</th><th>Tid</th><th>Besked</th></tr>
  {rows(smoke)}</table>

  <h2>Flow · bruger-flows ({len(flows)} tests &nbsp;·&nbsp; {fail_f} fejl)</h2>
  <table><tr><th>Test</th><th>Status</th><th>Tid</th><th>Besked</th></tr>
  {rows(flows)}</table>
  {k6_html}
</body></html>"""

os.makedirs('TestResults/dashboard', exist_ok=True)
with open('TestResults/dashboard/index.html', 'w', encoding='utf-8') as f:
    f.write(html)
print(f'Dashboard: {len(smoke)} smoke, {len(flows)} flow, k6={"yes" if k6 else "no"}')
