// ── Utilities ─────────────────────────────────────────────────────────────────

function esc(s) {
    return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

async function api(url, method, body) {
    const opts = { method: method || 'GET', headers: {} };
    if (body !== undefined && body !== null) {
        opts.headers['Content-Type'] = 'application/json';
        opts.body = JSON.stringify(body);
    }
    return fetch(url, opts);
}

function toast(msg, success) {
    let el = document.getElementById('_toast');
    if (!el) {
        el = document.createElement('div');
        el.id = '_toast';
        Object.assign(el.style, {
            position:'fixed', bottom:'20px', left:'50%', transform:'translateX(-50%)',
            color:'#fff', padding:'10px 20px', borderRadius:'8px', zIndex:'9999',
            fontSize:'14px', transition:'opacity .3s', pointerEvents:'none'
        });
        document.body.appendChild(el);
    }
    el.textContent = msg;
    el.style.background = success === false ? '#c0392b' : '#27ae60';
    el.style.opacity = '1';
    clearTimeout(el._t);
    el._t = setTimeout(() => el.style.opacity = '0', 2500);
}

// ── Floating emoji ────────────────────────────────────────────────────────────

function floatEmoji(emoji) {
    const zone = document.getElementById('float-zone');
    if (!zone) return;
    const el = document.createElement('span');
    el.className = 'float-emoji';
    el.textContent = emoji;
    el.style.left = (30 + Math.random() * 40) + '%';
    zone.appendChild(el);
    el.addEventListener('animationend', () => el.remove());
}

// ── Push helpers ──────────────────────────────────────────────────────────────

function urlBase64ToUint8Array(b64) {
    const pad = '='.repeat((4 - b64.length % 4) % 4);
    const raw = atob((b64 + pad).replace(/-/g,'+').replace(/_/g,'/'));
    return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
}

function arrayBufferToBase64(buf) {
    return btoa(String.fromCharCode(...new Uint8Array(buf)));
}

async function subscribePush(slug, vapidPublicKey) {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) return false;
    try {
        const perm = await Notification.requestPermission();
        if (perm !== 'granted') return false;
        const reg = await navigator.serviceWorker.ready;
        const existing = await reg.pushManager.getSubscription();
        const sub = existing ?? await reg.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: urlBase64ToUint8Array(vapidPublicKey)
        });
        await api(`/api/push/subscribe?slug=${encodeURIComponent(slug)}`, 'POST', {
            endpoint: sub.endpoint,
            keys: {
                p256dh: arrayBufferToBase64(sub.getKey('p256dh')),
                auth:   arrayBufferToBase64(sub.getKey('auth'))
            }
        });
        return true;
    } catch (e) { console.error('Push subscribe failed', e); return false; }
}

// ── Card renderer ─────────────────────────────────────────────────────────────
// UpdateType: Text=0, Score=1, Position=2, Poll=3, Breaking=4, Video=5, Summary=6

function cardHtml(slug, u, isWriter, pin) {
    const t = u.type;
    const raw = u.createdAt.endsWith('Z') ? u.createdAt : u.createdAt + 'Z';
    const time = new Date(raw).toLocaleTimeString('da-DK', { hour:'2-digit', minute:'2-digit' });

    const names = ['text','score','position','poll','breaking','video','summary'];
    let cls = 'card card-' + (names[t] || 'text') + (u.isStarred ? ' card-starred' : '');

    let label = '';
    if (t === 4) label = '<div class="card-breaking-label">🔴 BREAKING</div>';
    if (t === 6) label = '<div class="card-summary-label">📋 Opsummering</div>';

    let body = '';
    if (t === 1) {
        body = `<div class="card-score">${esc(u.text)}</div>`;
    } else if (t === 5 && u.videoPath) {
        if (u.text) body += `<div class="card-text">${esc(u.text)}</div>`;
        body += `<video class="card-video" controls playsinline preload="metadata">
                    <source src="${esc(u.videoPath)}"></video>`;
    } else if (t === 3 && u.pollOptions && u.pollOptions.length) {
        body = `<div class="card-text">${esc(u.text)}</div><div class="poll">`;
        const total = Math.max(1, u.pollOptions.reduce((a, o) => a + o.votes, 0));
        u.pollOptions.forEach((opt, i) => {
            const pct = Math.round(opt.votes * 100 / total);
            body += `<button class="poll-opt" onclick="vote('${slug}','${u.id}',${i})">
                <div class="poll-bar" style="width:${pct}%"></div>
                <span class="poll-label">${esc(opt.text)}</span>
                <span class="poll-pct">${pct}%</span></button>`;
        });
        body += '</div>';
    } else {
        body = `<div class="card-text">${esc(u.text)}</div>`;
    }

    const emojis = ['👍','🔥','😱','😢'];
    let rxn = '<div class="reactions">';
    emojis.forEach(e => {
        const n = (u.reactions && u.reactions[e]) || 0;
        rxn += `<button class="reaction${n > 0 ? ' reaction-active' : ''}"
            onclick="react('${slug}','${u.id}','${e}')">${e}${n > 0 ? '<span>'+n+'</span>' : ''}</button>`;
    });
    rxn += '</div>';

    let wa = '';
    if (isWriter) {
        wa = `<div class="writer-actions">
            <button class="wbtn" onclick="starUpdate('${u.id}')" title="${u.isStarred?'Fjern pin':'Pin øverst'}">${u.isStarred?'📌':'📍'}</button>
            <button class="wbtn" onclick="pushUpdate('${u.id}')" title="Send push">🔔</button>
            <button class="wbtn danger" onclick="writerDelete('${u.id}')" title="Slet">🗑️</button>
        </div>`;
    }

    return `<div class="${cls}" id="card-${u.id}">
        ${label}${body}
        <div class="card-footer"><span class="card-time">${time}</span>${rxn}${wa}</div>
    </div>`;
}

