// Indbakke - the page: Scan (background, progress every 2 s), then the overview: Det haster nu (with Læst + frist / Klaret),
// Skal svares, and the senders with a category filter. All text in Danish; meaning in words, not only colour.
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }

    // Lys/Mørk in the header - same button and memory as ElPriser and Temperatur.
    document.addEventListener('click', function (e) {
        if (!e.target.closest || !e.target.closest('#themeToggleBtn')) return;
        var next = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', next);
        try { localStorage.setItem('indbakke_theme', next); } catch (err) { }
    });

    if (!$('ib-scan')) return;   // the login page

    var NAMES = {
        DigitalPost: 'Digital Post', Haster: 'Haster', SkalSvares: 'Skal svares', Kvitteringer: 'Kvitteringer',
        Nyhedsbreve: 'Nyhedsbreve', Reklame: 'Reklame', Notifikationer: 'Notifikationer', Andet: 'Andet'
    };
    var filter = 'alle', data = null, poll = null;

    function esc(s) { var d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; }
    function day(iso) { return new Date(iso).toLocaleDateString('da-DK', { day: 'numeric', month: 'short', year: 'numeric' }); }
    function status(t) { $('ib-status').textContent = t || ''; }
    function num(n) { return Number(n).toLocaleString('da-DK'); }

    function api(method, url, body) {
        return fetch(url, {
            method: method, headers: body ? { 'Content-Type': 'application/json' } : {},
            body: body ? JSON.stringify(body) : undefined, credentials: 'same-origin'
        }).then(function (r) {
            if (r.status === 401) { location.reload(); throw new Error('login'); }
            return r.json();
        });
    }

    // Where to read the letter itself: the notice only says "du har fået post".
    function postButton(domain) {
        if (/e-boks/.test(domain)) return '<a class="k-btn k-btn-sm" href="https://www.e-boks.dk" target="_blank" rel="noopener">Åbn e-Boks</a>';
        if (/mit\.dk|digitalpost|borger\.dk/.test(domain)) return '<a class="k-btn k-btn-sm" href="https://mit.dk" target="_blank" rel="noopener">Åbn mit.dk</a>';
        return '';
    }
    function gmailButton(id) {
        return '<a class="k-btn k-btn-sm" href="https://mail.google.com/mail/u/0/#search/rfc822msgid%3A' + encodeURIComponent(id) + '" target="_blank" rel="noopener">Åbn i Gmail</a>';
    }

    function tag(i) {
        if (i.dageTilbage != null) {
            if (i.dageTilbage < 0) return '<span class="ib-tag ib-tag-over">Frist overskredet ' + (-i.dageTilbage) + ' dage</span>';
            if (i.dageTilbage === 0) return '<span class="ib-tag ib-tag-over">Frist i dag</span>';
            if (i.dageTilbage <= 7) return '<span class="ib-tag ib-tag-soon">Frist om ' + i.dageTilbage + (i.dageTilbage === 1 ? ' dag' : ' dage') + '</span>';
            return '<span class="ib-tag ib-tag-ok">Frist ' + day(i.frist) + '</span>';
        }
        if (i.status === 'Ny') return '<span class="ib-tag ib-tag-new">Ikke åbnet endnu</span>';
        return '<span class="ib-tag ib-tag-ok">Læst – ingen frist</span>';
    }

    function renderNow(items) {
        var ul = $('ib-now');
        $('ib-now-card').hidden = false;
        if (!items.length) { ul.innerHTML = '<li>Intet haster lige nu.</li>'; return; }
        ul.innerHTML = items.map(function (i) {
            var m = i.mail;
            return '<li data-id="' + esc(m.id) + '">' +
                '<div class="ib-top"><span class="ib-from">' + esc(m.fra) + '</span><span class="ib-date">' + day(m.dato) + '</span></div>' +
                '<div class="ib-subject">' + tag(i) + esc(m.emne) + '</div>' +
                (i.note ? '<div class="ib-sub">Din note: ' + esc(i.note) + '</div>' : '') +
                '<div class="ib-sub">' + NAMES[m.kategori] + '</div>' +
                '<div class="ib-actions">' + postButton(m.domaene) + gmailButton(m.id) +
                '<button type="button" class="k-btn k-btn-sm" data-act="vis-laest">' + (i.status === 'Ny' ? 'Læst – sæt frist' : 'Ret frist') + '</button>' +
                '<button type="button" class="k-btn k-btn-sm k-btn-primary" data-act="klaret">Klaret</button></div>' +
                '<form class="ib-read" hidden>' +
                '<label class="k-label">Frist <input class="k-input" type="date" name="frist" value="' + (i.frist || '') + '"></label>' +
                '<span class="ib-quick"><button type="button" class="k-btn k-btn-sm" data-days="7">1 uge</button> ' +
                '<button type="button" class="k-btn k-btn-sm" data-days="14">2 uger</button> ' +
                '<button type="button" class="k-btn k-btn-sm" data-days="28">4 uger</button></span>' +
                '<input class="k-input ib-note" name="note" maxlength="80" placeholder="Kort note, fx Møde på jobcentret" value="' + esc(i.note) + '">' +
                '<button type="submit" class="k-btn k-btn-sm k-btn-primary">Gem</button></form>' +
                '</li>';
        }).join('');
    }

    function renderReply(items) {
        $('ib-reply-card').hidden = false;
        $('ib-reply').innerHTML = items.length ? items.map(function (m) {
            return '<li><div class="ib-top"><span class="ib-from">' + esc(m.fra) + '</span><span class="ib-date">' + day(m.dato) + '</span></div>' +
                '<div class="ib-subject">' + esc(m.emne) + '</div><div class="ib-actions">' + gmailButton(m.id) + '</div></li>';
        }).join('') : '<li>Ingen venter på svar.</li>';
    }

    function renderFilter() {
        var cats = ['alle'].concat(Object.keys(NAMES));
        $('ib-filter').innerHTML = cats.map(function (c) {
            var n = c === 'alle' ? data.afsendere.length : data.afsendere.filter(function (s) { return s.kategori === c; }).length;
            if (c !== 'alle' && !n) return '';
            return '<button type="button" class="k-btn k-btn-sm' + (c === filter ? ' is-on' : '') + '" aria-pressed="' + (c === filter) + '" data-cat="' + c + '">' +
                (c === 'alle' ? 'Alle' : NAMES[c]) + ' (' + n + ')</button>';
        }).join('');
    }

    function renderSenders() {
        $('ib-senders-card').hidden = false;
        renderFilter();
        var list = data.afsendere.filter(function (s) { return filter === 'alle' || s.kategori === filter; });
        $('ib-senders').innerHTML = list.slice(0, 150).map(function (s) {
            return '<li><div class="ib-top"><span class="ib-from">' + esc(s.navn) + '</span><span class="ib-count">' + num(s.antal) + ' mails</span></div>' +
                '<div class="ib-sub">' + esc(s.adresse) + ' · ' + NAMES[s.kategori] + ' · ' + num(s.ulaeste) + ' ulæste · senest ' + day(s.nyeste) + '</div>' +
                (s.kanAfmeldes ? '<div class="ib-sub"><b>Kan afmeldes' + (s.etTryk ? ' med ét tryk' : '') + '</b></div>' : '') + '</li>';
        }).join('') || '<li>Ingen afsendere her.</li>';
    }

    function renderCats() {
        var k = data.kategorier;
        $('ib-cats').innerHTML = Object.keys(NAMES).filter(function (c) { return k[c]; }).map(function (c) {
            return '<span class="ib-cat">' + NAMES[c] + ' <b>' + num(k[c]) + '</b></span>';
        }).join('');
    }

    function load() {
        return api('GET', '/api/oversigt').then(function (d) {
            if (d.tom) { status('Tryk på Scan min mail for at begynde.'); return; }
            data = d;
            status(num(d.ialt) + ' mails de sidste ' + d.maaneder + ' måneder · ' + num(d.ulaeste) + ' ulæste · scannet ' +
                new Date(d.scannet).toLocaleString('da-DK', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }));
            renderCats(); renderNow(d.hasterNu); renderReply(d.skalSvares); renderSenders();
        });
    }

    function watch() {
        api('GET', '/api/status').then(function (s) {
            if (s.koerer) {
                $('ib-scan').disabled = true;
                status(s.ialt ? 'Læser ' + num(s.faerdige) + ' af ' + num(s.ialt) + ' mails …' : 'Forbinder til Gmail …');
                poll = setTimeout(watch, 2000);
                return;
            }
            $('ib-scan').disabled = false;
            if (s.fejl) { status(s.fejl); return; }
            if (!s.opsat) { status('Gmail er ikke sat op endnu.'); return; }
            load();
        });
    }

    $('ib-scan').addEventListener('click', function () {
        api('POST', '/api/scan?maaneder=' + $('ib-months').value).then(function (r) {
            if (r.fejl) { status(r.fejl); return; }
            clearTimeout(poll); watch();
        });
    });

    $('ib-filter').addEventListener('click', function (e) {
        var b = e.target.closest('[data-cat]'); if (!b) return;
        filter = b.getAttribute('data-cat'); renderSenders();
    });

    $('ib-now').addEventListener('click', function (e) {
        var li = e.target.closest('li[data-id]'); if (!li) return;
        var id = li.getAttribute('data-id'), form = li.querySelector('.ib-read');
        var act = e.target.closest('[data-act]'), quick = e.target.closest('[data-days]');
        if (quick) {
            var d = new Date(); d.setDate(d.getDate() + parseInt(quick.getAttribute('data-days'), 10));
            form.frist.value = d.toISOString().slice(0, 10);
            return;
        }
        if (!act) return;
        if (act.getAttribute('data-act') === 'vis-laest') { form.hidden = !form.hidden; return; }
        if (act.getAttribute('data-act') === 'klaret') api('POST', '/api/klaret', { id: id }).then(load);
    });

    $('ib-now').addEventListener('submit', function (e) {
        e.preventDefault();
        var li = e.target.closest('li[data-id]'), f = e.target;
        api('POST', '/api/laest', { id: li.getAttribute('data-id'), frist: f.frist.value || null, note: f.note.value }).then(load);
    });

    watch();
})();
