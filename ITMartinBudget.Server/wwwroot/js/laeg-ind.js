// Læg ind (/laeg-ind): listens on the document, so it works however the page was opened - Blazor's enhanced
// navigation does not run a <script> inside the page (user 2026-11-18: "i chose csv but nothing happens").
document.addEventListener('change', async function (e) {
    const fileInput = e.target;
    if (!fileInput || fileInput.id !== 'csvFile') return;
    const ledgerInput = document.getElementById('ledgerId');
    const status = document.getElementById('status');
    const show = (kind, html) => status.innerHTML = `<div class="k-alert k-alert-${kind}">${html}</div>`;
    const esc = (t) => String(t).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

    const file = fileInput.files[0];
    // Lowercased here and on the server - "Familie" and "familie" must be the same account.
    const ledgerId = ledgerInput.value.trim().toLowerCase();
    if (!file) return;
    if (!ledgerId) { show('danger', 'Skriv kontoens navn først.'); fileInput.value = ''; return; }
    if (!file.name.toLowerCase().endsWith('.csv')) { show('danger', 'Filen skal være en CSV-fil fra netbanken.'); fileInput.value = ''; return; }

    show('info', 'Lægger ind...');
    const form = new FormData();
    form.append('ledgerId', ledgerId);
    form.append('scopeMode', document.querySelector('input[name="scopeMode"]:checked')?.value || 'Both');
    form.append('file', file);
    try {
        const res = await fetch('/api/shop/upload', { method: 'POST', body: form });
        if (!res.ok) { show('danger', esc(await res.text())); return; }
        const body = await res.json();
        const msg = body.imported > 0
            ? `${body.imported} betalinger er lagt ind på "${esc(ledgerId)}".`
            : 'Der var ingen nye betalinger. Dem, der allerede var lagt ind, er sprunget over.';
        show('ok', `${msg}<div class="k-mt"><a class="k-btn k-btn-primary k-btn-sm" href="/kategorier/${encodeURIComponent(ledgerId)}">Se kategorierne</a></div>`);
    } catch (err) {
        show('danger', 'Det lykkedes ikke at lægge filen ind. Prøv igen.');
    } finally {
        fileInput.value = '';
    }
});
