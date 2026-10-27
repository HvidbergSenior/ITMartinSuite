// Character - the whole page. Plain JavaScript, no framework: start/open, the steps, live co-op with the game master, help thread.
(function () {
    'use strict';
    const $ = id => document.getElementById(id);
    const store = {
        get(k) { try { return localStorage.getItem(k) || ''; } catch { return ''; } },
        set(k, v) { try { localStorage.setItem(k, v); } catch { } },
    };
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    let code = '', fig = null, step = 'start', live = null, otherHere = false;
    const seen = {};           // comments seen per step, so a new one marks its step
    const pending = {};        // field saves waiting to go to the server
    let failed = false;

    // ---------- the rules the page shows (the server checks them again) ----------

    const SRD_PEOPLES = { Human: '+1 to every score', Elf: '+2 DEX', Dwarf: '+2 CON', Halfling: '+2 DEX', Gnome: '+2 INT', 'Half-Elf': '+2 CHA',
        'Half-Orc': '+2 STR, +1 CON', Dragonborn: '+2 STR, +1 CHA', Tiefling: '+2 CHA, +1 INT' };
    const OTHER_PEOPLES = { mystery: ['Human', 'Butler', 'Heir', 'Old aristocrat', 'Detective', 'Doctor', 'Artist'],
        coop: ['Human', 'Elf', 'Dwarf', 'Orc', 'Robot', 'Alien', 'Android'] };
    const CLASSES = { Barbarian: 12, Fighter: 10, Paladin: 10, Ranger: 10, Bard: 8, Cleric: 8, Druid: 8, Monk: 8, Rogue: 8, Warlock: 8, Sorcerer: 6, Wizard: 6 };
    const CLASS_TEXT = { Barbarian: 'strong and wild – rage in battle', Fighter: 'master of weapons and armour', Paladin: 'holy warrior with an oath',
        Ranger: 'hunter and tracker of the wild', Bard: 'music and words are magic', Cleric: 'magic from a god – heals the group',
        Druid: 'nature magic, can turn into animals', Monk: 'fast, fights without weapons', Rogue: 'sneaky, finds traps, strikes from hiding',
        Warlock: 'magic from a pact with a strange being', Sorcerer: 'magic in the blood', Wizard: 'magic from study and spell books' };
    const ROLES = { mystery: ['Detective', 'Suspect', 'Witness', 'Victim\'s relative', 'Servant', 'Guest'],
        coop: ['Leader', 'Scout', 'Engineer', 'Medic', 'Thinker', 'Strong one'] };
    const BACKGROUNDS = ['Acolyte', 'Criminal', 'Folk Hero', 'Noble', 'Sage', 'Soldier', 'Hermit', 'Entertainer', 'Outlander', 'Sailor', 'Urchin', 'Guild Artisan'];
    const GEAR = ['Longsword', 'Shortsword', 'Dagger', 'Shortbow and 20 arrows', 'Quarterstaff', 'Shield', 'Leather armour', 'Chain mail',
        'Explorer\'s pack', 'Rope (50 feet)', 'Torch', 'Healing potion', 'Spell book', 'Holy symbol', 'Thieves\' tools', '10 gold pieces'];
    const ABILITIES = [['STR', 'Strength – lift, push, hit hard'], ['DEX', 'Dexterity – quick, sneaky, aim'], ['CON', 'Constitution – tough, more hit points'],
        ['INT', 'Intelligence – know things, puzzles'], ['WIS', 'Wisdom – notice things, gut feeling'], ['CHA', 'Charisma – talk, lead, charm']];
    const STANDARD = [15, 14, 13, 12, 10, 8];
    const KINDS = { dnd: 'D&D adventure', mystery: 'Murder mystery', coop: 'Cooperative' };
    const mod = s => Math.floor((s - 10) / 2);
    const signed = n => (n >= 0 ? '+' : '−') + Math.abs(n);

    function label(s) {
        const dnd = fig.kind === 'dnd';
        return { start: 'Start', people: dnd ? 'People' : 'Who you are', class: dnd ? 'Class' : 'Role', background: 'Background',
            abilities: 'Ability scores', hp: 'Hit points', gear: dnd ? 'Gear' : 'Things you carry', spells: 'Spells', skills: 'Skills',
            picture: 'Picture', story: 'Story', ready: 'Ready' }[s] || s;
    }

    // ---------- talking to the server ----------

    async function api(method, url, body) {
        const r = await fetch(url, {
            method, credentials: 'same-origin',
            headers: { 'X-Kode': code, ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) },
            body: body !== undefined ? JSON.stringify(body) : undefined,
        });
        const data = r.headers.get('content-type')?.includes('json') ? await r.json() : null;
        if (!r.ok) { const e = new Error((data && data.fejl) || 'Something went wrong. Please try again.'); e.status = r.status; throw e; }
        return data;
    }

    // Every change waits half a second (typing), then goes to the server. A failed save tries again until it works -
    // people spend a long time on their figure, nothing may get lost.
    function save(key, fn, delay = 500) {
        if (pending[key]) clearTimeout(pending[key].t);
        pending[key] = { fn, t: setTimeout(() => run(key), delay) };
        status();
    }
    async function run(key) {
        const p = pending[key];
        if (!p) return;
        try {
            const v = await p.fn();
            if (pending[key] === p) delete pending[key];
            failed = false;
            if (v && v.id) apply(v);
        } catch (err) {
            if (err.status === 403 || err.status === 400) {
                if (pending[key] === p) delete pending[key];
                showErr(err.message);
            } else {
                failed = true;
                if (pending[key] === p) p.t = setTimeout(() => run(key), 3000);
            }
        }
        status();
    }
    function status() {
        const n = Object.keys(pending).length;
        $('gemt').textContent = failed ? 'Not saved – trying again…' : n ? 'Saving…' : 'Saved';
        $('gemt').classList.toggle('ikke', failed);
    }
    window.addEventListener('beforeunload', e => { if (Object.keys(pending).length) { e.preventDefault(); e.returnValue = ''; } });

    function showErr(m) {
        const el = $('trinFejl');
        if (el) { el.textContent = m; el.hidden = !m; }
    }

    // ---------- start ----------

    const entrance = location.pathname.replace(/\/+$/, '').toLowerCase();
    const isMasterEntrance = entrance === '/master';
    if (isMasterEntrance) {
        $('indgang').textContent = 'Entrance for the game master';
        $('indgang').hidden = false;
        $('nyForm').hidden = true;
        $('start').insertBefore($('aabnForm'), $('mineBoks'));
        $('aabnTitel').textContent = 'Help a player with their character';
        $('aabnLabel').textContent = 'The game master code from the player';
        $('aabnHjaelp').textContent = 'The player presses "Share with your game master" and sends you a link or a code. Write the code here.';
    } else if (entrance === '/player') {
        $('indgang').textContent = 'Entrance for players';
        $('indgang').hidden = false;
    }

    const mine = () => { try { return JSON.parse(store.get('character_mine') || '[]'); } catch { return []; } };
    function remember(c, name, viewer) {
        const list = mine().filter(x => x.code !== c);
        list.unshift({ code: c, name, viewer, at: Date.now() });
        store.set('character_mine', JSON.stringify(list.slice(0, 30)));
    }
    function forget(c) { store.set('character_mine', JSON.stringify(mine().filter(x => x.code !== c))); }

    function showStart() {
        if (live) { live.close(); live = null; }
        code = ''; fig = null;
        $('app').hidden = true;
        $('start').hidden = false;
        $('nyNavn').value = store.get('character_navn');
        const list = mine();
        $('mineBoks').hidden = list.length === 0;
        $('mineListe').innerHTML = list.map(x => `<button type="button" class="k-btn mine-knap" data-kode="${esc(x.code)}">
            <b>${esc(x.name || 'Character without a name')}</b><span>${x.viewer === 'master' ? 'you help as game master' : 'your character'}</span></button>`).join('');
        $('mineListe').querySelectorAll('[data-kode]').forEach(b => b.onclick = () => openCode(b.dataset.kode, $('aabnFejl')));
    }

    async function openCode(c, errEl) {
        errEl.hidden = true;
        try {
            const r = await fetch('/api/aabn', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ kode: c }) });
            const d = await r.json().catch(() => ({}));
            if (!r.ok) { if (r.status === 401) forget(c); throw new Error(d.fejl || 'Something went wrong. Please try again.'); }
            await enter(d.code);
        } catch (err) { showStart(); errEl.textContent = err.message; errEl.hidden = false; }
    }

    $('nyForm').addEventListener('submit', async e => {
        e.preventDefault();
        $('nyFejl').hidden = true;
        const navn = $('nyNavn').value.trim();
        store.set('character_navn', navn);
        try {
            const r = await fetch('/api/ny', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ navn, slags: document.querySelector('#nySlags input:checked')?.value, kundenummer: $('nyKunde').value }) });
            const d = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(d.fejl || 'Something went wrong. Please try again.');
            await enter(d.code);
        } catch (err) { $('nyFejl').textContent = err.message; $('nyFejl').hidden = false; }
    });
    $('aabnForm').addEventListener('submit', e => { e.preventDefault(); openCode($('aabnKode').value, $('aabnFejl')); });
    $('kundeForm').addEventListener('submit', async e => {
        e.preventDefault();
        $('kundeFejl').hidden = true;
        try {
            const r = await fetch('/api/mine', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ kundenummer: $('kundeNr').value }) });
            const d = await r.json().catch(() => ({}));
            if (!r.ok) throw new Error(d.fejl || 'Something went wrong. Please try again.');
            $('kundeListe').innerHTML = d.length ? d.map(x => `<button type="button" class="k-btn mine-knap" data-kode="${esc(x.playerCode)}">
                <b>${esc(x.name || 'Character without a name')}</b><span>${x.ready ? 'ready for adventure' : 'not finished'}</span></button>`).join('')
                : '<p class="k-help">No characters with that customer number yet.</p>';
            $('kundeListe').querySelectorAll('[data-kode]').forEach(b => b.onclick = () => openCode(b.dataset.kode, $('kundeFejl')));
        } catch (err) { $('kundeFejl').textContent = err.message; $('kundeFejl').hidden = false; }
    });
    $('tilbage').onclick = () => showStart();

    async function enter(c) {
        code = c;
        const v = await api('GET', '/api/figur');
        remember(c, v.name, v.viewer);
        fig = null;
        step = v.viewer === 'player' ? v.step : (v.step || 'start');
        for (const k of Object.keys(seen)) delete seen[k];
        for (const cm of v.comments) seen[cm.step] = (seen[cm.step] || 0) + 1;
        apply(v);
        $('start').hidden = true;
        $('app').hidden = false;
        connect();
        status();
        scrollTo(0, 0);
    }

    // ---------- live ----------

    function connect() {
        if (live) live.close();
        live = new EventSource('/api/live?kode=' + encodeURIComponent(code));
        // "hej" comes on every (re)connect: fetch whatever was missed and tell the other one we are here.
        live.addEventListener('hej', async () => {
            try { apply(await api('GET', '/api/figur')); await api('POST', '/api/her'); } catch { }
        });
        live.addEventListener('figur', e => apply(JSON.parse(e.data)));
        live.addEventListener('kommentar', e => {
            const c = JSON.parse(e.data);
            if (!fig || fig.comments.some(x => x.id === c.id)) return;
            fig.comments.push(c);
            if (c.step === step) seen[c.step] = (seen[c.step] || 0) + 1;
            renderComments(); renderNav();
        });
        live.addEventListener('her', e => {
            const d = JSON.parse(e.data);
            const was = otherHere;
            otherHere = d.here;
            renderPresence();
            if (d.here && !was) api('POST', '/api/her').catch(() => { });
        });
    }
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState !== 'visible' || $('app').hidden) return;
        if (!live || live.readyState === 2) connect();
    });

    // ---------- applying a new version of the figure ----------

    const master = () => fig && fig.viewer === 'master';
    const canEdit = () => fig && (master() || !fig.locked);

    // While someone types in a field, the screen must not jump: only fields that are not being typed in (and have no save
    // waiting) take the new value; the rest of the step is drawn again when nobody is typing.
    function typing() {
        const a = document.activeElement;
        return a && $('trinKort').contains(a) && (a.tagName === 'TEXTAREA' || (a.tagName === 'INPUT' && !['radio', 'checkbox', 'file'].includes(a.type)));
    }

    function apply(v) {
        const first = !fig;
        fig = v;
        if (!fig.steps.includes(step)) step = fig.steps[0];
        remember(code, v.name, v.viewer);
        renderTop(); renderNav(); renderShare(); renderComments(); renderCodes(); renderPresence();
        if (first || !typing()) renderStep();
        else syncFields();
    }

    function syncFields() {
        $('trinKort').querySelectorAll('[data-felt]').forEach(el => {
            const f = el.dataset.felt;
            if (el === document.activeElement || pending['felt:' + f]) return;
            if (el.value !== (fig[f] ?? '')) el.value = fig[f] ?? '';
        });
    }

    function renderTop() {
        $('titel').textContent = fig.name || 'New character';
        document.title = (fig.name || 'New character') + ' · Character';
        $('hvem').textContent = master()
            ? `You help ${fig.playerName} as game master · ${KINDS[fig.kind]}`
            : `${fig.playerName} · ${KINDS[fig.kind]}${fig.locked ? ' · locked' : ''}`;
    }

    function renderPresence() {
        $('tilstede').hidden = !otherHere;
        $('tilstede').textContent = master() ? `${fig.playerName} is here right now – you see each other's changes at once.`
            : `Your game master${fig.masterName ? ' ' + fig.masterName : ''} is here right now – you see each other's changes at once.`;
    }

    function renderNav() {
        $('trinListe').innerHTML = fig.steps.map((s, i) => {
            const count = fig.comments.filter(c => c.step === s).length;
            const fresh = count > (seen[s] || 0);
            const playerHere = master() && fig.step === s;
            return `<button type="button" data-trin="${s}" aria-current="${s === step ? 'step' : 'false'}" class="${done(s) ? 'faerdig' : ''}">
                <span class="nr">${i + 1}</span> ${esc(label(s))}${fresh ? ' <span class="ny" title="New message">new</span>' : ''}${playerHere ? ' <span class="her">player</span>' : ''}</button>`;
        }).join('');
        $('trinListe').querySelectorAll('[data-trin]').forEach(b => b.onclick = () => go(b.dataset.trin));
        $('trinListe').querySelector('[aria-current=step]')?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    }

    // Done = there is something in the step (a word, not a colour: the user is a bit colour-blind, so the chip also gets a tick).
    function done(s) {
        switch (s) {
            case 'start': return !!fig.name.trim();
            case 'people': return !!fig.people.trim();
            case 'class': return !!fig.class.trim();
            case 'background': return !!fig.background.trim();
            case 'abilities': return fig.abilities.every(x => x > 0);
            case 'hp': return fig.maxHp > 0 && (fig.maxHp !== 10 || fig.hp !== 10 || !fig.suggestedHp);
            case 'gear': return !!fig.gear.trim();
            case 'spells': return fig.spells.length > 0;
            case 'skills': return !!fig.skills.trim();
            case 'picture': return fig.picture > 0;
            case 'story': return !!fig.story.trim();
            case 'ready': return fig.ready;
        }
        return false;
    }

    function go(s) {
        step = s;
        seen[s] = fig.comments.filter(c => c.step === s).length;
        if (!master()) { fig.step = s; api('PUT', '/api/figur/trin', { trin: s }).catch(() => { }); }
        renderNav(); renderStep(); renderComments();
        $('trinListe').scrollIntoView({ block: 'start', behavior: 'smooth' });
    }
    $('forrige').onclick = () => { const i = fig.steps.indexOf(step); if (i > 0) go(fig.steps[i - 1]); };
    $('naeste').onclick = () => { const i = fig.steps.indexOf(step); if (i < fig.steps.length - 1) go(fig.steps[i + 1]); };

    function renderShare() {
        const box = $('delBoks');
        if (master()) {
            box.hidden = false;
            box.innerHTML = `<p class="del-linje">You are helping <b>${esc(fig.playerName)}</b>. They are on the step <b>${esc(label(fig.step))}</b>.
                Change anything – they see it at once. Write to them under each step.</p>`;
            return;
        }
        box.hidden = fig.ready;
        box.innerHTML = `<p class="del-linje">Want help from your game master – here or from another place? Send them this link.</p>
            <button type="button" class="k-btn k-btn-sm" id="delGm">Share with your game master</button>`;
        $('delGm').onclick = shareMaster;
    }
    async function shareMaster(ev) {
        const url = `${location.origin}/master?kode=${fig.masterCode}`;
        const text = `Help me make my character: ${url} (game master code ${fig.masterCode})`;
        try {
            if (navigator.share) await navigator.share({ text });
            else { await navigator.clipboard.writeText(text); ev.target.textContent = 'Copied – paste it in a message'; }
        } catch { }
    }

    function renderCodes() {
        $('koder').innerHTML = `
            ${master() ? '' : `<p><span class="felt-navn">Your personal code</span><br><b class="stor-kode">${esc(fig.playerCode)}</b><br>
                Write it under "Open a character with a code" on another phone, and you get your character back. Keep it to yourself.</p>`}
            <p><span class="felt-navn">Game master code</span><br><b class="stor-kode">${esc(fig.masterCode)}</b><br>
                ${master() ? 'Your code for helping with this character.' : 'Only for your game master – with it they can change and lock your character.'}</p>
            ${fig.figureCode ? `<p><span class="felt-navn">Figure code for Game Master</span><br><b class="stor-kode">${esc(fig.figureCode)}</b></p>` : ''}
            ${master() ? '' : '<button type="button" class="k-btn k-btn-sm" id="delGm2">Share with your game master</button>'}`;
        if ($('delGm2')) $('delGm2').onclick = shareMaster;
    }

    // ---------- the steps ----------

    const dis = () => canEdit() ? '' : 'disabled';
    const field = (felt, lbl, o = {}) => `<label class="k-label" for="f-${felt}">${lbl}</label>` + (o.area
        ? `<textarea class="k-textarea" id="f-${felt}" data-felt="${felt}" rows="${o.rows || 4}" maxlength="${o.max || 3000}" placeholder="${esc(o.ph || '')}" ${o.dis ?? dis()}>${esc(fig[felt])}</textarea>`
        : `<input class="k-input" id="f-${felt}" data-felt="${felt}" maxlength="${o.max || 60}" value="${esc(fig[felt])}" placeholder="${esc(o.ph || '')}" ${o.dis ?? dis()} />`)
        + (o.help ? `<p class="k-help">${o.help}</p>` : '');
    const chips = (felt, list, info) => `<div class="valg">${list.map(x => `<button type="button" class="chip${fig[felt].trim().toLowerCase() === x.toLowerCase() ? ' valgt' : ''}"
        data-saet="${felt}" data-v="${esc(x)}" ${dis()} aria-pressed="${fig[felt].trim().toLowerCase() === x.toLowerCase()}">${esc(x)}${info && info[x] ? `<small>${esc(info[x])}</small>` : ''}</button>`).join('')}</div>`;
    const lockedNote = () => fig.locked && !master()
        ? '<p class="laast">Your character is ready and locked. To change something, ask your game master to unlock it.</p>' : '';

    const STEPS = {
        start: () => `
            <h2>Start</h2>
            <p class="intro">Give your character a name. You can change everything later – nothing is lost, it is saved as you type.</p>
            ${field('name', 'Character\'s name', { ph: 'e.g. Thorin Stonehand' })}
            ${field('playerName', 'Your own name', { max: 30 })}
            <span class="k-label">Kind of game</span>
            <div class="valg">${Object.entries(KINDS).map(([k, l]) => `<button type="button" class="chip${fig.kind === k ? ' valgt' : ''}" data-slags="${k}" ${dis()} aria-pressed="${fig.kind === k}">${esc(l)}</button>`).join('')}</div>
            <p class="k-help">D&amp;D has dice, ability scores and spells. Murder mystery and cooperative are simpler: who you are, your role, skills and things.</p>
            ${master() ? `<hr>${field('masterName', 'Your name as game master', { max: 30, dis: '' })}
                ${field('masterNotes', 'Your own notes – only you see them', { area: true, dis: '', ph: 'e.g. secret about the family, where the character fits in the story' })}` : ''}`,

        people: () => {
            const dnd = fig.kind === 'dnd';
            const list = dnd ? Object.keys(SRD_PEOPLES) : OTHER_PEOPLES[fig.kind] || [];
            return `<h2>${esc(label('people'))}</h2>
            <p class="intro">${dnd ? 'What people does your character belong to? Each gives a bonus to the ability scores.' : 'What kind of person or being is your character?'}
                Your game master decides the world – anything goes: humans, orcs, old people, robots…</p>
            ${chips('people', list, dnd ? SRD_PEOPLES : null)}
            ${field('people', 'Or write your own', { ph: 'e.g. werewolf, android, retired sea captain' })}`;
        },

        class: () => {
            const dnd = fig.kind === 'dnd';
            const info = dnd ? Object.fromEntries(Object.keys(CLASSES).map(c => [c, CLASS_TEXT[c]])) : null;
            return `<h2>${esc(label('class'))}</h2>
            <p class="intro">${dnd ? 'What does your character do best? The class decides hit points and if you can use magic.' : 'What is your character\'s part in the game?'}</p>
            ${chips('class', dnd ? Object.keys(CLASSES) : ROLES[fig.kind] || [], info)}
            ${field('class', 'Or write your own', { ph: dnd ? 'e.g. Fighter' : 'e.g. the gardener who saw everything' })}`;
        },

        background: () => `<h2>Background</h2>
            <p class="intro">Where does your character come from? What did they do before the adventure?</p>
            ${chips('background', BACKGROUNDS)}
            ${field('background', 'Or write your own', { ph: 'e.g. grew up in a lighthouse' })}`,

        abilities: abilitiesHtml,

        hp: () => {
            const die = CLASSES[Object.keys(CLASSES).find(c => c.toLowerCase() === fig.class.trim().toLowerCase())];
            const con = fig.final[2];
            return `<h2>Hit points</h2>
            <p class="intro">Hit points are how much your character can take before falling. At 0 you are knocked out.</p>
            ${fig.suggestedHp ? `<div class="forslag"><p>${esc(fig.class)} has a d${die}: <b>${die}</b> at level 1, plus your CON modifier
                (<b>${signed(mod(con))}</b>) = <b>${fig.suggestedHp}</b> hit points.</p>
                <button type="button" class="k-btn k-btn-sm k-btn-primary" id="brugHp" ${dis()}>Use ${fig.suggestedHp}</button></div>`
                : `<p class="k-help">${fig.kind === 'dnd' ? 'Choose a class and set CON under Ability scores – then the app works out the number for you.' : 'Your game master tells you how many – often 10.'}</p>`}
            <div class="to">
                <div><label class="k-label" for="hpNu">Hit points now</label><input class="k-input" id="hpNu" type="number" inputmode="numeric" value="${fig.hp}" ${dis()} /></div>
                <div><label class="k-label" for="hpMax">Max hit points</label><input class="k-input" id="hpMax" type="number" inputmode="numeric" min="1" value="${fig.maxHp}" ${dis()} /></div>
            </div>`;
        },

        gear: () => `<h2>${esc(label('gear'))}</h2>
            <p class="intro">Weapons, armour and the things your character carries. Tap to add, or write your own – one per line.</p>
            ${fig.kind === 'dnd' ? `<div class="valg">${GEAR.map(g => `<button type="button" class="chip" data-gear="${esc(g)}" ${dis()}>+ ${esc(g)}</button>`).join('')}</div>` : ''}
            ${field('gear', 'Weapons and gear', { area: true, rows: 6, ph: 'e.g. longsword\nshield\n20 gold' })}`,

        spells: spellsHtml,

        skills: () => `<h2>Skills</h2>
            <p class="intro">What is your character good at – and bad at? A weakness makes the game more fun.</p>
            ${field('skills', 'Skills and weaknesses', { area: true, rows: 6, ph: 'e.g. good at reading people, can pick locks, afraid of the dark' })}`,

        picture: () => `<h2>Picture</h2>
            <p class="intro">A picture makes the character come alive at the table. Use your own drawing or a photo, or let the AI make one.</p>
            <img id="billede" class="figur-billede" ${fig.picture ? `src="/api/billede?kode=${encodeURIComponent(code)}&v=${fig.picture}"` : 'hidden'} alt="${esc(fig.name)}" />
            ${canEdit() ? '<label class="k-btn k-btn-block fil-knap">Choose picture<input type="file" id="fil" accept="image/*" hidden /></label>' : ''}
            ${field('look', 'What does the character look like?', { max: 300, ph: 'e.g. red hair, green cloak, bow on the back' })}
            ${canEdit() ? `<button type="button" class="k-btn k-btn-block" id="ai">Make a picture with AI (Billedbehandling)</button>
            <p class="k-help" id="aiHjaelp" hidden>Billedbehandling opens with your description (the page is in Danish). Press <b>Generér</b> (generate), then <b>Hent</b> (download).
                Come back here and press <b>Choose picture</b>. 5 free pictures a day.</p>` : ''}`,

        story: () => `<h2>Story</h2>
            <p class="intro">Who is your character? What do they want, what are they afraid of, who do they love or hate? A few lines are enough.</p>
            ${field('story', 'The story', { area: true, rows: 8, ph: 'e.g. Left home after the fire. Looking for the sister who disappeared.' })}`,

        ready: readyHtml,
    };

    function abilitiesHtml() {
        const m = fig.abilityMethod;
        const pool = m === 'array' ? STANDARD : m === 'roll' ? fig.rolls : [];
        const bonusText = SRD_PEOPLES[Object.keys(SRD_PEOPLES).find(p => p.toLowerCase() === fig.people.trim().toLowerCase())];
        const rows = ABILITIES.map(([n, t], i) => {
            const val = fig.abilities[i];
            let input;
            if (m === 'manual') input = `<input class="k-input evne-tal" type="number" min="3" max="18" inputmode="numeric" data-evne="${i}" value="${val || ''}" ${dis()} aria-label="${n}" />`;
            else if (pool.length) {
                // Each value from the pool can be used once: offer what is left plus this row's own value.
                const left = [...pool];
                fig.abilities.forEach((x, j) => { if (j !== i && x) { const k = left.indexOf(x); if (k >= 0) left.splice(k, 1); } });
                const opts = [...new Set(left)].sort((a, b) => b - a);
                input = `<select class="k-input evne-tal" data-evne="${i}" ${dis()} aria-label="${n}"><option value="0">–</option>${opts.map(o => `<option ${o === val ? 'selected' : ''}>${o}</option>`).join('')}</select>`;
            } else input = '<span class="k-help">choose a way above</span>';
            const fin = fig.final[i];
            return `<div class="evne"><div><b>${n}</b><small>${esc(t)}</small></div>${input}
                <span class="evne-res">${fin ? `${fin} <small>(${signed(mod(fin))})</small>` : ''}</span></div>`;
        }).join('');
        return `<h2>Ability scores</h2>
            <p class="intro">Six numbers that say what your character is good at. 10 is normal, 15 is very good, 8 is weak.
                The number in brackets is what you add to a d20 roll.</p>
            <div class="valg">
                <button type="button" class="chip${m === 'array' ? ' valgt' : ''}" data-metode="array" ${dis()} aria-pressed="${m === 'array'}">Standard numbers<small>15 14 13 12 10 8 – fair for everyone</small></button>
                <button type="button" class="chip${m === 'roll' ? ' valgt' : ''}" data-metode="roll" ${dis()} aria-pressed="${m === 'roll'}">Roll the dice<small>4d6, drop the lowest – exciting</small></button>
                <button type="button" class="chip${m === 'manual' ? ' valgt' : ''}" data-metode="manual" ${dis()} aria-pressed="${m === 'manual'}">Write them<small>when the game master says so</small></button>
            </div>
            ${m === 'roll' ? `<p>Your rolls: <b>${fig.rolls.join('  ')}</b> ${canEdit() ? '<button type="button" class="k-btn k-btn-sm k-btn-ghost" id="rulIgen">Roll again</button>' : ''}</p>
                <p class="k-help">The server rolls, so nobody can cheat – your game master sees the same numbers.</p>` : ''}
            ${pool.length ? '<p class="k-help">Put each number on the ability where you want it.</p>' : ''}
            <div class="evner">${rows}</div>
            ${bonusText ? `<p class="k-help">${esc(fig.people)}: ${esc(bonusText)} – already added in the last column.</p>` : ''}`;
    }

    let spells = [];
    function spellsHtml() {
        spells = fig.spells.map(s => ({ ...s }));
        return `<h2>Spells</h2>
            <p class="intro">Only if your character uses magic (Bard, Cleric, Druid, Paladin, Ranger, Sorcerer, Warlock, Wizard). Ask your game master how many you get.</p>
            ${canEdit() ? `<details class="spell-ret" open>
                <summary>Look up a D&amp;D spell</summary>
                <input class="k-input" id="soeg" placeholder="e.g. fireball" aria-label="Search spell" />
                <div id="hits" class="opslag"></div>
                <p class="k-help">From the D&amp;D 5e SRD 5.1 (Wizards of the Coast, CC-BY-4.0).</p>
            </details>` : ''}
            <div id="spells" class="k-stack"></div>
            ${canEdit() ? '<button type="button" class="k-btn k-btn-sm" id="nySpell">Add your own spell or ability</button>' : ''}`;
    }

    function readyHtml() {
        const missing = fig.steps.filter(s => s !== 'ready' && !done(s)).map(label);
        return `<h2>Ready</h2>
            ${fig.picture ? `<img class="figur-billede" src="/api/billede?kode=${encodeURIComponent(code)}&v=${fig.picture}" alt="${esc(fig.name)}" />` : ''}
            <div class="opsummering">
                <h3>${esc(fig.name || 'No name yet')}</h3>
                <p class="linje">${esc([fig.people, fig.class, fig.background].filter(Boolean).join(' · '))}</p>
                ${fig.final.some(x => x) ? `<p class="evne-linje">${ABILITIES.map(([n], i) => fig.final[i] ? `<span><b>${n}</b> ${fig.final[i]} (${signed(mod(fig.final[i]))})</span>` : '').join('')}</p>` : ''}
                <p><b>Hit points</b> ${fig.hp} / ${fig.maxHp}</p>
                ${fig.gear ? `<p class="noter"><b>Gear</b>\n${esc(fig.gear)}</p>` : ''}
                ${fig.skills ? `<p class="noter"><b>Skills</b>\n${esc(fig.skills)}</p>` : ''}
                ${fig.spells.length ? `<p><b>Spells</b> ${esc(fig.spells.map(s => s.name).join(', '))}</p>` : ''}
                ${fig.story ? `<p class="noter"><b>Story</b>\n${esc(fig.story)}</p>` : ''}
            </div>
            ${fig.adventureNotes || master() ? field('adventureNotes', 'From the adventures', { area: true, dis: master() ? '' : 'disabled', ph: 'Game Master writes here after play – what happened, what changed.' }) : ''}
            ${fig.ready ? `<div class="klar">
                <p>The figure code for <b>Game Master</b>:</p>
                <p class="stor-kode">${esc(fig.figureCode)}</p>
                <p>In Game Master (spilleder.itmartin.dk) press <b>Edit card</b> on your card, then <b>Import from Character</b> and write this code.
                    Hit points, gear and notes come back here after the adventure.</p></div>`
                : `${missing.length ? `<p class="k-help">Not filled in yet: ${esc(missing.join(', '))}. That is fine – your game master decides what is needed.</p>` : ''}
                <button type="button" class="k-btn k-btn-primary k-btn-block" id="klar" ${dis()}>Ready for adventure</button>
                <p class="k-help">${master() ? 'The figure gets its code and is locked for the player.' : 'Then your character gets a code for Game Master and is locked. Your game master can open it again, for example after a level up.'}</p>`}
            ${master() && fig.ready ? `<button type="button" class="k-btn" id="laas">${fig.locked ? 'Let the player change the character' : 'Lock the character again'}</button>` : ''}`;
    }

    function renderStep() {
        const box = $('trinKort');
        box.innerHTML = lockedNote() + STEPS[step]() + '<p class="fejl" id="trinFejl" hidden></p>';
        const i = fig.steps.indexOf(step);
        $('forrige').disabled = i <= 0;
        $('naeste').hidden = i >= fig.steps.length - 1;
        $('naeste').textContent = i === fig.steps.length - 2 ? 'Last step: Ready' : 'Next: ' + label(fig.steps[i + 1] || '');
        wireStep();
    }

    function setField(felt, value, delay) {
        fig[felt] = value;
        save('felt:' + felt, () => api('PUT', '/api/figur/felt', { felt, vaerdi: value }), delay);
    }

    function wireStep() {
        const box = $('trinKort');
        box.querySelectorAll('[data-felt]').forEach(el => el.oninput = () => {
            setField(el.dataset.felt, el.value);
            // A chip that matches what is written lights up as you type.
            box.querySelectorAll(`[data-saet="${el.dataset.felt}"]`).forEach(c => {
                const on = c.dataset.v.toLowerCase() === el.value.trim().toLowerCase();
                c.classList.toggle('valgt', on); c.setAttribute('aria-pressed', on);
            });
        });
        box.querySelectorAll('[data-saet]').forEach(b => b.onclick = () => {
            const f = b.dataset.saet;
            const v = fig[f].trim().toLowerCase() === b.dataset.v.toLowerCase() ? '' : b.dataset.v;
            setField(f, v, 0);
            renderStep();
        });
        box.querySelectorAll('[data-slags]').forEach(b => b.onclick = async () => {
            try { apply(await api('PUT', '/api/figur/slags', { slags: b.dataset.slags })); renderStep(); } catch (err) { showErr(err.message); }
        });
        box.querySelectorAll('[data-gear]').forEach(b => b.onclick = () => {
            const g = fig.gear.trim();
            setField('gear', (g ? g + '\n' : '') + b.dataset.gear, 0);
            const ta = $('f-gear'); if (ta) ta.value = fig.gear;
        });

        // ability scores
        box.querySelectorAll('[data-metode]').forEach(b => b.onclick = async () => {
            const m = b.dataset.metode;
            if (m === fig.abilityMethod) return;
            try {
                if (m === 'roll' && fig.rolls.length !== 6) apply(await api('POST', '/api/figur/rul'));
                else apply(await api('PUT', '/api/figur/evner', { metode: m, tal: [0, 0, 0, 0, 0, 0] }));
                renderStep();
            } catch (err) { showErr(err.message); }
        });
        if ($('rulIgen')) $('rulIgen').onclick = async () => {
            if ($('rulIgen').dataset.sikker !== '1') { $('rulIgen').dataset.sikker = '1'; $('rulIgen').textContent = 'Press again – the numbers you placed are cleared'; return; }
            try { apply(await api('POST', '/api/figur/rul')); renderStep(); } catch (err) { showErr(err.message); }
        };
        box.querySelectorAll('[data-evne]').forEach(el => {
            const send = () => {
                const tal = [...fig.abilities];
                tal[+el.dataset.evne] = parseInt(el.value, 10) || 0;
                if (fig.abilityMethod === 'manual' && tal[+el.dataset.evne] && (tal[+el.dataset.evne] < 3 || tal[+el.dataset.evne] > 18)) { showErr('Write a number from 3 to 18.'); return; }
                showErr('');
                fig.abilities = tal;
                save('evner', () => api('PUT', '/api/figur/evner', { metode: fig.abilityMethod, tal }), el.tagName === 'SELECT' ? 0 : 600);
            };
            if (el.tagName === 'SELECT') el.onchange = send; else el.oninput = send;
        });

        // hit points
        const hpSend = () => {
            const hp = parseInt($('hpNu').value, 10), max = parseInt($('hpMax').value, 10);
            if (isNaN(hp) || isNaN(max) || max < 1) return;
            fig.hp = hp; fig.maxHp = max;
            save('liv', () => api('PUT', '/api/figur/liv', { liv: hp, maxLiv: max }), 600);
        };
        if ($('hpNu')) { $('hpNu').oninput = hpSend; $('hpMax').oninput = hpSend; }
        if ($('brugHp')) $('brugHp').onclick = () => { $('hpNu').value = fig.suggestedHp; $('hpMax').value = fig.suggestedHp; hpSend(); run('liv'); };

        // spells
        if ($('spells')) wireSpells();

        // picture
        if ($('fil')) $('fil').onchange = async () => {
            const f = $('fil').files[0];
            if (!f) return;
            showErr('');
            try {
                const jpeg = await shrink(f);
                const r = await fetch('/api/figur/billede', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'image/jpeg', 'X-Kode': code }, body: jpeg });
                const d = await r.json().catch(() => ({}));
                if (!r.ok) throw new Error(d.fejl || 'The picture could not be saved.');
                apply(d); renderStep();
            } catch (err) { showErr(err.message); }
        };
        if ($('ai')) $('ai').onclick = () => {
            const style = fig.kind === 'mystery' ? 'Portrait, 1920s painted style, whole figure visible:' : fig.kind === 'coop' ? 'Portrait, adventure illustration, whole figure visible:' : 'Fantasy portrait, painted style, whole figure visible:';
            const beskriv = [style, fig.people && 'a ' + fig.people.toLowerCase(), fig.class, $('f-look')?.value.trim()].filter(Boolean).join(' ');
            window.open('https://billedbehandling.itmartin.dk/?beskriv=' + encodeURIComponent(beskriv), '_blank', 'noopener');
            $('aiHjaelp').hidden = false;
        };

        // ready
        if ($('klar')) $('klar').onclick = async () => {
            for (const k of Object.keys(pending)) { clearTimeout(pending[k].t); await run(k); }
            try { apply(await api('POST', '/api/figur/klar')); renderStep(); } catch (err) { showErr(err.message); }
        };
        if ($('laas')) $('laas').onclick = async () => {
            try { apply(await api('POST', '/api/figur/laas', { laast: !fig.locked })); renderStep(); } catch (err) { showErr(err.message); }
        };
    }

    function wireSpells() {
        const saveSpells = d => save('spells', () => api('PUT', '/api/figur/besvaergelser', spells.map(s => ({ navn: s.name, tekst: s.text }))), d);
        const draw = () => {
            $('spells').innerHTML = spells.length ? spells.map((s, i) => `<div class="spell-ret">
                <input class="k-input" data-sn="${i}" maxlength="60" value="${esc(s.name)}" placeholder="Name, e.g. Fireball" aria-label="Name" ${dis()} />
                <textarea class="k-textarea" data-st="${i}" rows="3" maxlength="3000" placeholder="What does it do?" aria-label="What does it do" ${dis()}>${esc(s.text)}</textarea>
                ${canEdit() ? `<button type="button" class="k-btn k-btn-sm k-btn-ghost" data-sx="${i}">Remove</button>` : ''}</div>`).join('')
                : '<p class="k-help">No spells yet.</p>';
            $('spells').querySelectorAll('[data-sn]').forEach(el => el.oninput = () => { spells[+el.dataset.sn].name = el.value; fig.spells = spells; saveSpells(); });
            $('spells').querySelectorAll('[data-st]').forEach(el => el.oninput = () => { spells[+el.dataset.st].text = el.value; fig.spells = spells; saveSpells(); });
            $('spells').querySelectorAll('[data-sx]').forEach(el => el.onclick = () => { spells.splice(+el.dataset.sx, 1); fig.spells = spells; draw(); saveSpells(0); });
        };
        draw();
        if ($('nySpell')) $('nySpell').onclick = () => { spells.push({ name: '', text: '' }); draw(); $('spells').querySelector(`[data-sn="${spells.length - 1}"]`)?.focus(); };
        let t = 0;
        if ($('soeg')) $('soeg').oninput = () => {
            clearTimeout(t);
            const q = $('soeg').value.trim();
            t = setTimeout(async () => {
                if (q.length < 2) { $('hits').innerHTML = ''; return; }
                try {
                    const hits = await api('GET', '/api/besvaergelser?q=' + encodeURIComponent(q));
                    $('hits').innerHTML = hits.length ? hits.map(h => `<button type="button" class="k-btn k-btn-sm" data-idx="${esc(h.index)}">${esc(h.name)}</button>`).join('')
                        : '<p class="k-help">None found. Add it yourself below.</p>';
                    $('hits').querySelectorAll('[data-idx]').forEach(b => b.onclick = async () => {
                        try {
                            const s = await api('GET', '/api/besvaergelser/' + b.dataset.idx);
                            spells.push({ name: s.name, text: s.text }); fig.spells = spells; draw(); saveSpells(0);
                            $('soeg').value = ''; $('hits').innerHTML = '';
                        } catch (err) { $('hits').innerHTML = `<p class="fejl">${esc(err.message)}</p>`; }
                    });
                } catch (err) { $('hits').innerHTML = `<p class="fejl">${esc(err.message)}</p>`; }
            }, 300);
        };
    }

    // Shrink to max 768 px JPEG on the phone, then send - a phone photo is several MB.
    async function shrink(file) {
        const bmp = await createImageBitmap(file);
        const k = Math.min(1, 768 / Math.max(bmp.width, bmp.height));
        const c = document.createElement('canvas');
        c.width = Math.round(bmp.width * k); c.height = Math.round(bmp.height * k);
        c.getContext('2d').drawImage(bmp, 0, 0, c.width, c.height);
        return await new Promise(res => c.toBlob(res, 'image/jpeg', 0.85));
    }

    // ---------- talk about this step ----------

    const timeFmt = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit' });
    function renderComments() {
        const list = fig.comments.filter(c => c.step === step);
        seen[step] = list.length;
        $('hjaelpTitel').textContent = `Talk about this step: ${label(step)}`;
        $('kommentarTekst').placeholder = master() ? `Write to ${fig.playerName}…` : 'Write to your game master…';
        $('kommentarer').innerHTML = list.length ? list.map(c => `<div class="kommentar${c.from === fig.viewer ? ' egen' : ''}">
            <span class="meta">${esc(c.from === fig.viewer ? 'You' : c.fromName)}${c.from === 'master' ? ' (game master)' : ''} · ${timeFmt.format(new Date(c.at))}</span>
            <p>${esc(c.text)}</p></div>`).join('')
            : `<p class="k-help">${master() ? 'Give a tip, suggest something or ask a question.' : 'Stuck? Ask your game master here – they see it at once if they have the character open.'}</p>`;
    }
    $('kommentarForm').addEventListener('submit', async e => {
        e.preventDefault();
        const tekst = $('kommentarTekst').value.trim();
        if (!tekst) return;
        $('kommentarFejl').hidden = true;
        try {
            const c = await api('POST', '/api/figur/kommentar', { trin: step, tekst });
            $('kommentarTekst').value = '';
            if (!fig.comments.some(x => x.id === c.id)) fig.comments.push(c);
            renderComments(); renderNav();
        } catch (err) { $('kommentarFejl').textContent = err.message; $('kommentarFejl').hidden = false; }
    });

    // ---------- Light/Dark + remembered boxes (same as ElPriser) ----------

    function syncTheme() {
        const cur = document.documentElement.getAttribute('data-theme') || 'light';
        $('themeToggleBtn').textContent = cur === 'light' ? 'Dark' : 'Light';
        document.querySelector('meta[name=theme-color]')?.setAttribute('content', cur === 'light' ? '#f4f6f5' : '#0b1220');
    }
    $('themeToggleBtn').onclick = () => {
        const next = (document.documentElement.getAttribute('data-theme') || 'light') === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', next);
        store.set('character_theme', next);
        syncTheme();
    };
    syncTheme();

    document.querySelectorAll('details[id], details.side-info').forEach(d => {
        const key = 'character_open_' + (d.id || 'om-siden');
        const saved = store.get(key);
        if (saved) d.open = saved === '1';
        d.addEventListener('toggle', () => store.set(key, d.open ? '1' : '0'));
    });
    const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    $(isIos ? 'hjemIphone' : /android/i.test(navigator.userAgent) ? 'hjemAndroid' : 'hjemPc').open = true;
    if (window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true) $('hjemBoks').hidden = true;

    // ---------- boot ----------

    // A link with ?kode= (the game master's help link) opens that character; the code then leaves the address bar.
    const linkCode = new URLSearchParams(location.search).get('kode');
    if (linkCode) {
        history.replaceState(null, '', location.pathname);
        openCode(linkCode, $('aabnFejl'));
    } else {
        const last = mine()[0];
        if (last && !isMasterEntrance && entrance !== '/player') openCode(last.code, $('aabnFejl'));
        else showStart();
    }
})();
