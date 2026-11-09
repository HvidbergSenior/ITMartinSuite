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
    let admin = false, alleRum = false, pushKey = '', endpoint = '', vaert = 'Martin', room = null;
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
                admin = !!room.admin; alleRum = !!room.alleRum; pushKey = room.pushKey || ''; vaert = room.vaert || 'Martin';
                store.set('snak_rum', roomKey);   // a phone that opens "/" (home-screen icon) comes back here
                document.title = room.titel + ' · Snak';
                if (admin) navn = vaert;
                if (!navn) return showLogin();
                return enter();
            }
            const me = await (await fetch('/api/me', { credentials: 'same-origin' })).json();
            admin = alleRum = !!me.admin; pushKey = me.pushKey || ''; vaert = me.vaert || 'Martin';
            if (admin) return showRooms();
            const last = store.get('snak_rum');
            if (last) { location.replace('/r/' + last); return; }
            showOnly('forside');
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
        for (const n of names) {
            const b = document.createElement('button'); b.type = 'button'; b.className = 'k-btn'; b.textContent = n;
            b.onclick = () => { $('loginNavn').value = n; $('loginForm').requestSubmit(); };
            box.appendChild(b);
        }
        if (names.length) {
            $('loginLead').textContent = 'Her skriver I sammen og ser, hvem der gør hvad. Tryk på dit navn.';
            document.querySelector('label[for=loginNavn]').textContent = 'Eller skriv dit navn';
            document.querySelector('#loginNavn + .k-help').textContent = 'Det er det navn, de andre ser ved dine beskeder.';
            document.querySelector('#loginFejl + .k-help').innerHTML = 'Når du går ind, spørger telefonen, om Snak må give dig besked. Sig <b>Tillad</b>, så får du besked om nye opgaver og påmindelser.';
        }
        // A fixed group (a course team, a club): the door speaks to the group (2026-11-09).
        if (room && room.gruppe) {
            $('loginLead').textContent = 'Her skriver holdet sammen og deler dokumenter. ' + (names.length ? 'Tryk på dit navn.' : 'Skriv dit navn.');
            document.querySelector('#loginNavn + .k-help').textContent = 'Det er det navn, de andre ser ved dine beskeder og filer.';
        }
    }

    function shared() { return !!(room && room.medlemmer && room.medlemmer.length); }

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
        enter();
    });

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
        $('nyOpg').hidden = !admin;
        $('tovBoks').hidden = admin || !(room && room.harKode);
        fillTil('');
        $('opgIntro').textContent = admin
            ? (shared() ? 'Læg opgaver ud her, og giv dem til en – eller lad den, der har tid, tage den. Alle får besked.'
                        : 'Læg opgaver ud her. Kunden ser dem og trykker "Jeg tager den". Du kan se, hvem der har taget hvad.')
            : shared() ? 'Her er det, der skal gøres. Står dit navn på en opgave, er den din. Tryk "Jeg tager den" på en, der mangler nogen.'
                       : 'Her er det, der skal gøres. Tryk "Jeg tager den" på en opgave, så ved Martin, at du gør den.';
        $('omSlet').hidden = !!(room && room.gruppe);
        klaretBar();
        renderRoster();
        $('holdAdmin').hidden = !(alleRum && room && room.gruppe);
        if (alleRum && room && room.gruppe) {
            const om = room.om || {};
            $('holdMedlemmer').value = (room.medlemmer || []).map(n => om[n] ? n + ' – ' + om[n] : n).join('\n');
            $('holdEmner').value = (room.emner || []).join('\n');
        }
        const tab = store.get('snak_tab') || (shared() ? 'opg' : 'chat');
        show(['opg', 'fil'].includes(tab) || (tab === 'hold' && !$('tabHold').hidden) ? tab : 'chat');
        loadMessages(); loadTasks(); loadFiles(); connect();
        setupPush(false);
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
        for (const [v, t] of [['chat', 'tabChat'], ['opg', 'tabOpg'], ['fil', 'tabFil'], ['hold', 'tabHold'], ['mig', 'tabMig']]) {
            $('view' + v[0].toUpperCase() + v.slice(1)).hidden = v !== which;
            $(t).setAttribute('aria-selected', String(v === which));
        }
        if (which !== 'mig') store.set('snak_tab', which);
        if (which === 'mig') { pushStatus(); openHomeGuide(); }
        if (which === 'fil') { store.set('snak_filer_set_' + roomKey, String(maxFileId())); renderFiles(); }
    }
    $('tabChat').onclick = () => show('chat');
    $('tabOpg').onclick = () => show('opg');
    $('tabFil').onclick = () => show('fil');
    $('tabHold').onclick = () => show('hold');
    $('tabMig').onclick = () => show('mig');

    // ---------- live ----------

    function connect() {
        if (live) live.close();
        live = new EventSource(base + '/live');
        // "hej" comes on every (re)connect: fetch whatever was missed while the line was down.
        live.addEventListener('hej', () => { loadMessages(); loadTasks(); loadFiles(); });
        live.addEventListener('besked', e => addMessages([JSON.parse(e.data)]));
        live.addEventListener('opgaver', e => { tasks = JSON.parse(e.data); renderTasks(); });
        live.addEventListener('filer', e => { files = JSON.parse(e.data); renderFiles(); });
        live.addEventListener('holdet', e => {
            const d = JSON.parse(e.data);
            if (room) { room.medlemmer = d.medlemmer; room.om = d.om; room.emner = d.emner; }
            renderRoster(); renderFiles();
        });
    }
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState !== 'visible') return;
        if (!$('rumside').hidden) { loadRooms(); return; }
        if ($('app').hidden) return;
        if (!live || live.readyState === 2) connect(); else { loadMessages(); loadTasks(); loadFiles(); }
    });

    // ---------- chat ----------

    const dayFmt = new Intl.DateTimeFormat('da-DK', { weekday: 'long', day: 'numeric', month: 'long' });
    const timeFmt = new Intl.DateTimeFormat('da-DK', { hour: '2-digit', minute: '2-digit' });
    let msgs = [];

    async function loadMessages() {
        try { addMessages(await api('GET', base + '/beskeder?efter=' + lastId)); } catch { }
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
    }

    function renderMessages() {
        const box = $('beskeder');
        $('tomChat').hidden = msgs.length > 0;
        box.textContent = '';
        let day = '';
        for (let i = msgs.length - 1; i >= 0; i--) {
            const m = msgs[i];
            const when = new Date(m.at);
            const d = dayFmt.format(when);
            if (d !== day) {
                const el = document.createElement('div'); el.className = 'dag'; el.textContent = d;
                box.appendChild(el); day = d;
            }
            const me = admin ? !!m.host : (!m.host && m.name.toLowerCase() === navn.toLowerCase());
            const el = document.createElement('div'); el.className = 'besked' + (me ? ' mig' : '');
            const meta = document.createElement('div'); meta.className = 'meta';
            meta.textContent = (me ? 'Dig' : m.name) + ' · ' + timeFmt.format(when);
            const b = document.createElement('div'); b.className = 'boble'; b.textContent = m.text;
            el.append(meta, b); box.appendChild(el);
        }
    }

    async function send() {
        const t = $('tekst').value.trim();
        if (!t) return;
        $('sendBtn').disabled = true;
        try {
            const m = await api('POST', base + '/beskeder', { navn, tekst: t, endpoint });
            if (room && room.klaret) { room.klaret = false; klaretBar(); }
            $('tekst').value = ''; grow();
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
            tasks = await api('GET', base + '/opgaver'); renderTasks();
        } catch { }
    }

    // ---- today (2026-11-08): the first picture = today's tasks in clock order, the next one at the top ----
    let todayStr = '';
    function today() { return todayStr || new Date().toLocaleDateString('sv-SE'); }
    function isDone(t) { return t.daily ? t.doneOn === today() : t.done; }
    function isToday(t) { return t.daily || t.day === today() || (!t.day && !t.done); }
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
        const add = n => { if (n && n !== vaert && !names.some(x => x.toLowerCase() === n.toLowerCase())) names.push(n); };
        for (const n of (room && room.medlemmer) || []) add(n);
        for (const m of msgs) if (!m.host) add(m.name);
        for (const t of tasks) t.takers.forEach(add);
        sel.textContent = '';
        const free = document.createElement('option'); free.value = ''; free.textContent = 'Den der har tid (alle kan tage den)'; sel.appendChild(free);
        for (const n of [vaert, ...names]) { const o = document.createElement('option'); o.value = n; o.textContent = n === vaert ? n + ' (mig)' : n; sel.appendChild(o); }
        sel.value = selected || '';
    }

    function isMine(t) { return t.takers.some(x => x.toLowerCase() === navn.toLowerCase()); }

    function renderTasks() {
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

        if (admin && !editId) fillTil($('opgTil').value);
        const box = $('opgaver');
        box.textContent = '';
        if (!dayList.length) {
            const e = document.createElement('div'); e.className = 'tom';
            e.innerHTML = admin
                ? '<strong>Ingen opgaver i dag</strong>Skriv en opgave i boksen nedenfor og tryk "Læg opgaven ud". De andre får besked.'
                : '<strong>Ingen opgaver i dag</strong>Når tovholderen lægger en opgave ud, kommer den her, og du får besked.';
            box.appendChild(e);
        }
        for (const t of dayList.sort(byClock)) box.appendChild(taskCard(t));

        // The tovholder also sees the other days (coming first, then old ones).
        const other = tasks.filter(t => !isToday(t));
        $('andreDage').hidden = !(admin && other.length);
        const ob = $('opgAndre'); ob.textContent = '';
        if (admin) for (const t of other.sort((a, b) => (b.day < today()) - (a.day < today()) || (a.day < b.day ? -1 : a.day > b.day ? 1 : byClock(a, b)))) ob.appendChild(taskCard(t));
    }

    function taskCard(t) {
        const c = document.createElement('article');
        // k-card + heading first = kolibri.js makes it foldable (tap the title), and remembers it per task.
        const done = isDone(t);
        c.className = 'k-card opgave' + (done ? ' done' : t.takers.length ? ' taget' : ' fri');
        const h = document.createElement('h3'); h.textContent = (clock(t) ? 'Kl. ' + clock(t) + ' · ' : '') + t.title;
        c.appendChild(h);
        const st = document.createElement('p'); st.className = 'status';
        st.textContent = (done ? '✓ Gjort' : t.takers.length ? '● Taget' : '○ Mangler nogen') + (t.daily ? ' · hver dag' : '');
        c.appendChild(st);
        const dayText = t.day && t.day !== today() ? dateFmt.format(new Date(t.day + 'T12:00')) : '';
        if (dayText || (t.when && !clock(t))) {
            const w = document.createElement('p'); w.className = 'hvornaar';
            w.textContent = [dayText, clock(t) ? '' : t.when].filter(Boolean).join(' · '); c.appendChild(w);
        }
        if (t.note) { const n = document.createElement('p'); n.className = 'note'; n.textContent = t.note; c.appendChild(n); }
        const who = document.createElement('p'); who.className = 'hvem';
        who.textContent = t.takers.length ? 'Tager den: ' + t.takers.join(', ') : 'Ingen har taget den endnu.';
        c.appendChild(who);

        const row = document.createElement('div'); row.className = 'k-row';
        if (!done) {
            const take = document.createElement('button'); take.type = 'button';
            take.className = 'k-btn ' + (isMine(t) ? '' : 'k-btn-primary');
            take.textContent = isMine(t) ? 'Jeg kan alligevel ikke' : 'Jeg tager den';
            take.onclick = () => act(take, () => api('POST', `${base}/opgaver/${t.id}/tag`, { navn, endpoint }));
            row.appendChild(take);
        }
        if (admin) {
            const doneBtn = document.createElement('button'); doneBtn.type = 'button'; doneBtn.className = 'k-btn k-btn-sm';
            doneBtn.textContent = done ? 'Åbn igen' : t.daily ? 'Gjort i dag' : 'Færdig';
            doneBtn.onclick = () => act(doneBtn, () => api('POST', `${base}/opgaver/${t.id}/faerdig`, { faerdig: !done }));
            const edit = document.createElement('button'); edit.type = 'button'; edit.className = 'k-btn k-btn-sm k-btn-ghost';
            edit.textContent = 'Ret';
            edit.onclick = () => startEdit(t);
            const del = document.createElement('button'); del.type = 'button'; del.className = 'k-btn k-btn-sm k-btn-ghost';
            del.textContent = 'Slet';
            del.onclick = () => {
                // Two taps: the first asks, the second deletes.
                if (del.dataset.sure) return act(del, () => api('DELETE', `${base}/opgaver/${t.id}`));
                del.dataset.sure = '1'; del.textContent = 'Ja, slet den'; del.className = 'k-btn k-btn-sm k-btn-danger';
                setTimeout(() => { delete del.dataset.sure; del.textContent = 'Slet'; del.className = 'k-btn k-btn-sm k-btn-ghost'; }, 4000);
            };
            row.append(doneBtn, edit, del);
        }
        c.appendChild(row);
        return c;
    }

    async function act(btn, fn) {
        btn.disabled = true;
        try { await fn(); await loadTasks(); } catch (err) { if (err.message !== 'login') flash(err.message); }
        btn.disabled = false;
    }

    function startEdit(t) {
        editId = t.id;
        $('opgTitel').value = t.title; $('opgHvornaar').value = clock(t); $('opgNote').value = t.note;
        $('opgDag').value = t.daily ? 'hver' : !t.day ? 'ingen' : t.day === today() ? 'idag' : 'anden';
        $('opgDato').value = t.day || ''; $('opgDato').hidden = $('opgDag').value !== 'anden';
        // Several on it: the list shows "as it is now", so saving never removes anyone by accident.
        fillTil(t.takers.length === 1 ? t.takers[0] : '');
        if (t.takers.length > 1) {
            const keep = document.createElement('option'); keep.value = '*'; keep.textContent = 'Som nu: ' + t.takers.join(', ');
            $('opgTil').prepend(keep); $('opgTil').value = '*';
        }
        $('opgTil').dataset.was = $('opgTil').value;
        $('nyTitel').textContent = 'Ret opgave'; $('opgGem').textContent = 'Gem ændringen'; $('opgFortryd').hidden = false;
        $('nyOpg').scrollIntoView({ behavior: 'smooth' }); $('opgTitel').focus();
    }
    function stopEdit() {
        editId = 0; $('nyOpg').reset(); fillTil(''); $('opgDato').hidden = true;
        $('nyTitel').textContent = 'Ny opgave'; $('opgGem').textContent = 'Læg opgaven ud'; $('opgFortryd').hidden = true;
    }
    $('opgFortryd').onclick = stopEdit;
    $('opgDag').onchange = () => { $('opgDato').hidden = $('opgDag').value !== 'anden'; if (!$('opgDato').hidden && !$('opgDato').value) $('opgDato').value = today(); };
    $('nyOpg').addEventListener('submit', async e => {
        e.preventDefault();
        const til = $('opgTil').value;
        const dag = $('opgDag').value;
        const body = {
            titel: $('opgTitel').value, hvornaar: $('opgHvornaar').value, note: $('opgNote').value, endpoint,
            hverDag: dag === 'hver', dag: dag === 'idag' ? today() : dag === 'anden' ? $('opgDato').value : '',
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
            a.target = '_blank'; a.rel = 'noopener';
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

    function renderRoster() {
        // Only a fixed group has the Holdet tab; a shared family room keeps its names at the door and on the tasks.
        const people = (room && room.gruppe && room.medlemmer) || [];
        const om = (room && room.om) || {};
        $('tabHold').hidden = people.length === 0;
        if (!people.length && !$('viewHold').hidden) show('chat');
        $('holdIntro').textContent = people.length + ' på holdet. Skriv til dem i chatten, eller del en fil under Filer.';
        const box = $('holdListe');
        box.textContent = '';
        for (const n of people) {
            const c = document.createElement('article');
            const me = n.toLowerCase() === navn.toLowerCase();
            c.className = 'k-card medlem' + (me ? ' mig-selv' : '');
            const h = document.createElement('h3'); h.textContent = n + (me ? ' (dig)' : '');
            c.appendChild(h);
            if (om[n]) { const p = document.createElement('p'); p.className = 'note'; p.textContent = om[n]; c.appendChild(p); }
            box.appendChild(c);
        }
    }

    $('holdGem').onclick = async () => {
        $('holdGem').disabled = true;
        try {
            await api('PUT', '/api/rum/' + room.id + '/holdet', { medlemmer: $('holdMedlemmer').value, emner: $('holdEmner').value });
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
            if (file.size > 25 * 1024 * 1024) { errors.push(file.name + ': for stor (højst 25 MB)'); continue; }
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

    $('pushTil').onclick = async () => { store.set('snak_push_fra', ''); await setupPush(true); pushStatus(); };
    $('pushFra').onclick = async () => {
        store.set('snak_push_fra', '1');
        try {
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.getSubscription();
            if (sub) { await api('POST', '/api/push/fra', { endpoint: sub.endpoint }); await sub.unsubscribe(); }
        } catch { }
        endpoint = ''; $('pushBar').hidden = true; pushStatus();
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

    boot();
})();
