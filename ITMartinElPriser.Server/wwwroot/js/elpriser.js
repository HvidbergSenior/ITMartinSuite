(function () {
    const AREA = 'DK1';
    let selectedHours = 2;
    let priceCache = [];

    const dayNamesDa = ['Søndag', 'Mandag', 'Tirsdag', 'Onsdag', 'Torsdag', 'Fredag', 'Lørdag'];

    function fmtTime(iso) {
        const d = new Date(iso);
        return d.toLocaleTimeString('da-DK', { hour: '2-digit', minute: '2-digit' });
    }

    function fmtDayTime(iso) {
        const d = new Date(iso);
        const today = new Date();
        const isToday = d.toDateString() === today.toDateString();
        const tomorrow = new Date(today); tomorrow.setDate(today.getDate() + 1);
        const isTomorrow = d.toDateString() === tomorrow.toDateString();
        const prefix = isToday ? 'i dag' : isTomorrow ? 'i morgen' : d.toLocaleDateString('da-DK', { weekday: 'long' });
        return prefix + ' kl. ' + fmtTime(iso);
    }

    function showToast(msg) {
        const el = document.getElementById('epToast');
        el.textContent = msg;
        el.classList.add('ep-toast--show');
        setTimeout(() => el.classList.remove('ep-toast--show'), 2200);
    }

    async function loadRecommendation() {
        const hero = document.getElementById('recHero');
        try {
            const res = await fetch(`/api/cheapest-window?hours=${selectedHours}&area=${AREA}`);
            if (!res.ok) {
                hero.innerHTML = '<div class="ep-rec-loading">Ikke nok data endnu — prøv igen om lidt.</div>';
                return;
            }
            const w = await res.json();
            hero.innerHTML = `
                <div class="ep-rec-headline">Start <span class="ep-time">${fmtDayTime(w.start)}</span></div>
                <div class="ep-rec-detail">til ${fmtDayTime(w.end)} · ca. ${w.avgTotalKrPerKwh.toFixed(2)} kr/kWh i alt i gennemsnit${w.isEstimated ? ' (estimeret)' : ''}</div>
            `;
        } catch {
            hero.innerHTML = '<div class="ep-rec-loading">Kunne ikke hente priser lige nu.</div>';
        }
    }

    function priceTier(price, min, max) {
        if (max === min) return 'mid';
        const t = (price - min) / (max - min);
        if (t < 0.34) return 'cheap';
        if (t < 0.67) return 'mid';
        return 'expensive';
    }

    function renderChart(prices) {
        const chart = document.getElementById('priceChart');
        const labels = document.getElementById('chartLabels');
        chart.innerHTML = '';
        labels.innerHTML = '';
        if (prices.length === 0) return;

        const now = new Date();
        const upcoming = prices.filter(p => new Date(p.timeDk) >= new Date(now.getTime() - 3600_000));
        const min = Math.min(...upcoming.map(p => p.totalKrPerKwh));
        const max = Math.max(...upcoming.map(p => p.totalKrPerKwh));
        const maxHeight = 100;

        upcoming.forEach(p => {
            const tier = priceTier(p.totalKrPerKwh, min, max);
            const h = max === min ? maxHeight / 2 : Math.max(4, ((p.totalKrPerKwh - min) / (max - min)) * maxHeight);
            const bar = document.createElement('div');
            const d = new Date(p.timeDk);
            const isNow = Math.abs(d - now) < 3600_000 && d <= now;
            bar.className = `ep-bar ep-bar--${tier}${isNow ? ' ep-bar--now' : ''}`;
            bar.style.height = h + 'px';
            bar.title = `${fmtDayTime(p.timeDk)}: ${p.totalKrPerKwh.toFixed(2)} kr/kWh`;
            chart.appendChild(bar);
        });

        const first = upcoming[0], last = upcoming[upcoming.length - 1];
        labels.innerHTML = `<span>${fmtTime(first.timeDk)}</span><span>${fmtTime(last.timeDk)}</span>`;
    }

    function renderPriceList(prices) {
        const list = document.getElementById('priceList');
        if (prices.length === 0) {
            list.innerHTML = '<div class="ep-price-row"><span class="ep-price-time">Ingen priser at vise endnu.</span></div>';
            return;
        }

        const now = new Date();
        const upcoming = prices.filter(p => new Date(p.timeDk) >= new Date(now.getTime() - 3600_000));
        const min = Math.min(...upcoming.map(p => p.totalKrPerKwh));
        const maxPrice = Math.max(...upcoming.map(p => p.totalKrPerKwh));

        list.innerHTML = upcoming.map(p => {
            const tier = priceTier(p.totalKrPerKwh, min, maxPrice);
            const d = new Date(p.timeDk);
            const isNow = Math.abs(d - now) < 3600_000 && d <= now;
            return `
                <div class="ep-price-row${isNow ? ' ep-price-row--now' : ''}">
                    <span class="ep-price-time">${fmtDayTime(p.timeDk)}${p.isEstimated ? '<span class="ep-price-estimated-tag">estimeret</span>' : ''}</span>
                    <span class="ep-price-value ep-price-value--${tier}">${p.totalKrPerKwh.toFixed(2)} kr/kWh</span>
                </div>
            `;
        }).join('');
    }

    function updateEstimatedNote(prices) {
        const note = document.getElementById('estimatedNote');
        note.style.display = prices.some(p => p.isEstimated) ? 'block' : 'none';
    }

    function renderBreakdown(prices) {
        const el = document.getElementById('priceBreakdown');
        if (!el || prices.length === 0) return;

        const now = new Date();
        const current = prices.find(p => {
            const d = new Date(p.timeDk);
            return Math.abs(d - now) < 3600_000 && d <= now;
        }) || prices[0];

        el.innerHTML = `
            <div class="ep-breakdown-row"><span>Spotpris</span><span>${current.spotKrPerKwh.toFixed(3)} kr/kWh</span></div>
            <div class="ep-breakdown-row"><span>+ Nettarif</span><span>${current.nettarifKrPerKwh.toFixed(3)} kr/kWh</span></div>
            <div class="ep-breakdown-row"><span>+ Elafgift</span><span>${current.elafgiftKrPerKwh.toFixed(3)} kr/kWh</span></div>
            <div class="ep-breakdown-row"><span>+ Leverandørtillæg</span><span>${current.leverandoertillaegKrPerKwh.toFixed(3)} kr/kWh</span></div>
            <div class="ep-breakdown-row"><span>+ Moms (25%)</span><span>${current.momsKrPerKwh.toFixed(3)} kr/kWh</span></div>
            <div class="ep-breakdown-row ep-breakdown-row--total"><span>= I alt lige nu</span><span>${current.totalKrPerKwh.toFixed(2)} kr/kWh</span></div>
        `;
    }

    async function loadPrices() {
        try {
            const res = await fetch(`/api/prices?area=${AREA}`);
            const prices = await res.json();
            priceCache = prices;
            renderChart(prices);
            renderPriceList(prices);
            renderBreakdown(prices);
            updateEstimatedNote(prices);
        } catch {
            // chart just stays empty; recommendation card already reports the failure
        }
    }

    async function loadRuns() {
        try {
            const res = await fetch('/api/runs');
            const data = await res.json();
            document.getElementById('totalRunsWeek').textContent = data.runsThisWeek;
            document.getElementById('totalCostWeek').textContent = data.costThisWeekKr.toFixed(2);
            document.getElementById('totalCostMonth').textContent = data.costThisMonthKr.toFixed(2);

            const list = document.getElementById('runList');
            list.innerHTML = data.recent.map(r => `
                <div class="ep-run-row">
                    <span class="ep-run-device">${r.device}</span>
                    <span class="ep-run-time">${fmtDayTime(r.startedAtDk)}</span>
                    <span class="ep-run-cost">${r.estCostKr.toFixed(2)} kr</span>
                </div>
            `).join('') || '<div class="ep-run-time">Ingen kørsler registreret endnu.</div>';
        } catch {
            // leave totals as-is
        }
    }

    async function logRun(device, kwh) {
        try {
            await fetch('/api/runs', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ device, estKwh: kwh, area: AREA }),
            });
            showToast(`${device} logget ✓`);
            loadRuns();
        } catch {
            showToast('Kunne ikke logge kørslen');
        }
    }

    async function loadWeeklyPattern() {
        try {
            const res = await fetch(`/api/weekly-pattern?area=${AREA}`);
            const data = await res.json();
            const note = document.getElementById('weekNote');
            const grid = document.getElementById('weekGrid');

            if (data.pattern.length === 0) {
                note.textContent = 'Bygger sig op over de næste dage — kom tilbage om lidt.';
                grid.innerHTML = '';
                return;
            }

            note.textContent = `Baseret på ${data.days} ${data.days === 1 ? 'dags' : 'dages'} observerede priser.`;

            const prices = data.pattern.map(p => p.avgPriceKrPerKwh);
            const min = Math.min(...prices), max = Math.max(...prices);

            const byDay = {};
            data.pattern.forEach(p => {
                (byDay[p.day] ??= {})[p.hour] = p.avgPriceKrPerKwh;
            });

            grid.innerHTML = '';
            for (let day = 0; day < 7; day++) {
                const label = document.createElement('div');
                label.className = 'ep-week-daylabel';
                label.textContent = dayNamesDa[day];
                grid.appendChild(label);

                for (let hour = 0; hour < 24; hour++) {
                    const cell = document.createElement('div');
                    cell.className = 'ep-week-cell';
                    const val = byDay[day]?.[hour];
                    if (val === undefined) {
                        cell.style.background = 'var(--bg3)';
                    } else {
                        const tier = priceTier(val, min, max);
                        cell.style.background = `var(--${tier})`;
                        cell.title = `${dayNamesDa[day]} kl. ${hour}: ${val.toFixed(2)} kr/kWh`;
                    }
                    grid.appendChild(cell);
                }
            }
        } catch {
            // leave the "building up" note in place
        }
    }

    // ── Elaftale: netselskab/leverandør presets, bill scan, comparison ──────

    let presetsCache = null;

    async function loadPresets() {
        if (presetsCache) return presetsCache;
        const res = await fetch('/api/settings/presets');
        presetsCache = await res.json();
        return presetsCache;
    }

    function fillSelect(select, items, currentId) {
        select.innerHTML = items.map(i =>
            `<option value="${i.id}"${i.id === currentId ? ' selected' : ''}>${i.name}${i.region ? ' — ' + i.region : ''}</option>`
        ).join('');
    }

    function toggleCustomField(select, input) {
        input.style.display = select.value === 'custom' ? 'block' : 'none';
    }

    async function loadSettingsUi() {
        const [presets, settingsRes] = await Promise.all([loadPresets(), fetch('/api/settings')]);
        const settings = await settingsRes.json();

        const gridSelect = document.getElementById('gridCompanySelect');
        const supplierSelect = document.getElementById('supplierSelect');
        fillSelect(gridSelect, presets.gridCompanies, settings.gridCompanyId);
        fillSelect(supplierSelect, presets.suppliers, settings.supplierId);

        document.getElementById('customNettarif').value = settings.customNettarifOre;
        document.getElementById('customTillaeg').value = settings.customTillaegOre;
        document.getElementById('annualUsage').value = settings.annualUsageKwh;

        toggleCustomField(gridSelect, document.getElementById('customNettarif'));
        toggleCustomField(supplierSelect, document.getElementById('customTillaeg'));

        loadComparison();
    }

    async function saveSettings() {
        const settings = {
            gridCompanyId: document.getElementById('gridCompanySelect').value,
            customNettarifOre: parseFloat(document.getElementById('customNettarif').value) || 0,
            supplierId: document.getElementById('supplierSelect').value,
            customTillaegOre: parseFloat(document.getElementById('customTillaeg').value) || 0,
            annualUsageKwh: parseFloat(document.getElementById('annualUsage').value) || 4000,
        };

        try {
            await fetch('/api/settings', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(settings),
            });
            showToast('Elaftale gemt ✓');
            loadPrices();
            loadRecommendation();
            loadComparison();
        } catch {
            showToast('Kunne ikke gemme elaftalen');
        }
    }

    async function loadComparison() {
        const list = document.getElementById('compareList');
        try {
            const res = await fetch('/api/supplier-comparison');
            const data = await res.json();
            list.innerHTML = data.map(r => `
                <div class="ep-compare-row${r.isCurrent ? ' ep-compare-row--current' : ''}">
                    <span class="ep-compare-name">${r.name}${r.isCurrent ? ' (nuværende)' : ''}</span>
                    <span class="ep-compare-cost">~${r.estYearlyCostKr.toFixed(0)} kr/år</span>
                </div>
            `).join('');
        } catch {
            list.innerHTML = '<div class="ep-compare-row"><span>Kunne ikke hente sammenligning.</span></div>';
        }
    }

    function matchPresetIdByName(items, name) {
        if (!name) return null;
        const lower = name.toLowerCase();
        const match = items.find(i => lower.includes(i.name.toLowerCase()) || i.name.toLowerCase().includes(lower));
        return match ? match.id : null;
    }

    function summarizeExtraction(r) {
        const parts = [];
        if (r.gridCompanyName) parts.push(`netselskab: ${r.gridCompanyName}`);
        if (r.supplierName) parts.push(`leverandør: ${r.supplierName}`);
        if (parts.length === 0) return 'Fandt ikke nok til at udfylde automatisk — udfyld selv nedenfor.';
        return `Fundet ${parts.join(', ')}. Tjek felterne nedenfor og tryk Gem.`;
    }

    async function applyExtractedResult(result) {
        const presets = await loadPresets();
        const gridSelect = document.getElementById('gridCompanySelect');
        const supplierSelect = document.getElementById('supplierSelect');
        const customNettarif = document.getElementById('customNettarif');
        const customTillaeg = document.getElementById('customTillaeg');

        const gridMatchId = matchPresetIdByName(presets.gridCompanies, result.gridCompanyName);
        if (gridMatchId) {
            gridSelect.value = gridMatchId;
        } else if (result.nettarifOrePerKwh) {
            gridSelect.value = 'custom';
            customNettarif.value = result.nettarifOrePerKwh;
        }

        const supplierMatchId = matchPresetIdByName(presets.suppliers, result.supplierName);
        if (supplierMatchId) {
            supplierSelect.value = supplierMatchId;
        } else if (result.supplierMarkupOrePerKwh) {
            supplierSelect.value = 'custom';
            customTillaeg.value = result.supplierMarkupOrePerKwh;
        }

        toggleCustomField(gridSelect, customNettarif);
        toggleCustomField(supplierSelect, customTillaeg);
    }

    async function scanBill(file) {
        const status = document.getElementById('scanStatus');
        status.style.display = 'block';
        status.textContent = 'Analyserer regningen…';

        try {
            const formData = new FormData();
            formData.append('bill', file);
            const res = await fetch('/api/bill-scan', { method: 'POST', body: formData });
            if (!res.ok) {
                status.textContent = 'Kunne ikke læse regningen — prøv et tydeligere billede, eller udfyld selv nedenfor.';
                return;
            }
            const result = await res.json();
            await applyExtractedResult(result);
            status.textContent = summarizeExtraction(result);
        } catch {
            status.textContent = 'Kunne ikke læse regningen — prøv igen, eller udfyld selv nedenfor.';
        }
    }

    document.getElementById('toggleSettingsBtn').addEventListener('click', () => {
        const section = document.getElementById('settingsSection');
        const show = section.style.display === 'none';
        section.style.display = show ? 'block' : 'none';
        if (show) loadSettingsUi();
    });

    document.getElementById('gridCompanySelect').addEventListener('change', e => {
        toggleCustomField(e.target, document.getElementById('customNettarif'));
    });
    document.getElementById('supplierSelect').addEventListener('change', e => {
        toggleCustomField(e.target, document.getElementById('customTillaeg'));
    });
    document.getElementById('saveSettingsBtn').addEventListener('click', saveSettings);

    document.getElementById('scanBillBtn').addEventListener('click', () => {
        document.getElementById('billFileInput').click();
    });
    document.getElementById('billFileInput').addEventListener('change', e => {
        const file = e.target.files[0];
        if (file) scanBill(file);
    });

    document.getElementById('durationRow').addEventListener('click', e => {
        const btn = e.target.closest('.ep-duration-btn');
        if (!btn) return;
        document.querySelectorAll('.ep-duration-btn').forEach(b => b.classList.remove('ep-duration-btn--active'));
        btn.classList.add('ep-duration-btn--active');
        selectedHours = parseInt(btn.dataset.hours, 10);
        loadRecommendation();
    });

    document.querySelectorAll('.ep-device-btn').forEach(btn => {
        btn.addEventListener('click', () => logRun(btn.dataset.device, parseFloat(btn.dataset.kwh)));
    });

    document.getElementById('toggleListBtn').addEventListener('click', e => {
        const list = document.getElementById('priceList');
        const show = list.style.display === 'none';
        list.style.display = show ? 'flex' : 'none';
        e.target.textContent = show ? 'Skjul alle priser ▴' : 'Vis alle priser ▾';
    });

    loadRecommendation();
    loadPrices();
    loadRuns();
    loadWeeklyPattern();

    setInterval(loadRecommendation, 5 * 60_000);
    setInterval(loadPrices, 5 * 60_000);
})();