// ── Viewer page ───────────────────────────────────────────────────────────────

var _vSlug = '', _vFp = '', _vTimer = null, _vNotif = false;

function initViewer(slug) {
    _vSlug = slug;
    if ('serviceWorker' in navigator) navigator.serviceWorker.register('/sw.js').catch(() => {});
    pollViewer();
}

async function pollViewer() {
    try {
        const res = await fetch('/api/event/' + encodeURIComponent(_vSlug));
        if (res.ok) {
            const ev = await res.json();
            const fp = (ev.updates.length) + '|' + (ev.updates[0]?.id || '') + '|' + ev.headerText + '|' + (ev.updates[0] && ev.updates[0].reactions ? JSON.stringify(ev.updates[0].reactions) : '');
            if (fp !== _vFp) { _vFp = fp; renderViewer(ev); }
        }
    } catch (e) { /* ignore */ }
    _vTimer = setTimeout(pollViewer, 3000);
}

function renderViewer(ev) {
    const nameEl  = document.getElementById('v-name');
    const scoreEl = document.getElementById('v-score');
    const countEl = document.getElementById('v-count');
    if (nameEl)  nameEl.textContent  = ev.sportEmoji + ' ' + ev.name;
    if (scoreEl) { scoreEl.textContent = ev.headerText || ''; scoreEl.style.display = ev.headerText ? '' : 'none'; }
    if (countEl) countEl.textContent = '👁️ ' + ev.viewerCount;

    const feed = document.getElementById('v-feed');
    if (!feed) return;
    const updates = ev.updates || [];
    const starred = updates.filter(u => u.isStarred);
    const normal  = updates.filter(u => !u.isStarred);
    let html = '';
    if (starred.length) {
        html += '<div class="pinned-section"><div class="section-label">📌 Fastgjorte</div>';
        starred.forEach(u => html += cardHtml(_vSlug, u, false, ''));
        html += '</div>';
    }
    if (normal.length) normal.forEach(u => html += cardHtml(_vSlug, u, false, ''));
    else if (!starred.length) html = '<div class="empty-state" style="padding:40px 0">📡 Ingen opdateringer endnu</div>';
    feed.innerHTML = html;
}

function react(slug, id, emoji) {
    floatEmoji(emoji);
    api(`/api/event/${encodeURIComponent(slug)}/react/${id}?emoji=${encodeURIComponent(emoji)}`, 'POST');
    clearTimeout(_vTimer); _vFp = ''; setTimeout(pollViewer, 500);
}

function vote(slug, id, idx) {
    api(`/api/event/${encodeURIComponent(slug)}/vote/${id}?idx=${idx}`, 'POST');
    clearTimeout(_vTimer); _vFp = ''; setTimeout(pollViewer, 500);
}

function openMsgModal() {
    document.getElementById('msg-modal').style.display = 'flex';
    document.getElementById('msg-sent').style.display  = 'none';
    document.getElementById('msg-send-btn').style.display = '';
}

function closeMsgModal() {
    document.getElementById('msg-modal').style.display = 'none';
}

async function sendViewerMsg() {
    const author = document.getElementById('msg-author')?.value?.trim() || 'Anonym';
    const text   = document.getElementById('msg-text')?.value?.trim() || '';
    if (!text) return;
    const res = await api(`/api/event/${encodeURIComponent(_vSlug)}/message`, 'POST', { author, text });
    if (res.ok) {
        document.getElementById('msg-text').value   = '';
        document.getElementById('msg-sent').style.display     = '';
        document.getElementById('msg-send-btn').style.display = 'none';
    }
}

