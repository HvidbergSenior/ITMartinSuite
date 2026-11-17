// Snak - the whole page. Plain JavaScript, no framework.
// Since 2026-11-04 rooms: /r/<key> = one room (the key is the way in, only a name is asked), / = Martin's overview
// of all rooms (his code), or a short "open your link" page for everybody else.
(function () {
    'use strict';
    const $ = id => document.getElementById(id);
    const store = {
        get(k) { try { return localStorage.getItem(k) || ''; } catch { return ''; } },
        set(k, v) { try { localStorage.setItem(k, v); } catch { } },
    };

    const keyMatch = location.pathname.match(/^\/r\/([A-Za-z0-9_-]{24})\/?$/);
    const roomKey = keyMatch ? keyMatch[1] : '';
    const base = roomKey ? '/api/r/' + roomKey : '';
    let navn = store.get('snak_navn');
    let admin = false, alleRum = false, pushKey = '', endpoint = '', vaert = 'Martin', firma = 'ITMartin', room = null;
    // 2026-11-12 trin 2: the host writes as himself ("mig") or as the company ("firma") - remembered on this phone.
    let rolle = store.get('snak_rolle') === 'firma' ? 'firma' : 'mig', svarPaa = 0;
    let lastId = 0, tasks = [], editId = 0, live = null;
    const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const standalone = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
    const pushPossible = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    // Most people arrive from a Messenger link. That built-in browser can chat, but cannot install or
    // get notifications, so offer one button that opens Snak in the phone's real browser.
    const inApp = /FBAN|FBAV|FB_IAB|FBIOS|Messenger|Instagram/i.test(navigator.userAgent || '');
    if (inApp) {
        const android = /android/i.test(navigator.userAgent);
        const name = android ? 'Chrome' : 'Safari';
        const here = 'snak.itmartin.dk' + location.pathname;
        $('browserNavn').textContent = name; $('browserNavn2').textContent = name;
        $('aabnBrowser').href = android
            ? 'intent://' + here + '#Intent;scheme=https;package=com.android.chrome;S.browser_fallback_url=' + encodeURIComponent('https://' + here) + ';end'
            : 'x-safari-https://' + here;
        $('inApp').hidden = false;
    }

    async function api(method, url, body) {
        const r = await fetch(url, {
            method, credentials: 'same-origin',
            headers: body ? { 'Content-Type': 'application/json' } : {},
            body: body ? JSON.stringify(body) : undefined,
        });
        if (r.status === 404 && roomKey) { showOnly('vaek'); throw new Error('login'); }
        if (r.status === 403 && roomKey) {
            const d = await r.clone().json().catch(() => null);
            if (d && d.doer) { showLogin(); throw new Error('login'); }
        }
        if (r.status === 401) { boot(); throw new Error('login'); }
        const data = r.headers.get('content-type')?.includes('json') ? await r.json() : null;
        if (!r.ok) throw new Error((data && data.fejl) || 'Det gik ikke. Prøv igen.');
        return data;
    }

    function showOnly(id) {
        for (const s of ['login', 'forside', 'vaek', 'rumside', 'app']) $(s).hidden = s !== id;
        if (id !== 'app' && live) { live.close(); live = null; }
    }

    // ---------- start ----------

    async function boot() {
        try {
            if (roomKey) {
                const r = await fetch(base, { credentials: 'same-origin' });
                if (r.status === 404) return showOnly('vaek');
                room = await r.json();
                admin = !!room.admin; alleRum = !!room.alleRum; pushKey = room.pushKey || ''; vaert = room.vaert || 'Martin'; firma = room.firma || 'ITMartin';
                document.body.classList.add('wow');
                store.set('snak_rum', roomKey);   // a phone that opens "/" (home-screen icon) comes back here
                document.title = room.titel + ' · Snak';
                // In a group the admin is a participant too: he picks his own name at the door (2026-11-09).
                const member = n => (room.medlemmer || []).some(x => x.toLowerCase() === (n || '').toLowerCase());
                // 2026-11-12 family room: this phone's own name, or knock with the one it remembers (first phone takes it).
                if (room.laast && !admin) {
                    if (room.mitNavn) { navn = room.mitNavn; store.set('snak_navn', navn); return enter(); }
                    if (member(navn) && await knock(navn) === 'ind') return enter();
                    return showLogin();
                }
                if (admin) navn = room.gruppe ? (member(navn) ? navn : '') : vaert;
                if (!navn) return showLogin();
                return enter();
            }
            const me = await (await fetch('/api/me', { credentials: 'same-origin' })).json();
            admin = alleRum = !!me.admin; pushKey = me.pushKey || ''; vaert = me.vaert || 'Martin';
            if (admin) return showRooms();
            const last = store.get('snak_rum');
            // /?indgang always shows Martin's way in, also on a phone that remembers a room (2026-11-09: it forwarded him away).
            const indgang = new URLSearchParams(location.search).has('indgang');
            if (last && !indgang) { location.replace('/r/' + last); return; }
            showOnly('forside');
            if (indgang) { const d = document.querySelector('#forside details'); if (d) d.open = true; }
        } catch { showOnly(roomKey ? 'login' : 'forside'); }
    }

    function showLogin() {
        showOnly('login');
        $('loginTitel').textContent = room ? room.titel : 'Snak';
        // From "Skriv til mig" on itmartin.dk the name comes along (?navn=) - one tap, which also asks for notifications.
        const fra = new URLSearchParams(location.search).get('navn');
        $('loginNavn').value = navn || (fra || '').slice(0, 60);
        // Shared room: the names as big buttons - one tap goes in (and asks for notifications inside that tap).
        const box = $('loginNavne');
        box.textContent = '';
        const names = shared() ? room.medlemmer : [];
        box.hidden = !names.length;
        document.body.classList.toggle('gruppe', isGroup());
        for (const n of names) {
            const b = document.createElement('button'); b.type = 'button'; b.className = 'k-btn';
            const t = document.createElement('span'); t.className = 'navn-tekst'; t.textContent = n; b.append(avatar(n), t);
            b.onclick = () => { $('loginNavn').value = n; $('loginForm').requestSubmit(); };
            box.appendChild(b);
        }
        if (names.length) {
            $('loginLead').textContent = 'Her skriver I sammen og ser, hvem der gør hvad. Tryk på dit navn.';
            document.querySelector('label[for=loginNavn]').textContent = 'Eller skriv dit navn';
            document.querySelector('#loginNavn + .k-help').textContent = 'Det er det navn, de andre ser ved dine beskeder.';
            document.querySelector('#loginFejl + .k-help').innerHTML = 'Når du går ind, spørger telefonen, om Snak må give dig besked. Sig <b>Tillad</b>, så får du besked om nye opgaver og påmindelser.';
        }
        // A family room: only the names - no typed name, no Gå ind button (2026-11-12).
        const locked = !!(room && room.laast && !admin);
        $('loginFrit').hidden = locked; $('loginGaa').hidden = locked;
        $('loginNavn').required = !locked;
        if (locked) $('loginLead').textContent = 'Her skriver vi sammen og ser, hvem der gør hvad. Tryk på dit navn – så husker telefonen dig.';
        // A fixed group (a course team, a club): the door speaks to the group (2026-11-09).
        if (room && room.gruppe) {
            $('loginLead').textContent = 'Velkommen! Her skriver holdet sammen og deler dokumenter. ' + (names.length ? `I er ${names.length} på holdet – tryk på dit navn.` : 'Skriv dit navn.');
            document.querySelector('#loginNavn + .k-help').textContent = 'Det er det navn, de andre ser ved dine beskeder og filer.';
        }
    }

    // Group rooms (2026-11-09, user: "make it prettier", "more wow"): every person gets a round badge with initials,
    // in a colour of their own that stays the same on the door, in Holdet and in the chat.
    function initials(n) {
        const parts = String(n || '?').trim().split(/\s+/);
        return ((parts[0] || '?')[0] + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase();
    }
    function avatar(n, size) {
        let h = 0; for (const ch of String(n).toLowerCase()) h = (h * 31 + ch.charCodeAt(0)) % 360;
        const a = document.createElement('span'); a.className = 'avatar' + (size ? ' ' + size : ''); a.setAttribute('aria-hidden', 'true');
        a.style.setProperty('--h', String(h)); a.textContent = initials(n);
        return a;
    }
    function isGroup() { return !!(room && room.gruppe); }

    function shared() { return !!(room && room.medlemmer && room.medlemmer.length); }

    // 2026-11-12: in a shared room (family, Sonja's circle) everyone adds dates and tasks and changes their own.
    function isMember() { return (room && room.medlemmer || []).some(x => x.toLowerCase() === (navn || '').toLowerCase()); }
    function canAdd() { return admin || (shared() && !isGroup() && isMember()); }
    function isMineBy(t) { return !!t.by && t.by.toLowerCase() === (navn || '').toLowerCase(); }
    function mayChange(t) { return admin || isMineBy(t); }
    // 2026-11-17: in a shared room (not a course group) everyone closes tasks and says how; the host everywhere.
    function closesOwn() { return shared() && !isGroup(); }
    function mayClose() { return admin || (closesOwn() && isMember()); }
    function mayReopen(t) { return admin || (!!t.doneBy && t.doneBy.toLowerCase() === (navn || '').toLowerCase()); }

    $('loginForm').addEventListener('submit', async e => {
        e.preventDefault();
        $('loginFejl').hidden = true;
        // Ask for notifications straight away, inside the tap - iPhone only allows the question there.
        const perm = pushPossible && Notification.permission === 'default'
            ? Notification.requestPermission().catch(() => 'default') : Promise.resolve(null);
        const n = $('loginNavn').value.trim();
        if (!n) return;
        navn = n; store.set('snak_navn', n);
        await perm;
        if (room && room.laast && !admin && await knock(n) !== 'ind') return;
        enter();
    });

    // The door of a family room: the first phone takes the name; another phone waits until the room's admin says yes.
    async function knock(n) {
        $('doerVent').hidden = true;
        try {
            const r = await fetch(base + '/doer', {
                method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ navn: n, enhed: isIos ? 'iphone' : /android/i.test(navigator.userAgent) ? 'android' : 'computer' }),
            });
            const d = await r.json();
            if (d.resultat === 'ind') { if (d.navn) { navn = d.navn; store.set('snak_navn', navn); } return 'ind'; }
            const w = $('doerVent');
            w.textContent = d.resultat === 'venter'
                ? (n + ' er allerede i brug på en anden telefon. ' + vaert + ' har fået besked og kan godkende denne telefon. Prøv igen om lidt.'
                   + (isIos && standalone ? ' (Ikonet på hjemmeskærmen tæller som en ny telefon.)' : ''))
                : 'Kun navnene her i rummet kan komme ind.';
            w.hidden = false;
            return d.resultat;
        } catch { return 'fejl'; }
    }

    $('adminForm').addEventListener('submit', async e => {
        e.preventDefault();
        $('adminFejl').hidden = true;
        const perm = pushPossible && Notification.permission === 'default'
            ? Notification.requestPermission().catch(() => 'default') : Promise.resolve(null);
        try {
            const r = await fetch('/api/login', {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ kode: $('adminKode').value.trim() }),
            });
            const d = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(d.fejl || 'Det gik ikke. Prøv igen.');
            await perm;
            boot();
        } catch (err) {
            $('adminFejl').textContent = err.message;
            $('adminFejl').hidden = false;
        }
    });

    // ---------- Martin's overview of all rooms ----------

    const shortFmt = new Intl.DateTimeFormat('da-DK', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
    const dateFmt = new Intl.DateTimeFormat('da-DK', { day: 'numeric', month: 'long' });

    async function showRooms() {
        showOnly('rumside');
        $('hjemKnap2').hidden = standalone || inApp;
        $('hjemKnap2').onclick = () => { location.href = '/om'; };
        setupPush(false);
        await loadRooms();
    }

    async function loadRooms() {
        let list = [];
        try { list = await api('GET', '/api/rum'); } catch { return; }
        const box = $('rumListe');
        box.textContent = '';
        if (!list.length) {
            const e = document.createElement('div'); e.className = 'tom';
            e.innerHTML = '<strong>Ingen rum endnu</strong>Lav et rum ovenfor, og send linket til kunden.';
            box.appendChild(e);
            return;
        }
        for (const r of list) box.appendChild(roomCard(r));
    }

    function roomCard(r) {
        const c = document.createElement('article');
        c.className = 'k-card opgave rum' + (r.waiting ? ' fri' : r.doneAt ? ' done' : ' taget');
        const h = document.createElement('h3'); h.textContent = r.title; c.appendChild(h);
        const st = document.createElement('p'); st.className = 'status';
        st.textContent = (r.gruppe ? '👥 Gruppe · ' : '') +
            (r.waiting ? '● Venter på dit svar' : r.doneAt ? '✓ Klaret – slettes ' + dateFmt.format(new Date(r.deleteAt)) : '○ Ingen venter');
        c.appendChild(st);
        if (r.lastText) {
            const p = document.createElement('p'); p.className = 'note';
            p.textContent = r.lastName + ': ' + r.lastText; c.appendChild(p);
        }
        const when = document.createElement('p'); when.className = 'hvem';
        when.textContent = 'Sidst: ' + shortFmt.format(new Date(r.lastAt)); c.appendChild(when);
        const row = document.createElement('div'); row.className = 'k-row';
        const open = document.createElement('a'); open.className = 'k-btn k-btn-primary'; open.href = r.link.replace(/^https?:\/\/[^/]+/, ''); open.textContent = 'Åbn';
        row.append(open, copyButton(r.link, r.title));
        if (r.doneAt) {
            const re = document.createElement('button'); re.type = 'button'; re.className = 'k-btn k-btn-sm k-btn-ghost'; re.textContent = 'Åbn sagen igen';
            re.onclick = async () => { re.disabled = true; try { await api('POST', '/api/rum/' + r.id + '/genaabn'); } catch { } loadRooms(); };
            row.appendChild(re);
        }
        c.appendChild(row);
        return c;
    }

    // Share on a phone (the phone's own share menu), copy on a computer.
    function copyButton(link, title) {
        const b = document.createElement('button'); b.type = 'button'; b.className = 'k-btn';
        b.textContent = navigator.share ? 'Send linket' : 'Kopier linket';
        b.onclick = async () => {
            try {
                if (navigator.share) await navigator.share({ title: 'Snak med Martin', text: title, url: link });
                else { await navigator.clipboard.writeText(link); b.textContent = 'Kopieret'; setTimeout(() => { b.textContent = 'Kopier linket'; }, 1500); }
            } catch { }
        };
        return b;
    }

    $('nytRum').addEventListener('submit', async e => {
        e.preventDefault();
        try {
            const r = await api('POST', '/api/rum', { titel: $('rumTitel').value, medlemmer: $('rumMedlemmer').value, kode: $('rumKode').value, adresse: $('rumAdresse').value, gruppe: $('rumGruppe').checked });
            $('rumTitel').value = ''; $('rumMedlemmer').value = ''; $('rumKode').value = ''; $('rumAdresse').value = ''; $('rumGruppe').checked = false;
            const box = $('nytLink');
            box.textContent = '';
            const p = document.createElement('p'); p.textContent = 'Rummet "' + r.titel + '" er klar. Send linket til dem, der skal med:';
            const code = document.createElement('code'); code.textContent = r.nemLink || r.link;
            box.append(p, code, copyButton(r.nemLink || r.link, r.titel));
            box.hidden = false;
            loadRooms();
        } catch (err) { if (err.message !== 'login') alertBar(err.message); }
    });

    function alertBar(text) { const b = $('pushBar2'); b.textContent = text; b.className = 'bar fejl'; b.hidden = false; }

    $('logUd2').onclick = async () => {
        try { await fetch('/api/logout', { method: 'POST', credentials: 'same-origin' }); } catch { }
        location.href = '/';
    };

    function enter() {
        showOnly('app');
        $('rumNavn').textContent = room ? room.titel : 'Snak';
        $('hvem').textContent = navn;
        $('tilbage').hidden = !alleRum;
        $('tilbage').onclick = () => { location.href = '/'; };
        $('nytNavn').value = navn;
        $('hjemKnap').hidden = standalone || inApp;
        $('hjemKnap').onclick = () => show('mig');
        $('nyOpg').hidden = !canAdd();
        renderRolle();
        $('navnBoks').hidden = !!(room && room.laast && !admin);   // the name belongs to the phone in a family room
        $('tovBoks').hidden = admin || !(room && room.harKode);
        fillTil('');
        $('opgIntro').textContent = admin
            ? (shared() ? 'Læg opgaver ud her, og giv dem til en – eller lad den, der har tid, tage den. Alle får besked.'
                        : 'Læg opgaver ud her. Kunden ser dem og trykker "Jeg tager den". Du kan se, hvem der har taget hvad.')
            : shared() ? 'Her er dagens aftaler og opgaver. Står dit navn på en, er den din. Tryk "Jeg tager den" på en, der mangler nogen.'
                         + (canAdd() ? ' Du kan også selv lægge en dato eller opgave ind nederst – fx en fødselsdag.' : '')
                       : 'Her er det, der skal gøres. Tryk "Jeg tager den" på en opgave, så ved Martin, at du gør den.';
        $('omSlet').hidden = !!(room && room.gruppe);
        document.body.classList.toggle('gruppe', isGroup());
        klaretBar();
        renderRoster();
        $('holdAdmin').hidden = !(alleRum && room && room.gruppe);
        if (alleRum && room && room.gruppe) {
            const om = room.om || {};
            $('holdMedlemmer').value = (room.medlemmer || []).map(n => om[n] ? n + ' – ' + om[n] : n).join('\n');
            $('holdEmner').value = (room.emner || []).join('\n');
            $('holdLinks').value = (room.links || []).map(l => l.title + ' – ' + l.url).join('\n');
            $('holdKalender').value = room.kalender || '';
        }
        calTabs();
        // A course room opens on its calendar (the "front page"); a family room on its tasks; a customer on the chat.
        const tab = store.get('snak_tab') || (hasCal() ? 'kal' : shared() && !isGroup() ? 'opg' : 'chat');
        const ok = t => !$('tab' + t[0].toUpperCase() + t.slice(1)).hidden;
        show(['kal', 'ng', 'opg', 'fil', 'hold', 'dato'].includes(tab) && ok(tab) ? tab : hasCal() ? 'kal' : 'chat');
        $('opsBoks').hidden = !admin;
        loadMessages(); loadTasks(); loadFiles(); loadReads(); loadCalendar(); loadProfiles(); connect();
        setupPush(false).finally(reportSeen);
    }

    // Martin closes the case here; everybody sees that it is closed and when the room goes.
    function klaretBar() {
        const bar = $('klaretBar');
        bar.className = 'bar';
        bar.textContent = '';
        if (room && room.gruppe) { bar.hidden = true; return; }   // a group has no end
        if (room && room.klaret) {
            bar.append('Sagen er klaret. Rummet bliver slettet ' + dateFmt.format(new Date(room.sletDato)) + '. Skriver du igen, åbner sagen igen.');
            bar.hidden = false;
        } else if (admin && !shared()) {   // a shared room (Sonja) is not a case that closes
            const b = document.createElement('button'); b.type = 'button'; b.className = 'k-btn k-btn-sm'; b.textContent = 'Sagen er klaret';
            b.onclick = async () => {
                // Two taps: the first asks, the second closes.
                if (!b.dataset.sure) {
                    b.dataset.sure = '1'; b.textContent = 'Ja, sagen er klaret (slettes om 14 dage)'; b.className = 'k-btn k-btn-sm k-btn-primary';
                    setTimeout(() => { delete b.dataset.sure; b.textContent = 'Sagen er klaret'; b.className = 'k-btn k-btn-sm'; }, 4000);
                    return;
                }
                b.disabled = true;
                try { await api('POST', '/api/rum/' + room.id + '/klaret'); room = await api('GET', base); klaretBar(); } catch { b.disabled = false; }
            };
            bar.append(b);
            bar.hidden = false;
        } else bar.hidden = true;
    }

    // ---------- tabs ----------

    function show(which) {
        for (const [v, t] of [['chat', 'tabChat'], ['kal', 'tabKal'], ['ng', 'tabNg'], ['opg', 'tabOpg'], ['fil', 'tabFil'], ['hold', 'tabHold'], ['dato', 'tabDato'], ['mig', 'tabMig']]) {
            $('view' + v[0].toUpperCase() + v.slice(1)).hidden = v !== which;
            $(t).setAttribute('aria-selected', String(v === which));
        }
        if (which !== 'mig') store.set('snak_tab', which);
        const nb = $('naesteBar'); if (nb) nb.hidden = !nb.dataset.har || which === 'kal';
        if (which === 'mig') { pushStatus(); openHomeGuide(); if (admin) loadSetup(); }
        if (which === 'chat') reportSeen();
        if (which === 'fil') { store.set('snak_filer_set_' + roomKey, String(maxFileId())); renderFiles(); }
    }
    $('tabChat').onclick = () => show('chat');
    $('tabOpg').onclick = () => show('opg');
    $('tabKal').onclick = () => show('kal');
    $('tabNg').onclick = () => show('ng');
    $('tabFil').onclick = () => show('fil');
    $('tabHold').onclick = () => show('hold');
    $('tabDato').onclick = () => show('dato');
    $('tabMig').onclick = () => show('mig');

    // ---------- live ----------

    function connect() {
        if (live) live.close();
        live = new EventSource(base + '/live');
        // "hej" comes on every (re)connect: fetch whatever was missed while the line was down.
        live.addEventListener('hej', () => { loadMessages(true); loadTasks(); loadFiles(); loadReads(); loadCalendar(); });
        live.addEventListener('besked-ret', e => {
            const m = JSON.parse(e.data), i = msgs.findIndex(x => x.id === m.id);
            if (i >= 0) { msgs[i] = m; keepPlace(renderMessages); }
        });
        live.addEventListener('set', e => { reads = JSON.parse(e.data); renderMessages(); });
        live.addEventListener('profiler', e => { profiles = JSON.parse(e.data); if (!document.querySelector('.profil-form')) renderRoster(); });
        live.addEventListener('besked', e => addMessages([JSON.parse(e.data)]));
        live.addEventListener('opgaver', e => { tasks = JSON.parse(e.data); renderTasks(); });
        live.addEventListener('filer', e => { files = JSON.parse(e.data); renderFiles(); if (msgs.some(m => m.fileId)) keepPlace(renderMessages); });
        live.addEventListener('holdet', e => {
            const d = JSON.parse(e.data);
            if (room) { room.medlemmer = d.medlemmer; room.om = d.om; room.emner = d.emner; room.links = d.links; room.harKalender = d.harKalender; }
            renderRoster(); renderFiles(); calTabs(); loadCalendar();
        });
    }
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState !== 'visible') return;
        if (!$('rumside').hidden) { loadRooms(); return; }
        if ($('app').hidden) return;
        if (!live || live.readyState === 2) connect(); else { loadMessages(); loadTasks(); loadFiles(); }
        reportSeen();
    });

    // ---------- chat ----------

    const dayFmt = new Intl.DateTimeFormat('da-DK', { weekday: 'long', day: 'numeric', month: 'long' });
    const timeFmt = new Intl.DateTimeFormat('da-DK', { hour: '2-digit', minute: '2-digit' });
    let msgs = [];

    // all = after a lost line: fetch everything again, so reactions given meanwhile come along too.
    async function loadMessages(all) {
        try {
            const list = await api('GET', base + '/beskeder?efter=' + (all ? 0 : lastId));
            if (all) { msgs = list; lastId = list.reduce((x, m) => Math.max(x, m.id), 0); keepPlace(renderMessages); reportSeen(); }
            else addMessages(list);
        } catch { }
    }
    // Re-draw without moving what the person is reading.
    function keepPlace(fn) {
        const box = $('beskeder'), before = box.scrollHeight, top = window.scrollY;
        fn();
        if (top > 0) window.scrollTo(0, top + (box.scrollHeight - before));
    }

    // Newest message first, right under the text field (user: "always the latest one first, I only
    // need to scroll if I want to see earlier messages"). Nothing ever scrolls by itself.
    function addMessages(list) {
        const fresh = list.filter(m => m.id > lastId);
        if (!fresh.length) return;
        for (const m of fresh) { msgs.push(m); lastId = Math.max(lastId, m.id); }
        const box = $('beskeder');
        // Reading older messages further down: keep them exactly where they are on the screen.
        const before = box.scrollHeight, top = window.scrollY;
        renderMessages();
        if (top > 0) window.scrollTo(0, top + (box.scrollHeight - before));
        reportSeen();
    }

    function renderMessages() {
        const box = $('beskeder');
        $('tomChat').hidden = msgs.length > 0;
        box.textContent = '';
        let day = '';
        const mine = msgs.filter(isMyMsg);
        const myNewest = mine.length ? mine[mine.length - 1].id : -1;
        for (let i = msgs.length - 1; i >= 0; i--) {
            const m = msgs[i];
            const when = new Date(m.at);
            const d = dayFmt.format(when);
            if (d !== day) {
                const el = document.createElement('div'); el.className = 'dag'; el.textContent = d;
                box.appendChild(el); day = d;
            }
            const me = isMyMsg(m);
            const el = document.createElement('div'); el.className = 'besked' + (me ? ' mig' : ''); el.id = 'm' + m.id;
            const meta = document.createElement('div'); meta.className = 'meta';
            meta.textContent = (me ? (admin && m.name === firma ? 'Dig som ' + firma : 'Dig') : m.name) + ' · ' + timeFmt.format(when);
            const col = document.createElement('div'); col.className = 'besked-kol'; col.appendChild(meta);
            if (m.replyTo) col.appendChild(quote(m.replyTo));
            if (m.fileId) col.appendChild(chatFile(m.fileId));
            if (m.text) {
                const b = document.createElement('div'); b.className = 'boble'; b.textContent = m.text;
                b.title = 'Tryk for at svare eller give en reaktion';
                b.onclick = () => toggleActions(el, m);
                col.appendChild(b);
            } else col.lastChild.addEventListener('contextmenu', e => { e.preventDefault(); toggleActions(el, m); });
            const rx = reactionRow(m); if (rx) col.appendChild(rx);
            if (!me) {
                const row = document.createElement('div'); row.className = 'besked-raekke';
                row.append(avatar(m.name, 'lille'), col); el.appendChild(row);
            } else el.appendChild(col);
            if (!m.text) {   // a picture alone: a small "Svar" under it
                const s = document.createElement('button'); s.type = 'button'; s.className = 'link lille-svar'; s.textContent = 'Svar · reager';
                s.onclick = () => toggleActions(el, m); el.appendChild(s);
            }
            // "Set af": under your own newest message and under the newest message of all.
            if ((me && m.id === myNewest) || i === msgs.length - 1) { const s = seenLine(m); if (s) el.appendChild(s); }
            box.appendChild(el);
        }
    }

    // ---------- seen (2026-11-09) ----------
    // The page tells the room how far this person has read (only while the chat is on the screen) and how the phone is
    // set up, so the one who made the room can help. Everyone sees "Set af ..."; only the room's admin sees phone details.

    let reads = [], seenTimer = 0;
    async function loadReads() { try { reads = await api('GET', base + '/set'); renderMessages(); } catch { } }

    function author(m) { return m.host ? vaert : m.name; }
    // Mine: the host's messages when I am the host; otherwise the ones under my name (in a group also when I am admin).
    function isMyMsg(m) { return admin && !isGroup() ? !!m.host : (!m.host && m.name.toLowerCase() === navn.toLowerCase()); }
    function seenBy(m) {
        const a = author(m).toLowerCase(), me = navn.toLowerCase();
        return reads.filter(r => r.besked >= m.id && r.navn.toLowerCase() !== a && r.navn.toLowerCase() !== me).map(r => r.navn);
    }
    function seenLine(m) {
        const who = seenBy(m);
        if (!who.length) return null;
        const d = document.createElement('details'); d.className = 'set-af';
        const s = document.createElement('summary');
        const list = who.length === 1 ? who[0] : who.slice(0, -1).join(', ') + ' og ' + who[who.length - 1];
        if (who.length <= 3) { s.textContent = 'Set af ' + list; d.append(s); }
        else { s.textContent = 'Set af ' + who.length + ' – tryk for at se hvem'; const p = document.createElement('div'); p.textContent = list; d.append(s, p); }
        return d;
    }

    // ---------- trin 2 (2026-11-12): reply, reactions, pictures in the chat, search ----------

    const reactions = ['👍', '❤️', '😂', '😮', '😢', '🎉'];
    function myName() { return admin && !isGroup() ? (rolle === 'firma' ? firma : vaert) : navn; }

    function quote(id) {
        const q = msgs.find(x => x.id === id);
        const d = document.createElement('button'); d.type = 'button'; d.className = 'citat';
        const who = document.createElement('b'); who.textContent = q ? q.name : 'En tidligere besked';
        const txt = document.createElement('span'); txt.textContent = q ? (q.text || (q.fileId ? 'Billede eller fil' : '')).slice(0, 90) : '';
        d.append(who, txt);
        d.onclick = () => jumpTo(id);
        return d;
    }
    function jumpTo(id) {
        const el = $('m' + id);
        if (!el) return;
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.classList.add('fremhaev'); setTimeout(() => el.classList.remove('fremhaev'), 2200);
    }

    // A picture opens on top of the room, not in a new tab (user 2026-11-15: "When i open the picture on fx SonjaSnak i can
    // not navigate back" - a home-screen app has no back button for a new tab). Tilbage, a tap outside, Esc or the phone's
    // own back all close it.
    function isPicture(f) { return f.contentType.startsWith('image/') && f.contentType !== 'image/heic'; }

    function showPicture(url, name) {
        if (document.querySelector('.billede-visning')) return;
        const box = document.createElement('div'); box.className = 'billede-visning';
        box.setAttribute('role', 'dialog'); box.setAttribute('aria-modal', 'true'); box.setAttribute('aria-label', name || 'Billede');
        const back = document.createElement('button'); back.type = 'button'; back.className = 'k-btn k-btn-primary billede-tilbage'; back.textContent = 'Tilbage';
        const img = document.createElement('img'); img.src = url; img.alt = name || '';
        const get = document.createElement('a'); get.className = 'k-btn k-btn-sm k-btn-ghost billede-hent'; get.href = url; get.download = name || ''; get.textContent = 'Gem billedet';
        box.append(back, img, get);
        let open = true;
        const close = fromHistory => {
            if (!open) return; open = false;
            box.remove(); document.removeEventListener('keydown', onKey); window.removeEventListener('popstate', onPop);
            if (!fromHistory && history.state && history.state.billede) history.back();
        };
        const onKey = e => { if (e.key === 'Escape') close(false); };
        const onPop = () => close(true);
        back.onclick = () => close(false);
        box.onclick = e => { if (e.target === box) close(false); };
        document.addEventListener('keydown', onKey);
        history.pushState({ billede: true }, '');
        window.addEventListener('popstate', onPop);
        document.body.appendChild(box);
        back.focus();
    }

    function chatFile(id) {
        const f = files.find(x => x.id === id);
        if (!f) { const p = document.createElement('div'); p.className = 'boble slettet'; p.textContent = 'Filen er slettet.'; return p; }
        const a = document.createElement('a'); a.href = base + '/filer/' + f.id; a.target = '_blank'; a.rel = 'noopener';
        if (isPicture(f)) {
            a.className = 'chat-billede';
            a.removeAttribute('target');
            a.onclick = e => { e.preventDefault(); showPicture(a.href, f.name); };
            const img = document.createElement('img'); img.src = a.href; img.alt = f.name; img.loading = 'lazy';
            a.appendChild(img);
        } else {
            a.className = 'chat-fil';
            const ic = document.createElement('span'); ic.className = 'ikon'; ic.textContent = icon(f); ic.setAttribute('aria-hidden', 'true');
            const t = document.createElement('span'); t.className = 'chat-fil-tekst';
            const n = document.createElement('b'); n.textContent = f.name;
            const s = document.createElement('small'); s.textContent = size(f.size) + ' · tryk for at hente';
            t.append(n, s); a.append(ic, t);
        }
        return a;
    }

    function reactionRow(m) {
        const r = m.reactions || {};
        const keys = reactions.filter(e => r[e] && r[e].length);
        if (!keys.length) return null;
        const row = document.createElement('div'); row.className = 'reaktioner';
        const me = myName().toLowerCase();
        for (const e of keys) {
            const b = document.createElement('button'); b.type = 'button';
            const mine = r[e].some(n => n.toLowerCase() === me);
            b.className = 'reak' + (mine ? ' min' : '');
            b.textContent = e + ' ' + r[e].length;
            b.title = r[e].join(', ');
            b.setAttribute('aria-label', e + ' fra ' + r[e].join(', ') + (mine ? ' – tryk for at tage din tilbage' : ''));
            b.onclick = () => react(m.id, e);
            row.appendChild(b);
        }
        return row;
    }
    async function react(id, emoji) {
        try {
            const m = await api('POST', base + '/beskeder/' + id + '/reaktion', { navn, emoji, rolle });
            const i = msgs.findIndex(x => x.id === m.id); if (i >= 0) { msgs[i] = m; keepPlace(renderMessages); }
        } catch (err) { if (err.message !== 'login') flash(err.message); }
    }

    // Tap a message: the six reactions + Svar right under it. Tap again (or another one) and it goes.
    function toggleActions(el, m) {
        const open = el.querySelector('.handling');
        document.querySelectorAll('.besked .handling').forEach(x => x.remove());
        if (open) return;
        const bar = document.createElement('div'); bar.className = 'handling';
        for (const e of reactions) {
            const b = document.createElement('button'); b.type = 'button'; b.className = 'reak-valg'; b.textContent = e;
            b.setAttribute('aria-label', 'Giv ' + e);
            b.onclick = () => { bar.remove(); react(m.id, e); };
            bar.appendChild(b);
        }
        const s = document.createElement('button'); s.type = 'button'; s.className = 'k-btn k-btn-sm'; s.textContent = 'Svar';
        s.onclick = () => { bar.remove(); startReply(m); };
        bar.appendChild(s);
        el.appendChild(bar);
    }
    function startReply(m) {
        svarPaa = m.id;
        $('svarTekst').textContent = 'Svarer ' + m.name + ': ' + (m.text || 'billede eller fil').slice(0, 70);
        $('svarBar').hidden = false;
        window.scrollTo({ top: 0, behavior: 'smooth' });
        $('tekst').focus();
    }
    function stopReply() { svarPaa = 0; $('svarBar').hidden = true; }
    $('svarFortryd').onclick = stopReply;

    // Two roles (user: "Martin Hvidberg (user) + ITMartin (admin)"): the room's admin picks who is writing.
    function renderRolle() {
        const box = $('rolleValg');
        box.hidden = !admin;
        if (!admin) return;
        $('rolleMig').textContent = isGroup() ? (navn || vaert) : vaert;
        $('rolleFirma').textContent = firma;
        $('rolleMig').setAttribute('aria-pressed', String(rolle !== 'firma'));
        $('rolleFirma').setAttribute('aria-pressed', String(rolle === 'firma'));
    }
    $('rolleMig').onclick = () => { rolle = 'mig'; store.set('snak_rolle', 'mig'); renderRolle(); };
    $('rolleFirma').onclick = () => { rolle = 'firma'; store.set('snak_rolle', 'firma'); renderRolle(); };

    // A picture or file straight into the chat: it is put under Filer too (and goes when the room goes).
    $('chatFil').addEventListener('change', async () => {
        const file = $('chatFil').files[0];
        $('chatFil').value = '';
        if (!file) return;
        const st = $('chatFilStatus'); st.hidden = false; st.className = 'k-help';
        if (file.size > 90 * 1024 * 1024) { st.textContent = file.name + ': for stor (højst 90 MB).'; st.className = 'k-help fejl'; return; }
        st.textContent = 'Sender ' + file.name + ' …';
        try {
            const fd = new FormData();
            fd.append('fil', file); fd.append('navn', navn); fd.append('endpoint', endpoint); fd.append('chat', '1'); fd.append('rolle', rolle);
            const r = await fetch(base + '/filer', { method: 'POST', credentials: 'same-origin', body: fd });
            const d = await r.json().catch(() => null);
            if (!r.ok) throw new Error((d && d.fejl) || 'Det gik ikke. Prøv igen.');
            if (!files.some(f => f.id === d.id)) files.unshift(d);
            const m = await api('POST', base + '/beskeder', { navn, tekst: $('tekst').value.trim(), endpoint, fil: d.id, svarPaa: svarPaa || null, rolle });
            $('tekst').value = ''; grow(); stopReply();
            addMessages([m]); window.scrollTo(0, 0);
            st.hidden = true;
        } catch (err) { st.textContent = err.message; st.className = 'k-help fejl'; }
    });

    // Search the whole history (also what is older than the page has loaded).
    let soegTimer = 0;
    $('soegFelt').addEventListener('input', () => {
        clearTimeout(soegTimer);
        soegTimer = setTimeout(async () => {
            const q = $('soegFelt').value.trim(), box = $('soegListe');
            box.textContent = '';
            if (q.length < 2) return;
            let list = [];
            try { list = await api('GET', base + '/soeg?q=' + encodeURIComponent(q)); } catch { return; }
            if (!list.length) { const p = document.createElement('p'); p.className = 'k-help'; p.textContent = 'Ingen beskeder med "' + q + '".'; box.appendChild(p); return; }
            for (const m of list) {
                const b = document.createElement('button'); b.type = 'button'; b.className = 'soeg-hit';
                const h = document.createElement('small'); h.textContent = m.name + ' · ' + shortFmt.format(new Date(m.at));
                const t = document.createElement('span'); t.textContent = m.text || 'Billede eller fil';
                b.append(h, t);
                b.onclick = () => { $('soegBoks').open = false; jumpTo(m.id); };
                box.appendChild(b);
            }
        }, 300);
    });

    function pushState() {
        if (inApp || !pushPossible) return 'kan-ikke';
        if (store.get('snak_push_fra')) return 'fra';
        if (Notification.permission === 'denied') return 'nej';
        if (Notification.permission === 'default') return 'ikke-spurgt';
        return endpoint ? 'til' : 'fra';
    }
    function reportSeen() {
        clearTimeout(seenTimer);
        seenTimer = setTimeout(() => {
            if ($('app').hidden || !navn) return;
            const reading = document.visibilityState === 'visible' && !$('viewChat').hidden;
            api('POST', base + '/set', {
                navn, besked: reading ? lastId : 0,
                enhed: isIos ? 'iphone' : /android/i.test(navigator.userAgent) ? 'android' : 'computer',
                hjem: standalone, push: pushState(), inApp,
            }).catch(() => { });
        }, 1200);
    }

    // The room admin's "Hvem er klar?" card under ⋯.
    const pushText = { til: 'Beskeder: til', fra: 'Beskeder: slået fra', nej: 'Beskeder: har sagt nej', 'ikke-spurgt': 'Beskeder: ikke slået til endnu', 'kan-ikke': 'Beskeder: kan ikke på denne telefon' };
    const deviceText = { iphone: 'iPhone', android: 'Android', computer: 'Computer' };
    function advice(p) {
        if (!p.sidstInde) return 'Har ikke åbnet linket endnu. Send linket igen.';
        if (p.telefoner > 0) return '';
        if (p.inApp) return 'Har åbnet Snak inde i Messenger/Facebook. Bed dem åbne linket i ' + (p.enhed === 'android' ? 'Chrome' : 'Safari') + '.';
        if (p.enhed === 'iphone' && !p.hjem) return 'Snak skal på hjemmeskærmen, før iPhone kan give besked (menuen ⋯ eller ☰ → Del → Føj til hjemmeskærm).';
        if (p.push === 'nej') return p.enhed === 'iphone' ? 'Har sagt nej. Indstillinger → Notifikationer → Snak → Tillad.' : 'Har sagt nej. Låsen ved adressen → Notifikationer → Tillad.';
        return 'Bed dem trykke Slå beskeder til under ⋯.';
    }
    async function loadSetup() {
        let d;
        try { d = await api('GET', base + '/opsaetning'); } catch { return; }
        const rows = d.folk.map(p => ({ ...p, raad: advice(p) }));
        rows.sort((a, b) => (b.raad ? 1 : 0) - (a.raad ? 1 : 0) || a.navn.localeCompare(b.navn, 'da'));
        const inde = rows.filter(p => p.sidstInde).length, besked = rows.filter(p => p.telefoner > 0).length;
        $('opsSum').textContent = inde + ' af ' + rows.length + ' har været inde · ' + besked + ' får beskeder på telefonen.';
        const box = $('opsListe'); box.textContent = '';
        // 2026-11-12 the door: phones waiting for a yes, at the very top.
        for (const v of (d.doer && d.doer.venter) || []) {
            const c = document.createElement('div'); c.className = 'ops-raekke mangler doer-ny';
            const h = document.createElement('h3'); h.textContent = v.navn + ' vil ind på en ny ' + (v.enhed === 'computer' ? 'computer' : 'telefon');
            const p = document.createElement('p'); p.className = 'ops-raad';
            p.textContent = (deviceText[v.enhed] || 'Ukendt enhed') + ' · ' + dayFmt.format(new Date(v.at)) + ' kl. ' + timeFmt.format(new Date(v.at))
                + '. Er det ' + v.navn + ' selv (fx ikonet på hjemmeskærmen eller en ny telefon)? Så tryk Godkend.';
            const row = document.createElement('div'); row.className = 'k-row';
            const ok = document.createElement('button'); ok.type = 'button'; ok.className = 'k-btn k-btn-sm k-btn-primary'; ok.textContent = 'Godkend';
            ok.onclick = async () => { ok.disabled = true; try { await api('POST', base + '/doer/godkend', { id: v.id }); } catch { } loadSetup(); };
            const no = document.createElement('button'); no.type = 'button'; no.className = 'k-btn k-btn-sm k-btn-ghost'; no.textContent = 'Afvis';
            no.onclick = async () => { no.disabled = true; try { await api('POST', base + '/doer/afvis', { id: v.id }); } catch { } loadSetup(); };
            row.append(ok, no); c.append(h, p, row); box.appendChild(c);
        }
        // Not in yet: one short block with the names (the advice is the same for all of them).
        const ude = rows.filter(p => !p.sidstInde);
        if (ude.length) {
            const c = document.createElement('div'); c.className = 'ops-raekke mangler';
            const h = document.createElement('h3'); h.textContent = 'Ikke inde endnu (' + ude.length + ')';
            const n = document.createElement('p'); n.className = 'ops-navne'; n.textContent = ude.map(p => p.navn).join(', ');
            const r = document.createElement('p'); r.className = 'ops-raad'; r.textContent = 'Send dem linket igen: snak.itmartin.dk/' + (room.adresse || 'r/' + roomKey);
            c.append(h, n, r); box.appendChild(c);
        }
        for (const p of rows.filter(p => p.sidstInde)) {
            const c = document.createElement('div'); c.className = 'ops-raekke ' + (p.raad ? 'mangler' : 'klar');
            const h = document.createElement('h3'); h.textContent = p.navn;
            const f = document.createElement('div'); f.className = 'ops-fakta';
            const facts = [];
            if (p.sidstInde) {
                const t = new Date(p.sidstInde);
                facts.push('Sidst inde ' + dayFmt.format(t) + ' kl. ' + timeFmt.format(t));
                facts.push(p.telefoner > 0 ? 'Beskeder: til' + (p.telefoner > 1 ? ' (' + p.telefoner + ' enheder)' : '') : (pushText[p.push] || 'Beskeder: fra'));
                if (p.enhed) facts.push(deviceText[p.enhed] + (p.enhed !== 'computer' ? (p.hjem ? ' · på hjemmeskærmen' : ' · ikke på hjemmeskærmen') : ''));
                facts.push(p.laest >= d.sidsteBesked ? 'Har læst alt' : 'Har ikke læst de nyeste beskeder');
            } else facts.push('Ikke inde endnu');
            for (const t of facts) { const sp = document.createElement('span'); sp.textContent = t; f.appendChild(sp); }
            c.append(h, f);
            if (d.doer && p.medlem) {
                const n = d.doer.telefoner[Object.keys(d.doer.telefoner).find(k => k.toLowerCase() === p.navn.toLowerCase())] || 0;
                const l = document.createElement('div'); l.className = 'k-row doer-laas';
                const t = document.createElement('span'); t.className = 'k-help';
                t.textContent = n ? 'Navnet er låst til ' + n + (n === 1 ? ' telefon' : ' telefoner') + '.' : 'Navnet er frit – den første, der trykker på det, får det.';
                l.appendChild(t);
                if (n) {
                    const fr = document.createElement('button'); fr.type = 'button'; fr.className = 'k-btn k-btn-sm k-btn-ghost'; fr.textContent = 'Frigiv navnet';
                    fr.onclick = async () => {
                        if (!fr.dataset.sure) { fr.dataset.sure = '1'; fr.textContent = 'Ja, frigiv (alle telefoner skal trykke igen)'; setTimeout(() => { delete fr.dataset.sure; fr.textContent = 'Frigiv navnet'; }, 4000); return; }
                        fr.disabled = true; try { await api('POST', base + '/doer/frigiv', { navn: p.navn }); } catch { } loadSetup();
                    };
                    l.appendChild(fr);
                }
                c.appendChild(l);
            }
            if (p.raad) { const r = document.createElement('p'); r.className = 'ops-raad'; r.textContent = p.raad; c.appendChild(r); }
            box.appendChild(c);
        }
    }
    $('opsOpdater').onclick = loadSetup;

    async function send() {
        const t = $('tekst').value.trim();
        if (!t) return;
        $('sendBtn').disabled = true;
        try {
            const m = await api('POST', base + '/beskeder', { navn, tekst: t, endpoint, svarPaa: svarPaa || null, rolle });
            if (room && room.klaret) { room.klaret = false; klaretBar(); }
            $('tekst').value = ''; grow(); stopReply();
            addMessages([m]);
            window.scrollTo(0, 0);   // your own message: show it at the top
        } catch (err) {
            if (err.message !== 'login') flash(err.message);
        }
        $('sendBtn').disabled = false;
        $('tekst').focus();
    }
    $('skriv').addEventListener('submit', e => { e.preventDefault(); send(); });
    $('tekst').addEventListener('keydown', e => {
        // Enter sends on a computer; on a phone Enter makes a new line and the Send button sends.
        if (e.key === 'Enter' && !e.shiftKey && !matchMedia('(pointer: coarse)').matches) { e.preventDefault(); send(); }
    });
    function grow() { const t = $('tekst'); t.style.height = 'auto'; t.style.height = Math.min(t.scrollHeight, 160) + 'px'; }
    $('tekst').addEventListener('input', grow);

    function flash(text) {
        const bar = $('pushBar');
        bar.textContent = text; bar.className = 'bar fejl'; bar.hidden = false;
        setTimeout(() => setupPush(false), 5000);
    }

    // ---------- tasks ----------

    async function loadTasks() {
        try {
            if (!todayStr) { try { todayStr = (await api('GET', base + '/idag')).dag; } catch { } }
            tasks = await api('GET', base + '/opgaver');
            if (isGroup()) { try { mine = await api('GET', base + '/mine?navn=' + encodeURIComponent(navn)); } catch { } }
            renderTasks();
        } catch { }
    }

    // ---- today (2026-11-08): the first picture = today's tasks in clock order, the next one at the top ----
    let todayStr = '';
    function today() { return todayStr || new Date().toLocaleDateString('sv-SE'); }
    function isDone(t) { return t.daily || t.yearly ? t.doneOn === today() : t.done; }
    // A yearly day (birthday) falls on the same day and month every year; 29 February on the 28th in other years.
    function yearDay(t, y) {
        const [, m, d] = t.day.split('-').map(Number);
        const leap = (y % 4 === 0 && y % 100 !== 0) || y % 400 === 0;
        return y + '-' + String(m).padStart(2, '0') + '-' + String(m === 2 && d === 29 && !leap ? 28 : d).padStart(2, '0');
    }
    // The next date it falls on (today included).
    function nextDay(t) {
        if (t.endDay && t.day <= today() && today() <= t.endDay) return today();   // several days, going on now
        if (!t.yearly || !t.day) return t.day;
        const y = Number(today().slice(0, 4)), d = yearDay(t, y);
        return d >= today() ? d : yearDay(t, y + 1);
    }
    function isToday(t) { return t.daily || nextDay(t) === today() || (!t.day && !t.done); }
    function clock(t) { return /^\d{2}:\d{2}$/.test(t.when) ? t.when : ''; }
    function byClock(a, b) {
        const d = Number(isDone(a)) - Number(isDone(b));
        if (d) return d;
        const ca = clock(a) || '99', cb = clock(b) || '99';
        return ca < cb ? -1 : ca > cb ? 1 : a.id - b.id;
    }
    // Midnight: tomorrow's list (daily tasks come back unticked).
    setInterval(() => { const n = new Date().toLocaleDateString('sv-SE'); if (todayStr && n !== todayStr) { todayStr = ''; loadTasks(); } }, 60000);

    // "Hvem skal gøre det?": the room's people + anyone who has written or taken a task.
    function fillTil(selected) {
        const sel = $('opgTil');
        const names = [];
        const add = n => { if (n && !names.some(x => x.toLowerCase() === n.toLowerCase())) names.push(n); };
        if (admin) add(vaert);
        for (const n of (room && room.medlemmer) || []) add(n);
        for (const m of msgs) if (!m.host) add(m.name);
        for (const t of tasks) t.takers.forEach(add);
        const me = (admin ? vaert : navn).toLowerCase();
        sel.textContent = '';
        const free = document.createElement('option'); free.value = ''; free.textContent = 'Den der har tid (alle kan tage den)'; sel.appendChild(free);
        for (const n of names) { const o = document.createElement('option'); o.value = n; o.textContent = n.toLowerCase() === me ? n + ' (mig)' : n; sel.appendChild(o); }
        sel.value = selected || '';
    }

    function isMine(t) { return t.takers.some(x => x.toLowerCase() === navn.toLowerCase()); }

    // ---------- Til næste gang (course groups) ----------
    let mine = [];
    function ngDay(t) {
        // The course day it is for: the calendar's day with that date, else just the date.
        if (!t.day) return null;
        const e = calEvents.find(x => new Date(x.start).toLocaleDateString('sv-SE') === t.day);
        const d = new Date(t.day + 'T12:00:00'), txt = kalDag.format(d);
        return 'Til ' + txt + (e ? ' – ' + e.titel : '');
    }
    function ngCard(t) {
        const c = document.createElement('article'), done = mine.includes(t.id);
        c.className = 'k-card ng-opg' + (done ? ' gjort' : '');
        const row = document.createElement('label'); row.className = 'ng-raekke';
        const box = document.createElement('input'); box.type = 'checkbox'; box.checked = done;
        box.setAttribute('aria-label', 'Gjort: ' + t.title);
        box.onchange = async () => {
            box.disabled = true;
            try { mine = await api('POST', base + '/opgaver/' + t.id + '/mig', { navn, gjort: box.checked }); renderNg(); }
            catch { box.checked = !box.checked; box.disabled = false; }
        };
        const txt = document.createElement('div');
        const h = document.createElement('h3'); h.textContent = t.title; txt.appendChild(h);
        const when = ngDay(t);
        if (when) { const w = document.createElement('div'); w.className = 'kal-dato'; w.textContent = when; txt.appendChild(w); }
        if (t.note) { const p = document.createElement('p'); p.className = 'note'; p.textContent = t.note; txt.appendChild(p); }
        row.append(box, txt); c.appendChild(row);
        if (admin) {
            const del = document.createElement('button'); del.type = 'button'; del.className = 'k-btn k-btn-ghost k-btn-sm'; del.textContent = 'Fjern';
            del.onclick = async () => {
                if (!del.dataset.sure) { del.dataset.sure = '1'; del.textContent = 'Ja, fjern den'; setTimeout(() => { delete del.dataset.sure; del.textContent = 'Fjern'; }, 4000); return; }
                try { await api('DELETE', base + '/opgaver/' + t.id); } catch { }
            };
            c.appendChild(del);
        }
        return c;
    }
    function renderNg() {
        if (!isGroup()) return;
        const by = (a, b) => (a.day || '9999') < (b.day || '9999') ? -1 : (a.day || '9999') > (b.day || '9999') ? 1 : a.id - b.id;
        const open = tasks.filter(t => !mine.includes(t.id)).sort(by), done = tasks.filter(t => mine.includes(t.id)).sort(by);
        const l = $('ngListe'); l.textContent = ''; for (const t of open) l.appendChild(ngCard(t));
        const g = $('ngGjortListe'); g.textContent = ''; for (const t of done) g.appendChild(ngCard(t));
        $('ngGjort').hidden = done.length === 0;
        $('ngTom').hidden = tasks.length > 0;
        $('ngBadge').textContent = String(open.length); $('ngBadge').hidden = open.length === 0;
        // Admin's form: the coming course days from the calendar.
        const sel = $('ngDag'), keep = sel.value, now = new Date();
        sel.textContent = '';
        const none = document.createElement('option'); none.value = ''; none.textContent = 'Ingen bestemt dag'; sel.appendChild(none);
        for (const e of calEvents.filter(x => new Date(x.slut) >= now)) {
            const o = document.createElement('option'); o.value = new Date(e.start).toLocaleDateString('sv-SE');
            o.textContent = kalDag.format(new Date(e.start)) + ' – ' + e.titel; sel.appendChild(o);
        }
        if ([...sel.options].some(o => o.value === keep)) sel.value = keep;
        else if (sel.options.length > 1) sel.selectedIndex = 1;
    }
    $('ngNy').addEventListener('submit', async e => {
        e.preventDefault();
        $('ngGem').disabled = true;
        try {
            const d = $('ngDag').value;
            await api('POST', base + '/opgaver', { titel: $('ngTitel').value, note: $('ngNote').value, dag: d || null, endpoint });
            $('ngTitel').value = ''; $('ngNote').value = '';
        } catch (err) { if (err.message !== 'login') flash(err.message); }
        $('ngGem').disabled = false;
    });

    // "Snart" (2026-11-12): what is on the coming 7 days, for everyone in the room.
    const snartFmt = new Intl.DateTimeFormat('da-DK', { weekday: 'long', day: 'numeric', month: 'long' });
    function addDays(day, n) { const d = new Date(day + 'T12:00'); d.setDate(d.getDate() + n); return d.toLocaleDateString('sv-SE'); }
    function renderSnart() {
        const from = today(), to = addDays(from, 7), tomorrow = addDays(from, 1);
        const soon = tasks.filter(t => !t.daily && t.day && nextDay(t) > from && nextDay(t) <= to && !(t.done && !t.yearly))
            .sort((a, b) => nextDay(a) < nextDay(b) ? -1 : nextDay(a) > nextDay(b) ? 1 : byClock(a, b));
        $('snartBoks').hidden = soon.length === 0;
        const l = $('snartListe'); l.textContent = '';
        for (const t of soon) {
            const li = document.createElement('li'), nd = nextDay(t);
            const w = document.createElement('span'); w.className = 'snart-hvornaar' + (nd === tomorrow ? ' i-morgen' : '');
            w.textContent = (t.endDay ? spanText(t) : nd === tomorrow ? 'I morgen' : snartFmt.format(new Date(nd + 'T12:00'))) + (clock(t) ? ' · kl. ' + clock(t) : '')
                + (t.place ? ' · ' + t.place : '');
            const h = document.createElement('span'); h.className = 'snart-hvad';
            h.textContent = t.title + (t.takers.length ? ' – ' + t.takers.join(', ') : '');
            li.append(w, h); l.appendChild(li);
        }
    }

    function renderTasks() {
        if (isGroup()) { renderNg(); return; }
        renderSnart();
        renderDato();
        const dayList = tasks.filter(isToday);
        const open = dayList.filter(t => !isDone(t));
        const free = open.filter(t => t.takers.length === 0);
        $('idagTitel').textContent = 'I dag – ' + dayFmt.format(new Date(today() + 'T12:00'));
        const badge = $('opgBadge');
        badge.textContent = String(free.length);
        badge.hidden = free.length === 0;
        badge.title = free.length + ' opgaver uden nogen på';

        const ov = $('opgOversigt');
        $('opgOversigtBoks').hidden = !(admin && dayList.length);
        if (admin && dayList.length) {
            ov.textContent = '';
            for (const [n, label, cls] of [[open.length, 'mangler i dag', ''], [free.length, 'uden nogen på', free.length ? 'advar' : ''], [dayList.length - open.length, 'gjort', '']]) {
                const d = document.createElement('div'); d.className = 'tal ' + cls;
                const b = document.createElement('b'); b.textContent = n;
                const s = document.createElement('span'); s.textContent = label;
                d.append(b, s); ov.appendChild(d);
            }
        }

        if (canAdd() && !editId) fillTil($('opgTil').value);
        const box = $('opgaver');
        box.textContent = '';
        if (!dayList.length) {
            const e = document.createElement('div'); e.className = 'tom';
            e.innerHTML = canAdd()
                ? '<strong>Ingen opgaver i dag</strong>Skriv det kort i boksen nedenfor – fx "Tandlæge Eigil 13/11 kl 14" – og tryk "Læg det ind". De andre får besked.'
                : '<strong>Ingen opgaver i dag</strong>Når tovholderen lægger en opgave ud, kommer den her, og du får besked.';
            box.appendChild(e);
        }
        for (const t of dayList.sort(byClock)) box.appendChild(taskCard(t));

        // The tovholder also sees the other days (coming first, then old ones).
        // The tovholder sees all other days; the others in a shared room see the coming ones (birthdays, visits).
        const other = tasks.filter(t => !isToday(t) && (admin || (shared() && nextDay(t) > today())));
        $('andreDage').hidden = !other.length;
        const ob = $('opgAndre'); ob.textContent = '';
        for (const t of other.sort((a, b) => { const x = nextDay(a), y = nextDay(b); return (y < today()) - (x < today()) || (x < y ? -1 : x > y ? 1 : byClock(a, b)); })) ob.appendChild(taskCard(t));
    }

    function taskCard(t) {
        const c = document.createElement('article');
        // k-card + heading first = kolibri.js makes it foldable (tap the title), and remembers it per task.
        const done = isDone(t);
        c.className = 'k-card opgave' + (done ? ' done' : t.takers.length ? ' taget' : ' fri');
        const h = document.createElement('h3'); h.textContent = (clock(t) ? 'Kl. ' + clock(t) + ' · ' : '') + t.title;
        c.appendChild(h);
        const st = document.createElement('p'); st.className = 'status';
        st.textContent = (done ? '✓ Gjort' : t.takers.length ? '● Taget' : '○ Mangler nogen') + (t.daily ? ' · hver dag' : t.yearly ? ' · hvert år' : '');
        c.appendChild(st);
        const nd = nextDay(t);
        const dayText = t.endDay ? spanText(t) : nd && nd !== today() ? dayFmt.format(new Date(nd + 'T12:00')) : '';
        if (dayText || (t.when && !clock(t))) {
            const w = document.createElement('p'); w.className = 'hvornaar';
            w.textContent = [dayText, clock(t) ? '' : t.when].filter(Boolean).join(' · '); c.appendChild(w);
        }
        if (t.place) {
            const p = document.createElement('p'); p.className = 'sted';
            const s = document.createElement('span'); s.textContent = t.place;
            const a = document.createElement('a'); a.className = 'k-btn k-btn-sm k-btn-ghost'; a.target = '_blank'; a.rel = 'noopener';
            a.href = 'https://www.openstreetmap.org/search?query=' + encodeURIComponent(t.place); a.textContent = 'Find vej';
            p.append(s, a); c.appendChild(p);
        }
        if (t.note) { const n = document.createElement('p'); n.className = 'note'; n.textContent = t.note; c.appendChild(n); }
        const who = document.createElement('p'); who.className = 'hvem';
        who.textContent = t.takers.length ? 'Tager den: ' + t.takers.join(', ') : 'Ingen har taget den endnu.';
        c.appendChild(who);
        if (t.by) { const b = document.createElement('p'); b.className = 'hvem'; b.textContent = 'Lagt ind af ' + t.by; c.appendChild(b); }
        if (done && (t.doneNote || t.doneBy)) {
            const k = document.createElement('p'); k.className = 'klaret';
            k.textContent = '✓ ' + (t.doneBy ? 'Klaret af ' + t.doneBy : 'Klaret') + (t.doneDay ? ' ' + shortDay(t.doneDay) : '')
                + (t.doneNote ? ': ' + t.doneNote : '') + (t.inCalendar ? ' · i kalenderen' : '');
            c.appendChild(k);
        }

        const row = document.createElement('div'); row.className = 'k-row';
        if (!done) {
            const take = document.createElement('button'); take.type = 'button';
            take.className = 'k-btn ' + (isMine(t) ? '' : 'k-btn-primary');
            take.textContent = isMine(t) ? 'Jeg kan alligevel ikke' : 'Jeg tager den';
            take.onclick = () => act(take, () => api('POST', `${base}/opgaver/${t.id}/tag`, { navn, endpoint }));
            row.appendChild(take);
        }
        if (done ? mayReopen(t) : mayClose()) {
            const doneBtn = document.createElement('button'); doneBtn.type = 'button'; doneBtn.className = 'k-btn k-btn-sm';
            doneBtn.textContent = done ? 'Åbn igen' : t.daily ? 'Gjort i dag' : 'Færdig';
            doneBtn.onclick = done
                ? () => act(doneBtn, () => api('POST', `${base}/opgaver/${t.id}/faerdig`, { faerdig: false, navn }))
                : () => { const f = c.querySelector('.luk'); if (f) { f.remove(); return; } c.appendChild(closeForm(t)); c.querySelector('.luk input').focus(); };
            row.appendChild(doneBtn);
        }
        if (mayChange(t)) {
            const edit = document.createElement('button'); edit.type = 'button'; edit.className = 'k-btn k-btn-sm k-btn-ghost';
            edit.textContent = 'Ret';
            edit.onclick = () => startEdit(t);
            const del = document.createElement('button'); del.type = 'button'; del.className = 'k-btn k-btn-sm k-btn-ghost';
            del.textContent = 'Slet';
            del.onclick = () => {
                // Two taps: the first asks, the second deletes.
                if (del.dataset.sure) return act(del, () => api('DELETE', `${base}/opgaver/${t.id}?navn=${encodeURIComponent(navn)}`));
                del.dataset.sure = '1'; del.textContent = 'Ja, slet den'; del.className = 'k-btn k-btn-sm k-btn-danger';
                setTimeout(() => { delete del.dataset.sure; del.textContent = 'Slet'; del.className = 'k-btn k-btn-sm k-btn-ghost'; }, 4000);
            };
            row.append(edit, del);
        }
        c.appendChild(row);
        return c;
    }

    // ---------- closing a task (2026-11-17) ----------
    // User: "a field where they say how they closed it. Like Vaccination d. 17" + "if a task has a date you can add to calendar when done".
    const dmFmt = new Intl.DateTimeFormat('da-DK', { day: 'numeric', month: 'numeric' });
    function shortDay(d) { return 'd. ' + dmFmt.format(new Date(d + 'T12:00')); }
    function closeForm(t) {
        const f = document.createElement('form'); f.className = 'luk';
        const id = 'luk' + t.id;
        const l = document.createElement('label'); l.className = 'k-label'; l.htmlFor = id + 'h'; l.textContent = 'Hvordan blev det klaret?';
        const how = document.createElement('input'); how.className = 'k-input'; how.id = id + 'h'; how.maxLength = 300;
        how.placeholder = t.daily ? 'fx Pillerne taget kl. 8' : 'fx Vaccination givet d. ' + dmFmt.format(new Date());
        f.append(l, how);
        let cal = null, next = null;
        if (!t.daily) {
            const cl = document.createElement('label'); cl.className = 'luk-kal';
            cal = document.createElement('input'); cal.type = 'checkbox'; cal.checked = !!t.day;
            cl.append(cal, document.createTextNode('Sæt den i kalenderen'));
            const nl = document.createElement('label'); nl.className = 'k-label'; nl.htmlFor = id + 'n';
            nl.innerHTML = 'Næste gang <span class="valgfri">(hvis det skal ske igen)</span>';
            next = document.createElement('input'); next.type = 'date'; next.className = 'k-input'; next.id = id + 'n'; next.min = addDays(today(), 1);
            f.append(cl, nl, next);
        }
        const row = document.createElement('div'); row.className = 'k-row';
        const ok = document.createElement('button'); ok.type = 'submit'; ok.className = 'k-btn k-btn-primary'; ok.textContent = 'Gem – det er klaret';
        const no = document.createElement('button'); no.type = 'button'; no.className = 'k-btn k-btn-ghost'; no.textContent = 'Fortryd';
        no.onclick = () => f.remove();
        row.append(ok, no); f.appendChild(row);
        f.onsubmit = e => {
            e.preventDefault();
            act(ok, () => api('POST', `${base}/opgaver/${t.id}/faerdig`,
                { faerdig: true, hvordan: how.value, kalender: !!(cal && cal.checked), naesteGang: (next && next.value) || null, navn, endpoint }));
        };
        return f;
    }

    // ---------- Kalender in shared rooms (2026-11-17) ----------
    // "a calendar on the last tab where you can see the next important thing... Not too much": the next one big, a short list, what was closed.
    function renderDato() {
        if (!closesOwn()) return;
        const from = today(), until = addDays(from, 60);
        const coming = tasks.filter(t => !t.daily && t.day && !isDone(t) && nextDay(t) >= from && nextDay(t) <= until)
            .sort((a, b) => nextDay(a) < nextDay(b) ? -1 : nextDay(a) > nextDay(b) ? 1 : byClock(a, b));
        const when = t => {
            const nd = nextDay(t);
            return (t.endDay ? spanText(t) : nd === from ? 'I dag' : nd === addDays(from, 1) ? 'I morgen' : snartFmt.format(new Date(nd + 'T12:00')))
                + (clock(t) ? ' · kl. ' + clock(t) : '') + (t.place ? ' · ' + t.place : '');
        };
        const first = coming[0];
        $('datoNaeste').hidden = !first;
        if (first) {
            $('datoNaesteHvornaar').textContent = when(first);
            $('datoNaesteHvad').textContent = first.title;
            $('datoNaesteHvem').textContent = first.takers.length ? 'Tager den: ' + first.takers.join(', ') : 'Ingen har taget den endnu.';
        }
        const line = (ul, w, h) => {
            const li = document.createElement('li');
            const a = document.createElement('span'); a.className = 'snart-hvornaar'; a.textContent = w;
            const b = document.createElement('span'); b.className = 'snart-hvad'; b.textContent = h;
            li.append(a, b); ul.appendChild(li);
        };
        const rest = coming.slice(1, 8), kl = $('datoKommerListe'); kl.textContent = '';
        for (const t of rest) line(kl, when(t), t.title + (t.takers.length ? ' – ' + t.takers.join(', ') : ''));
        $('datoKommer').hidden = !rest.length;
        // A closed task sits on its own day (if it had one), else on the day it was closed; newest first, only the last 8.
        const closedDay = t => t.day && !t.yearly && t.day <= t.doneDay ? t.day : t.doneDay;
        const closed = tasks.filter(t => t.inCalendar && t.doneDay).sort((a, b) => closedDay(a) < closedDay(b) ? 1 : -1).slice(0, 8);
        const cl = $('datoKlaretListe'); cl.textContent = '';
        for (const t of closed) line(cl, snartFmt.format(new Date(closedDay(t) + 'T12:00')),
            t.title + (t.doneNote ? ': ' + t.doneNote : '') + (t.doneBy ? ' – ' + t.doneBy : ''));
        $('datoKlaret').hidden = !closed.length;
        $('datoTom').hidden = !!(first || closed.length);
    }

    async function act(btn, fn) {
        btn.disabled = true;
        try { await fn(); await loadTasks(); } catch (err) { if (err.message !== 'login') flash(err.message); }
        btn.disabled = false;
    }

    function startEdit(t) {
        editId = t.id;
        $('opgTitel').value = t.title; $('opgHvornaar').value = clock(t); $('opgNote').value = t.note;
        $('opgDag').value = t.daily ? 'hver' : t.yearly ? 'aar' : !t.day ? 'ingen' : t.day === today() ? 'idag' : 'anden';
        $('opgDato').value = t.day || ''; $('opgDato').hidden = !['anden', 'aar'].includes($('opgDag').value);
        $('opgSlut').value = t.endDay || ''; $('opgSlutBoks').hidden = $('opgDag').value !== 'anden'; $('opgSted').value = t.place || '';
        // Several on it: the list shows "as it is now", so saving never removes anyone by accident.
        fillTil(t.takers.length === 1 ? t.takers[0] : '');
        if (t.takers.length > 1) {
            const keep = document.createElement('option'); keep.value = '*'; keep.textContent = 'Som nu: ' + t.takers.join(', ');
            $('opgTil').prepend(keep); $('opgTil').value = '*';
        }
        $('opgTil').dataset.was = $('opgTil').value;
        $('nyTitel').textContent = 'Ret opgave'; $('opgGem').textContent = 'Gem ændringen'; $('opgFortryd').hidden = false;
        $('flereValg').open = true; $('hurtigBoks').hidden = true;
        $('nyOpg').scrollIntoView({ behavior: 'smooth' }); $('opgTitel').focus();
    }
    function stopEdit() {
        editId = 0; $('nyOpg').reset(); fillTil(''); $('opgDato').hidden = true; $('opgSlutBoks').hidden = true;
        $('nyTitel').textContent = 'Ny opgave'; $('opgGem').textContent = 'Læg opgaven ud'; $('opgFortryd').hidden = true;
        $('hurtigBoks').hidden = false;
    }
    $('opgFortryd').onclick = stopEdit;

    // ---- "Skriv det kort" (2026-11-12): one line -> what, who, day, clock. Simple rules in the page, no AI. ----
    const maaneder = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec'];
    const ugedage = ['søndag', 'mandag', 'tirsdag', 'onsdag', 'torsdag', 'fredag', 'lørdag'];
    function parseQuick(text) {
        let s = ' ' + (text || '').replace(/\s+/g, ' ') + ' ';
        const cut = re => { const m = s.match(re); if (m) s = s.replace(m[0], ' '); return m; };
        const r = { titel: '', dag: '', slut: '', hvornaar: '', til: '', sted: '', hvertAar: false };
        if (cut(/\s(hvert år|hvert aar|årligt)(?=[\s.,!])/i)) r.hvertAar = true;
        if (/fødselsdag|fdag|bryllupsdag|mærkedag/i.test(s)) r.hvertAar = true;
        const t0 = today(), y0 = Number(t0.slice(0, 4));
        const iso = (y, m, d) => y + '-' + String(m).padStart(2, '0') + '-' + String(d).padStart(2, '0');
        const ok = (y, m, d) => { const x = new Date(y, m - 1, d); return x.getMonth() === m - 1 && x.getDate() === d; };
        // A date without a year that has passed this year is next year's (not for a yearly day - it keeps its date).
        const fix = (m, d, y) => {
            if (!(m >= 1 && m <= 12)) return '';
            let year = y ? (y < 100 ? 2000 + y : y) : y0;
            if (!ok(year, m, d)) return '';
            if (!y && !r.hvertAar && iso(year, m, d) < t0) year++;
            return iso(year, m, d);
        };
        let m;
        const hhmm = (h, mi) => (h >= 0 && h < 24 && mi >= 0 && mi < 60) ? String(h).padStart(2, '0') + ':' + String(mi).padStart(2, '0') : '';
        // "indtil 12" / "til kl. 12" / "frem til 14.30" = when something ENDS (user 2026-11-12: "Har møder indtil 12" became a 12:00
        // start with reminders). It stays in the title, sets no clock and gives no reminders.
        let slutTekst = '';
        if ((m = s.match(/\s(indtil|frem til|til)\s+(kl\.?\s*|klokken\s+)?(\d{1,2})(?:[.:](\d{2}))?(?![\/\d-]|\.\d)(?=[\s,!]|\.\s|\.$)/i)) && (m[1].toLowerCase() !== 'til' || m[2])) {
            const h = +m[3], mi = m[4] ? +m[4] : 0;
            // "indtil 7.11" without kl. is a date (7 November), not a time.
            const looksLikeDate = !m[2] && m[4] && m[0].includes(".") && mi >= 1 && mi <= 12;
            if (h < 24 && mi < 60 && !looksLikeDate) {
                slutTekst = m[1].toLowerCase() + ' kl. ' + h + (m[4] ? '.' + m[4] : '');
                s = s.replace(m[0], ' §SLUT§ ');
            }
        }
        // "kl 18.30" first, so 18.30 is never read as a date.
        if ((m = cut(/\s(?:kl\.?|klokken)\s*(\d{1,2})(?:[.:](\d{2}))?(?=[\s.,!])/i))) r.hvornaar = hhmm(+m[1], m[2] ? +m[2] : 0);
        // Several days (trin 2): "4/11-7/11", "4-7/11", "4.-7. november", "fra 4/11 til 7/11".
        const mon = '(januar|februar|marts|april|maj|juni|juli|august|september|oktober|november|december|jan|feb|mar|apr|jun|jul|aug|sep|sept|okt|nov|dec)';
        const monNo = w => maaneder.indexOf(w.slice(0, 3).toLowerCase()) + 1;
        const span = (d1, m1, d2, m2, y) => {
            // Still going, or not over yet this year: this year (only a span that is all over moves to next year).
            if (!y && m1 <= m2 && ok(y0, m2, d2) && iso(y0, m2, d2) >= t0) y = y0;
            const a = fix(m1, d1, y); if (!a) return false;
            let b = fix(m2, d2, y || Number(a.slice(0, 4)));
            if (b && b < a) b = fix(m2, d2, Number(a.slice(0, 4)) + 1);   // 28/12-2/1 runs into the new year
            if (!b || b <= a) return false;
            r.dag = a; r.slut = b; return true;
        };
        const til = '\\s*(?:-|–|til)\\s*';
        if ((m = s.match(new RegExp('\\s(?:fra\\s+)?(\\d{1,2})[\\/.](\\d{1,2})' + til + '(\\d{1,2})[\\/.](\\d{1,2})(?:[\\/.](\\d{2,4}))?(?=[\\s.,!])', 'i')))
            && span(+m[1], +m[2], +m[3], +m[4], m[5] ? +m[5] : 0)) s = s.replace(m[0], ' ');
        else if ((m = s.match(new RegExp('\\s(?:fra\\s+)?(\\d{1,2})\\.?' + til + '(\\d{1,2})[\\/.](\\d{1,2})(?:[\\/.](\\d{2,4}))?(?=[\\s.,!])', 'i')))
            && span(+m[1], +m[3], +m[2], +m[3], m[4] ? +m[4] : 0)) s = s.replace(m[0], ' ');
        else if ((m = s.match(new RegExp('\\s(?:fra\\s+)?(\\d{1,2})\\.?(?:\\s*' + mon + '\\.?)?' + til + '(\\d{1,2})\\.?\\s*' + mon + '\\.?(?:\\s+(\\d{4}))?(?=[\\s.,!])', 'i')))
            && span(+m[1], m[2] ? monNo(m[2]) : monNo(m[4]), +m[3], monNo(m[4]), m[5] ? +m[5] : 0)) s = s.replace(m[0], ' ');
        // A date is only taken when it is a real one (18.30 is not).
        const dm = r.dag ? null : s.match(/\s(\d{1,2})[\/.-](\d{1,2})(?:[\/.-](\d{2,4}))?(?=[\s.,!])/);
        const dd = dm ? fix(+dm[2], +dm[1], dm[3] ? +dm[3] : 0) : '';
        if (r.dag) { }
        else if (dd) { r.dag = dd; s = s.replace(dm[0], ' '); }
        else if ((m = cut(/\s(\d{1,2})\.?\s*(januar|februar|marts|april|maj|juni|juli|august|september|oktober|november|december|jan|feb|mar|apr|jun|jul|aug|sep|sept|okt|nov|dec)\.?(?:\s+(\d{4}))?(?=[\s.,!])/i)))
            r.dag = fix(maaneder.indexOf(m[2].slice(0, 3).toLowerCase()) + 1, +m[1], m[3] ? +m[3] : 0);
        else if (cut(/\s(i dag|idag)(?=[\s.,!])/i)) r.dag = t0;
        else if (cut(/\s(i overmorgen|overmorgen)(?=[\s.,!])/i)) r.dag = addDays(t0, 2);
        else if (cut(/\s(i morgen|imorgen)(?=[\s.,!])/i)) r.dag = addDays(t0, 1);
        else if ((m = cut(/\s(på\s+)?(mandag|tirsdag|onsdag|torsdag|fredag|lørdag|søndag)(?=[\s.,!])/i))) {
            const want = ugedage.indexOf(m[2].toLowerCase());
            for (let i = 0; i < 7; i++) { const d = addDays(t0, i); if (new Date(d + 'T12:00').getDay() === want) { r.dag = d; break; } }
        }
        if (r.hvornaar) { }
        else if ((m = cut(/\s(\d{1,2})[.:](\d{2})(?=[\s.,!])/))) r.hvornaar = hhmm(+m[1], +m[2]);
        else if ((m = cut(/\s(\d{2})(\d{2})(?=[\s.,!])/))) r.hvornaar = hhmm(+m[1], +m[2]);
        // A name from the room = who it is for (a yearly day keeps the name in its title: "Julius fødselsdag").
        for (const n of (room && room.medlemmer) || []) {
            const re = new RegExp('\\s' + n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '(?=[\\s.,!:])', 'i');
            if (re.test(s)) { if (!r.hvertAar) { r.til = n; s = s.replace(re, ' '); } break; }
        }
        // Where (trin 2): "i Kbh", "på Rådhuset", "i Tivoli Friheden" - a place starts with a capital letter.
        if ((m = cut(/\s(?:i|på|hos)\s+([A-ZÆØÅ0-9][\wæøåÆØÅ'.-]*(?:\s+[A-ZÆØÅ][\wæøåÆØÅ'.-]*){0,3})(?=[\s.,!]|$)/))) r.sted = m[1];
        s = s.replace(/\s(den|d\.|på|til|kl\.?|klokken)(?=\s)/gi, ' ').replace(/[\s.,!:]+/g, ' ').trim().replace('§SLUT§', slutTekst).trim();
        r.titel = s ? s[0].toUpperCase() + s.slice(1) : '';
        return r;
    }
    function quickText(q) {
        if (!q.titel) return 'Skriv hvad det er – fx "Tandlæge".';
        const parts = [q.titel];
        if (q.slut) parts.push(spanText({ day: q.dag, endDay: q.slut }));
        else if (q.dag) parts.push((q.hvertAar ? 'hvert år, næste gang ' : '') + snartFmt.format(new Date(nextDay({ day: q.dag, yearly: q.hvertAar }) + 'T12:00')));
        if (q.hvornaar) parts.push('kl. ' + q.hvornaar);
        if (q.til) parts.push(q.til);
        if (q.sted) parts.push(q.sted);
        let tip = '';
        if (!q.dag) tip = ' Mangler en dag – skriv fx 13/11, fredag eller i morgen. Uden dag står den bare på listen.';
        else if (!q.hvornaar && !q.hvertAar) tip = ' Skriv også klokken (fx kl 14), så får alle en påmindelse før.';
        else if (!q.til && !q.hvertAar && shared()) tip = ' Skriv evt. et navn, så ved alle, hvem det gælder.';
        return 'Forstået: ' + parts.join(' · ') + '.' + tip;
    }
    $('hurtig').addEventListener('input', () => {
        const v = $('hurtig').value.trim();
        $('hurtigGem').disabled = !v || !parseQuick(v).titel;
        $('hurtigViser').textContent = v ? quickText(parseQuick(v)) : 'Skriv hvad, hvem, dag, klokkeslæt og sted – i den rækkefølge du vil. Fx "Julius fødselsdag 12/4 hvert år" eller "Ferie 4/11-7/11 i Kbh".';
    });
    $('hurtig').addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); if (!$('hurtigGem').disabled) $('hurtigGem').click(); } });
    $('hurtigGem').onclick = async () => {
        const q = parseQuick($('hurtig').value);
        if (!q.titel) return;
        await act($('hurtigGem'), () => api('POST', base + '/opgaver', { titel: q.titel, hvornaar: q.hvornaar, note: '', endpoint, til: q.til,
            dag: q.dag, slut: q.slut, sted: q.sted, hvertAar: q.hvertAar && !!q.dag && !q.slut, hverDag: false, navn }));
        $('hurtig').value = ''; $('hurtigGem').disabled = true;
        $('hurtigViser').textContent = 'Lagt ind. Alle i rummet får besked.';
    };
    $('opgDag').onchange = () => {
        $('opgDato').hidden = !['anden', 'aar'].includes($('opgDag').value);
        $('opgSlutBoks').hidden = $('opgDag').value !== 'anden';
        if (!$('opgDato').hidden && !$('opgDato').value) $('opgDato').value = today();
    };
    // "4. – 7. november" / "28. december – 2. januar".
    function spanText(t) {
        const a = new Date(t.day + 'T12:00'), b = new Date(t.endDay + 'T12:00');
        return (a.getMonth() === b.getMonth() ? a.getDate() + '.' : kalMd.format(a)) + ' – ' + kalMd.format(b);
    }
    $('nyOpg').addEventListener('submit', async e => {
        e.preventDefault();
        const til = $('opgTil').value;
        const dag = $('opgDag').value;
        const body = {
            titel: $('opgTitel').value, hvornaar: $('opgHvornaar').value, note: $('opgNote').value, endpoint,
            hverDag: dag === 'hver', hvertAar: dag === 'aar', dag: dag === 'idag' ? today() : dag === 'anden' || dag === 'aar' ? $('opgDato').value : '',
            slut: dag === 'anden' ? $('opgSlut').value : '', sted: $('opgSted').value,
            navn,
        };
        // New task: the chosen person. Edit: only when it was changed (null on the server = keep who has it).
        if (!editId || (til !== '*' && til !== $('opgTil').dataset.was)) body.til = til;
        await act($('opgGem'), () => editId ? api('PUT', base + '/opgaver/' + editId, body) : api('POST', base + '/opgaver', body));
        stopEdit();
    });

    // ---------- files (2026-11-09) ----------

    let files = [];
    const maxFileId = () => files.reduce((m, f) => Math.max(m, f.id), 0);

    async function loadFiles() {
        try { files = await api('GET', base + '/filer'); renderFiles(); } catch { }
    }

    function size(n) {
        if (n < 1024) return n + ' B';
        if (n < 1024 * 1024) return Math.round(n / 1024) + ' KB';
        return (n / 1024 / 1024).toFixed(1).replace('.', ',') + ' MB';
    }

    function icon(f) {
        const t = f.contentType;
        if (t === 'application/pdf') return '📕';
        if (t.startsWith('image/')) return '🖼️';
        if (t.startsWith('audio/')) return '🎵';
        if (t.startsWith('video/')) return '🎬';
        if (/sheet|excel|csv/.test(t)) return '📊';
        if (/presentation|powerpoint/.test(t)) return '📽️';
        if (t === 'application/zip') return '🗜️';
        return '📄';
    }

    function renderFiles() {
        // Badge = files added by others since this phone last looked at the tab.
        const seen = Number(store.get('snak_filer_set_' + roomKey) || 0);
        const fresh = files.filter(f => f.id > seen && f.by.toLowerCase() !== navn.toLowerCase()).length;
        const onTab = !$('viewFil').hidden;
        if (onTab) store.set('snak_filer_set_' + roomKey, String(maxFileId()));
        $('filBadge').textContent = String(fresh);
        $('filBadge').hidden = onTab || fresh === 0;

        // Topics (the workshops): a choice when uploading, and the list grouped under them in the room's order, Andet last.
        const topics = (room && room.emner) || [];
        const sel = $('filEmne');
        const was = sel.options.length ? sel.value : null;   // first fill: the remembered choice, else the first workshop
        $('filEmneBoks').hidden = topics.length === 0;
        sel.textContent = '';
        for (const t of [...topics, 'Andet']) {
            const o = document.createElement('option'); o.value = t === 'Andet' ? '' : t; o.textContent = t; sel.appendChild(o);
        }
        const saved = store.get('snak_emne_' + roomKey);
        const has = v => v !== null && [...sel.options].some(o => o.value === v);
        sel.value = has(was) ? was : has(saved) && saved ? saved : sel.options[0].value;

        const box = $('filer');
        box.textContent = '';
        if (!files.length) {
            const e = document.createElement('div'); e.className = 'tom';
            e.innerHTML = '<strong>Ingen filer endnu</strong>Tryk "Vælg fil" ovenfor for at lægge det første dokument op. De andre får besked.';
            box.appendChild(e);
            return;
        }
        const known = t => topics.includes(t) ? t : '';
        const groups = topics.length ? [...topics, ''] : [null];
        for (const g of groups) {
            const inGroup = g === null ? files : files.filter(f => known(f.topic || '') === g);
            if (!inGroup.length) continue;
            if (g !== null) { const h = document.createElement('h3'); h.className = 'emne-titel'; h.textContent = g || 'Andet'; box.appendChild(h); }
            for (const f of inGroup) box.appendChild(fileCard(f));
        }
    }

    function fileCard(f) {
        {
            const c = document.createElement('article'); c.className = 'k-card fil-kort';
            const ic = document.createElement('span'); ic.className = 'ikon'; ic.textContent = icon(f); ic.setAttribute('aria-hidden', 'true');
            const info = document.createElement('div'); info.className = 'info';
            const a = document.createElement('a'); a.className = 'navn'; a.href = base + '/filer/' + f.id; a.textContent = f.name;
            if (isPicture(f)) a.onclick = e => { e.preventDefault(); showPicture(a.href, f.name); };
            else { a.target = '_blank'; a.rel = 'noopener'; }
            const who = document.createElement('p'); who.className = 'hvem';
            who.textContent = (f.by.toLowerCase() === navn.toLowerCase() ? 'Dig' : f.by) + ' · ' + shortFmt.format(new Date(f.at)) + ' · ' + size(f.size);
            info.append(a, who);
            c.append(ic, info);
            if (admin || f.by.toLowerCase() === navn.toLowerCase()) {
                const del = document.createElement('button'); del.type = 'button'; del.className = 'k-btn k-btn-sm k-btn-ghost'; del.textContent = 'Slet';
                del.onclick = async () => {
                    if (!del.dataset.sure) {
                        del.dataset.sure = '1'; del.textContent = 'Ja, slet'; del.className = 'k-btn k-btn-sm k-btn-danger';
                        setTimeout(() => { delete del.dataset.sure; del.textContent = 'Slet'; del.className = 'k-btn k-btn-sm k-btn-ghost'; }, 4000);
                        return;
                    }
                    del.disabled = true;
                    try { await api('DELETE', base + '/filer/' + f.id + '?navn=' + encodeURIComponent(navn)); await loadFiles(); }
                    catch (err) { if (err.message !== 'login') flash('Filen kunne ikke slettes.'); del.disabled = false; }
                };
                c.appendChild(del);
            }
            return c;
        }
    }

    // ---------- the group's people (2026-11-09) ----------

    // The course's own pages as buttons (Holdet + Filer). They open in a new tab, so the room stays open.
    function renderLinks() {
        const links = (room && room.links) || [];
        for (const sec of document.querySelectorAll('.kursus-links')) {
            sec.hidden = links.length === 0;
            const row = sec.querySelector('.link-knapper'); row.textContent = '';
            for (const l of links) {
                const a = document.createElement('a'); a.className = 'k-btn'; a.href = l.url;
                if (/^https?:/.test(l.url)) { a.target = '_blank'; a.rel = 'noopener'; }
                a.textContent = l.title; row.appendChild(a);
            }
        }
    }

    // ---------- kalender (2026-11-09) ----------
    // A group room with the course's calendar shows Kalender instead of Opgaver, and the next day on every tab.

    function hasCal() { return !!(room && room.harKalender); }
    function calTabs() {
        $('tabKal').hidden = !hasCal();
        $('tabOpg').hidden = isGroup();   // a course group has no tasks (user 2026-11-09: "no opgaver here for a start")
        $('tabNg').hidden = !isGroup();   // ...but "Til næste gang": assignments for the course days
        $('tabDato').hidden = !closesOwn();   // 2026-11-17: shared rooms get their own small calendar (dates from the tasks)
        $('opgHjaelp').hidden = !closesOwn();
        if ($('tabDato').hidden && !$('viewDato').hidden) show('opg');
        $('ngNy').hidden = !admin;
        if (($('tabKal').hidden && !$('viewKal').hidden) || ($('tabOpg').hidden && !$('viewOpg').hidden)) show(hasCal() ? 'kal' : 'chat');
        if (!hasCal()) $('naesteBar').hidden = true;
    }

    const kalDag = new Intl.DateTimeFormat('da-DK', { weekday: 'long', day: 'numeric', month: 'long' });
    let calEvents = [];
    async function loadCalendar() {
        if (!hasCal()) return;
        // Last copy first, so the Næste gang card is there at once and nothing jumps when the fresh list arrives.
        if (!calEvents.length) { try { calEvents = JSON.parse(store.get('snak_kal_' + roomKey) || '[]'); } catch { } if (calEvents.length) renderCalendar(); }
        try {
            const fresh = await api('GET', base + '/kalender');
            if (fresh.length || !calEvents.length) calEvents = fresh;
            store.set('snak_kal_' + roomKey, JSON.stringify(calEvents));
        } catch { }
        renderCalendar();
    }
    function sameDay(a, b) { return a.toDateString() === b.toDateString(); }
    function span(e) {
        const s = new Date(e.start), t = new Date(e.slut);
        return e.heleDagen ? 'Hele dagen' : timeFmt.format(s) + '–' + timeFmt.format(t);
    }
    function mapsLink(place) {
        const a = document.createElement('a'); a.className = 'k-btn k-btn-sm'; a.target = '_blank'; a.rel = 'noopener';
        a.href = 'https://www.openstreetmap.org/search?query=' + encodeURIComponent(place); a.textContent = 'Find vej';
        return a;
    }
    // Several slots with the same title on the same day (pitch training, 4 at a time) = one card with the times.
    function groupDays(list) {
        const out = [];
        for (const e of list) {
            const last = out[out.length - 1];
            if (last && last.titel === e.titel && sameDay(new Date(last.start), new Date(e.start))) last.slots.push(e);
            else out.push({ ...e, slots: [e] });
        }
        return out;
    }
    // ISO week number - Danish calendars count weeks this way.
    function weekNo(d) {
        const t = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
        const day = t.getUTCDay() || 7; t.setUTCDate(t.getUTCDate() + 4 - day);
        return Math.ceil(((t - new Date(Date.UTC(t.getUTCFullYear(), 0, 1))) / 86400000 + 1) / 7);
    }
    const kalMd = new Intl.DateTimeFormat('da-DK', { day: 'numeric', month: 'long' });
    function weekHead(d) {
        const mon = new Date(d); mon.setDate(d.getDate() - ((d.getDay() + 6) % 7));
        const sun = new Date(mon); sun.setDate(mon.getDate() + 6);
        const h = document.createElement('h2'); h.className = 'kal-uge';
        h.textContent = 'Uge ' + weekNo(d) + ' · ' + (mon.getMonth() === sun.getMonth() ? mon.getDate() + '.–' + kalMd.format(sun) : kalMd.format(mon) + ' – ' + kalMd.format(sun));
        return h;
    }
    function calCard(e, now, next) {
        const s = new Date(e.start), c = document.createElement('article');
        const today = sameDay(s, now), over = new Date(e.slots[e.slots.length - 1].slut) < now;
        c.className = 'k-card kal-dag' + (next ? ' naest' : '') + (today ? ' i-dag' : '') + (over ? ' forbi' : '');
        if (next || today) { const tag = document.createElement('div'); tag.className = 'kal-tag'; tag.textContent = today ? 'I dag' : 'Næste gang'; c.appendChild(tag); }
        const dag = kalDag.format(s);
        const d = document.createElement('div'); d.className = 'kal-dato';
        d.textContent = dag.charAt(0).toUpperCase() + dag.slice(1) + (e.slots.length === 1 ? ' · ' + span(e) : '');
        const h = document.createElement('h3'); h.textContent = e.titel;
        c.append(d, h);
        if (e.slots.length > 1) {
            const t = document.createElement('div'); t.className = 'kal-tider';
            t.textContent = 'Tider: ' + e.slots.map(span).join(' · ');
            c.appendChild(t);
        }
        if (e.sted) {
            const p = document.createElement('div'); p.className = 'kal-sted';
            const t = document.createElement('span'); t.textContent = e.sted;
            const a = mapsLink(e.sted); a.className = 'k-btn k-btn-sm k-btn-ghost';
            p.append(t, a); c.appendChild(p);
        }
        return c;
    }
    function renderList(box, list, now, markNext) {
        box.textContent = '';
        let week = -1;
        list.forEach((e, i) => {
            const d = new Date(e.start), w = weekNo(d);
            if (w !== week) { box.appendChild(weekHead(d)); week = w; }
            box.appendChild(calCard(e, now, markNext && i === 0));
        });
    }
    function renderCalendar() {
        const now = new Date();
        const coming = calEvents.filter(e => new Date(e.slut) >= now), past = calEvents.filter(e => new Date(e.slut) < now);
        const box = $('kalListe');
        renderList(box, groupDays(coming), now, true);
        renderList($('kalFoerListe'), groupDays(past).reverse(), now, false);
        $('kalFoer').hidden = past.length === 0;
        $('kalTom').hidden = calEvents.length > 0;
        if (isGroup()) renderNg();
        if (!coming.length && calEvents.length) box.innerHTML = '<p class="k-help">Der er ikke flere kursusdage i kalenderen.</p>';
        // The next day as a big card on the other tabs (the Kalender tab marks it in the list instead).
        const nb = $('naesteBar'), n = coming[0];
        nb.dataset.har = n ? '1' : '';
        nb.hidden = !n || !$('viewKal').hidden;
        if (n) {
            const s = new Date(n.start);
            const days = Math.round((new Date(s.toDateString()) - new Date(now.toDateString())) / 86400000);
            $('naesteHvornaar').textContent = s <= now ? 'I gang nu' : days === 0 ? 'Næste gang · i dag' : days === 1 ? 'Næste gang · i morgen'
                : 'Næste gang · om ' + days + ' dage';
            const dag = kalDag.format(s);
            const nd = $('naesteDag'); nd.textContent = dag.charAt(0).toUpperCase() + dag.slice(1) + ' · ';
            const tid = document.createElement('span'); tid.className = 'naeste-tid'; tid.textContent = span(n); nd.appendChild(tid);
            $('naesteHvad').textContent = n.titel;
            $('naesteSted').textContent = n.sted || '';
            $('naesteVej').hidden = !n.sted;
            $('naesteVej').href = 'https://www.openstreetmap.org/search?query=' + encodeURIComponent(n.sted || '');
            nb.classList.toggle('snart', days <= 1);
            $('naesteAlle').onclick = () => show('kal');
        }
    }
    setInterval(() => { if (hasCal() && !$('app').hidden) renderCalendar(); }, 60000);

    // ---------- profiles (2026-11-09): each person's own card ----------
    let profiles = [];
    async function loadProfiles() { if (!shared()) return; try { profiles = await api('GET', base + '/profiler'); renderRoster(); } catch { } }
    function profileOf(n) { return profiles.find(p => p.name.toLowerCase() === n.toLowerCase()); }
    function contactButtons(p) {
        if (!p) return null;
        const row = document.createElement('div'); row.className = 'kontakt';
        const add = (label, href, ext) => {
            if (!href) return;
            const a = document.createElement('a'); a.className = 'k-btn k-btn-sm'; a.href = href; a.textContent = label;
            if (ext) { a.target = '_blank'; a.rel = 'noopener'; }
            row.appendChild(a);
        };
        add('Mail', p.mail && 'mailto:' + p.mail);
        add('Ring', p.phone && 'tel:' + p.phone);
        add('Hjemmeside', p.web, true);
        add('LinkedIn', p.linkedIn, true);
        return row.children.length ? row : null;
    }
    function openProfileForm(card, n, p, about) {
        if (card.querySelector('form')) return;
        const f = document.createElement('form'); f.className = 'profil-form';
        const field = (id, label, type, value, ph) =>
            `<label class="k-label" for="${id}">${label}</label><input class="k-input" id="${id}" type="${type}" value="" placeholder="${ph}" autocomplete="off" />`;
        f.innerHTML = '<p class="k-help">Alt kan undværes. Skriv kun det, du gerne vil dele – kun dem i rummet kan se det.</p>' +
            field('pMail', 'Mail', 'email', '', 'navn@firma.dk') + field('pTlf', 'Telefon', 'tel', '', '12 34 56 78') +
            field('pWeb', 'Hjemmeside', 'text', '', 'www.mitfirma.dk') + field('pLi', 'LinkedIn', 'text', '', 'linkedin.com/in/ditnavn') +
            '<label class="k-label" for="pOm">' + (isGroup() ? 'Hvad laver du?' : 'Lidt om dig') + '</label><textarea class="k-textarea" id="pOm" maxlength="300" rows="3"></textarea>' +
            '<p class="fejl" hidden></p><div class="k-row"><button class="k-btn k-btn-primary" type="submit">Gem</button>' +
            '<button class="k-btn k-btn-ghost" type="button">Fortryd</button></div>';
        // Values set as properties, never as HTML.
        f.querySelector('#pMail').value = (p && p.mail) || ''; f.querySelector('#pTlf').value = (p && p.phone) || '';
        f.querySelector('#pWeb').value = (p && p.web) || ''; f.querySelector('#pLi').value = (p && p.linkedIn) || '';
        f.querySelector('#pOm').value = about || '';
        f.querySelector('.k-btn-ghost').onclick = () => f.remove();
        f.addEventListener('submit', async e => {
            e.preventDefault();
            const err = f.querySelector('.fejl'); err.hidden = true;
            try {
                await api('PUT', base + '/profil', { navn: n, mail: f.querySelector('#pMail').value, telefon: f.querySelector('#pTlf').value,
                    web: f.querySelector('#pWeb').value, linkedIn: f.querySelector('#pLi').value, om: f.querySelector('#pOm').value });
                await loadProfiles();
            } catch (x) { if (x.message !== 'login') { err.textContent = x.message; err.hidden = false; } }
        });
        card.appendChild(f);
        f.querySelector('#pMail').focus();
    }

    function renderRoster() {
        renderLinks();
        // Trin 2 (2026-11-12): every shared room has its people tab - "Holdet" in a course group, "Personer" in a family room.
        const people = (shared() && room.medlemmer) || [];
        const om = (room && room.om) || {};
        $('tabHold').hidden = people.length === 0;
        $('tabHold').textContent = isGroup() ? 'Holdet' : 'Personer';
        if (!people.length && !$('viewHold').hidden) show('chat');
        $('holdIntro').textContent = isGroup() ? people.length + ' på holdet. Skriv til dem i chatten, eller del en fil under Filer.'
            : people.length + ' i rummet. Tryk på dit eget kort og skriv, hvad du vil dele – telefon, mail eller lidt om dig. Kun dem i rummet kan se det.';
        const box = $('holdListe');
        box.textContent = '';
        // Your own card first, so it is easy to fill in.
        const sorted = [...people].sort((a, b) => (b.toLowerCase() === navn.toLowerCase()) - (a.toLowerCase() === navn.toLowerCase()));
        for (const n of sorted) {
            const c = document.createElement('article');
            const me = n.toLowerCase() === navn.toLowerCase();
            const pr = profileOf(n);
            c.className = 'k-card medlem' + (me ? ' mig-selv' : '');
            const txt = document.createElement('div'); txt.className = 'medlem-tekst';
            const h = document.createElement('h3'); h.textContent = n + (me ? ' (dig)' : '');
            txt.appendChild(h);
            const about = (pr && pr.about) || om[n];
            if (about) { const p = document.createElement('p'); p.className = 'note'; p.textContent = about; txt.appendChild(p); }
            const btns = contactButtons(pr);
            if (btns) txt.appendChild(btns);
            if (me) {
                const ed = document.createElement('button'); ed.type = 'button'; ed.className = 'k-btn k-btn-sm ret-mig';
                ed.textContent = pr ? 'Ret mine oplysninger' : 'Tilføj mail, telefon og hjemmeside';
                ed.onclick = () => openProfileForm(c, n, pr, about);
                txt.appendChild(ed);
            }
            c.append(avatar(n, 'stor'), txt);
            box.appendChild(c);
        }
    }

    $('holdGem').onclick = async () => {
        $('holdGem').disabled = true;
        try {
            await api('PUT', '/api/rum/' + room.id + '/holdet', { medlemmer: $('holdMedlemmer').value, emner: $('holdEmner').value, links: $('holdLinks').value, kalender: $('holdKalender').value });
            room = await api('GET', base);
            renderRoster(); renderFiles();
            $('holdGemt').hidden = false; setTimeout(() => { $('holdGemt').hidden = true; }, 3000);
        } catch (err) { if (err.message !== 'login') flash(err.message); }
        $('holdGem').disabled = false;
    };

    $('filEmne').addEventListener('change', () => store.set('snak_emne_' + roomKey, $('filEmne').value));

    $('filInput').addEventListener('change', async () => {
        const list = [...$('filInput').files];
        $('filInput').value = '';
        const st = $('filStatus');
        st.hidden = false; st.className = 'k-help';
        let ok = 0; const errors = [];
        for (const [i, file] of list.entries()) {
            st.textContent = 'Lægger op' + (list.length > 1 ? ` (${i + 1} af ${list.length})` : '') + ': ' + file.name + ' …';
            if (file.size > 90 * 1024 * 1024) { errors.push(file.name + ': for stor (højst 90 MB)'); continue; }
            const fd = new FormData();
            fd.append('fil', file); fd.append('navn', navn); fd.append('endpoint', endpoint); fd.append('emne', $('filEmne').value);
            try {
                const r = await fetch(base + '/filer', { method: 'POST', credentials: 'same-origin', body: fd });
                const d = await r.json().catch(() => null);
                if (!r.ok) throw new Error((d && d.fejl) || 'Det gik ikke. Prøv igen.');
                ok++;
            } catch (err) { errors.push(file.name + ': ' + err.message); }
        }
        await loadFiles();
        st.textContent = (ok ? (ok === 1 ? 'Filen er lagt op. ' : ok + ' filer er lagt op. ') : '') + errors.join(' · ');
        if (errors.length) st.className = 'k-help fejl';
        setTimeout(() => { if (!errors.length) st.hidden = true; }, 5000);
    });

    // ---------- push (on by default) ----------

    function b64ToBytes(s) {
        const pad = '='.repeat((4 - s.length % 4) % 4);
        const raw = atob((s + pad).replace(/-/g, '+').replace(/_/g, '/'));
        return Uint8Array.from(raw, ch => ch.charCodeAt(0));
    }

    async function subscribe() {
        const reg = await navigator.serviceWorker.ready;
        let sub = await reg.pushManager.getSubscription();
        if (!sub) sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: b64ToBytes(pushKey) });
        const j = sub.toJSON();
        endpoint = j.endpoint;
        // Martin's phone: every room. A customer's phone: this room only.
        await api('POST', roomKey && !alleRum ? base + '/push' : '/api/push', { endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth, navn });
    }

    // Called on every start: if the phone already said yes, (re)register silently; otherwise show
    // one line with what to do. `ask` = the person tapped "Slå beskeder til".
    async function setupPush(ask) {
        const bar = $('rumside').hidden ? $('pushBar') : $('pushBar2');
        bar.className = 'bar';
        if ((store.get('snak_push_fra') && !ask) || inApp) { bar.hidden = true; return; }
        if (!pushPossible) {
            if (isIos && !standalone) {
                bar.innerHTML = '<b>Få besked på din iPhone:</b> sæt Snak på hjemmeskærmen først. <button type="button" class="link" id="visHjem">Sådan gør du</button>';
                bar.hidden = false;
                $('visHjem').onclick = () => show('mig');
            } else bar.hidden = true;
            return;
        }
        if (Notification.permission === 'default' && ask) await Notification.requestPermission().catch(() => {});
        if (Notification.permission === 'granted') {
            try { await subscribe(); store.set('snak_push_fra', ''); bar.hidden = true; }
            catch { bar.textContent = 'Beskeder på telefonen kunne ikke slås til lige nu. Prøv igen senere under ⋯.'; bar.hidden = false; }
        } else if (Notification.permission === 'denied') {
            bar.innerHTML = '<b>Du får ikke besked om nye beskeder.</b> Du har sagt nej til beskeder. <button type="button" class="link" id="visHjem">Sådan slår du dem til</button>';
            bar.hidden = false;
            $('visHjem').onclick = () => show('mig');
        } else {
            bar.innerHTML = '<b>Få besked, når der kommer noget nyt.</b> <button type="button" class="k-btn k-btn-sm k-btn-primary" id="barTil">Slå beskeder til</button>';
            bar.hidden = false;
            $('barTil').onclick = () => setupPush(true);
        }
        pushStatus();
    }

    function pushStatus() {
        const s = $('pushStatus');
        const on = pushPossible && Notification.permission === 'granted' && endpoint && !store.get('snak_push_fra');
        $('pushTil').hidden = !!on; $('pushFra').hidden = !on;
        if (on) s.textContent = 'Slået til. Du får besked, når der kommer en ny besked eller opgave i rummet.';
        else if (!pushPossible && isIos && !standalone) s.textContent = 'På iPhone skal Snak ligge på hjemmeskærmen, før den kan give besked. Se nedenfor, og åbn så Snak fra ikonet.';
        else if (!pushPossible) s.textContent = 'Denne browser kan ikke give besked. Brug Chrome, Edge eller Safari.';
        else if (Notification.permission === 'denied') s.textContent = isIos
            ? 'Du har sagt nej. Åbn Indstillinger → Notifikationer → Snak (Settings → Notifications → Snak) og slå Tillad notifikationer til.'
            : 'Du har sagt nej. Tryk på låsen ved adressen øverst → Notifikationer → Tillad (Notifications → Allow), og tryk så Slå beskeder til.';
        else s.textContent = 'Slået fra. Tryk Slå beskeder til, og sig Tillad.';
    }

    $('pushTil').onclick = async () => { store.set('snak_push_fra', ''); await setupPush(true); pushStatus(); reportSeen(); };
    $('pushFra').onclick = async () => {
        store.set('snak_push_fra', '1');
        try {
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.getSubscription();
            if (sub) { await api('POST', '/api/push/fra', { endpoint: sub.endpoint }); await sub.unsubscribe(); }
        } catch { }
        endpoint = ''; $('pushBar').hidden = true; pushStatus(); reportSeen();
    };

    function openHomeGuide() {
        $('hjemBoks').hidden = standalone;
        const id = isIos ? 'hjemIphone' : /android/i.test(navigator.userAgent) ? 'hjemAndroid' : 'hjemPc';
        $(id).open = true;
    }

    // ---------- me ----------

    $('gemNavn').onclick = async () => {
        const n = $('nytNavn').value.trim();
        if (!n) return;
        navn = n; store.set('snak_navn', n);
        $('hvem').textContent = navn;
        renderTasks(); renderMessages();
        if (endpoint) setupPush(false);
        $('gemNavn').textContent = 'Gemt';
        setTimeout(() => { $('gemNavn').textContent = 'Gem'; }, 1500);
    };
    // The room's own tovholder code (2026-11-08): admin in this room only.
    $('tovForm').addEventListener('submit', async e => {
        e.preventDefault();
        $('tovFejl').hidden = true;
        try {
            await api('POST', base + '/admin', { kode: $('tovKode').value.trim() });
            $('tovKode').value = '';
            room = await api('GET', base);
            admin = !!room.admin; navn = vaert;
            enter(); show('opg');
        } catch (err) { $('tovFejl').textContent = err.message; $('tovFejl').hidden = false; }
    });

    $('logUd').onclick = async () => {
        try { await fetch('/api/logout', { method: 'POST', credentials: 'same-origin' }); } catch { }
        store.set('snak_rum', ''); navn = ''; store.set('snak_navn', '');
        location.href = '/';
    };

    // ---------- light/dark (the button in the Kolibri header) ----------

    function syncTheme() {
        const cur = document.documentElement.getAttribute('data-theme') || 'light';
        $('themeToggleBtn').textContent = cur === 'light' ? 'Mørk' : 'Lys';
        document.querySelector('meta[name=theme-color]')?.setAttribute('content', cur === 'light' ? '#f4f6f5' : '#0b1220');
    }
    $('themeToggleBtn').onclick = () => {
        const next = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', next);
        store.set('snak_theme', next);
        syncTheme();
    };
    syncTheme();

    boot();
})();
