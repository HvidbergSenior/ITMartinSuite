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

    function render(a, box) {
        var m = a.monthCount, html = '';
        html += '<section class="k-card bu-result" data-nofold><h2 class="k-card-title">💰 Det fandt jeg i din fil</h2>' +
            '<p class="k-help">' + a.lines + ' linjer fra ' + dato(a.first) + ' til ' + dato(a.last) + ' (' + m + ' måned' + (m === 1 ? '' : 'er') + '). ' +
            'Overførsler mellem dine egne konti er trukket fra, så godt det kan lade sig gøre.</p>';

        html += '<div class="bu-big"><div class="bu-big-label">📱 MobilePay ud</div><div class="bu-big-num">' + kr(a.mp.sum) + '</div>' +
            '<div class="bu-big-sub">' + a.mp.n + ' betalinger · ' + perMonth(a.mp.sum, a) + '</div>' +
            (a.mp.topMonth && m > 1 ? '<div class="bu-big-sub">Mest i ' + maaned(a.mp.topMonth) + ': <b>' + kr(a.mp.topSum) + '</b></div>' : '') + '</div>';
        // MobilePay lines carry only the receiver's name ("MobilePay Peter", "Vdk Mob.Pay*Anne"), never what it was for.
        if (a.mp.n > 0)
            html += '<p class="bu-blind">🙈 <b>Hvad var de ' + kr(a.mp.sum) + ' til?</b> Det står ingen steder. Banken viser kun, <i>hvem</i> der fik pengene – ' +
                'ikke <i>hvad</i> de var til. Derfor kan hverken bankens oversigt, budget-apps eller denne side sortere dem. ' +
                '<a href="#bu-mobilepay">Læs hvorfor</a></p>';

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
            '<p>Det her er kun det første kig. I den <b>udvidede udgave</b> sorterer jeg <b>hver eneste linje</b> i <b>dine egne kategorier</b> – også MobilePay, fordelt på det, det faktisk var: benzin, dagligvarer, lommepenge, brugtkøb … Jeg sammenligner måned for måned, ' +
            'finder de abonnementer, du har glemt, og laver et budget, der passer til jeres liv – og jeg hjælper dig med at opsige det, du ikke bruger.</p>' +
            '<a class="k-btn k-btn-primary" href="/#chat">💬 Spørg om den udvidede udgave</a></section>';

        show(box, html);
        box.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function tile(label, n, a) {
        return '<div class="bu-tile"><div class="bu-tile-label">' + label + '</div><div class="bu-tile-num">' + kr(n) + '</div>' +
            '<div class="bu-tile-sub">' + perMonth(n, a) + '</div></div>';
    }

    function fail(box, msg) {
        show(box, '<section class="k-card bu-error" data-nofold><h2 class="k-card-title">😕 Det gik ikke</h2><p>' + msg + '</p>' +
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