async function toggleNotifications() {
    if (_vNotif) return;
    try {
        const { publicKey } = await (await fetch('/api/push/vapid-key')).json();
        const ok = await subscribePush(_vSlug, publicKey);
        if (ok) { _vNotif = true; document.getElementById('v-notify-btn')?.classList.add('notify-on'); toast('🔔 Notifikationer aktiveret'); }
    } catch (e) { toast('Kunne ikke aktivere notifikationer', false); }
}

// ── Writer page ───────────────────────────────────────────────────────────────

var _wSlug = '', _wPin = '', _wType = 0, _wVideoPath = null, _wFp = '', _wTimer = null;

function initWriter(slug, pin) {
    _wSlug = slug;
    _wPin  = pin;
    pollWriter();
}

async function pollWriter() {
    try {
        const res = await fetch(`/api/event/${encodeURIComponent(_wSlug)}/writer?pin=${encodeURIComponent(_wPin)}`);
        if (res.ok) {
            const ev = await res.json();
            const fp = ev.updates.length + '|' + (ev.updates[0]?.id || '') + '|' + (ev.pendingMessages?.length || 0);
            if (fp !== _wFp) { _wFp = fp; renderWriterFeed(ev); renderPending(ev); }
            const badge = document.getElementById('pending-badge');
            const n = ev.pendingMessages?.length || 0;
            if (badge) { badge.textContent = n; badge.style.display = n > 0 ? '' : 'none'; }
            const countEl = document.getElementById('w-count');
            if (countEl) countEl.textContent = '👁️ ' + ev.viewerCount;
            const tBtn = document.getElementById('toggle-btn');
            if (tBtn) tBtn.textContent = ev.isActive ? '⏹️ Afslut begivenhed' : '▶️ Genåbn begivenhed';
        }
    } catch (e) { /* ignore */ }
    _wTimer = setTimeout(pollWriter, 4000);
}

function renderWriterFeed(ev) {
    const feed = document.getElementById('w-feed');
    if (!feed) return;
    const updates = ev.updates || [];
    feed.innerHTML = updates.length
        ? updates.map(u => cardHtml(_wSlug, u, true, _wPin)).join('')
        : '<div class="empty-state">Ingen opdateringer endnu</div>';
}

function renderPending(ev) {
    const container = document.getElementById('w-pending');
    if (!container) return;
    const msgs = ev.pendingMessages || [];
    if (!msgs.length) { container.innerHTML = ''; return; }
    let html = '<div class="pending-section"><div class="section-label">💬 Afventende beskeder</div>';
    msgs.forEach(m => {
        const raw  = m.createdAt.endsWith('Z') ? m.createdAt : m.createdAt + 'Z';
        const time = new Date(raw).toLocaleTimeString('da-DK', { hour:'2-digit', minute:'2-digit' });
        html += `<div class="pending-card">
            <div class="pending-author">${esc(m.author)} <span class="pending-time">${time}</span></div>
            <div class="pending-text">${esc(m.text)}</div>
            <div class="pending-actions">
                <button class="primary-btn compact" onclick="approveMsg('${m.id}')">✅ Godkend</button>
                <button class="ghost-btn compact danger" onclick="rejectMsg('${m.id}')">❌ Afvis</button>
            </div></div>`;
    });
    container.innerHTML = html + '</div>';
}

function writerTab(tab) {
    const isWrite = tab === 'write';
    document.getElementById('pane-write').style.display = isWrite ? '' : 'none';
    document.getElementById('pane-feed').style.display  = isWrite ? 'none' : '';
    document.getElementById('tab-write').classList.toggle('tab-active', isWrite);
    document.getElementById('tab-feed').classList.toggle('tab-active', !isWrite);
}

// UpdateType: Text=0, Score=1, Position=2, Poll=3, Breaking=4, Video=5
// Button display order: Text, Score, Position, Breaking, Poll, Video → UpdateType values [0,1,2,4,3,5]
var _typeOrder = [0, 1, 2, 4, 3, 5];
var _typePlaceholders = {
    0: 'Hvad sker der?',
    1: 'f.eks. Danmark 2 – 1 Frankrig',
    2: 'f.eks. Vingegaard rykker alene fra favoritter',
    3: 'Hvad tror du?',
    4: 'Den store nyhed...',
    5: 'Beskriv videoen (valgfrit)'
};

