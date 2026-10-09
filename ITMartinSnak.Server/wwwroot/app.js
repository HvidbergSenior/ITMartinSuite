// Snak - the whole page. Plain JavaScript, no framework: login, chat, tasks, push.
(function () {
    'use strict';
    const $ = id => document.getElementById(id);
    const store = {
        get(k) { try { return localStorage.getItem(k) || ''; } catch { return ''; } },
        set(k, v) { try { localStorage.setItem(k, v); } catch { } },
    };

    let navn = store.get('snak_navn');
    let admin = false, pushKey = '', endpoint = '';
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
        $('browserNavn').textContent = name; $('browserNavn2').textContent = name;
        $('aabnBrowser').href = android
            ? 'intent://snak.itmartin.dk/#Intent;scheme=https;package=com.android.chrome;S.browser_fallback_url=https%3A%2F%2Fsnak.itmartin.dk%2F;end'
            : 'x-safari-https://snak.itmartin.dk/';
        $('inApp').hidden = false;
    }

    async function api(method, url, body) {
        const r = await fetch(url, {
            method, credentials: 'same-origin',
            headers: body ? { 'Content-Type': 'application/json' } : {},
            body: body ? JSON.stringify(body) : undefined,
        });
        if (r.status === 401) { showLogin(); throw new Error('login'); }
        const data = r.headers.get('content-type')?.includes('json') ? await r.json() : null;
        if (!r.ok) throw new Error((data && data.fejl) || 'Det gik ikke. Prøv igen.');
        return data;
    }

    // ---------- start ----------

    async function boot() {
        try {
            const r = await fetch('/api/me', { credentials: 'same-origin' });
            if (!r.ok || !navn) return showLogin();
            const me = await r.json();
            enter(me);
        } catch { showLogin(); }
    }

    function showLogin() {
        $('app').hidden = true;
        $('login').hidden = false;
        if (navn) $('loginNavn').value = navn;
        if (live) { live.close(); live = null; }
    }

    $('loginForm').addEventListener('submit', async e => {
        e.preventDefault();
        $('loginFejl').hidden = true;
        // Ask for notifications straight away, inside the tap - iPhone only allows the question there.
        const perm = pushPossible && Notification.permission === 'default'
            ? Notification.requestPermission().catch(() => 'default') : Promise.resolve(null);
        const kode = $('loginKode').value.trim();
        const n = $('loginNavn').value.trim();
        try {
            const r = await fetch('/api/login', {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ kode }),
            });
            const d = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(d.fejl || 'Det gik ikke. Prøv igen.');
            navn = n; store.set('snak_navn', n);
            const me = await (await fetch('/api/me', { credentials: 'same-origin' })).json();
            await perm;
            enter(me);
        } catch (err) {
            $('loginFejl').textContent = err.message;
            $('loginFejl').hidden = false;
        }
    });

    function enter(me) {
        admin = !!me.admin; pushKey = me.pushKey || '';
        $('login').hidden = true;
        $('app').hidden = false;
        $('hvem').textContent = navn + (admin ? ' · tovholder' : '');
        $('nytNavn').value = navn;
        $('hjemKnap').hidden = standalone || inApp;
        $('hjemKnap').onclick = () => show('mig');
        $('nyOpg').hidden = !admin;
        $('opgIntro').textContent = admin
            ? 'Læg opgaver ud her. De andre ser dem og trykker "Jeg tager den". Du kan se, hvem der har taget hvad.'
            : 'Her er opgaverne. Tryk "Jeg tager den" på en opgave, så kan alle se, at du gør den.';
        const tab = store.get('snak_tab') || 'chat';
        show(tab === 'opg' ? 'opg' : 'chat');
        loadMessages(); loadTasks(); connect();
        setupPush(false);
    }

    // ---------- tabs ----------

    function show(which) {
        for (const [v, t] of [['chat', 'tabChat'], ['opg', 'tabOpg'], ['mig', 'tabMig']]) {
            $('view' + v[0].toUpperCase() + v.slice(1)).hidden = v !== which;
            $(t).setAttribute('aria-selected', String(v === which));
        }
        if (which !== 'mig') store.set('snak_tab', which);
        if (which === 'mig') { pushStatus(); openHomeGuide(); }
    }
    $('tabChat').onclick = () => show('chat');
    $('tabOpg').onclick = () => show('opg');
    $('tabMig').onclick = () => show('mig');

    // ---------- live ----------

    function connect() {
        if (live) live.close();
        live = new EventSource('/api/live');
        // "hej" comes on every (re)connect: fetch whatever was missed while the line was down.
        live.addEventListener('hej', () => { loadMessages(); loadTasks(); });
        live.addEventListener('besked', e => addMessages([JSON.parse(e.data)]));
        live.addEventListener('opgaver', e => { tasks = JSON.parse(e.data); renderTasks(); });
    }
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState !== 'visible' || $('app').hidden) return;
        if (!live || live.readyState === 2) connect(); else { loadMessages(); loadTasks(); }
    });

    // ---------- chat ----------

    const dayFmt = new Intl.DateTimeFormat('da-DK', { weekday: 'long', day: 'numeric', month: 'long' });
    const timeFmt = new Intl.DateTimeFormat('da-DK', { hour: '2-digit', minute: '2-digit' });
    let msgs = [];

    async function loadMessages() {
        try { addMessages(await api('GET', '/api/beskeder?efter=' + lastId)); } catch { }
    }

    // Newest message first, right under the text field (user: "always the latest one first, I only
    // need to scroll if I want to see earlier messages"). Nothing ever scrolls by itself.
    function addMessages(list) {
        const fresh = list.filter(m => m.id > lastId);
        if (!fresh.length) return;
        for (const m of fresh) { msgs.push(m); lastId = Math.max(lastId, m.id); }
        const box = $('beskeder');
        // Reading older messages further down: keep them exactly where they are on the screen.
        const before = box.scrollHeight, top = box.scrollTop;
        renderMessages();
        if (top > 0) box.scrollTop = top + (box.scrollHeight - before);
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
            const me = m.name.toLowerCase() === navn.toLowerCase();
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
            const m = await api('POST', '/api/beskeder', { navn, tekst: t, endpoint });
            $('tekst').value = ''; grow();
            addMessages([m]);
            $('beskeder').scrollTop = 0;   // your own message: show it at the top
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
        try { tasks = await api('GET', '/api/opgaver'); renderTasks(); } catch { }
    }

    function isMine(t) { return t.takers.some(x => x.toLowerCase() === navn.toLowerCase()); }

    function renderTasks() {
        const open = tasks.filter(t => !t.done);
        const free = open.filter(t => t.takers.length === 0);
        const badge = $('opgBadge');
        badge.textContent = String(free.length);
        badge.hidden = free.length === 0;
        badge.title = free.length + ' opgaver uden nogen på';

        const ov = $('opgOversigt');
        $('opgOversigtBoks').hidden = !(admin && tasks.length);
        if (admin && tasks.length) {
            ov.textContent = '';
            for (const [n, label, cls] of [[open.length, 'åbne', ''], [free.length, 'mangler nogen', free.length ? 'advar' : ''], [tasks.length - open.length, 'færdige', '']]) {
                const d = document.createElement('div'); d.className = 'tal ' + cls;
                const b = document.createElement('b'); b.textContent = n;
                const s = document.createElement('span'); s.textContent = label;
                d.append(b, s); ov.appendChild(d);
            }
        }

        const box = $('opgaver');
        box.textContent = '';
        if (!tasks.length) {
            const e = document.createElement('div'); e.className = 'tom';
            e.innerHTML = admin
                ? '<strong>Ingen opgaver endnu</strong>Skriv den første opgave i boksen ovenfor og tryk "Læg opgaven ud". De andre får besked.'
                : '<strong>Ingen opgaver endnu</strong>Når tovholderen lægger en opgave ud, kommer den her, og du får besked.';
            box.appendChild(e);
            return;
        }
        // Free tasks first, then taken, then done.
        const order = t => t.done ? 2 : (t.takers.length ? 1 : 0);
        for (const t of [...tasks].sort((a, b) => order(a) - order(b) || a.id - b.id)) box.appendChild(taskCard(t));
    }

    function taskCard(t) {
        const c = document.createElement('article');
        // k-card + heading first = kolibri.js makes it foldable (tap the title), and remembers it per task.
        c.className = 'k-card opgave' + (t.done ? ' done' : t.takers.length ? ' taget' : ' fri');
        const h = document.createElement('h3'); h.textContent = t.title;
        c.appendChild(h);
        const st = document.createElement('p'); st.className = 'status';
        st.textContent = t.done ? '✓ Færdig' : t.takers.length ? '● Taget' : '○ Mangler nogen';
        c.appendChild(st);
        if (t.when) { const w = document.createElement('p'); w.className = 'hvornaar'; w.textContent = '🕑 ' + t.when; c.appendChild(w); }
        if (t.note) { const n = document.createElement('p'); n.className = 'note'; n.textContent = t.note; c.appendChild(n); }
        const who = document.createElement('p'); who.className = 'hvem';
        who.textContent = t.takers.length ? 'Tager den: ' + t.takers.join(', ') : 'Ingen har taget den endnu.';
        c.appendChild(who);

        const row = document.createElement('div'); row.className = 'k-row';
        if (!t.done) {
            const take = document.createElement('button'); take.type = 'button';
            take.className = 'k-btn ' + (isMine(t) ? '' : 'k-btn-primary');
            take.textContent = isMine(t) ? 'Jeg kan alligevel ikke' : 'Jeg tager den';
            take.onclick = () => act(take, () => api('POST', `/api/opgaver/${t.id}/tag`, { navn, endpoint }));
            row.appendChild(take);
        }
        if (admin) {
            const done = document.createElement('button'); done.type = 'button'; done.className = 'k-btn k-btn-sm';
            done.textContent = t.done ? 'Åbn igen' : 'Færdig';
            done.onclick = () => act(done, () => api('POST', `/api/opgaver/${t.id}/faerdig`, { faerdig: !t.done }));
            const edit = document.createElement('button'); edit.type = 'button'; edit.className = 'k-btn k-btn-sm k-btn-ghost';
            edit.textContent = 'Ret';
            edit.onclick = () => startEdit(t);
            const del = document.createElement('button'); del.type = 'button'; del.className = 'k-btn k-btn-sm k-btn-ghost';
            del.textContent = 'Slet';
            del.onclick = () => {
                // Two taps: the first asks, the second deletes.
                if (del.dataset.sure) return act(del, () => api('DELETE', `/api/opgaver/${t.id}`));
                del.dataset.sure = '1'; del.textContent = 'Ja, slet den'; del.className = 'k-btn k-btn-sm k-btn-danger';
                setTimeout(() => { delete del.dataset.sure; del.textContent = 'Slet'; del.className = 'k-btn k-btn-sm k-btn-ghost'; }, 4000);
            };
            row.append(done, edit, del);
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
        $('opgTitel').value = t.title; $('opgHvornaar').value = t.when; $('opgNote').value = t.note;
        $('nyTitel').textContent = 'Ret opgave'; $('opgGem').textContent = 'Gem ændringen'; $('opgFortryd').hidden = false;
        $('nyOpg').scrollIntoView({ behavior: 'smooth' }); $('opgTitel').focus();
    }
    function stopEdit() {
        editId = 0; $('nyOpg').reset();
        $('nyTitel').textContent = 'Ny opgave'; $('opgGem').textContent = 'Læg opgaven ud'; $('opgFortryd').hidden = true;
    }
    $('opgFortryd').onclick = stopEdit;
    $('nyOpg').addEventListener('submit', async e => {
        e.preventDefault();
        const body = { titel: $('opgTitel').value, hvornaar: $('opgHvornaar').value, note: $('opgNote').value, endpoint };
        await act($('opgGem'), () => editId ? api('PUT', '/api/opgaver/' + editId, body) : api('POST', '/api/opgaver', body));
        stopEdit();
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
        await api('POST', '/api/push', { endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth, navn });
    }

    // Called on every start: if the phone already said yes, (re)register silently; otherwise show
    // one line with what to do. `ask` = the person tapped "Slå beskeder til".
    async function setupPush(ask) {
        const bar = $('pushBar');
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
        if (on) s.textContent = 'Slået til. Du får besked, når der kommer en ny besked eller opgave.';
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
        $('hvem').textContent = navn + (admin ? ' · tovholder' : '');
        renderTasks(); renderMessages();
        if (endpoint) setupPush(false);
        $('gemNavn').textContent = 'Gemt';
        setTimeout(() => { $('gemNavn').textContent = 'Gem'; }, 1500);
    };
    $('logUd').onclick = async () => {
        try { await fetch('/api/logout', { method: 'POST', credentials: 'same-origin' }); } catch { }
        showLogin();
    };

    boot();
})();
