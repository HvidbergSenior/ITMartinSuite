// Spilleder - the whole page. Plain JavaScript, no framework: start/join, player cards with live HP, secret messages, dice, push.
(function () {
    'use strict';
    const $ = id => document.getElementById(id);
    const store = {
        get(k) { try { return localStorage.getItem(k) || ''; } catch { return ''; } },
        set(k, v) { try { localStorage.setItem(k, v); } catch { } },
    };
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    let spil = null, me = '', leder = false, pushKey = '', endpoint = '', live = null;
    let players = [], notes = [], editId = '', unread = 0, tab = 'spillere';
    const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const standalone = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
    const pushPossible = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;

    // Lines the game master often needs (user: "You see something interesting to the right").
    const QUICK = [
        'Du ser noget glimte til højre.',
        'Du hører fodtrin bag dig.',
        'Kun du bemærker, at døren står på klem.',
        'Du har en mærkelig følelse af at blive iagttaget.',
        'Slå en d20 og send mig resultatet – hemmeligt.',
        'Du ved, at personen lyver.',
    ];

    // The SRD races (D&D 5e) as quick picks - any other text works too, the app is not tied to D&D.
    const RACES = ['Menneske', 'Elver', 'Dværg', 'Halvling', 'Gnom', 'Halvelver', 'Halvork', 'Dragefødt', 'Tiefling'];
    const RACE_EN = { Menneske: 'human', Elver: 'elf', 'Dværg': 'dwarf', Halvling: 'halfling', Gnom: 'gnome', Halvelver: 'half-elf',
        Halvork: 'half-orc', 'Dragefødt': 'dragonborn', Tiefling: 'tiefling' };
    // Example figures per race: figurer/eksempler.json = { "Halvling": ["figurer/halvling-1.jpg", ...] } (made once with Billedbehandling).
    let examples = {};
    fetch('figurer/eksempler.json').then(r => r.ok ? r.json() : {}).then(j => { examples = j || {}; }).catch(() => { });

    async function api(method, url, body) {
        const r = await fetch(url, {
            method, credentials: 'same-origin',
            headers: body ? { 'Content-Type': 'application/json' } : {},
            body: body ? JSON.stringify(body) : undefined,
        });
        if (r.status === 401) { showStart(); throw new Error('Du er ikke med i et spil.'); }
        const data = r.headers.get('content-type')?.includes('json') ? await r.json() : null;
        if (!r.ok) throw new Error((data && data.fejl) || 'Det gik ikke. Prøv igen.');
        return data;
    }

    // ---------- start ----------

    async function boot() {
        try {
            const r = await fetch('/api/spil', { credentials: 'same-origin' });
            if (!r.ok) return showStart();
            enter(await r.json());
        } catch { showStart(); }
    }

    // /leder = the game master's entrance, /spiller = the players' entrance, / = both.
    const entrance = location.pathname.replace(/\/+$/, '').toLowerCase();
    function setupEntrance() {
        if (entrance === '/leder') {
            $('indgang').textContent = 'Indgang for spillederen';
            $('indgang').hidden = false;
            $('nytBoks').open = true;
            $('start').insertBefore($('nytBoks'), $('deltagForm'));
            $('deltagTitel').textContent = 'Har du allerede startet et spil?';
            $('deltagKodeLabel').textContent = 'Din lederkode';
            $('deltagKodeHjaelp').textContent = 'Lederkoden (8 tegn) står under Spilkode til spillerne på den telefon, hvor du startede spillet.';
            $('deltagNavnFelt').hidden = true;
            $('deltagForm').querySelector('button[type=submit]').textContent = 'Fortsæt som spilleder';
        } else if (entrance === '/spiller') {
            $('indgang').textContent = 'Indgang for spillere';
            $('indgang').hidden = false;
            $('nytBoks').hidden = true;
        }
    }
    setupEntrance();

    function showStart() {
        $('app').hidden = true;
        $('start').hidden = false;
        $('deltagNavn').value = store.get('spil_navn');
        if (live) { live.close(); live = null; }
    }

    async function signIn(url, body, errEl) {
        errEl.hidden = true;
        // Ask for notifications inside the tap - iPhone only allows the question there.
        const perm = pushPossible && Notification.permission === 'default'
            ? Notification.requestPermission().catch(() => 'default') : Promise.resolve(null);
        try {
            const r = await fetch(url, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
            const d = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(d.fejl || 'Det gik ikke. Prøv igen.');
            await perm;
            enter(await (await fetch('/api/spil', { credentials: 'same-origin' })).json());
        } catch (err) {
            errEl.textContent = err.message;
            errEl.hidden = false;
        }
    }

    $('deltagForm').addEventListener('submit', e => {
        e.preventDefault();
        const navn = $('deltagNavn').value.trim();
        store.set('spil_navn', navn);
        signIn('/api/deltag', { kode: $('deltagKode').value, navn }, $('deltagFejl'));
    });
    $('nytForm').addEventListener('submit', e => {
        e.preventDefault();
        signIn('/api/nyt-spil', { kode: $('nytKode').value, titel: $('nytTitel').value, navn: $('nytNavn').value }, $('nytFejl'));
    });

    function enter(d) {
        pushKey = d.pushKey || '';
        // A new player starts by making their figure (user: "focus on setup and communication").
        const mine = d.spil.players.find(p => p.id === d.spil.me);
        if (mine && !mine.character) { editId = mine.id; store.set('spil_tab', 'spillere'); }
        apply(d.spil);
        $('start').hidden = true;
        $('app').hidden = false;
        show(store.get('spil_tab') === 'beskeder' ? 'beskeder' : 'spillere');
        connect();
        setupPush(false);
    }

    function apply(s) {
        spil = s; me = s.me; leder = s.leader;
        players = s.players; notes = s.notes;
        $('titel').textContent = s.title;
        document.title = s.title + ' · Spilleder';
        $('hvem').textContent = leder ? s.leaderName + ' · spilleder' : (players.find(p => p.id === me)?.name || '') + ' · spiller';
        $('koderBoks').hidden = !leder;
        $('spilKode').textContent = s.joinCode;
        $('lederKode').textContent = s.leaderCode || '';
        $('spilBoks').hidden = !leder;
        $('retTitel').value = s.title; $('retLederNavn').value = s.leaderName;
        $('hurtige').hidden = !leder;
        $('skrivTitel').textContent = leder ? 'Send en besked – også hemmelig' : 'Send en besked';
        $('beskedInfo').textContent = leder
            ? 'Vælg hvem beskeden er til. Vælger du én spiller, er beskeden hemmelig – kun den spiller ser den. Du ser alle beskeder.'
            : 'Her kommer beskeder fra spillederen. "Hemmelig" betyder, at kun du kan se den. Du kan skrive til alle eller kun til spillederen.';
        $('hemmeligTekst').textContent = leder ? 'Hemmeligt – kun du ser slaget' : 'Hemmeligt – kun spillederen ser slaget';
        renderHurtige(); renderTil(); renderCards(); renderNotes();
    }

    // ---------- tabs ----------

    function show(which) {
        tab = which;
        for (const [v, t] of [['Spillere', 'tabSpillere'], ['Beskeder', 'tabBeskeder'], ['Mig', 'tabMig']]) {
            $('view' + v).hidden = v.toLowerCase() !== which;
            $(t).setAttribute('aria-selected', String(v.toLowerCase() === which));
        }
        if (which !== 'mig') store.set('spil_tab', which);
        if (which === 'beskeder') { unread = 0; badge(); }
        if (which === 'mig') { pushStatus(); openHomeGuide(); }
    }
    $('tabSpillere').onclick = () => show('spillere');
    $('tabBeskeder').onclick = () => show('beskeder');
    $('tabMig').onclick = () => show('mig');
    function badge() { $('nyeBadge').hidden = unread === 0; $('nyeBadge').textContent = unread; }

    // ---------- live ----------

    function connect() {
        if (live) live.close();
        live = new EventSource('/api/live');
        // "hej" comes on every (re)connect: fetch whatever was missed while the line was down.
        live.addEventListener('hej', reload);
        live.addEventListener('spillere', e => {
            players = JSON.parse(e.data);
            if (!leder && !players.some(p => p.id === me)) { showStart(); return; }
            renderTil(); renderCards();
        });
        live.addEventListener('hp', e => flash(JSON.parse(e.data)));
        live.addEventListener('spil', e => { const d = JSON.parse(e.data); spil.title = d.title; spil.leaderName = d.leaderName; $('titel').textContent = d.title; });
        live.addEventListener('besked', e => {
            const n = JSON.parse(e.data);
            if (notes.some(x => x.id === n.id)) return;
            notes.push(n); renderNotes();
            if (tab !== 'beskeder' && n.from !== me) { unread++; badge(); }
        });
    }
    async function reload() {
        try { const d = await api('GET', '/api/spil'); apply(d.spil); } catch { }
    }
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState !== 'visible' || $('app').hidden) return;
        if (!live || live.readyState === 2) connect(); else reload();
    });

    // ---------- player cards ----------

    // HP: the player and the leader. The sheet: the leader always, the player only while it is unlocked.
    const canEdit = id => leder || id === me;
    const canEditSheet = p => leder || (p.id === me && !p.locked);

    function renderCards() {
        const box = $('kort');
        $('ingenSpillere').hidden = players.length > 0;
        // Me first, then the others in the order they joined.
        const list = [...players].sort((a, b) => (b.id === me) - (a.id === me));
        box.innerHTML = list.map(p => p.id === editId ? editHtml(p) : cardHtml(p)).join('');
        for (const p of list) {
            if (p.id === editId) wireEdit(p);
            else wireCard(p);
        }
    }

    function cardHtml(p) {
        const pct = Math.max(0, Math.min(100, Math.round(100 * p.hp / Math.max(1, p.maxHp))));
        const low = p.hp <= Math.ceil(p.maxHp / 4);
        const line = [p.race, p.role].filter(Boolean).join(' · ');
        const edit = canEdit(p.id);
        return `<article class="kort${p.id === me ? ' mig' : ''}${low ? ' lav' : ''}" id="k-${p.id}">
            ${p.picture ? `<img class="figur-billede" src="/api/billede/${p.id}?v=${p.picture}" alt="${esc(p.character || p.name)}" loading="lazy" />` : ''}
            <div><h3>${esc(p.character || p.name)}</h3><span class="spiller">${p.character ? 'spilles af ' + esc(p.name) : 'har ikke skrevet sin figur endnu'}${p.id === me ? ' (dig)' : ''}</span></div>
            ${line ? `<p class="linje">${esc(line)}</p>` : ''}
            <div class="liv">
                ${edit ? `<button type="button" class="k-btn" data-hp="-1" aria-label="Mist 1 liv">−</button>` : ''}
                <span class="tal" aria-label="Liv">${p.hp}<small> / ${p.maxHp}</small></span>
                ${edit ? `<button type="button" class="k-btn" data-hp="1" aria-label="Få 1 liv">+</button>` : ''}
                <span class="flash-plads"></span>
            </div>
            <div class="livbar" aria-hidden="true"><i style="width:${pct}%"></i></div>
            ${p.gear ? `<div class="felt"><span class="felt-navn">Udstyr og våben</span><p class="noter">${esc(p.gear)}</p></div>` : ''}
            ${p.notes ? `<div class="felt"><span class="felt-navn">Noter</span><p class="noter">${esc(p.notes)}</p></div>` : ''}
            ${p.spells.length ? '<span class="felt-navn">Besværgelser og evner</span>' : ''}
            ${p.spells.map(s => `<details class="besv"><summary>${esc(s.name)}</summary><p>${esc(s.text || 'Ingen beskrivelse endnu.')}</p></details>`).join('')}
            ${p.id === me && p.locked && !leder ? '<p class="laast">Dit kort er gemt og låst. Vil du ændre noget, fx nyt våben, så spørg spillederen, som kan åbne det for dig.</p>' : ''}
            ${p.id === me && !p.locked && !leder && p.character ? '<p class="laast">Spillederen har åbnet dit kort. Ret det, og tryk Gem kortet – så låses det igen.</p>' : ''}
            ${edit ? `<div class="kort-knapper">
                ${canEditSheet(p) ? `<button type="button" class="k-btn k-btn-sm" data-ret>${p.character ? 'Ret kort' : 'Lav din figur'}</button>` : ''}
                ${leder ? `<button type="button" class="k-btn k-btn-sm" data-laas>${p.locked ? 'Lad spilleren rette' : 'Lås kortet'}</button>
                    <button type="button" class="k-btn k-btn-sm k-btn-ghost" data-fjern>Fjern spiller</button>` : ''}</div>` : ''}
        </article>`;
    }

    function wireCard(p) {
        const el = $('k-' + p.id);
        if (!el) return;
        el.querySelectorAll('[data-hp]').forEach(b => b.onclick = () => changeHp(p.id, +b.dataset.hp));
        const laas = el.querySelector('[data-laas]');
        if (laas) laas.onclick = async () => { try { await api('POST', `/api/spillere/${p.id}/laas`, { laast: !p.locked }); } catch { } };
        const ret = el.querySelector('[data-ret]');
        if (ret) ret.onclick = () => { editId = p.id; renderCards(); };
        const fjern = el.querySelector('[data-fjern]');
        if (fjern) fjern.onclick = async () => {
            if (fjern.dataset.sikker !== '1') { fjern.dataset.sikker = '1'; fjern.textContent = 'Tryk igen for at fjerne ' + p.name; return; }
            try { await api('DELETE', '/api/spillere/' + p.id); } catch { }
        };
    }

    // Several quick taps on − become one "−3" for the others; the screen changes at once for you.
    const pending = {};
    function changeHp(id, d) {
        const p = players.find(x => x.id === id);
        if (!p) return;
        p.hp = Math.min(p.maxHp, p.hp + d);
        renderCards();
        const q = pending[id] || (pending[id] = { sum: 0, t: 0 });
        q.sum += d;
        clearTimeout(q.t);
        q.t = setTimeout(async () => {
            const sum = q.sum; delete pending[id];
            try { await api('POST', `/api/spillere/${id}/liv`, { aendring: sum }); } catch { reload(); }
        }, 600);
    }

    function flash(d) {
        const p = players.find(x => x.id === d.id);
        if (p && !pending[d.id]) { p.hp = d.hp; p.maxHp = d.maxHp; if (editId !== d.id) renderCards(); }
        const spot = document.querySelector(`#k-${d.id} .flash-plads`);
        if (!spot || !d.delta) return;
        spot.innerHTML = `<span class="flash">${d.delta > 0 ? '+' : '−'}${Math.abs(d.delta)}</span>`;
    }

    function editHtml(p) {
        return `<article class="kort${p.id === me ? ' mig' : ''}" id="k-${p.id}">
            <form class="ret" id="retForm">
                <h3>Ret kort</h3>
                <p class="kladde" id="rKladde" hidden>Din kladde er hentet – intet er gået tabt. Tryk <b>Gem kortet</b>, når du er færdig.
                    <button type="button" class="link" id="rGlemKladde">Glem kladden</button></p>
                <label class="k-label" for="rNavn">Spillerens navn</label>
                <input class="k-input" id="rNavn" maxlength="30" value="${esc(p.name)}" />
                <label class="k-label" for="rFigur">Figurens navn</label>
                <input class="k-input" id="rFigur" maxlength="60" value="${esc(p.character)}" placeholder="fx Thorin Stenhånd" />
                <span class="k-label">Race / folk</span>
                <div class="til" id="rRacer">${RACES.map(r => `<label><input type="radio" name="race" value="${esc(r)}" ${p.race === r ? 'checked' : ''} /> ${esc(r)}</label>`).join('')}</div>
                <input class="k-input" id="rRace" maxlength="60" value="${esc(p.race)}" placeholder="eller skriv selv, fx varulv" aria-label="Race eller folk" />
                <div id="rEksempler" class="eksempler"></div>
                <span class="k-label">Billede af figuren</span>
                <div class="billede-ret">
                    <img id="rBillede" class="figur-billede" ${p.picture ? `src="/api/billede/${p.id}?v=${p.picture}"` : 'hidden'} alt="" />
                    <label class="k-btn k-btn-sm fil-knap">Vælg billede<input type="file" id="rFil" accept="image/*" hidden /></label>
                </div>
                <label class="k-label" for="rUdseende">Hvordan ser figuren ud? <span class="valgfri">(til AI-billedet)</span></label>
                <input class="k-input" id="rUdseende" maxlength="200" placeholder="fx rødt hår, grøn kappe, bue på ryggen" />
                <button type="button" class="k-btn k-btn-sm" id="rAi">Lav din figur med AI i Billedbehandling</button>
                <p class="k-help" id="rAiHjaelp" hidden>Billedbehandling åbner med din beskrivelse. Tryk <b>Generér</b>, så <b>Hent</b>. Kom tilbage hertil og tryk <b>Vælg billede</b>. Gratis 5 billeder om dagen.</p>
                <p class="fejl" id="rBilledFejl" hidden></p>
                <div class="to">
                    <div><label class="k-label" for="rKlasse">Klasse / rolle</label><input class="k-input" id="rKlasse" maxlength="60" value="${esc(p.role)}" placeholder="fx kriger" /></div>
                    <div></div>
                    <div><label class="k-label" for="rLiv">Liv nu</label><input class="k-input" id="rLiv" type="number" inputmode="numeric" value="${p.hp}" /></div>
                    <div><label class="k-label" for="rMax">Liv højst</label><input class="k-input" id="rMax" type="number" inputmode="numeric" min="1" value="${p.maxHp}" /></div>
                </div>
                <label class="k-label" for="rUdstyr">Udstyr og våben</label>
                <textarea class="k-textarea" id="rUdstyr" maxlength="2000" rows="3" placeholder="fx langsværd, skjold, 20 guld, reb">${esc(p.gear)}</textarea>
                <label class="k-label" for="rNoter">Noter (alle kan se dem)</label>
                <textarea class="k-textarea" id="rNoter" maxlength="2000" rows="3" placeholder="fx udstyr, guld, hvad der skete sidst">${esc(p.notes)}</textarea>
                <span class="k-label">Besværgelser og evner</span>
                <div id="rSpells" class="k-stack"></div>
                <button type="button" class="k-btn k-btn-sm" id="rNySpell">Tilføj en besværgelse eller evne</button>
                <details class="spell-ret">
                    <summary>Slå en D&amp;D-besværgelse op</summary>
                    <input class="k-input" id="rSoeg" placeholder="Skriv på engelsk, fx fireball" aria-label="Søg besværgelse" />
                    <div id="rHits" class="opslag"></div>
                    <p class="k-help">Fra D&amp;D 5e SRD 5.1 (Wizards of the Coast, CC-BY-4.0). Teksten er på engelsk.</p>
                </details>
                <div class="kort-knapper">
                    <button class="k-btn k-btn-primary" type="submit">Gem kortet</button>
                    <button class="k-btn k-btn-ghost" type="button" id="rFortryd">Luk</button>
                </div>
                <p class="k-help">Alt, du skriver, gemmes som kladde på denne telefon, også hvis du lukker. De andre ser det først, når du trykker <b>Gem kortet</b>.</p>
                ${leder ? '' : '<p class="laast">Når du trykker <b>Gem kortet</b>, bliver kortet låst. Vil du ændre noget senere, skal spillederen åbne det for dig.</p>'}
                <p class="fejl" id="rFejl" hidden></p>
            </form>
        </article>`;
    }

    // Shrink to max 768 px JPEG on the phone, then send - a phone photo is several MB.
    async function shrink(fileOrBlob) {
        const bmp = await createImageBitmap(fileOrBlob);
        const k = Math.min(1, 768 / Math.max(bmp.width, bmp.height));
        const c = document.createElement('canvas');
        c.width = Math.round(bmp.width * k); c.height = Math.round(bmp.height * k);
        c.getContext('2d').drawImage(bmp, 0, 0, c.width, c.height);
        return await new Promise(res => c.toBlob(res, 'image/jpeg', 0.85));
    }
    async function uploadPicture(id, fileOrBlob) {
        const jpeg = await shrink(fileOrBlob);
        const r = await fetch(`/api/spillere/${id}/billede`, { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'image/jpeg' }, body: jpeg });
        const d = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(d.fejl || 'Billedet kunne ikke gemmes.');
        const pl = players.find(x => x.id === id);
        if (pl) pl.picture = d.picture;
        return d;
    }

    function wireEdit(p) {
        const showPic = src => { $('rBillede').src = src; $('rBillede').hidden = false; };
        const picErr = m => { $('rBilledFejl').textContent = m; $('rBilledFejl').hidden = !m; };
        $('rFil').onchange = async () => {
            const f = $('rFil').files[0];
            if (!f) return;
            picErr('');
            try { const d = await uploadPicture(p.id, f); showPic(`/api/billede/${p.id}?v=${d.picture}`); }
            catch (err) { picErr(err.message); }
        };
        const drawExamples = () => {
            const list = examples[$('rRace').value.trim()] || [];
            $('rEksempler').innerHTML = list.length
                ? `<span class="k-help">Eksempler – tryk på en for at bruge den:</span><div class="eksempel-liste">${list.map((src, i) =>
                    `<button type="button" class="eksempel" data-ex="${i}"><img src="${esc(src)}" alt="Eksempel ${i + 1}" loading="lazy" /></button>`).join('')}</div>`
                : '';
            $('rEksempler').querySelectorAll('[data-ex]').forEach(b => b.onclick = async () => {
                picErr('');
                try {
                    const blob = await (await fetch(list[+b.dataset.ex])).blob();
                    const d = await uploadPicture(p.id, blob);
                    showPic(`/api/billede/${p.id}?v=${d.picture}`);
                } catch (err) { picErr(err.message); }
            });
        };
        $('rRacer').querySelectorAll('input').forEach(i => i.onchange = () => { $('rRace').value = i.value; drawExamples(); });
        $('rRace').oninput = () => { $('rRacer').querySelectorAll('input').forEach(i => i.checked = i.value === $('rRace').value.trim()); drawExamples(); };
        drawExamples();
        $('rAi').onclick = () => {
            const race = $('rRace').value.trim(), klasse = $('rKlasse').value.trim(), look = $('rUdseende').value.trim();
            const beskriv = ['Fantasy-portræt, malet stil, hele figuren synlig:', RACE_EN[race] ? `en ${race.toLowerCase()} (${RACE_EN[race]})` : race,
                klasse, look].filter(Boolean).join(' ');
            window.open('https://billedbehandling.itmartin.dk/?beskriv=' + encodeURIComponent(beskriv), '_blank', 'noopener');
            $('rAiHjaelp').hidden = false;
        };
        const spells = p.spells.map(s => ({ ...s }));
        // People spend a long time on their figure: every keystroke is kept as a draft on this phone until "Gem kortet",
        // so a locked phone, a lost connection or a closed tab never throws the work away.
        const draftKey = 'spil_kladde_' + p.id;
        const fields = ['rNavn', 'rFigur', 'rRace', 'rKlasse', 'rLiv', 'rMax', 'rUdstyr', 'rNoter', 'rUdseende'];
        const saveDraft = () => store.set(draftKey, JSON.stringify({ f: Object.fromEntries(fields.map(id => [id, $(id).value])), spells }));
        try {
            const d = JSON.parse(store.get(draftKey) || 'null');
            if (d) {
                for (const id of fields) if (d.f[id] !== undefined) $(id).value = d.f[id];
                spells.splice(0, spells.length, ...(d.spells || []));
                $('rKladde').hidden = false;
            }
        } catch { }
        $('retForm').addEventListener('input', saveDraft);
        $('rRacer').addEventListener('change', saveDraft);
        $('rGlemKladde').onclick = () => { store.set(draftKey, ''); editId = p.id; renderCards(); };
        const drawSpells = () => {
            $('rSpells').innerHTML = spells.map((s, i) => `<div class="spell-ret">
                <input class="k-input" data-sn="${i}" maxlength="60" value="${esc(s.name)}" placeholder="Navn, fx Ildkugle" aria-label="Navn" />
                <textarea class="k-textarea" data-st="${i}" rows="3" maxlength="3000" placeholder="Hvad gør den?" aria-label="Hvad gør den">${esc(s.text)}</textarea>
                <button type="button" class="k-btn k-btn-sm k-btn-ghost" data-sx="${i}">Fjern</button></div>`).join('');
            $('rSpells').querySelectorAll('[data-sn]').forEach(el => el.oninput = () => spells[+el.dataset.sn].name = el.value);
            $('rSpells').querySelectorAll('[data-st]').forEach(el => el.oninput = () => spells[+el.dataset.st].text = el.value);
            $('rSpells').querySelectorAll('[data-sx]').forEach(el => el.onclick = () => { spells.splice(+el.dataset.sx, 1); drawSpells(); saveDraft(); });
        };
        drawSpells();
        $('rNySpell').onclick = () => { spells.push({ name: '', text: '' }); drawSpells(); $('rSpells').querySelector(`[data-sn="${spells.length - 1}"]`)?.focus(); };
        let searchT = 0;
        $('rSoeg').oninput = () => {
            clearTimeout(searchT);
            const q = $('rSoeg').value.trim();
            searchT = setTimeout(async () => {
                if (q.length < 2) { $('rHits').innerHTML = ''; return; }
                try {
                    const hits = await api('GET', '/api/besvaergelser?q=' + encodeURIComponent(q));
                    $('rHits').innerHTML = hits.length ? hits.map(h => `<button type="button" class="k-btn k-btn-sm" data-idx="${esc(h.index)}">${esc(h.name)}</button>`).join('')
                        : '<p class="k-help">Ingen fundet. Skriv selv besværgelsen ovenfor.</p>';
                    $('rHits').querySelectorAll('[data-idx]').forEach(b => b.onclick = async () => {
                        try {
                            const s = await api('GET', '/api/besvaergelser/' + b.dataset.idx);
                            spells.push({ name: s.name, text: s.text }); drawSpells(); saveDraft();
                            $('rSoeg').value = ''; $('rHits').innerHTML = '';
                        } catch (err) { $('rHits').innerHTML = `<p class="fejl">${esc(err.message)}</p>`; }
                    });
                } catch (err) { $('rHits').innerHTML = `<p class="fejl">${esc(err.message)}</p>`; }
            }, 300);
        };
        $('rFortryd').onclick = () => { editId = ''; renderCards(); };
        $('rRacer').querySelectorAll('input').forEach(i => i.checked = i.value === $('rRace').value.trim());
        drawExamples();
        $('retForm').onsubmit = async e => {
            e.preventDefault();
            try {
                await api('PUT', '/api/spillere/' + p.id, {
                    navn: $('rNavn').value, figur: $('rFigur').value, race: $('rRace').value, klasse: $('rKlasse').value,
                    liv: parseInt($('rLiv').value, 10) || 0, maxLiv: parseInt($('rMax').value, 10) || 1, noter: $('rNoter').value, udstyr: $('rUdstyr').value,
                    besvaergelser: spells.map(s => ({ navn: s.name, tekst: s.text })),
                });
                store.set(draftKey, ''); editId = ''; await reload();
            } catch (err) { $('rFejl').textContent = err.message; $('rFejl').hidden = false; }
        };
    }

    // ---------- messages ----------

    function renderHurtige() {
        $('hurtige').innerHTML = QUICK.map((q, i) => `<button type="button" class="k-btn k-btn-sm" data-q="${i}">${esc(q)}</button>`).join('');
        $('hurtige').querySelectorAll('[data-q]').forEach(b => b.onclick = () => { $('tekst').value = QUICK[+b.dataset.q]; $('tekst').focus(); });
    }

    // Leader: Alle, or one or more players (= secret). Player: Alle or only the leader.
    function renderTil() {
        const was = [...document.querySelectorAll('#til input:checked')].map(i => i.value);
        const opts = leder
            ? [['', 'Alle'], ...players.map(p => [p.id, p.character || p.name])]
            : [['', 'Alle'], ['leder', 'Kun spillederen']];
        const type = leder ? 'checkbox' : 'radio';
        $('til').innerHTML = opts.map(([v, l]) =>
            `<label><input type="${type}" name="til" value="${esc(v)}" ${was.includes(v) || (!was.length && v === '') ? 'checked' : ''} /> ${esc(l)}</label>`).join('');
        // Ticking a player unticks Alle and the other way round.
        $('til').querySelectorAll('input').forEach(i => i.onchange = () => {
            if (!leder) return;
            const all = $('til').querySelector('input[value=""]');
            if (i.value === '' && i.checked) $('til').querySelectorAll('input').forEach(x => { if (x.value !== '') x.checked = false; });
            else if (i.value !== '' && i.checked) all.checked = false;
            if (![...$('til').querySelectorAll('input')].some(x => x.checked)) all.checked = true;
        });
    }

    $('skriv').addEventListener('submit', async e => {
        e.preventDefault();
        const tekst = $('tekst').value.trim();
        if (!tekst) return;
        const til = [...document.querySelectorAll('#til input:checked')].map(i => i.value).filter(Boolean);
        $('skrivFejl').hidden = true;
        try {
            const n = await api('POST', '/api/beskeder', { til, tekst });
            $('tekst').value = '';
            if (!notes.some(x => x.id === n.id)) { notes.push(n); renderNotes(); }
        } catch (err) { $('skrivFejl').textContent = err.message; $('skrivFejl').hidden = false; }
    });

    async function roll(t) {
        $('terningFejl').hidden = true;
        try {
            const n = await api('POST', '/api/terning', { terning: t, hemmelig: $('terningHemmelig').checked, hvorfor: $('terningHvorfor').value });
            $('terningHvorfor').value = '';
            if (!notes.some(x => x.id === n.id)) { notes.push(n); renderNotes(); }
        } catch (err) { $('terningFejl').textContent = err.message; $('terningFejl').hidden = false; }
    }
    document.querySelectorAll('[data-t]').forEach(b => b.onclick = () => roll(b.dataset.t));
    $('slaaEgen').onclick = () => roll($('terningTekst').value);

    const nameOf = id => id === 'leder' ? (spil?.leaderName || 'spillederen') : (players.find(p => p.id === id)?.character || players.find(p => p.id === id)?.name || 'en spiller');
    const timeFmt = new Intl.DateTimeFormat('da-DK', { hour: '2-digit', minute: '2-digit' });

    function renderNotes() {
        $('ingenBeskeder').hidden = notes.length > 0;
        // Newest first, right under the write box.
        $('beskeder').innerHTML = [...notes].reverse().map(n => {
            const secret = n.to.length > 0;
            const to = secret ? (n.to.length === 1 && n.to[0] === n.from ? 'kun til dig selv' : 'kun til ' + n.to.map(id => id === me ? 'dig' : nameOf(id)).join(', ')) : 'til alle';
            const dice = n.kind === 'terning';
            return `<article class="besked${secret ? ' hemmelig-b' : ''}${dice ? ' terning-b' : ''}">
                <div class="meta">${secret ? '<span class="maerke">Hemmelig</span>' : ''}<span>${esc(n.from === me ? 'Dig' : n.fromName)} ${esc(to)}</span><span>${timeFmt.format(new Date(n.at))}</span></div>
                <p class="tekst">${dice ? 'Terning: ' + esc(n.fromName) + ' ' : ''}${esc(n.text)}</p></article>`;
        }).join('');
    }

    // ---------- the game (leader) ----------

    $('gemSpil').onclick = async () => {
        try { await api('PUT', '/api/spil', { titel: $('retTitel').value, navn: $('retLederNavn').value }); $('gemSpil').textContent = 'Gemt'; await reload(); }
        catch (err) { $('gemSpil').textContent = err.message; }
        setTimeout(() => { $('gemSpil').textContent = 'Gem'; }, 1500);
    };
    $('delKode').onclick = async () => {
        const text = `Vær med i ${spil.title}: åbn https://spilleder.itmartin.dk/spiller og skriv spilkoden ${spil.joinCode}`;
        try { if (navigator.share) await navigator.share({ text }); else { await navigator.clipboard.writeText(text); $('delKode').textContent = 'Kopieret'; } } catch { }
    };
    $('forlad').onclick = async () => {
        try { await fetch('/api/forlad', { method: 'POST', credentials: 'same-origin' }); } catch { }
        showStart();
    };

    // ---------- push ----------

    function b64(s) {
        const pad = '='.repeat((4 - s.length % 4) % 4);
        const raw = atob((s + pad).replace(/-/g, '+').replace(/_/g, '/'));
        return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
    }
    async function subscribe() {
        const reg = await navigator.serviceWorker.ready;
        let sub = await reg.pushManager.getSubscription();
        if (!sub) sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: b64(pushKey) });
        const j = sub.toJSON();
        endpoint = j.endpoint;
        await api('POST', '/api/push', { endpoint: j.endpoint, p256dh: j.keys.p256dh, auth: j.keys.auth });
    }

    async function setupPush(ask) {
        const bar = $('pushBar');
        if (store.get('spil_push_fra') && !ask) { bar.hidden = true; return; }
        if (!pushPossible) {
            if (isIos && !standalone) {
                bar.innerHTML = '<b>Få hemmelige beskeder på din iPhone:</b> sæt Spilleder på hjemmeskærmen først. <button type="button" class="link" id="visHjem">Sådan gør du</button>';
                bar.hidden = false;
                $('visHjem').onclick = () => show('mig');
            } else bar.hidden = true;
            return;
        }
        if (Notification.permission === 'default' && ask) await Notification.requestPermission().catch(() => { });
        if (Notification.permission === 'granted') {
            try { await subscribe(); store.set('spil_push_fra', ''); bar.hidden = true; }
            catch { bar.textContent = 'Beskeder på telefonen kunne ikke slås til lige nu. Prøv igen senere under ⋯.'; bar.hidden = false; }
        } else if (Notification.permission === 'denied') bar.hidden = true;
        else {
            bar.innerHTML = '<b>Få besked, når ' + (leder ? 'en spiller' : 'spillederen') + ' skriver til dig.</b> <button type="button" class="k-btn k-btn-sm k-btn-primary" id="barTil">Slå beskeder til</button>';
            bar.hidden = false;
            $('barTil').onclick = () => setupPush(true);
        }
        pushStatus();
    }

    function pushStatus() {
        const s = $('pushStatus');
        const on = pushPossible && Notification.permission === 'granted' && endpoint && !store.get('spil_push_fra');
        $('pushTil').hidden = !!on; $('pushFra').hidden = !on;
        if (on) s.textContent = 'Slået til. Telefonen giver besked, når der kommer en besked til dig.';
        else if (!pushPossible && isIos && !standalone) s.textContent = 'På iPhone skal Spilleder ligge på hjemmeskærmen, før den kan give besked. Se nedenfor.';
        else if (!pushPossible) s.textContent = 'Denne browser kan ikke give besked. Brug Chrome, Edge eller Safari.';
        else if (Notification.permission === 'denied') s.textContent = isIos
            ? 'Du har sagt nej. Åbn Indstillinger → Notifikationer → Spilleder (Settings → Notifications) og slå Tillad til.'
            : 'Du har sagt nej. Tryk på låsen ved adressen øverst → Notifikationer → Tillad (Notifications → Allow).';
        else s.textContent = 'Slået fra. Tryk Slå beskeder til, og sig Tillad.';
    }
    $('pushTil').onclick = async () => { store.set('spil_push_fra', ''); await setupPush(true); pushStatus(); };
    $('pushFra').onclick = async () => {
        store.set('spil_push_fra', '1');
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

    // ---------- Lys/Mørk + remembered boxes (same as ElPriser) ----------

    function syncTheme() {
        const cur = document.documentElement.getAttribute('data-theme') || 'light';
        $('themeToggleBtn').textContent = cur === 'light' ? 'Mørk' : 'Lys';
        document.querySelector('meta[name=theme-color]')?.setAttribute('content', cur === 'light' ? '#f4f6f5' : '#0b1220');
    }
    $('themeToggleBtn').onclick = () => {
        const next = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', next);
        store.set('spilleder_theme', next);
        syncTheme();
    };
    syncTheme();

    // Every box on the page remembers open/closed on this device (user: "all boxes show/hide").
    document.querySelectorAll('details').forEach(d => {
        const key = 'spil_open_' + (d.id || d.querySelector('summary')?.textContent.trim() || '');
        const saved = store.get(key);
        if (saved) d.open = saved === '1';
        d.addEventListener('toggle', () => store.set(key, d.open ? '1' : '0'));
    });

    boot();
})();
