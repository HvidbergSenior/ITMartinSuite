// Gratis Budget - the PAGE (itmartin.dk/budget): the visitor drops their bank's CSV, budget-domain.js works it out,
// and this file draws the answer. Everything happens in the browser - the file is never sent anywhere, nothing is saved.
(function () {
    'use strict';
    var D = window.BudgetDomain;

    // The page is drawn by Blazor: when its live connection starts (a second after load), it redraws the page and empties
    // the answer box. So the last answer is remembered and put back whenever the box turns up empty again.
    var lastHtml = '', boxId = 'bu-out';
    function show(box, html) { lastHtml = html; box.innerHTML = html; }
    new MutationObserver(function () {
        var box = document.getElementById(boxId);
        if (box && lastHtml && !box.innerHTML.trim()) box.innerHTML = lastHtml;
    }).observe(document.documentElement, { childList: true, subtree: true });


    // ---------- showing it ----------
    var nf = new Intl.NumberFormat('da-DK', { maximumFractionDigits: 0 });
    function kr(n) { return nf.format(Math.round(n)) + ' kr'; }
    function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
    function dato(d) { return d.toLocaleDateString('da-DK', { day: 'numeric', month: 'long', year: 'numeric' }); }
    function maaned(k) { var p = k.split('-'); return new Date(+p[0], +p[1] - 1, 1).toLocaleDateString('da-DK', { month: 'long', year: 'numeric' }); }
    function perMonth(n, a) { return kr(n / a.monthCount) + ' om måneden'; }

    // The answer (rebuilt 2026-11-18, user: "make it easy and intuitive"): the three numbers people ask first, then month by
    // month, then what the money goes to (the bank's own groups when the file has them), the rest folded away. No emojis.
    function render(a, box) {
        var m = a.monthCount, avgIn = a.income / m, avgOut = a.spent / m, left = avgIn - avgOut, html = '';
        html += '<section class="k-card bu-result" data-nofold><h2 class="k-card-title">Det fandt jeg i din fil</h2>' +
            '<p class="k-help">' + a.lines + ' linjer fra ' + dato(a.first) + ' til ' + dato(a.last) + ' (' + m + ' måned' + (m === 1 ? '' : 'er') + '). ' +
            'Penge til og fra din opsparing og dine egne konti tæller ikke med – de er hverken indtægt eller forbrug.</p>' +
            '<div class="bu-grid bu-key">' +
            key('Ind om måneden', avgIn, '') + key('Ud om måneden', avgOut, '') +
            key(left >= 0 ? 'Til overs om måneden' : 'Mangler om måneden', Math.abs(left), left >= 0 ? 'bu-good' : 'bu-bad') + '</div>';

        // Month by month - a bar for in and out, so a bad month is seen at once.
        var max = Math.max.apply(null, a.months.map(function (x) { return Math.max(x.inn, x.out); }).concat([1]));
        html += '<h3 class="bu-h3">Ind og ud pr. måned</h3><ul class="bu-months">' + a.months.map(function (x) {
            var diff = x.inn - x.out;
            return '<li><span class="bu-mname">' + maaned(x.key) + '</span>' +
                '<span class="bu-bars"><i class="bu-in" style="width:' + (100 * x.inn / max).toFixed(1) + '%"></i><i class="bu-out" style="width:' + (100 * x.out / max).toFixed(1) + '%"></i></span>' +
                '<span class="bu-mnum">' + kr(x.inn) + ' ind<br>' + kr(x.out) + ' ud</span>' +
                '<b class="' + (diff >= 0 ? 'bu-good' : 'bu-bad') + '">' + (diff >= 0 ? '+' : '−') + kr(Math.abs(diff)) + '</b></li>';
        }).join('') + '</ul><p class="k-help"><i class="bu-dot bu-in"></i> ind &nbsp; <i class="bu-dot bu-out"></i> ud</p></section>';

        // What the money goes to.
        html += '<section class="k-card" data-nofold><h2 class="k-card-title">Hvad går pengene til?</h2>' +
            (a.hasGroups ? '<p class="k-help">Bankens egne kategorier. Tryk på en kategori for at se, hvad den består af.</p>'
                         : '<p class="k-help">Din fil har ingen kategorier, så her er de steder, hvor flest penge går hen.</p>') +
            '<div class="bu-groups">' + (a.hasGroups ? a.groups : a.top.map(function (p) { return { name: p.name, sum: p.sum, n: p.n, monthly: p.sum / m, subs: [] }; }))
                .slice(0, 14).map(function (g) {
                var share = a.spent > 0 ? Math.round(100 * g.sum / a.spent) : 0;
                return '<details class="bu-group"><summary><span>' + esc(g.name) + ' <small>' + share + ' %</small></span><b>' + kr(g.monthly) + ' <small>/md</small></b></summary>' +
                    (g.subs.length ? '<ul class="bu-list">' + g.subs.map(function (x) {
                        return '<li><span>' + esc(x.name) + ' <small>(' + x.n + ')</small></span><b>' + kr(x.sum / m) + ' <small>/md</small></b></li>'; }).join('') + '</ul>'
                        : '<p class="k-help">' + g.n + ' betalinger, ' + kr(g.sum) + ' i alt.</p>') + '</details>';
            }).join('') + '</div></section>';

        // The payments nobody can sort - the free page's own point (2026-10-08), now folded.
        var u = a.unclear;
        html += '<details class="k-card"><summary><b>Det banken ikke kan sortere – ' + kr(u.sum / m) + ' om måneden</b></summary>' +
            '<p>MobilePay, overførsler til andre og kontanter: her står kun, <i>hvem</i> der fik pengene – ikke <i>hvad</i> de var til. ' +
            '<a href="#bu-mobilepay">Læs hvorfor</a></p><div class="bu-grid">' +
            part('MobilePay', u.parts.mp) + part('Overført til andre konti', u.parts.transfer) + part('Kontanter', u.parts.cash) + '</div>' +
            (u.who.length ? '<h3 class="bu-h3">Hvem fik pengene?</h3><ul class="bu-list">' +
                u.who.map(function (p) { return '<li><span>' + esc(p.name) + ' <small>(' + p.n + ' ' + (p.n === 1 ? 'gang' : 'gange') + ')</small></span><b>' + kr(p.sum) + '</b></li>'; }).join('') + '</ul>' : '') +
            '</details>';

        html += '<details class="k-card"><summary><b>Faste betalinger – ' + a.subs.length + ' fundet, ca. ' + kr(a.subsMonthly) + ' om måneden</b></summary>' +
            (a.subs.length ? '<p class="k-help">Samme sted og samme beløb i flere måneder. Betalinger hver 3. måned tæller med en tredjedel pr. måned. Bruger du dem alle sammen?</p><ul class="bu-list">' +
                a.subs.map(function (s) { return '<li><span>' + esc(s.name) + (s.every > 1 ? ' <small>(hver ' + s.every + '. måned)</small>' : '') + '</span><b>' + kr(s.amount) + '</b></li>'; }).join('') + '</ul>'
                : '<p class="k-help">Jeg fandt ingen. Prøv med en fil, der dækker mindst 3 måneder.</p>') + '</details>';

        // The paid app, like ElPriser -> MinElpris (2026-11-18).
        html += '<section class="k-card bu-paid" data-nofold><h2 class="k-card-title">Vil du følge med hver måned? – MitBudget</h2>' +
            '<p>Her ser du kun det, din fil viser i dag – og intet bliver gemt. I <b>MitBudget</b> bliver dine tal gemt, så du kan følge med måned for måned:</p>' +
            '<ul class="bu-steps"><li><b>Dine egne kategorier</b> – også MobilePay: lommepenge, fodbold, brugtkøb …</li>' +
            '<li><b>Familiens budget</b> – hvad I bruger pr. måned, og hvad der er til overs</li>' +
            '<li><b>Privat og firma hver for sig</b> – og hvad der kan trækkes fra</li>' +
            '<li><b>Del med din rådgiver</b> – kun de vigtige tal, aldrig navne</li></ul>' +
            '<p><b>Ingen abonnement.</b> Prisen aftaler vi sammen – du betaler kun for den tid, du bruger den.</p>' +
            '<a class="k-btn k-btn-primary" href="/#chat">Spørg om MitBudget</a></section>';

        show(box, html);
        box.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function key(label, n, cls) {
        return '<div class="bu-tile ' + cls + '"><div class="bu-tile-label">' + label + '</div><div class="bu-tile-num">' + kr(n) + '</div></div>';
    }

    function part(label, p) {
        return '<div class="bu-tile"><div class="bu-tile-label">' + label + '</div><div class="bu-tile-num">' + kr(p.sum) + '</div>' +
            '<div class="bu-tile-sub">' + p.n + ' ' + (p.n === 1 ? 'betaling' : 'betalinger') + '</div></div>';
    }

    function tile(label, n, a) {
        return '<div class="bu-tile"><div class="bu-tile-label">' + label + '</div><div class="bu-tile-num">' + kr(n) + '</div>' +
            '<div class="bu-tile-sub">' + perMonth(n, a) + '</div></div>';
    }

    function fail(box, msg) {
        show(box, '<section class="k-card bu-error" data-nofold><h2 class="k-card-title">Det gik ikke</h2><p>' + msg + '</p>' +
            '<p class="k-help">Se guiden ovenfor, eller skriv til mig i chatten – så kigger vi på det sammen.</p></section>');
    }

    function handle(file) {
        var box = document.getElementById('bu-out');
        if (!box || !file) return;
        if (/\.(xlsx?|pdf)$/i.test(file.name)) {
            fail(box, 'Det er en ' + file.name.split('.').pop().toUpperCase() + '-fil. Vælg <b>CSV</b>, når du henter posteringerne i netbanken.');
            return;
        }
        file.arrayBuffer().then(function (buf) {
            var list = D.readTransactions(D.decode(buf));
            if (!list || list.length < 3) { fail(box, 'Jeg kunne ikke finde dato, tekst og beløb i filen. Er det en CSV-fil med posteringer?'); return; }
            render(D.analyse(list), box);
        }).catch(function () { fail(box, 'Filen kunne ikke læses.'); });
    }

    // Event delegation: the page is drawn by Blazor, so the elements may come and go.
    document.addEventListener('click', function (e) {
        var link = e.target && e.target.closest && e.target.closest('a[href="#bu-mobilepay"]');
        var box = link && document.getElementById('bu-mobilepay');
        if (!box) return;
        e.preventDefault();
        box.open = true;
        box.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
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

})();
