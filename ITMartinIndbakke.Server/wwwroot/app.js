// Indbakke - the page: Scan (background, progress every 2 s), then the overview: Det haster nu (with Læst + frist / Klaret),
// Skal svares, and the senders with a category filter. All text in Danish; meaning in words, not only colour.
(function () {
    'use strict';
    function $(id) { return document.getElementById(id); }

    // Lys/Mørk in the header - same button, word and memory as ElPriser and Temperatur: it says what you GET ("Mørk" in light mode).
    function sync() {
        var btn = $('themeToggleBtn');
        if (btn) btn.textContent = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'Mørk' : 'Lys';
    }
    document.addEventListener('click', function (e) {
        if (!e.target.closest || !e.target.closest('#themeToggleBtn')) return;
        var next = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', next);
        try { localStorage.setItem('indbakke_theme', next); } catch (err) { }
        sync();
    });
    sync();

    // Free guide: one mail tab at a time (remembered on this phone). One click = one function.
    var tabs = document.querySelectorAll('.ib-tabs [data-tab]');
    if (tabs.length) {
        var show = function (name) {
            tabs.forEach(function (t) { var on = t.getAttribute('data-tab') === name; t.classList.toggle('active', on); t.setAttribute('aria-selected', on ? 'true' : 'false'); });
            document.querySelectorAll('.ib-tab').forEach(function (p) { p.hidden = p.getAttribute('data-tab') !== name; });
        };
        var saved = ''; try { saved = localStorage.getItem('indbakke_mail') || ''; } catch (err) { }
        show(document.querySelector('.ib-tab[data-tab="' + saved + '"]') ? saved : 'gmail');
        tabs.forEach(function (t) {
            t.addEventListener('click', function () {
                var name = t.getAttribute('data-tab'); show(name);
                try { localStorage.setItem('indbakke_mail', name); } catch (err) { }
            });
        });
    }

    if (!$('ib-scan')) return;   // the login page

    var NAMES = {
        DigitalPost: 'Digital Post', Haster: 'Haster', SkalSvares: 'Skal svares', Kvitteringer: 'Kvitteringer',
        Nyhedsbreve: 'Nyhedsbreve', Reklame: 'Reklame', Notifikationer: 'Notifikationer', Andet: 'Andet'
    };
    var filter = 'alle', data = null, poll = null, nowAll = false, senderShow = 20;
    var NOW_FIRST = 5;   // a short first page - the rest behind Vis alle

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

    function renderBaseline(iso) {
        var p = $('ib-baseline'), b = $('ib-alldone');
        b.textContent = 'Alt før i dag er klaret'; b.removeAttribute('data-sure');
        if (!iso) { p.hidden = true; return; }
        p.hidden = false;
        p.innerHTML = 'Alt før ' + new Date(iso).toLocaleString('da-DK', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }) +
            ' er klaret – kun breve, der kommer efter, står her (og dem, du har sat en frist på). ' +
            '<button type="button" class="k-btn k-btn-sm" id="ib-baseline-undo">Fortryd</button>';
    }

    function renderNow(items) {
        var ul = $('ib-now');
        $('ib-now-card').hidden = false;
        var more = $('ib-now-more');
        if (!items.length) { ul.innerHTML = '<li>Intet haster lige nu.</li>'; more.hidden = true; return; }
        more.hidden = items.length <= NOW_FIRST;
        more.textContent = nowAll ? 'Vis færre' : 'Vis alle ' + items.length;
        ul.innerHTML = (nowAll ? items : items.slice(0, NOW_FIRST)).map(function (i) {
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
        $('ib-senders-more').hidden = list.length <= senderShow;
        $('ib-senders').innerHTML = list.slice(0, senderShow).map(function (s) {
            return '<li><div class="ib-top"><span class="ib-from">' + esc(s.navn) + '</span><span class="ib-count">' + num(s.antal) + ' mails</span></div>' +
                '<div class="ib-sub">' + esc(s.adresse) + ' · ' + NAMES[s.kategori] + ' · ' + num(s.ulaeste) + ' ulæste · senest ' + day(s.nyeste) + '</div>' +
                (s.kanAfmeldes ? '<div class="ib-sub"><b>Kan afmeldes' + (s.etTryk ? ' med ét tryk' : '') + '</b></div>' : '') +
                '<div class="ib-actions"><button type="button" class="k-btn k-btn-sm" data-ryd="' + esc(s.adresse) + '">Ryd op</button></div>' +
                '<div class="ib-panel" hidden></div></li>';
        }).join('') || '<li>Ingen afsendere her.</li>';
    }

    var UNSUB = {
        OneClick: 'Afmeldes med ét tryk – du skal ikke ind på nogen side.',
        Mail: 'Afmeldes ved at sende en kort afmeldings-mail fra din Gmail.',
        Page: 'Afsenderen vil have dig ind på deres side for at afmelde – den åbner vi for dig.'
    };

    // Ryd op: first SHOW what will happen (count, oldest, examples, what is kept and why), then the presses.
    function openPanel(li, adresse) {
        var panel = li.querySelector('.ib-panel');
        if (!panel.hidden) { panel.hidden = true; return; }
        panel.hidden = false; panel.textContent = 'Henter …';
        api('GET', '/api/afsender?adresse=' + encodeURIComponent(adresse)).then(function (p) {
            var h = '';
            if (p.slettes) {
                h += '<p><b>' + num(p.slettes) + ' mails kan slettes</b> – ældste fra ' + day(p.aeldste) + '.</p>';
                h += p.eksempler.map(function (e) { return '<p class="ib-ex">Fx: ' + esc(e) + '</p>'; }).join('');
                h += '<p>De lægges i papirkurven. Gmail gemmer dem i 30 dage, og du kan trykke Fortryd her.</p>';
            } else h += '<p>Ingen mails her kan slettes.</p>';
            p.beholdes.forEach(function (k) { h += '<p><b>' + num(k.antal) + ' bliver liggende</b> – ' + esc(k.hvorfor) + '.</p>'; });
            h += '<p>' + (p.afmeld ? UNSUB[p.afmeld] : 'Afsenderen tilbyder ingen afmelding.') + '</p>';
            h += '<div class="ib-actions">';
            if (p.slettes && p.afmeld) h += '<button type="button" class="k-btn k-btn-sm k-btn-primary" data-go="begge">Afmeld og slet ' + num(p.slettes) + '</button>';
            if (p.slettes) h += '<button type="button" class="k-btn k-btn-sm' + (p.afmeld ? '' : ' k-btn-primary') + '" data-go="slet">Kun slet</button>';
            if (p.afmeld) h += '<button type="button" class="k-btn k-btn-sm" data-go="afmeld">Kun afmeld</button>';
            h += '</div><p class="ib-done" aria-live="polite"></p>';
            panel.innerHTML = h;
            panel.setAttribute('data-adresse', p.adresse);
        }).catch(function () { panel.textContent = 'Kunne ikke hente afsenderen. Scan igen.'; });
    }

    function runCleanup(panel, mode) {
        var done = panel.querySelector('.ib-done');
        panel.querySelectorAll('[data-go]').forEach(function (b) { b.disabled = true; });
        done.textContent = 'Arbejder …';
        api('POST', '/api/afsender/ryd', { adresse: panel.getAttribute('data-adresse'), afmeld: mode !== 'slet', slet: mode !== 'afmeld' })
            .then(function (r) {
                if (r.fejl) { done.textContent = r.fejl; return; }
                done.innerHTML = esc(r.besked) + (r.aabn ? ' <a class="k-btn k-btn-sm" href="' + esc(r.aabn) + '" target="_blank" rel="noopener">Åbn afmeldingssiden</a>' : '');
                loadHistory();
                if (r.flyttet) setTimeout(load, 2500);   // the sender's numbers change
            }).catch(function () { done.textContent = 'Det gik galt. Prøv igen.'; });
    }

    function loadHistory() {
        api('GET', '/api/oprydning').then(function (list) {
            $('ib-history-card').hidden = !list.length;
            $('ib-history').innerHTML = list.map(function (a) {
                var what = [a.antal ? num(a.antal) + ' mails i papirkurven' : '', a.arkiv ? num(a.arkiv) + ' i arkivet' : '',
                    a.etiket ? 'Gmail-etiket: ' + a.etiket : '', a.afmeldt].filter(Boolean).join(' · ');
                return '<li><div class="ib-top"><span class="ib-from">' + esc(a.navn) + '</span><span class="ib-date">' + day(a.tid) + '</span></div>' +
                    '<div class="ib-sub">' + esc(what) + (a.fortrudt ? ' · <b>fortrudt</b>' : '') + '</div>' +
                    (a.kanFortrydes ? '<div class="ib-actions"><button type="button" class="k-btn k-btn-sm" data-undo="' + a.id + '">Fortryd</button></div>' : '') +
                    '<p class="ib-done" aria-live="polite"></p></li>';
            }).join('');
        });
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
            renderCats(); loadHistory(); renderBaseline(d.grundlinje); renderNow(d.hasterNu); renderReply(d.skalSvares); renderSenders();
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

    // Ryd alt det gamle: first the numbers, then one press.
    function oldChoice() {
        var mode = document.querySelector('input[name=ib-old-mode]:checked');
        return { foer: $('ib-old-before').value, maade: mode ? mode.value : 'SletReklame' };
    }
    function oldReset() { $('ib-old-result').hidden = true; }
    $('ib-old-before').addEventListener('change', oldReset);
    document.querySelectorAll('input[name=ib-old-mode]').forEach(function (r) { r.addEventListener('change', oldReset); });

    $('ib-old-preview').addEventListener('click', function () {
        var c = oldChoice(), box = $('ib-old-result');
        box.hidden = false; box.textContent = 'Tæller dine mails …';
        api('GET', '/api/gammelt?foer=' + c.foer + '&maade=' + c.maade).then(function (p) {
            if (p.fejl) { box.textContent = p.fejl; return; }
            if (!p.ialt) { box.innerHTML = '<p>Der ligger ingen mails i indbakken fra før ' + day(p.foer) + '.</p>'; return; }
            var h = '<p><b>' + num(p.ialt) + ' mails</b> i indbakken er fra før ' + day(p.foer) + '.</p>' +
                '<p><b>' + num(p.papirkurv) + '</b> lægges i papirkurven – Gmail gemmer dem i 30 dage, og du kan trykke Fortryd her.</p>';
            if (p.arkiv) h += '<p><b>' + num(p.arkiv) + '</b> flyttes til arkivet – ude af indbakken, men du kan altid finde dem i Gmail.</p>';
            if (p.stjerne) h += '<p><b>' + num(p.stjerne) + '</b> har stjerne og bliver liggende.</p>';
            h += '<div class="ib-actions"><button type="button" class="k-btn k-btn-primary" id="ib-old-go">Ryd op nu</button></div>' +
                '<p class="ib-done" aria-live="polite"></p>';
            box.innerHTML = h;
        }).catch(function () { box.textContent = 'Kunne ikke tælle. Prøv igen.'; });
    });

    $('ib-old-result').addEventListener('click', function (e) {
        if (e.target.id !== 'ib-old-go') return;
        var box = $('ib-old-result'), done = box.querySelector('.ib-done');
        e.target.disabled = true; done.textContent = 'Starter …';
        api('POST', '/api/gammelt', oldChoice()).then(function (r) {
            if (r.fejl) { done.textContent = r.fejl; e.target.disabled = false; return; }
            loadHistory();   // the Fortryd line is there from the start
            followJob(done);
        }).catch(function () { done.textContent = 'Det gik galt. Prøv igen.'; e.target.disabled = false; });
    });

    // A big job runs on the server; ask every 3 s until it is done, then show the result and scan again.
    function followJob(el) {
        api('GET', '/api/job').then(function (j) {
            el.textContent = j.besked || '';
            if (j.koerer) { setTimeout(function () { followJob(el); }, 3000); return; }
            loadHistory(); setTimeout(watch, 1500);
        }).catch(function () { setTimeout(function () { followJob(el); }, 5000); });
    }

    $('ib-senders-more').addEventListener('click', function () { senderShow += 30; renderSenders(); });
    $('ib-now-more').addEventListener('click', function () { nowAll = !nowAll; renderNow(data.hasterNu); });

    $('ib-senders').addEventListener('click', function (e) {
        var ryd = e.target.closest('[data-ryd]');
        if (ryd) { openPanel(ryd.closest('li'), ryd.getAttribute('data-ryd')); return; }
        var go = e.target.closest('[data-go]');
        if (go) runCleanup(go.closest('.ib-panel'), go.getAttribute('data-go'));
    });

    $('ib-history').addEventListener('click', function (e) {
        var b = e.target.closest('[data-undo]'); if (!b) return;
        var done = b.closest('li').querySelector('.ib-done');
        b.disabled = true; done.textContent = 'Starter …';
        api('POST', '/api/oprydning/fortryd', { id: b.getAttribute('data-undo') }).then(function (r) {
            if (r.fejl) { done.textContent = r.fejl; b.disabled = false; return; }
            followJob(done);
        });
    });

    $('ib-filter').addEventListener('click', function (e) {
        var b = e.target.closest('[data-cat]'); if (!b) return;
        filter = b.getAttribute('data-cat'); senderShow = 20; renderSenders();
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

    // The base line - two presses, no browser dialog: the first asks, the second sets "everything before now is handled".
    $('ib-alldone').addEventListener('click', function () {
        var b = $('ib-alldone');
        if (!b.hasAttribute('data-sure')) {
            b.setAttribute('data-sure', '1');
            b.textContent = 'Tryk igen: alt før nu er klaret';
            return;
        }
        api('POST', '/api/grundlinje').then(load);
    });
    $('ib-baseline').addEventListener('click', function (e) {
        if (e.target.id === 'ib-baseline-undo') api('POST', '/api/grundlinje/fortryd').then(load);
    });

    $('ib-now').addEventListener('submit', function (e) {
        e.preventDefault();
        var li = e.target.closest('li[data-id]'), f = e.target;
        api('POST', '/api/laest', { id: li.getAttribute('data-id'), frist: f.frist.value || null, note: f.note.value }).then(load);
    });

    watch();
})();