function selectType(t) {
    _wType = t;
    _wVideoPath = null;
    document.querySelectorAll('.type-btn').forEach((btn, i) => btn.classList.toggle('type-active', _typeOrder[i] === t));
    document.getElementById('poll-section').style.display  = (t === 3) ? '' : 'none';
    document.getElementById('video-section').style.display = (t === 5) ? '' : 'none';
    const ta = document.getElementById('w-text');
    if (ta) ta.placeholder = _typePlaceholders[t] || 'Hvad sker der?';
    const vr = document.getElementById('video-ready');
    const vi = document.getElementById('w-video');
    if (vr) vr.style.display = 'none';
    if (vi) vi.value = '';
    document.getElementById('upload-status').style.display = 'none';
}

function addPollOpt() {
    const c = document.getElementById('poll-opts');
    if (!c || c.querySelectorAll('.poll-opt-input').length >= 4) return;
    const n = c.querySelectorAll('.poll-opt-input').length + 1;
    const inp = document.createElement('input');
    inp.className = 'field poll-opt-input';
    inp.placeholder = 'Mulighed ' + n;
    inp.style.marginBottom = '6px';
    c.appendChild(inp);
}

async function updateHeader() {
    const text = document.getElementById('w-header')?.value?.trim();
    if (!text) return;
    const res = await api(`/api/event/${encodeURIComponent(_wSlug)}/header?pin=${encodeURIComponent(_wPin)}`, 'POST', { text });
    if (res.ok) {
        const v = document.getElementById('w-header-val');
        const c = document.getElementById('w-current-header');
        if (v) v.textContent = text;
        if (c) c.style.display = '';
        toast('✅ Header opdateret');
    } else { toast('Fejl', false); }
}

async function postUpdate() {
    const text = document.getElementById('w-text')?.value?.trim() || '';
    if (!text && _wType !== 5) return;
    if (_wType === 5 && !_wVideoPath) { toast('Upload en video først', false); return; }

    const update = { type: _wType, text, videoPath: _wVideoPath || null, pollOptions: [] };
    if (_wType === 3) {
        document.querySelectorAll('.poll-opt-input').forEach(o => {
            if (o.value.trim()) update.pollOptions.push({ text: o.value.trim(), votes: 0 });
        });
        if (update.pollOptions.length < 2) { toast('Tilføj mindst 2 valgmuligheder', false); return; }
    }

    const sendPush = document.getElementById('w-send-push')?.checked;
    const btn = document.getElementById('post-btn');
    if (btn) { btn.disabled = true; btn.textContent = 'Poster...'; }

    try {
        const res = await api(
            `/api/event/${encodeURIComponent(_wSlug)}/update?pin=${encodeURIComponent(_wPin)}&sendPush=${sendPush}`,
            'POST', update);
        if (res.ok) {
            document.getElementById('w-text').value = '';
            document.getElementById('w-send-push').checked = false;
            _wVideoPath = null;
            selectType(0);
            toast('✅ Postet');
            clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
        } else { toast('Fejl ved post', false); }
    } catch (e) { toast('Netværksfejl', false); }
    if (btn) { btn.disabled = false; btn.textContent = 'Post opdatering'; }
}

async function onVideoSelected(input) {
    const file = input.files[0];
    if (!file) return;
    const statusEl = document.getElementById('upload-status');
    const readyEl  = document.getElementById('video-ready');
    if (statusEl) { statusEl.textContent = '⏳ Uploader...'; statusEl.style.display = ''; }
    if (readyEl)  readyEl.style.display = 'none';
    const fd = new FormData();
    fd.append('file', file);
    try {
        const res = await fetch(`/api/upload?slug=${encodeURIComponent(_wSlug)}&pin=${encodeURIComponent(_wPin)}`, { method:'POST', body: fd });
        if (res.ok) {
            const data = await res.json();
            _wVideoPath = data.webPath;
            if (statusEl) statusEl.style.display = 'none';
            if (readyEl)  readyEl.style.display = '';
        } else {
            if (statusEl) statusEl.textContent = '❌ Upload fejlede';
        }
    } catch (e) {
        if (statusEl) statusEl.textContent = '❌ Netværksfejl';
    }
}

