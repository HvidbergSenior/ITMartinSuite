// Tjek dine billeder - the PAGE (itmartin.dk/billeder): the visitor picks or drops a folder, billeder-domain.js works
// it out, this file draws the answer. Everything is read here in the browser - no file is sent anywhere, nothing saved.
(function () {
    'use strict';
    var D = window.BillederDomain;

    // The page is drawn by Blazor: when its live connection starts (a second after load), it redraws the page and empties
    // the answer box. So the last answer is remembered and put back whenever the box turns up empty again.
    var lastHtml = '', boxId = 'bi-out';
    function show(box, html) { lastHtml = html; box.innerHTML = html; }
    new MutationObserver(function () {
        var box = document.getElementById(boxId);
        if (box && lastHtml && !box.innerHTML.trim()) box.innerHTML = lastHtml;
    }).observe(document.documentElement, { childList: true, subtree: true });


    // Files dropped as a folder: walk the entries (the folder picker gives a flat list by itself)
    function walk(entry, prefix, out) {
        return new Promise(function (done) {
            if (entry.isFile) {
                entry.file(function (f) { f._path = prefix + f.name; out.push(f); done(); }, function () { done(); });
            } else if (entry.isDirectory) {
                var reader = entry.createReader(), all = [];
                (function more() {
                    reader.readEntries(function (batch) {
                        if (!batch.length) {
                            Promise.all(all.map(function (e) { return walk(e, prefix + entry.name + '/', out); })).then(done);
                            return;
                        }
                        all = all.concat(Array.prototype.slice.call(batch));
                        more();
                    }, function () { done(); });
                })();
            } else done();
        });
    }

    // ---------- showing it ----------
    var nf = new Intl.NumberFormat('da-DK');
    function n(x) { return nf.format(x); }
    function gb(b) {
        if (b < 1e9) return new Intl.NumberFormat('da-DK', { maximumFractionDigits: 0 }).format(b / 1e6) + ' MB';
        return new Intl.NumberFormat('da-DK', { maximumFractionDigits: 1 }).format(b / 1e9) + ' GB';
    }

    function tile(label, count, bytes, sub) {
        return '<div class="bu-tile"><div class="bu-tile-label">' + label + '</div><div class="bu-tile-num">' + n(count) + '</div>' +
            '<div class="bu-tile-sub">' + gb(bytes) + (sub ? ' · ' + sub : '') + '</div></div>';
    }

    function render(r, box) {
        var total = r.photos + r.videos;
        var maybe = r.screenshots.bytes + r.chat.bytes + r.small.bytes;
        var html = '<section class="k-card bu-result" data-nofold><h2 class="k-card-title">🖼️ Det fandt jeg i din mappe</h2>';
        html += '<div class="bu-big"><div class="bu-big-label">Billeder og videoer</div><div class="bu-big-num">' + n(total) + '</div>' +
            '<div class="bu-big-sub">' + n(r.photos) + ' billeder · ' + n(r.videos) + ' videoer · ' + gb(r.bytes) + ' i alt</div></div>';
        html += '<div class="bu-grid">' +
            tile('♊ Dubletter (ekstra kopier)', r.dups.extra, r.dups.bytes, 'kan fjernes – én kopi bliver') +
            tile('📱 Skærmbilleder', r.screenshots.n, r.screenshots.bytes) +
            tile('💬 Billeder fra WhatsApp, Messenger m.fl.', r.chat.n, r.chat.bytes) +
            tile('🔍 Meget små billeder', r.small.n, r.small.bytes, 'ofte ikoner og miniaturer') + '</div>';
        html += '<p class="bu-sum">Dubletterne alene fylder <b>' + gb(r.dups.bytes) + '</b>. Skærmbilleder, chat-billeder og små billeder fylder <b>' +
            gb(maybe) + '</b> mere – dem kan de fleste undvære en stor del af.</p></section>';

        var years = Object.keys(r.years).map(Number).sort();
        if (years.length) {
            var max = Math.max.apply(null, years.map(function (y) { return r.years[y]; })), missing = [];
            html += '<details class="k-card" open><summary><b>📅 Billeder pr. år</b></summary><div class="bi-years">';
            for (var y = years[0]; y <= years[years.length - 1]; y++) {
                var c = r.years[y] || 0;
                if (!c) missing.push(y);
                html += '<div class="bi-year"><span class="bi-y">' + y + '</span><span class="bi-bar' + (c ? '' : ' none') + '" style="--w:' +
                    (c ? Math.max(2, Math.round(c / max * 100)) : 0) + '%"></span><span class="bi-c">' + (c ? n(c) : 'ingen') + '</span></div>';
            }
            html += '</div>';
            if (missing.length) html += '<p class="bu-blind">🕳️ <b>Ingen billeder fra ' + missing.join(', ') + '.</b> Ligger de på en gammel telefon, en harddisk eller i en sky?</p>';
            if (r.undated) html += '<p class="k-help">' + n(r.undated) + ' billeder og videoer har ingen dato, jeg kunne læse. De tæller ikke med her.</p>';
            html += '</details>';
        }

        html += '<section class="k-card bu-paid" data-nofold><h2 class="k-card-title">➕ Skal jeg rydde op for dig?</h2>' +
            '<p>I den <b>udvidede udgave</b> sorterer jeg <b>alle</b> dine billeder og videoer efter år og måned, fjerner dubletter og skrammel, ' +
            'finder personerne på billederne og laver et <b>galleri</b>, hele familien kan se på telefonen – plus en backup, så intet går tabt.</p>' +
            '<p>Se hvordan det ser ud: <a href="https://gallery.itmartin.dk/demo" target="_blank" rel="noopener">eksempel-galleriet</a> – koden er <b>Demo2026</b>.</p>' +
            '<a class="k-btn k-btn-primary" href="/#chat">💬 Spørg om oprydning</a></section>';
        show(box, html);
        box.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function status(box, text, done, totalCount) {
        show(box, '<section class="k-card" data-nofold><p><b>' + text + '</b> ' + (totalCount ? n(done) + ' af ' + n(totalCount) : '') + '</p>' +
            '<p class="k-help">Alt sker her på din computer. Mange tusinde billeder kan tage et minut eller to.</p></section>');
    }

    async function handle(files) {
        var box = document.getElementById('bi-out');
        if (!box) return;
        if (!files.length) {
            show(box, '<section class="k-card bu-error" data-nofold><p>Mappen var tom – eller din browser kan ikke åbne mapper. Prøv Chrome eller Edge på en computer.</p></section>');
            return;
        }
        var r = await D.analyse(files, function (t, d, c) { status(box, t, d, c); });
        if (!r.photos && !r.videos) {
            show(box, '<section class="k-card bu-error" data-nofold><p>Jeg fandt ingen billeder eller videoer i den mappe. Vælg mappen, der indeholder dine billeder.</p></section>');
            return;
        }
        render(r, box);
    }

    document.addEventListener('change', function (e) {
        if (e.target && e.target.id === 'bi-folder') handle(Array.prototype.slice.call(e.target.files));
    });
    ['dragover', 'drop'].forEach(function (ev) {
        document.addEventListener(ev, function (e) {
            if (!(e.target && e.target.closest && e.target.closest('#bi-drop'))) return;
            e.preventDefault();
            if (ev !== 'drop') return;
            var out = [], entries = Array.prototype.map.call(e.dataTransfer.items || [], function (i) { return i.webkitGetAsEntry && i.webkitGetAsEntry(); }).filter(Boolean);
            Promise.all(entries.map(function (en) { return walk(en, '', out); })).then(function () { handle(out); });
        });
    });

})();
