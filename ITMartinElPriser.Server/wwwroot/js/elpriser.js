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
                <div class="ep-rec-detail">til ${fmtDayTime(w.end)} · ca. ${w.avgPriceKrPerKwh.toFixed(2)} kr/kWh i gennemsnit${w.isEstimated ? ' (estimeret)' : ''}</div>
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
        const min = Math.min(...upcoming.map(p => p.priceKrPerKwh));
        const max = Math.max(...upcoming.map(p => p.priceKrPerKwh));
        const maxHeight = 100;

        upcoming.forEach(p => {
            const tier = priceTier(p.priceKrPerKwh, min, max);
            const h = max === min ? maxHeight / 2 : Math.max(4, ((p.priceKrPerKwh - min) / (max - min)) * maxHeight);
            const bar = document.createElement('div');
            const d = new Date(p.timeDk);
            const isNow = Math.abs(d - now) < 3600_000 && d <= now;
            bar.className = `ep-bar ep-bar--${tier}${isNow ? ' ep-bar--now' : ''}`;
            bar.style.height = h + 'px';
            bar.title = `${fmtDayTime(p.timeDk)}: ${p.priceKrPerKwh.toFixed(2)} kr/kWh`;
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
        const min = Math.min(...upcoming.map(p => p.priceKrPerKwh));
        const maxPrice = Math.max(...upcoming.map(p => p.priceKrPerKwh));

        list.innerHTML = upcoming.map(p => {
            const tier = priceTier(p.priceKrPerKwh, min, maxPrice);
            const d = new Date(p.timeDk);
            const isNow = Math.abs(d - now) < 3600_000 && d <= now;
            return `
                <div class="ep-price-row${isNow ? ' ep-price-row--now' : ''}">
                    <span class="ep-price-time">${fmtDayTime(p.timeDk)}${p.isEstimated ? '<span class="ep-price-estimated-tag">estimeret</span>' : ''}</span>
                    <span class="ep-price-value ep-price-value--${tier}">${p.priceKrPerKwh.toFixed(2)} kr/kWh</span>
                </div>
            `;
        }).join('');
    }

    function updateEstimatedNote(prices) {
        const note = document.getElementById('estimatedNote');
        note.style.display = prices.some(p => p.isEstimated) ? 'block' : 'none';
    }

    async function loadPrices() {
        try {
            const res = await fetch(`/api/prices?area=${AREA}`);
            const prices = await res.json();
            priceCache = prices;
            renderChart(prices);
            renderPriceList(prices);
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
