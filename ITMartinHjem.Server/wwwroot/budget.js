// Gratis Budget (itmartin.dk/budget, user 2026-10-08): the visitor drops the CSV file from their netbank and
// sees where the money goes. Everything happens HERE in the browser - the file is never sent anywhere and
// nothing is saved, so the page can promise "filen forlader aldrig din computer".
// Works with the common Danish exports (Danske Bank, Nordea, Jyske, Sydbank, Lunar, sparekasser …): it finds the
// date/text/amount columns by their header names, or by their contents when the file has no header row.
(function () {
    'use strict';

    // ---------- reading the file ----------
    function decode(buf) {
        // Many Danish banks still export Windows-1252 (æøå break in UTF-8), so try strict UTF-8 first.
        try { return new TextDecoder('utf-8', { fatal: true }).decode(buf).replace(/^﻿/, ''); }
        catch (e) { return new TextDecoder('windows-1252').decode(buf); }
    }

    function pickDelimiter(text) {
        var lines = text.split(/\r?\n/).filter(function (l) { return l.trim(); }).slice(0, 10);
        var best = ';', bestScore = -1;
        [';', '\t', ','].forEach(function (d) {
            var counts = lines.map(function (l) { return splitLine(l, d).length; });
            var min = Math.min.apply(null, counts);
            if (min > 1 && min > bestScore) { best = d; bestScore = min; }
        });
        return best;
    }

    function splitLine(line, d) {
        var out = [], cur = '', q = false;
        for (var i = 0; i < line.length; i++) {
            var ch = line[i];
            if (q) {
                if (ch === '"' && line[i + 1] === '"') { cur += '"'; i++; }
                else if (ch === '"') q = false;
                else cur += ch;
            } else if (ch === '"') q = true;
            else if (ch === d) { out.push(cur.trim()); cur = ''; }
            else cur += ch;
        }
        out.push(cur.trim());
        return out;
    }

    function parseDate(s) {
        s = (s || '').trim();
        var m = s.match(/^(\d{1,2})[.\-\/](\d{1,2})[.\-\/](\d{2}|\d{4})$/);
        if (m) {
            var y = +m[3]; if (y < 100) y += 2000;
            var d = new Date(y, +m[2] - 1, +m[1]);
            return d.getMonth() === +m[2] - 1 ? d : null;
        }
        m = s.match(/^(\d{4})[.\-\/](\d{1,2})[.\-\/](\d{1,2})/);
        if (m) {
            var d2 = new Date(+m[1], +m[2] - 1, +m[3]);
            return d2.getMonth() === +m[2] - 1 ? d2 : null;
        }
        return null;
    }

    function parseAmount(s) {
        s = (s || '').replace(/[\s ]|kr\.?|dkk/gi, '');
        if (!/^[+\-−]?[\d.,]+-?$/.test(s) || !/\d/.test(s)) return null;
        var neg = /^[\-−]/.test(s) || /-$/.test(s);
        s = s.replace(/^[+\-−]|-$/g, '');
        var lastComma = s.lastIndexOf(','), lastDot = s.lastIndexOf('.');
        if (lastComma > -1 && lastDot > -1) {
            // the last separator is the decimal one: 1.234,56 or 1,234.56
            s = lastComma > lastDot ? s.replace(/\./g, '').replace(',', '.') : s.replace(/,/g, '');
        } else if (lastComma > -1) {
            s = s.replace(/,(?=\d{3}(\D|$))(?!\d{1,2}$)/g, '').replace(',', '.');
        } else if (/^\d{1,3}(\.\d{3})+$/.test(s)) {
            s = s.replace(/\./g, '');   // 1.234 = thousands
        }
        var n = parseFloat(s);
        return isNaN(n) ? null : (neg ? -n : n);
    }

    // Finds which column is what. Header names first, contents as the fallback.
    function findColumns(rows) {
        var DATE = /bogf|^dato$|^date$|posteringsdato|transaktionsdato|^dato\b/i,
            AMOUNT = /^beløb|^belob|^amount|^beløb i kr|^bel.b$/i,
            TEXT = /tekst|beskrivelse|text|description|navn|modtager|meddelelse|^titel/i,
            SKIP_TEXT = /valuta|currency|status|afstemt|konto|saldo|balance|kategori/i;
        for (var h = 0; h < Math.min(5, rows.length); h++) {
            var head = rows[h].map(function (c) { return c.toLowerCase(); });
            var date = head.findIndex(function (c) { return DATE.test(c); });
            var amount = head.findIndex(function (c) { return AMOUNT.test(c); });
            if (date > -1 && amount > -1) {
                var texts = [];
                head.forEach(function (c, i) { if (TEXT.test(c) && !SKIP_TEXT.test(c) && i !== date && i !== amount) texts.push(i); });
                return { start: h + 1, date: date, amount: amount, texts: texts };
            }
        }
        // No header (e.g. a raw export that starts with the account number): judge by contents.
        var sample = rows.slice(0, 40), cols = Math.max.apply(null, sample.map(function (r) { return r.length; }));
        function share(i, fn) { return sample.filter(function (r) { return fn(r[i]) !== null; }).length / sample.length; }
        var dateCol = -1, numCols = [], textCol = -1, textLen = 0;
        for (var i = 0; i < cols; i++) {
            if (dateCol < 0 && share(i, parseDate) > 0.8) { dateCol = i; continue; }
            if (share(i, parseAmount) > 0.8) { numCols.push(i); continue; }
            var len = sample.reduce(function (a, r) { return a + (r[i] || '').length; }, 0);
            if (len > textLen) { textLen = len; textCol = i; }
        }
        // the amount column has both + and -; a balance column usually only one sign
        var amountCol = numCols.find(function (i) {
            var v = sample.map(function (r) { return parseAmount(r[i]); });
            return v.some(function (x) { return x < 0; }) && v.some(function (x) { return x > 0; });
        });
        if (amountCol === undefined) amountCol = numCols[0];
        if (dateCol < 0 || amountCol === undefined) return null;
        return { start: 0, date: dateCol, amount: amountCol, texts: textCol > -1 ? [textCol] : [] };
    }

    function readTransactions(text) {
        var d = pickDelimiter(text);
        var rows = text.split(/\r?\n/).filter(function (l) { return l.trim(); }).map(function (l) { return splitLine(l, d); });
        var c = findColumns(rows);
        if (!c) return null;
        var list = [];
        rows.slice(c.start).forEach(function (r) {
            var date = parseDate(r[c.date]), amount = parseAmount(r[c.amount]);
            if (!date || amount === null) return;
            var txt = c.texts.map(function (i) { return r[i] || ''; }).filter(Boolean)
                .filter(function (t, i, a) { return a.indexOf(t) === i; }).join(' · ');
            list.push({ date: date, amount: amount, text: txt || '(uden tekst)' });
        });
        return list;
    }

    // ---------- understanding the lines ----------
    var MOBILEPAY = /mobile\s?pay|mob\.?\s?pay|^mp\s|mobilepay|vipps/i;
    var GROCERY = /netto|rema|lidl|føtex|fotex|bilka|kvickly|superbrugsen|dagli.?brugsen|brugsen|coop|meny|irma|spar\b|aldi|løvbjerg|lovbjerg|min købmand|365discount|salling/i;
    var EATOUT = /wolt|just ?eat|hungry|mcdonald|burger king|sunset|max burger|pizza|sushi|cafe|café|bager|lagkagehuset|espresso house|joe & the juice|starbucks|7-eleven|7 eleven|kebab|grill|restaurant/i;
    var FUEL = /circle ?k|q8|shell|\bok\b|ok plus|uno-?x|ingo|f24|go'on|clever|tesla|e\.on|spirii|rejsekort|dsb|midttrafik|movia|parkering|easypark|parkman|apcoa|brobizz/i;
    var FEES = /gebyr|rente|renter|overtræk|rykker|kortgebyr|årsgebyr/i;
    var OWN = /overf|overført|opsparing|egen konto|til konto|fra konto/i;

    // "Vdk Netflix.com 12.05" and "Netflix.com Dankort-nota 13.06" -> "netflix.com"
    function clean(t) {
        return t.toLowerCase()
            .replace(/^(vdk|visa\/dankort|dankort-nota|dankort|mastercard|visa|kortkøb|købt|dk|nota)\s+/g, '')
            .replace(/\b(dankort-nota|visa\/dankort|kortnr\.?\s*\S+|nota nr\.?\s*\S+)/g, '')
            .replace(/\d{1,2}[.\-\/]\d{1,2}([.\-\/]\d{2,4})?/g, '')
            .replace(/\b\d{3,}\b/g, '')
            .replace(/[*#]+/g, ' ')
            .replace(/\s+/g, ' ').trim();
    }

    function monthKey(d) { return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0'); }

    function analyse(list) {
        list.sort(function (a, b) { return a.date - b.date; });
        var months = {};
        list.forEach(function (t) { months[monthKey(t.date)] = true; });
        var monthCount = Math.max(1, Object.keys(months).length);
        var out = list.filter(function (t) { return t.amount < 0 && !OWN.test(t.text); });
        function sum(a) { return a.reduce(function (s, t) { return s - t.amount; }, 0); }
        function where(re) { return out.filter(function (t) { return re.test(t.text); }); }

        var mp = where(MOBILEPAY), mpByMonth = {};
        mp.forEach(function (t) { var k = monthKey(t.date); mpByMonth[k] = (mpByMonth[k] || 0) - t.amount; });
        var mpTop = Object.keys(mpByMonth).sort(function (a, b) { return mpByMonth[b] - mpByMonth[a]; })[0];

        // Where most money goes (MobilePay counted as one, it is shown on its own above)
        var places = {};
        out.forEach(function (t) {
            var k = MOBILEPAY.test(t.text) ? 'MobilePay (alle)' : (clean(t.text) || t.text);
            places[k] = places[k] || { name: k, sum: 0, n: 0 };
            places[k].sum -= t.amount; places[k].n++;
        });
        var top = Object.values(places).sort(function (a, b) { return b.sum - a.sum; }).slice(0, 8);

        // Subscriptions: same place, about the same amount, in at least 2 different months
        var groups = {};
        out.forEach(function (t) {
            var k = clean(t.text);
            if (!k || MOBILEPAY.test(t.text) || GROCERY.test(t.text)) return;
            (groups[k] = groups[k] || []).push(t);
        });
        var subs = [];
        Object.keys(groups).forEach(function (k) {
            var g = groups[k], byAmount = {};
            g.forEach(function (t) {
                var a = Math.round(-t.amount);
                var hit = Object.keys(byAmount).find(function (x) { return Math.abs(x - a) <= Math.max(2, a * 0.05); });
                (byAmount[hit || a] = byAmount[hit || a] || []).push(t);
            });
            Object.keys(byAmount).forEach(function (a) {
                var hits = byAmount[a], ms = {};
                hits.forEach(function (t) { ms[monthKey(t.date)] = true; });
                var nMonths = Object.keys(ms).length;
                if (nMonths >= 2 && hits.length <= nMonths + 1 && +a >= 10)
                    subs.push({ name: hits[hits.length - 1].text, amount: -hits[hits.length - 1].amount, n: hits.length });
            });
        });
        subs.sort(function (a, b) { return b.amount - a.amount; });

        return {
            first: list[0].date, last: list[list.length - 1].date, monthCount: monthCount, lines: list.length,
            income: list.filter(function (t) { return t.amount > 0 && !OWN.test(t.text); }).reduce(function (s, t) { return s + t.amount; }, 0),
            spent: sum(out),
            mp: { sum: sum(mp), n: mp.length, topMonth: mpTop, topSum: mpTop ? mpByMonth[mpTop] : 0 },
            grocery: sum(where(GROCERY)), eatout: sum(where(EATOUT)), fuel: sum(where(FUEL)), fees: sum(where(FEES)),
            top: top, subs: subs.slice(0, 12), subsMonthly: subs.reduce(function (s, x) { return s + x.amount; }, 0)
        };
    }

    // ---------- showing it ----------
    var nf = new Intl.NumberFormat('da-DK', { maximumFractionDigits: 0 });
    function kr(n) { return nf.format(Math.round(n)) + ' kr'; }
    function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
    function dato(d) { return d.toLocaleDateString('da-DK', { day: 'numeric', month: 'long', year: 'numeric' }); }
    function maaned(k) { var p = k.split('-'); return new Date(+p[0], +p[1] - 1, 1).toLocaleDateString('da-DK', { month: 'long', year: 'numeric' }); }
    function perMonth(n, a) { return kr(n / a.monthCount) + ' om måneden'; }

    function render(a, box) {
        var m = a.monthCount, html = '';
        html += '<section class="k-card bu-result" data-nofold><h2 class="k-card-title">💰 Det fandt jeg i din fil</h2>' +
            '<p class="k-help">' + a.lines + ' linjer fra ' + dato(a.first) + ' til ' + dato(a.last) + ' (' + m + ' måned' + (m === 1 ? '' : 'er') + '). ' +
            'Overførsler mellem dine egne konti er trukket fra, så godt det kan lade sig gøre.</p>';

        html += '<div class="bu-big"><div class="bu-big-label">📱 MobilePay ud</div><div class="bu-big-num">' + kr(a.mp.sum) + '</div>' +
            '<div class="bu-big-sub">' + a.mp.n + ' betalinger · ' + perMonth(a.mp.sum, a) + '</div>' +
            (a.mp.topMonth && m > 1 ? '<div class="bu-big-sub">Mest i ' + maaned(a.mp.topMonth) + ': <b>' + kr(a.mp.topSum) + '</b></div>' : '') + '</div>';

        html += '<div class="bu-grid">' +
            tile('🛒 Dagligvarer', a.grocery, a) + tile('🍔 Mad ude og takeaway', a.eatout, a) +
            tile('⛽ Benzin, strøm til bilen og transport', a.fuel, a) + tile('🏦 Gebyrer og renter', a.fees, a) + '</div>';

        html += '<p class="bu-sum">Ind: <b>' + kr(a.income) + '</b> · Ud: <b>' + kr(a.spent) + '</b> · ' +
            (a.income >= a.spent ? 'Overskud' : 'Underskud') + ': <b>' + kr(Math.abs(a.income - a.spent)) + '</b></p></section>';

        html += '<details class="k-card" open><summary><b>🔁 Faste betalinger – ' + a.subs.length + ' fundet, ca. ' + kr(a.subsMonthly) + ' om måneden</b></summary>' +
            (a.subs.length ? '<p class="k-help">Samme sted og samme beløb i flere måneder. Bruger du dem alle sammen?</p><ul class="bu-list">' +
                a.subs.map(function (s) { return '<li><span>' + esc(s.name) + '</span><b>' + kr(s.amount) + '</b></li>'; }).join('') + '</ul>'
                : '<p class="k-help">Jeg fandt ingen. Prøv med en fil, der dækker mindst 3 måneder.</p>') + '</details>';

        html += '<details class="k-card"><summary><b>📊 Hvor de fleste penge går hen</b></summary><ul class="bu-list">' +
            a.top.map(function (p) { return '<li><span>' + esc(p.name) + ' <small>(' + p.n + ' gange)</small></span><b>' + kr(p.sum) + '</b></li>'; }).join('') +
            '</ul></details>';

        html += '<section class="k-card bu-paid" data-nofold><h2 class="k-card-title">➕ Vil du vide mere?</h2>' +
            '<p>Det her er kun det første kig. I den <b>udvidede udgave</b> sorterer jeg <b>hver eneste linje</b> i kategorier, sammenligner måned for måned, ' +
            'finder de abonnementer, du har glemt, og laver et budget, der passer til jeres liv – og jeg hjælper dig med at opsige det, du ikke bruger.</p>' +
            '<a class="k-btn k-btn-primary" href="/#chat">💬 Spørg om den udvidede udgave</a></section>';

        box.innerHTML = html;
        box.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function tile(label, n, a) {
        return '<div class="bu-tile"><div class="bu-tile-label">' + label + '</div><div class="bu-tile-num">' + kr(n) + '</div>' +
            '<div class="bu-tile-sub">' + perMonth(n, a) + '</div></div>';
    }

    function fail(box, msg) {
        box.innerHTML = '<section class="k-card bu-error" data-nofold><h2 class="k-card-title">😕 Det gik ikke</h2><p>' + msg + '</p>' +
            '<p class="k-help">Se guiden ovenfor, eller skriv til mig i chatten – så kigger vi på det sammen.</p></section>';
    }

    function handle(file) {
        var box = document.getElementById('bu-out');
        if (!box || !file) return;
        if (/\.(xlsx?|pdf)$/i.test(file.name)) {
            fail(box, 'Det er en ' + file.name.split('.').pop().toUpperCase() + '-fil. Vælg <b>CSV</b>, når du henter posteringerne i netbanken.');
            return;
        }
        file.arrayBuffer().then(function (buf) {
            var list = readTransactions(decode(buf));
            if (!list || list.length < 3) { fail(box, 'Jeg kunne ikke finde dato, tekst og beløb i filen. Er det en CSV-fil med posteringer?'); return; }
            render(analyse(list), box);
        }).catch(function () { fail(box, 'Filen kunne ikke læses.'); });
    }

    // Event delegation: the page is drawn by Blazor, so the elements may come and go.
    document.addEventListener('change', function (e) {
        if (e.target && e.target.id === 'bu-file') handle(e.target.files[0]);
    });
    ['dragover', 'drop'].forEach(function (ev) {
        document.addEventListener(ev, function (e) {
            var zone = e.target && e.target.closest && e.target.closest('#bu-drop');
            if (!zone) return;
            e.preventDefault();
            if (ev === 'drop' && e.dataTransfer.files.length) handle(e.dataTransfer.files[0]);
        });
    });

    window.gratisBudget = { readTransactions: readTransactions, analyse: analyse };   // for tests
})();
