// Møde - the page. Each person publishes their own camera (WHIP) and plays everyone else (WHEP), all through
// /media/<slug>/... on this host. The star board comes live over SSE (/api/<slug>/live).
(function () {
    'use strict';
    const $ = id => document.getElementById(id);
    const slug = location.pathname.replace(/^\/|\/$/g, '').toLowerCase();
    // Short link (user 2026-11-27 "easier link?"): moede.itmartin.dk/gruppe1 - the door asks for the code word from the mail
    // (remembered on this device). An old link with ?k= still works.
    let key = new URLSearchParams(location.search).get('k') || '';
    try { if (!key) key = localStorage.getItem('mode_kode_' + slug) || ''; } catch (e) { }
    const POINTS = [
        ['Hvad', 'Hvad?', 'Den konkrete idé: projektet, produktet, begivenheden eller udfordringen.'],
        ['Hvorfor', 'Hvorfor?', 'Værdien for dig og for kunder eller deltagere. Motivation og drivkraft.'],
        ['Hvem', 'Hvem?', 'Hvem er med i projektet? Hvem er målgruppe, kunder eller brugere?'],
        ['Hvor', 'Hvor?', 'Hvor foregår det? Hvor færdes målgruppen?'],
        ['Hvornaar', 'Hvornår?', 'Tid og timing: hvor lang tid, milepæle, deadlines, gode og dårlige tidspunkter.'],
        ['Hvordan', 'Hvordan?', 'Det praktiske: hvem gør hvad, i hvilken rækkefølge? Hvad er næste skridt?'],
    ];

    let me = null;            // { id, token, path }
    let myName = '';
    let media = null;         // my camera + mic
    let pub = null, pubUrl = null;
    const players = {};       // path -> { pc, tile, retry }
    let view = { people: [], notes: [], idea: '' };
    let es = null, hello = null;

    // ── theme ──
    $('themeBtn').onclick = () => {
        const t = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', t);
        try { localStorage.setItem('mode_theme', t); } catch (e) { }
    };

    if (!slug) { $('intet').hidden = false; return; }
    $('dor').hidden = false;
    $('kodeFelt').hidden = !!key;
    $('kode').required = !key;
    try { $('navn').value = localStorage.getItem('mode_navn') || ''; } catch (e) { }

    const q = (extra) => `k=${encodeURIComponent(key)}` + (extra || '');
    const post = (what, body) => fetch(`/api/${slug}/${what}`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(Object.assign({ k: key, id: me && me.id, token: me && me.token }, body || {}))
    });

    function status(t) { $('status').textContent = t || ''; }

    // ── the door ──
    $('dorForm').onsubmit = async e => {
        e.preventDefault();
        const navn = $('navn').value.trim();
        if (!navn) return;
        if (!$('kodeFelt').hidden) key = $('kode').value.trim().toLowerCase();
        if (!key) return;
        $('gaaInd').disabled = true; $('dorFejl').hidden = true;
        try {
            try { localStorage.setItem('mode_navn', navn); } catch (e) { }
            myName = navn;
            try {
                media = await navigator.mediaDevices.getUserMedia({
                    video: { width: { ideal: 640 }, height: { ideal: 360 }, frameRate: { ideal: 20 } },
                    audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
                });
            } catch (err) {
                // no camera? try mic only - still in the meeting
                try { media = await navigator.mediaDevices.getUserMedia({ audio: true }); }
                catch (err2) { media = null; }
            }
            await join();
            $('dor').hidden = true; $('mode').hidden = false;
            buildStar();
            listen();
            publish();
            hello = setInterval(alive, 10000);
        } catch (err) {
            $('dorFejl').textContent = err.message || 'Det lykkedes ikke at gå ind. Prøv igen.';
            $('dorFejl').hidden = false;
        } finally { $('gaaInd').disabled = false; }
    };

    async function join() {
        const r = await post('ind', { navn: myName });
        if (r.status === 403) {
            try { localStorage.removeItem('mode_kode_' + slug); } catch (e) { }
            $('kodeFelt').hidden = false; $('kode').required = true;
            throw new Error('Koden passer ikke. Se koden i mailen.');
        }
        try { localStorage.setItem('mode_kode_' + slug, key); } catch (e) { }
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.fejl || 'Det lykkedes ikke at gå ind.');
        me = j;
    }

    async function alive() {
        const r = await post('hej').catch(() => null);
        if (r && r.status === 410) {           // the server forgot me (restart) - come back in quietly
            unpublish();
            try { await join(); publish(); } catch (e) { status('Forbindelsen er tabt – genindlæs siden.'); }
        }
    }

    // ── live view ──
    function listen() {
        es = new EventSource(`/api/${slug}/live?${q()}`);
        es.onmessage = ev => { view = JSON.parse(ev.data); render(); };
        es.onerror = () => status('Forbinder igen …');
        es.onopen = () => status('');
    }

    function render() {
        $('titel').textContent = view.title || 'Møde';
        document.title = (view.title || 'Møde') + ' – Møde';
        renderVideos();
        renderNotes();
        renderGame();
        const ide = $('ide');
        if (document.activeElement !== ide) ide.value = view.idea || '';
    }

    // ── video tiles ──
    function tile(id, name, mine) {
        let t = $('t-' + id);
        if (!t) {
            t = document.createElement('figure');
            t.className = 'tile' + (mine ? ' mig' : '');
            t.id = 't-' + id;
            t.innerHTML = '<video autoplay playsinline></video><figcaption></figcaption><span class="vent">Forbinder …</span>';
            $('videoer').appendChild(t);
        }
        t.querySelector('figcaption').textContent = mine ? name + ' (dig)' : name;
        return t;
    }

    function renderVideos() {
        if (!me) return;
        const mine = tile('mig', myName, true);
        const v = mine.querySelector('video');
        if (media && v.srcObject !== media) { v.srcObject = media; v.muted = true; }
        mine.classList.toggle('ingen-video', !media || media.getVideoTracks().length === 0 || !media.getVideoTracks()[0].enabled);
        mine.querySelector('.vent').hidden = true;

        const others = view.people.filter(p => p.id !== me.id);
        const want = new Set(others.map(p => p.path));
        for (const p of others) { tile(p.path, p.name, false); if (!players[p.path]) play(p.path); }
        for (const path of Object.keys(players)) if (!want.has(path)) stopPlay(path);
        document.querySelectorAll('.tile:not(.mig)').forEach(t => { if (!want.has(t.id.slice(2))) t.remove(); });
        $('videoer').dataset.n = String(others.length + 1);
    }

    async function negotiate(pc, url) {
        await pc.setLocalDescription(await pc.createOffer());
        await new Promise(ok => {
            if (pc.iceGatheringState === 'complete') return ok();
            const t = setTimeout(ok, 2000);
            pc.addEventListener('icegatheringstatechange', () => { if (pc.iceGatheringState === 'complete') { clearTimeout(t); ok(); } });
        });
        const r = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/sdp' }, body: pc.localDescription.sdp });
        if (!r.ok) { const e = new Error('media ' + r.status); e.status = r.status; throw e; }
        await pc.setRemoteDescription({ type: 'answer', sdp: await r.text() });
        return r.headers.get('Location');
    }

    async function publish() {
        if (!media || !me) { status(media ? '' : 'Ingen kamera/mikrofon – du kan se og høre de andre.'); return; }
        const pc = new RTCPeerConnection({ iceServers: [{ urls: 'stun:stun.l.google.com:19302' }] });
        pub = pc;
        media.getTracks().forEach(t => pc.addTransceiver(t, { direction: 'sendonly' }));
        try {
            pubUrl = await negotiate(pc, `/media/${slug}/${me.path}/whip?${q(`&p=${me.id}&t=${me.token}`)}`);
            const vs = pc.getSenders().find(s => s.track && s.track.kind === 'video');
            if (vs) { const p = vs.getParameters(); p.encodings = p.encodings && p.encodings.length ? p.encodings : [{}]; p.encodings[0].maxBitrate = 600000; vs.setParameters(p).catch(() => { }); }
        } catch (e) {
            status('Dit kamera kom ikke igennem – prøver igen …');
            setTimeout(() => { if (pub === pc) { pc.close(); pub = null; publish(); } }, 4000);
            return;
        }
        pc.onconnectionstatechange = () => {
            if (pc.connectionState === 'failed' && pub === pc) {
                status('Dit kamera mistede forbindelsen – prøver igen …');
                unpublish(); setTimeout(publish, 2000);
            } else if (pc.connectionState === 'connected') status('');
        };
    }

    function unpublish() {
        if (pubUrl) fetch(pubUrl, { method: 'DELETE' }).catch(() => { });
        if (pub) pub.close();
        pub = null; pubUrl = null;
    }

    async function play(path) {
        const t = $('t-' + path);
        if (!t) return;
        const pc = new RTCPeerConnection({ iceServers: [{ urls: 'stun:stun.l.google.com:19302' }] });
        const entry = { pc, retry: null };
        players[path] = entry;
        pc.addTransceiver('video', { direction: 'recvonly' });
        pc.addTransceiver('audio', { direction: 'recvonly' });
        const stream = new MediaStream();
        const v = t.querySelector('video');
        pc.ontrack = ev => {
            stream.addTrack(ev.track);
            v.srcObject = stream;
            v.play().catch(() => { });
            t.querySelector('.vent').hidden = true;
        };
        const again = () => {
            if (players[path] !== entry) return;
            pc.close(); delete players[path];
            const tt = $('t-' + path); if (tt) tt.querySelector('.vent').hidden = false;
            entry.retry = setTimeout(() => { if (!players[path] && $('t-' + path)) play(path); }, 3000);
        };
        pc.onconnectionstatechange = () => { if (pc.connectionState === 'failed' || pc.connectionState === 'closed') again(); };
        try { await negotiate(pc, `/media/${slug}/${path}/whep?${q()}`); }
        catch (e) { again(); }   // 404 = that person's camera is not there yet - try again in a moment
    }

    function stopPlay(path) {
        const e = players[path]; if (!e) return;
        clearTimeout(e.retry); e.pc.close(); delete players[path];
    }

    // ── buttons ──
    $('mikBtn').onclick = () => {
        if (!media) return;
        if (forced) return;
        myMicWish = !myMicWish;
        micRule(false);
    };
    $('kamBtn').onclick = () => {
        if (!media) return;
        const v = media.getVideoTracks(); if (!v.length) return;
        const on = !v[0].enabled; v.forEach(t => t.enabled = on);
        $('kamBtn').textContent = on ? 'Slå kamera fra' : 'Slå kamera til';
        $('kamBtn').classList.toggle('slukket', !on);
        renderVideos();
    };
    $('forladBtn').onclick = () => { leave(); location.href = '/' + slug; };

    function leave() {
        if (!me) return;
        clearInterval(hello);
        unpublish();
        Object.keys(players).forEach(stopPlay);
        if (es) es.close();
        if (media) media.getTracks().forEach(t => t.stop());
        navigator.sendBeacon(`/api/${slug}/ud`, new Blob([JSON.stringify({ k: key, id: me.id, token: me.token })], { type: 'application/json' }));
        me = null;
    }
    addEventListener('pagehide', leave);

    // ── the game: whose turn, the shared minute, and only the speaker's mic open while the minute runs ──
    let gameAt = 0, tick = null, myMicWish = true;
    const NAMES = Object.fromEntries(POINTS.map(([id, t]) => [id, t]));

    function renderGame() {
        const g = view.game;
        $('turFoer').hidden = !!g && !g.finished;
        $('turNu').hidden = !g || g.finished;
        document.querySelectorAll('.tile').forEach(t => t.classList.remove('taler'));
        document.querySelectorAll('.spids').forEach(b => b.classList.remove('nu'));
        clearInterval(tick);
        if (!g || g.finished) {
            $('startBtn').textContent = g && g.finished ? 'Alle har haft en idé – start igen' : 'Start spillet';
            micRule(false);
            return;
        }
        const meSpeak = me && g.speakerId === me.id;
        $('runde').textContent = `Runde ${g.round + 1} af ${g.rounds} – ${g.ownerId === (me && me.id) ? 'din' : g.ownerName + 's'} idé`;
        $('taler').textContent = g.step === 0
            ? (meSpeak ? 'Fortæl din idé' : `${g.ownerName} fortæller sin idé`)
            : (meSpeak ? `Din tur: ${NAMES[g.point]} – svar som om det var din idé` : `${g.speakerName} svarer på ${NAMES[g.point]}`);
        const st = g.speakerId === (me && me.id) ? $('t-mig') : document.querySelector(`.tile[id$="-${g.speakerId}"]`);
        if (st) st.classList.add('taler');
        if (g.point) { const b = $('n-' + g.point); if (b) b.closest('.spids').classList.add('nu'); }
        $('naesteBtn').textContent = g.step === 6 ? (g.round + 1 < g.rounds ? 'Næste idé' : 'Afslut') : 'Næste';

        // the minute: only during the six HV turns; everyone else listens
        micRule(g.step > 0 && !meSpeak);
        if (g.leftMs == null) { $('ur').textContent = ''; return; }
        gameAt = Date.now() + g.leftMs;
        const show = () => {
            const left = Math.max(0, gameAt - Date.now());
            const sec = Math.ceil(left / 1000);
            $('ur').textContent = left > 0 ? `0:${String(sec).padStart(2, '0')}` : 'Tiden er gået';
            $('ur').classList.toggle('slut', left === 0);
        };
        show(); tick = setInterval(show, 250);
    }

    // forced = someone else has the minute: my mic is off whatever I chose; afterwards it goes back to my own choice
    let forced = false;
    function micRule(f) {
        forced = f;
        if (!media) return;
        const on = !forced && myMicWish;
        media.getAudioTracks().forEach(t => t.enabled = on);
        $('mikBtn').disabled = forced;
        $('mikBtn').textContent = forced ? 'Du lytter nu' : (myMicWish ? 'Slå mikrofon fra' : 'Slå mikrofon til');
        $('mikBtn').classList.toggle('slukket', !on);
        const t = $('t-mig');
        if (t) { let l = t.querySelector('.lytter'); if (!l) { l = document.createElement('span'); l.className = 'lytter'; l.textContent = 'Lytter'; t.appendChild(l); } l.hidden = !forced; }
    }

    $('startBtn').onclick = async () => {
        const r = await post('spil/start');
        if (!r.ok) { const j = await r.json().catch(() => ({})); status(j.fejl || 'Spillet kunne ikke starte.'); }
    };
    $('naesteBtn').onclick = () => post('spil/naeste');
    $('stopBtn').onclick = () => post('spil/stop');

    // ── the star ──
    function buildStar() {
        const star = $('stjerne');
        POINTS.forEach(([id, title, help], i) => {
            const box = document.createElement('section');
            box.className = 'spids s' + i;
            box.innerHTML = `<h3>${title}</h3><p class="hjaelp">${help}</p><ul id="n-${id}"></ul>
                <form class="ny"><input class="k-input" maxlength="300" placeholder="Skriv en seddel …" aria-label="Ny seddel under ${title}"><button class="k-btn k-btn-sm k-btn-primary" type="submit">Tilføj</button></form>`;
            box.querySelector('form').onsubmit = async e => {
                e.preventDefault();
                const inp = e.target.querySelector('input');
                const text = inp.value.trim(); if (!text) return;
                inp.value = '';
                const r = await post('seddel', { point: id, text });
                if (!r.ok) { const j = await r.json().catch(() => ({})); status(j.fejl || 'Sedlen blev ikke gemt.'); inp.value = text; }
            };
            star.appendChild(box);
        });
        let t = null;
        $('ide').oninput = () => { clearTimeout(t); t = setTimeout(() => post('ide', { idea: $('ide').value }), 600); };
        $('rydBtn').onclick = () => {
            if ($('rydBtn').dataset.sikker) { delete $('rydBtn').dataset.sikker; $('rydBtn').textContent = 'Ryd stjernen (start forfra)'; post('ryd'); }
            else { $('rydBtn').dataset.sikker = '1'; $('rydBtn').textContent = 'Tryk igen for at rydde alt'; setTimeout(() => { delete $('rydBtn').dataset.sikker; $('rydBtn').textContent = 'Ryd stjernen (start forfra)'; }, 4000); }
        };
    }

    // The idea owner takes the notes home: a plain text file, grouped by idea and HV-word (made here in the browser).
    $('hentBtn').onclick = () => {
        const ideas = [...new Set(view.notes.map(n => n.ideaOf || ''))];
        const g = view.game, mine = g && !g.finished ? g.ownerName : myName;
        ideas.sort((a, b) => (b === mine) - (a === mine));
        const NL = '\n';
        let txt = `${view.title || 'Møde'} – De 6 HV – ${new Date().toLocaleString('da-DK')}` + NL;
        if (view.idea) txt += `Idéen i midten: ${view.idea}` + NL;
        for (const who of ideas) {
            txt += NL + `==== ${who ? who + 's idé' : 'Uden for spillet'} ====` + NL;
            for (const [id, title] of POINTS) {
                const ns = view.notes.filter(n => (n.ideaOf || '') === who && n.point === id);
                if (!ns.length) continue;
                txt += NL + title + NL + ns.map(n => `- ${n.text}  (${n.author})`).join(NL) + NL;
            }
        }
        const a = document.createElement('a');
        a.href = URL.createObjectURL(new Blob([txt], { type: 'text/plain;charset=utf-8' }));
        a.download = `de-6-hv-${new Date().toISOString().slice(0, 10)}.txt`;
        a.click(); setTimeout(() => URL.revokeObjectURL(a.href), 2000);
    };

    function renderNotes() {
        for (const [id] of POINTS) {
            const ul = $('n-' + id); if (!ul) continue;
            // during the game: only the notes for the idea on the table now
            const g = view.game, owner = g && !g.finished ? g.ownerName : null;
            ul.replaceChildren(...view.notes.filter(n => n.point === id && (!owner || n.ideaOf === owner)).map(n => {
                const li = document.createElement('li');
                const span = document.createElement('span'); span.textContent = n.text;
                const who = document.createElement('small'); who.textContent = n.author;
                const x = document.createElement('button'); x.type = 'button'; x.className = 'fjern'; x.textContent = '×'; x.title = 'Fjern sedlen';
                x.onclick = () => post('fjern', { noteId: n.id });
                li.append(span, who, x);
                return li;
            }));
        }
    }
})();