async function starUpdate(id) {
    await api(`/api/event/${encodeURIComponent(_wSlug)}/star/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function writerDelete(id) {
    if (!confirm('Slet denne opdatering?')) return;
    await api(`/api/event/${encodeURIComponent(_wSlug)}/delete/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function pushUpdate(id) {
    const res = await api(`/api/event/${encodeURIComponent(_wSlug)}/push/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    toast(res.ok ? '🔔 Push sendt' : 'Ingen abonnenter', res.ok);
}

async function approveMsg(id) {
    await api(`/api/event/${encodeURIComponent(_wSlug)}/approve/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function rejectMsg(id) {
    await api(`/api/event/${encodeURIComponent(_wSlug)}/reject/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function toggleActive() {
    await api(`/api/event/${encodeURIComponent(_wSlug)}/toggle?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function generateSummary() {
    const btn = document.getElementById('summary-btn');
    if (btn) { btn.disabled = true; btn.textContent = '⏳ Genererer...'; }
    try {
        const res = await api(`/api/event/${encodeURIComponent(_wSlug)}/summary?pin=${encodeURIComponent(_wPin)}`, 'POST');
        toast(res.ok ? '📋 Opsummering oprettet' : 'Fejl ved opsummering', res.ok);
        if (res.ok) { clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300); }
    } catch (e) { toast('Netværksfejl', false); }
    if (btn) { btn.disabled = false; btn.textContent = '📋 Generer opsummering (AI)'; }
}

// ── Admin page ────────────────────────────────────────────────────────────────

var _aPin = '';

function initAdmin(pin) {
    _aPin = pin;
    loadEvents();
}

async function loadEvents() {
    try {
        const res = await fetch(`/api/admin/events?pin=${encodeURIComponent(_aPin)}`);
        if (!res.ok) return;
        const events = await res.json();
        const container = document.getElementById('a-events');
        if (!container) return;
        if (!events.length) { container.innerHTML = '<div class="empty-state">Ingen begivenheder endnu</div>'; return; }
        container.innerHTML = events.map(ev => `
            <div class="admin-event-row">
                <span class="admin-event-emoji">${esc(ev.sportEmoji)}</span>
                <div class="admin-event-info">
                    <div class="admin-event-name">${esc(ev.name)}</div>
                    <div class="admin-event-meta">/${esc(ev.slug)} · PIN: ${esc(ev.writerPin)} · ${ev.updates.length} opdateringer</div>
                </div>
                <div class="admin-event-actions">
                    <a href="/w/${esc(ev.slug)}?pin=${esc(ev.writerPin)}" class="ghost-btn">✏️</a>
                    <a href="/e/${esc(ev.slug)}" class="ghost-btn">👁️</a>
                    <button class="ghost-btn ${ev.isActive ? '' : 'danger'}" onclick="toggleEvent('${ev.slug}')">
                        ${ev.isActive ? '🟢' : '🔴'}
                    </button>
                </div>
            </div>`).join('');
    } catch (e) { /* ignore */ }
}

function pickEmoji(btn) {
    document.querySelectorAll('.emoji-pick').forEach(b => b.classList.remove('emoji-active'));
    btn.classList.add('emoji-active');
    document.getElementById('a-emoji').value = btn.dataset.emoji;
}

async function createEvent() {
    const name     = document.getElementById('a-name')?.value?.trim();
    const emoji    = document.getElementById('a-emoji')?.value || '🏆';
    const slug     = document.getElementById('a-slug')?.value?.trim().toLowerCase();
    const writerPin = document.getElementById('a-pin-val')?.value?.trim();
    const errEl    = document.getElementById('a-error');

    if (!name || !slug || !writerPin) {
        if (errEl) { errEl.textContent = 'Udfyld alle felter'; errEl.style.display = ''; }
        return;
    }
    if (errEl) errEl.style.display = 'none';

    const res = await api(`/api/admin/events?pin=${encodeURIComponent(_aPin)}`, 'POST', { name, emoji, slug, writerPin });
    if (res.ok) {
        document.getElementById('a-name').value    = '';
        document.getElementById('a-slug').value    = '';
        document.getElementById('a-pin-val').value = '';
        toast('✅ Begivenhed oprettet');
        loadEvents();
    } else if (res.status === 409) {
        if (errEl) { errEl.textContent = 'Slug er allerede i brug'; errEl.style.display = ''; }
    } else {
        if (errEl) { errEl.textContent = 'Fejl'; errEl.style.display = ''; }
    }
}

async function toggleEvent(slug) {
    await api(`/api/admin/toggle/${encodeURIComponent(slug)}?pin=${encodeURIComponent(_aPin)}`, 'POST');
    loadEvents();
}

// ── Auto-init: read page type from data attributes ────────────────────────────

(function () {
    const d = document.getElementById('page-data');
    if (!d) return;
    const page = d.dataset.page;
    const slug = d.dataset.slug || '';
    const pin  = d.dataset.pin  || '';
    if (page === 'viewer') initViewer(slug);
    if (page === 'writer') initWriter(slug, pin);
    if (page === 'admin')  initAdmin(pin);
})();
