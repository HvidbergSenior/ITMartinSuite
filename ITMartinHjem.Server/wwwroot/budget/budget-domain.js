// Gratis Budget - the RULES (user 2026-10-08): read a Danish bank's CSV export and work out where the money goes.
// Pure functions only - no page, no network - so the same file runs in the browser and in Node's tests
// (ITMartinHjem.Server/js-tests). The page part is budget-ui.js. Nothing here sends anything anywhere.
(function (root, factory) {
    if (typeof module === 'object' && module.exports) module.exports = factory();
    else root.BudgetDomain = factory();
})(typeof self !== 'undefined' ? self : this, function () {
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
            SKIP_TEXT = /valuta|currency|status|afstemt|konto|saldo|balance|kategori/i,
            CATEGORY = /^kategori$|^category$|^hovedkategori$/i;
        for (var h = 0; h < Math.min(5, rows.length); h++) {
            var head = rows[h].map(function (c) { return c.toLowerCase(); });
            var date = head.findIndex(function (c) { return DATE.test(c); });
            var amount = head.findIndex(function (c) { return AMOUNT.test(c); });
            if (date > -1 && amount > -1) {
                var texts = [];
                head.forEach(function (c, i) { if (TEXT.test(c) && !SKIP_TEXT.test(c) && i !== date && i !== amount) texts.push(i); });
                // The bank's own category, the finer one if both exist ("Kategori" before "Hovedkategori").
                var cat = head.findIndex(function (c) { return /^kategori$|^category$/i.test(c); });
                if (cat < 0) cat = head.findIndex(function (c) { return CATEGORY.test(c); });
                return { start: h + 1, date: date, amount: amount, texts: texts, category: cat };
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
            list.push({ date: date, amount: amount, text: txt || '(uden tekst)', category: c.category >= 0 ? (r[c.category] || '') : '' });
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
    // The point of the free Budget (user 2026-10-08): show the payments nobody can sort - MobilePay to people,
    // transfers to an account number, cash. The bank knows only who got the money, never what it was for.
    var TRANSFER_OTHER = /^(til|overf\S*(\s+til)?)\s+\d[\d ]{5,}/i;
    var CASH = /hævning|hævet|kontant|pengeautomat|\batm\b|udbetaling/i;
    var VAGUE_BANK = /andet|anden|overf|diverse|ukendt|øvrig/i;   // the bank's own "we don't know" categories

    function unclearKind(t) {
        // When the file carries the bank's own category and it is a real one ("Telefon, internet ..."), the bank DOES know
        // what it was - e.g. Telenor paid with MobilePay - so it is not a difficult payment.
        if (t.category && !VAGUE_BANK.test(t.category)) return null;
        if (MOBILEPAY.test(t.text)) return 'mp';
        if (TRANSFER_OTHER.test(t.text)) return 'transfer';
        if (CASH.test(t.text)) return 'cash';
        return null;
    }

    // Who got it: "MobilePay Eigil Hvidberg Johns" and "VDK MOB.PAY*EIGIL HVID" are the same person; "Til 7633 0008318157" an account.
    function recipient(t, kind) {
        if (kind === 'cash') return { key: 'kontanter', name: 'Kontanter (hævet)' };
        if (kind === 'transfer') {
            var acc = (t.text.match(/\d[\d ]{5,}/) || [''])[0].trim();
            var rest = t.text.slice(t.text.indexOf(acc) + acc.length).trim();   // "Til 97272 Skive Tek.sk" -> "Skive Tek.sk"
            return { key: 'konto ' + acc, name: rest ? rest + ' (' + acc + ')' : 'Konto ' + acc };
        }
        var name = t.text.replace(/^(vdk|debetkort|visa|dankort|dk)\s+/i, '').replace(/^(debetkort\s+)?(mobile\s?pay|mob\.?\s?pay)\s*\*?\s*/i, '').trim();
        if (!name) return { key: 'mobilepay', name: 'MobilePay (uden navn)' };
        var w = name.toLowerCase().split(/\s+/);
        var pretty = name.toLowerCase().replace(/(^|\s)\S/g, function (x) { return x.toUpperCase(); });
        return { key: w[0] + ' ' + (w[1] || '').slice(0, 4), name: pretty };
    }


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

    // How often a fixed payment comes, in months: the average gap between its payments, rounded to what bills use
    // (1, 2, 3, 4, 6 or 12). Quarterly water counts a third per month - not in full (fixed 2026-10-08).
    function interval(hits) {
        if (hits.length < 2) return 1;
        var ds = hits.map(function (t) { return t.date; }).sort(function (x, y) { return x - y; });
        var months = (ds[ds.length - 1] - ds[0]) / (1000 * 60 * 60 * 24 * 30.44) / (ds.length - 1);
        return [1, 2, 3, 4, 6, 12].reduce(function (best, m) { return Math.abs(m - months) < Math.abs(best - months) ? m : best; }, 1);
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

        var unclear = { sum: 0, n: 0, parts: { mp: { sum: 0, n: 0 }, transfer: { sum: 0, n: 0 }, cash: { sum: 0, n: 0 } }, who: [], bankVague: 0, bankCategorised: 0 };
        var people = {};
        out.forEach(function (t) {
            var kind = unclearKind(t);
            if (!kind) return;
            unclear.sum -= t.amount; unclear.n++;
            unclear.parts[kind].sum -= t.amount; unclear.parts[kind].n++;
            var r = recipient(t, kind);
            var p = people[r.key] = people[r.key] || { name: r.name, kind: kind, sum: 0, n: 0 };
            if (r.name.length > p.name.length) p.name = r.name;   // the longest spelling reads best
            p.sum -= t.amount; p.n++;
            if (t.category) { unclear.bankCategorised++; if (VAGUE_BANK.test(t.category)) unclear.bankVague++; }
        });
        unclear.who = Object.values(people).sort(function (a, b) { return b.sum - a.sum; }).slice(0, 8);

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
                if (nMonths >= 2 && hits.length <= nMonths + 1 && +a >= 10) {
                    var amount = -hits[hits.length - 1].amount, every = interval(hits);
                    subs.push({ name: hits[hits.length - 1].text, amount: amount, n: hits.length, every: every, monthly: amount / every });
                }
            });
        });
        subs.sort(function (a, b) { return b.amount - a.amount; });

        return {
            first: list[0].date, last: list[list.length - 1].date, monthCount: monthCount, lines: list.length,
            income: list.filter(function (t) { return t.amount > 0 && !OWN.test(t.text); }).reduce(function (s, t) { return s + t.amount; }, 0),
            spent: sum(out),
            unclear: unclear,
            mp: { sum: sum(mp), n: mp.length, topMonth: mpTop, topSum: mpTop ? mpByMonth[mpTop] : 0 },
            grocery: sum(where(GROCERY)), eatout: sum(where(EATOUT)), fuel: sum(where(FUEL)), fees: sum(where(FEES)),
            top: top, subs: subs.slice(0, 12), subsMonthly: subs.reduce(function (s, x) { return s + x.monthly; }, 0)
        };
    }

    return {
        decode: decode, pickDelimiter: pickDelimiter, splitLine: splitLine, parseDate: parseDate, parseAmount: parseAmount,
        findColumns: findColumns, readTransactions: readTransactions, unclearKind: unclearKind, recipient: recipient, clean: clean, monthKey: monthKey, interval: interval, analyse: analyse,
        patterns: { MOBILEPAY: MOBILEPAY, GROCERY: GROCERY, EATOUT: EATOUT, FUEL: FUEL, FEES: FEES, OWN: OWN }
    };
});
