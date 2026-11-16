// Kolibri Hjemmeside - one page, two doors (2026-11-09).
// Martin (hjemmeside.itmartin.dk): overview of every homepage -> set-up / edit. The owner (<their domain>/ret): only their site.
(function () {
    'use strict';
    var $ = function (id) { return document.getElementById(id); };
    var dor = null, site = null, openPages = {};
    var APP = 'https://hjemmeside.itmartin.dk';

    function show(id) {
        ['venter', 'login', 'oversigt', 'rediger'].forEach(function (s) { $(s).hidden = s !== id; });
        window.scrollTo(0, 0);
    }

    function api(method, url, body) {
        var opt = { method: method, headers: {}, credentials: 'same-origin' };
        if (body instanceof FormData) opt.body = body;
        else if (body !== undefined) { opt.headers['Content-Type'] = 'application/json'; opt.body = JSON.stringify(body); }
        return fetch(url, opt).then(function (r) {
            return r.text().then(function (t) {
                var data = null; try { data = t ? JSON.parse(t) : null; } catch (e) { }
                if (!r.ok) throw new Error((data && data.fejl) || (r.status === 401 ? 'Du er ikke logget ind.' : 'Noget gik galt. Prøv igen.'));
                return data;
            });
        });
    }

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text !== undefined) e.textContent = text;
        return e;
    }

    function btn(text, cls, onClick) {
        var b = el('button', 'k-btn k-btn-sm' + (cls ? ' ' + cls : ''), text);
        b.type = 'button';
        b.addEventListener('click', onClick);
        return b;
    }

    var msgTimer = null;
    function ok(text) {
        $('fejl').hidden = true;
        $('besked').textContent = text; $('besked').hidden = false;
        clearTimeout(msgTimer); msgTimer = setTimeout(function () { $('besked').hidden = true; }, 3000);
    }
    function fail(e) { $('besked').hidden = true; $('fejl').textContent = e.message || e; $('fejl').hidden = false; }

    function mb(bytes) { return bytes < 1048576 ? Math.round(bytes / 1024) + ' KB' : (bytes / 1048576).toFixed(bytes < 10485760 ? 1 : 0) + ' MB'; }
    function when(iso) {
        var d = new Date(iso), days = Math.floor((Date.now() - d) / 86400000);
        if (days <= 0) return 'i dag ' + d.toLocaleTimeString('da-DK', { hour: '2-digit', minute: '2-digit' });
        if (days === 1) return 'i går';
        return d.toLocaleDateString('da-DK', { day: 'numeric', month: 'short', year: d.getFullYear() === new Date().getFullYear() ? undefined : 'numeric' });
    }

    // ---- start ----

    function start() {
        var side = new URLSearchParams(location.search).get('side');
        api('GET', '/api/dor' + (side ? '?side=' + encodeURIComponent(side) : '')).then(function (d) {
            dor = d;
            if (d.egetDomaene) {
                // On the customer's domain the app's own links (Om, Hjælp) live on the app's address.
                document.querySelectorAll('.k-footer a[href^="/"]').forEach(function (a) { a.href = APP + a.getAttribute('href'); });
                $('hjemLink').href = '/ret';
                $('brandNavn').textContent = d.navn;
            }
            if (d.side && (d.ejer || d.martin)) return openSite(d.side);
            if (d.martin) return overview();
            $('loginTitel').textContent = d.side ? 'Ret ' + d.navn : 'Kolibri Hjemmeside';
            $('loginLead').textContent = d.fuldHjaelp
                ? 'Martin retter din side for dig. Skriv til ham, hvad der skal ændres.'
                : d.side ? 'Her retter du tekst og billeder på din side. Skriv den kode, du har fået af Martin.'
                    : 'Martins side til kunders hjemmesider. Skriv din kode.';
            show('login');
            $('kode').focus();
        }).catch(function () { show('login'); });
    }

    $('loginForm').addEventListener('submit', function (ev) {
        ev.preventDefault();
        $('loginFejl').hidden = true;
        api('POST', '/api/login', { kode: $('kode').value, side: dor && dor.side }).then(function () {
            $('kode').value = '';
            start();
        }).catch(function (e) { $('loginFejl').textContent = e.message; $('loginFejl').hidden = false; });
    });

    $('logud').addEventListener('click', function () { api('POST', '/api/logout').then(function () { location.reload(); }); });

    // ---- Martin: overview ----

    function overview() {
        site = null;
        history.replaceState(null, '', location.pathname);
        api('GET', '/api/sites').then(function (list) {
            show('oversigt');
            var month = 0, live = 0;
            list.forEach(function (s) { month += s.pris; if (s.live) live++; });
            var tal = $('tal'); tal.innerHTML = '';
            [[list.length, 'hjemmesider'], [live, 'live på eget domæne'], [month + ' kr', 'om måneden i alt']].forEach(function (t) {
                var b = el('div', 'hj-tal-boks'); b.appendChild(el('b', '', String(t[0]))); b.appendChild(el('span', '', t[1])); tal.appendChild(b);
            });
            var box = $('liste'); box.innerHTML = '';
            $('tomListe').hidden = list.length > 0;
            list.forEach(function (s) { box.appendChild(siteCard(s)); });
        }).catch(function () { show('login'); });
    }

    function siteCard(s) {
        var c = el('div', 'k-card hj-site');
        var head = el('div', 'hj-site-head');
        head.appendChild(el('b', 'hj-site-navn', s.navn));
        head.appendChild(el('span', 'k-tag', s.pakkeNavn));
        head.appendChild(el('span', 'k-tag' + (s.live ? ' hj-live' : ''), s.live ? 'Live' : 'Kun prøve'));
        c.appendChild(head);

        var dl = el('dl', 'hj-info');
        function row(k, v) { dl.appendChild(el('dt', '', k)); dl.appendChild(el('dd', '', v)); }
        row('Adresse', s.live ? s.domaener.join(', ') : s.preview.replace('https://', ''));
        row('Pris', (s.pris === 0 ? 'Gratis' : s.pris + ' kr/md') + (s.egenPris ? ' (aftalt, normalt ' + s.normalPris + ')' : ''));
        row('Sidst ændret', when(s.aendret) + ' · ' + s.sider + ' sider');
        row('Billeder', mb(s.brugt) + ' af ' + mb(s.plads));
        row('Kundens kode', s.ejerRetter ? 'Ja – kunden kan rette selv' : s.pakke === 'Fuld' ? 'Fuld hjælp – kun du retter' : 'Ingen kode endnu');
        row('Oprettet', when(s.oprettet));
        c.appendChild(dl);
        if (s.note) c.appendChild(el('p', 'hj-note', s.note));

        var bar = el('div', 'hj-bar');
        bar.appendChild(btn('Ret', 'k-btn-primary', function () { openSite(s.id); }));
        bar.appendChild(btn('Opsætning', '', function () { openSite(s.id, true); }));
        var see = el('a', 'k-btn k-btn-sm', 'Se siden'); see.href = s.live || s.preview; see.target = '_blank'; see.rel = 'noopener';
        bar.appendChild(see);
        c.appendChild(bar);
        return c;
    }

    $('nyKnap').addEventListener('click', function () { $('nyBoks').open = true; $('nyNavn').focus(); });

    $('nyForm').addEventListener('submit', function (ev) {
        ev.preventDefault();
        $('nyFejl').hidden = true;
        api('POST', '/api/sites', {
            navn: $('nyNavn').value, kortnavn: $('nyKort').value || null, pakke: $('nyPakke').value,
            kode: $('nyKode').value || null, domaene: $('nyDomaene').value || null,
        }).then(function (s) {
            $('nyForm').reset(); $('nyBoks').open = false;
            openSite(s.id, true);
        }).catch(function (e) { $('nyFejl').textContent = e.message; $('nyFejl').hidden = false; });
    });

    $('tilbage').addEventListener('click', overview);

    // ---- one site ----

    function openSite(id, setUp) {
        return api('GET', '/api/sites/' + encodeURIComponent(id)).then(function (s) {
            var first = !site || site.id !== s.id;
            site = s;
            if (first) { openPages = {}; openPages[s.sider[0].slug] = true; }
            if (dor && dor.martin && !dor.egetDomaene) history.replaceState(null, '', '?side=' + encodeURIComponent(s.id));
            show('rediger');
            render(setUp);
        }).catch(fail);
    }

    function reload() { return openSite(site.id); }

    function render(setUp) {
        var s = site, martin = dor && dor.martin;
        $('sideNavn').textContent = s.navn;
        $('tilbage').hidden = !martin || dor.egetDomaene;
        $('seSiden').href = dor.egetDomaene ? '/' : (s.live || s.preview);
        $('fortryd').disabled = s.fortryd === 0;
        $('fortryd').textContent = s.fortryd ? 'Fortryd (' + s.fortryd + ')' : 'Fortryd';
        $('plads').textContent = 'Billeder: ' + mb(s.brugt) + ' af ' + mb(s.plads) + ' brugt. Billeder bliver gjort mindre, når du lægger dem op.';
        $('zip').href = '/api/sites/' + s.id + '/zip';

        $('opsaetBoks').hidden = !martin;
        if (martin && s.info) fillSetUp(s);
        if (setUp) $('opsaetBoks').open = true;

        var box = $('sider'); box.innerHTML = '';
        s.sider.forEach(function (p, i) { box.appendChild(pageCard(p, i, s.sider.length)); });
    }

    function fillSetUp(s) {
        $('oNavn').value = s.navn; $('oUnder').value = s.undertitel; $('oFarve').value = s.farve;
        $('oTlf').value = s.telefon; $('oMail').value = s.mail;
        $('oDomaener').value = s.info.domaener.join('\n');
        $('oPakke').value = s.pakke;
        $('oPris').value = s.info.egenPris ? s.info.pris : '';
        $('oNote').value = s.info.note;
        $('oKode').value = '';
        $('oKodeInfo').textContent = s.info.harKode ? 'Kunden har en kode. Skriv en ny her for at skifte den (den gamle virker så ikke mere).' : 'Kunden har ingen kode endnu.';
    }

    $('opsaetForm').addEventListener('submit', function (ev) {
        ev.preventDefault();
        var pris = $('oPris').value.trim();
        api('PUT', '/api/sites/' + site.id + '/opsaet', {
            navn: $('oNavn').value, undertitel: $('oUnder').value, farve: $('oFarve').value, telefon: $('oTlf').value, mail: $('oMail').value,
            domaener: $('oDomaener').value.split('\n').map(function (x) { return x.trim(); }).filter(Boolean),
            pakke: $('oPakke').value, pris: pris === '' ? null : parseInt(pris, 10), note: $('oNote').value, nyKode: $('oKode').value || null,
        }).then(function () { ok('Opsætningen er gemt.'); return reload(); }).catch(fail);
    });

    $('oLogo').addEventListener('change', function () {
        var f = $('oLogo').files[0]; if (!f) return;
        var fd = new FormData(); fd.append('fil', f);
        api('POST', '/api/sites/' + site.id + '/logo', fd).then(function () { $('oLogo').value = ''; ok('Logoet er sat på.'); }).catch(fail);
    });

    $('fortryd').addEventListener('click', function () {
        api('POST', '/api/sites/' + site.id + '/fortryd').then(function () { ok('Fortrudt – siden er som før.'); return reload(); }).catch(fail);
    });

    $('nySideForm').addEventListener('submit', function (ev) {
        ev.preventDefault();
        api('POST', '/api/sites/' + site.id + '/sider', { titel: $('nySideTitel').value }).then(function (r) {
            $('nySideTitel').value = ''; openPages[r.slug] = true; ok('Siden er lavet.'); return reload();
        }).catch(fail);
    });

    function pageCard(p, index, count) {
        var d = el('details', 'k-card hj-fold hj-page');
        d.open = !!openPages[p.slug];
        d.addEventListener('toggle', function () { openPages[p.slug] = d.open; });
        var sum = el('summary');
        sum.appendChild(el('b', '', p.titel));
        sum.appendChild(el('span', 'k-muted', p.forside ? ' – forsiden' : p.iMenu ? ' – i menuen' : ' – skjult (ikke i menuen)'));
        d.appendChild(sum);
        var body = el('div', 'hj-pad k-stack');
        var url = '/api/sites/' + site.id;

        // The page itself: name, menu, order.
        var bar = el('div', 'hj-bar');
        bar.appendChild(btn('Skift navn', '', function () {
            var t = prompt('Sidens nye navn:', p.titel); // a plain prompt: one field, nothing to design
            if (t && t.trim() && t !== p.titel) api('PUT', url + '/sider/' + p.slug, { titel: t }).then(reload).catch(fail);
        }));
        if (!p.forside) {
            bar.appendChild(btn(p.iMenu ? 'Skjul i menuen' : 'Vis i menuen', '', function () {
                api('PUT', url + '/sider/' + p.slug, { iMenu: !p.iMenu }).then(reload).catch(fail);
            }));
            if (index > 1) bar.appendChild(btn('Flyt op', '', function () { api('POST', url + '/sider/' + p.slug + '/flyt', { retning: -1 }).then(reload).catch(fail); }));
            if (index < count - 1) bar.appendChild(btn('Flyt ned', '', function () { api('POST', url + '/sider/' + p.slug + '/flyt', { retning: 1 }).then(reload).catch(fail); }));
            bar.appendChild(btn('Slet siden', 'k-btn-danger', function () {
                if (confirm('Slette siden "' + p.titel + '"? Du kan fortryde bagefter.'))
                    api('DELETE', url + '/sider/' + p.slug).then(function () { ok('Siden er slettet. Tryk Fortryd, hvis det var en fejl.'); return reload(); }).catch(fail);
            }));
        }
        body.appendChild(bar);

        p.bokse.forEach(function (b, i) { body.appendChild(blockBox(b, i, p.bokse.length, p)); });

        // Add a box: text, or a picture straight from the camera.
        var add = el('div', 'hj-bar hj-add');
        add.appendChild(btn('Tilføj tekst', '', function () {
            api('POST', url + '/sider/' + p.slug + '/tekst', { tekst: 'Skriv her.' }).then(reload).catch(fail);
        }));
        add.appendChild(pictureButton('Tilføj billede', p.slug, null));
        body.appendChild(add);

        d.appendChild(body);
        return d;
    }

    function pictureButton(text, slug, blockId) {
        var label = el('label', 'k-btn k-btn-sm', text);
        var input = el('input'); input.type = 'file'; input.accept = 'image/*'; input.hidden = true;
        input.addEventListener('change', function () {
            var f = input.files[0]; if (!f) return;
            label.textContent = 'Lægger op …';
            var fd = new FormData(); fd.append('fil', f); if (blockId) fd.append('boks', blockId);
            api('POST', '/api/sites/' + site.id + '/sider/' + slug + '/billede', fd)
                .then(function () { ok('Billedet er lagt op.'); return reload(); })
                .catch(function (e) { label.textContent = text; fail(e); });
        });
        label.appendChild(input);
        return label;
    }

    function blockBox(b, i, count, page) {
        var box = el('div', 'hj-block');
        var url = '/api/sites/' + site.id + '/bokse/' + b.id;
        var area;
        if (b.billede) {
            var img = el('img'); img.src = b.url; img.alt = b.tekst; img.loading = 'lazy';
            box.appendChild(img);
            area = el('input', 'k-input'); area.value = b.tekst; area.maxLength = 200; area.placeholder = 'Linje under billedet (kan være tom)';
        } else {
            area = el('textarea', 'k-textarea k-input'); area.value = b.tekst; area.rows = Math.min(14, Math.max(3, b.tekst.split('\n').length + 1));
            area.maxLength = 4000;
        }
        box.appendChild(area);
        var bar = el('div', 'hj-bar');
        var save = btn('Gem', 'k-btn-primary', function () {
            api('PUT', url, { tekst: area.value }).then(function () { b.tekst = area.value; save.disabled = true; ok('Gemt.'); site.fortryd++; render(); }).catch(fail);
        });
        save.disabled = true;
        area.addEventListener('input', function () { save.disabled = area.value === b.tekst; });
        bar.appendChild(save);
        if (b.billede) bar.appendChild(pictureButton('Skift billede', page.slug, b.id));
        if (i > 0) bar.appendChild(btn('Op', '', function () { api('POST', url + '/flyt', { retning: -1 }).then(reload).catch(fail); }));
        if (i < count - 1) bar.appendChild(btn('Ned', '', function () { api('POST', url + '/flyt', { retning: 1 }).then(reload).catch(fail); }));
        bar.appendChild(btn('Fjern', 'k-btn-ghost', function () {
            api('DELETE', url).then(function () { ok('Fjernet. Tryk Fortryd, hvis det var en fejl.'); return reload(); }).catch(fail);
        }));
        box.appendChild(bar);
        return box;
    }

    $('themeBtn').addEventListener('click', function () {
        var t = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', t);
        try { localStorage.setItem('hj_theme', t); } catch (e) { }
    });
    $('kYear').textContent = new Date().getFullYear();

    start();
})();
