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

// ── Live-link embed (YouTube/Twitch) ────────────────────────────────────────────

function buildEmbedHtml(url) {
    if (!url) return '';
    let u;
    try { u = new URL(url); } catch (e) { return ''; }

    if (u.hostname.includes('youtube.com') || u.hostname.includes('youtu.be')) {
        let videoId = '';
        if (u.hostname.includes('youtu.be')) videoId = u.pathname.slice(1);
        else if (u.pathname === '/watch') videoId = u.searchParams.get('v') || '';
        else if (u.pathname.startsWith('/live/')) videoId = u.pathname.split('/')[2] || '';
        else if (u.pathname.startsWith('/embed/')) videoId = u.pathname.split('/')[2] || '';
        if (!videoId) return '';
        return `<div class="player-wrap"><iframe src="https://www.youtube.com/embed/${encodeURIComponent(videoId)}"
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen></iframe></div>`;
    }

    if (u.hostname.includes('twitch.tv')) {
        const channel = u.pathname.split('/').filter(Boolean)[0] || '';
        if (!channel) return '';
        return `<div class="player-wrap"><iframe src="https://player.twitch.tv/?channel=${encodeURIComponent(channel)}&parent=${encodeURIComponent(location.hostname)}&autoplay=false" allowfullscreen></iframe></div>`;
    }

    return '';
}

// ── Card renderer ─────────────────────────────────────────────────────────────
// UpdateType: Text=0, Milestone=1, Breaking=2, Poll=3

function cardHtml(slug, u, isWriter, pin) {
    const t = u.type;
    const raw = u.createdAt.endsWith('Z') ? u.createdAt : u.createdAt + 'Z';
    const time = new Date(raw).toLocaleTimeString('da-DK', { hour:'2-digit', minute:'2-digit' });

    const names = ['text','milestone','breaking','poll'];
    let cls = 'card card-' + (names[t] || 'text') + (u.isStarred ? ' card-starred' : '');

    let label = '';
    if (t === 1) label = '<div class="card-milestone-label">🎯 MILEPÆL</div>';
    if (t === 2) label = '<div class="card-breaking-label">🔴 BREAKING</div>';

    let replyRef = '';
    if (u.replyToId) {
        replyRef = `<div class="reply-ref">↳ Svar til: "${esc((u.replyToText || '').slice(0, 60))}"</div>`;
    }

    let body = '';
    if (t === 3 && u.pollOptions && u.pollOptions.length) {
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

    const emojis = ['👍','🔥','💡','❤️'];
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
            <button class="wbtn" onclick="startReply('${u.id}')" title="Svar">💬</button>
            <button class="wbtn" onclick="starUpdate('${u.id}')" title="${u.isStarred?'Fjern pin':'Pin øverst'}">${u.isStarred?'📌':'📍'}</button>
            <button class="wbtn danger" onclick="writerDelete('${u.id}')" title="Slet">🗑️</button>
        </div>`;
    }

    return `<div class="${cls}" id="card-${u.id}">
        ${replyRef}${label}${body}
        <div class="card-footer"><span class="card-time">${time}</span>${rxn}${wa}</div>
    </div>`;
}

// ── Viewer page ───────────────────────────────────────────────────────────────

var _vSlug = '', _vFp = '', _vTimer = null, _vLastStreamUrl = undefined;

function initViewer(slug) {
    _vSlug = slug;
    pollViewer();
}

async function pollViewer() {
    try {
        const res = await fetch('/api/project/' + encodeURIComponent(_vSlug));
        if (res.ok) {
            const p = await res.json();
            const fp = (p.updates.length) + '|' + (p.updates[0]?.id || '') + '|' + p.statusText + '|' + p.streamUrl + '|' + (p.updates[0] && p.updates[0].reactions ? JSON.stringify(p.updates[0].reactions) : '');
            if (fp !== _vFp) { _vFp = fp; renderViewer(p); }
        }
    } catch (e) { /* ignore */ }
    _vTimer = setTimeout(pollViewer, 3000);
}

function renderViewer(p) {
    const nameEl  = document.getElementById('v-name');
    const scoreEl = document.getElementById('v-score');
    if (nameEl)  nameEl.textContent  = p.emoji + ' ' + p.name;
    if (scoreEl) { scoreEl.textContent = p.statusText || ''; scoreEl.style.display = p.statusText ? '' : 'none'; }

    // Only touch the player when the link actually changed — rebuilding the iframe on every
    // poll would restart/interrupt playback for someone actively watching.
    if (p.streamUrl !== _vLastStreamUrl) {
        _vLastStreamUrl = p.streamUrl;
        const playerEl = document.getElementById('v-player');
        if (playerEl) playerEl.innerHTML = buildEmbedHtml(p.streamUrl);
    }

    const feed = document.getElementById('v-feed');
    if (!feed) return;
    const updates = p.updates || [];
    const starred = updates.filter(u => u.isStarred);
    const normal  = updates.filter(u => !u.isStarred);
    let html = '';
    if (starred.length) {
        html += '<div class="pinned-section"><div class="section-label">📌 Fastgjorte</div>';
        starred.forEach(u => html += cardHtml(_vSlug, u, false, ''));
        html += '</div>';
    }
    if (normal.length) normal.forEach(u => html += cardHtml(_vSlug, u, false, ''));
    else if (!starred.length) html = '<div class="empty-state" style="padding:40px 0">🚀 Ingen opdateringer endnu</div>';
    feed.innerHTML = html;
}

function react(slug, id, emoji) {
    floatEmoji(emoji);
    api(`/api/project/${encodeURIComponent(slug)}/react/${id}?emoji=${encodeURIComponent(emoji)}`, 'POST');
    clearTimeout(_vTimer); _vFp = ''; setTimeout(pollViewer, 500);
}

function vote(slug, id, idx) {
    api(`/api/project/${encodeURIComponent(slug)}/vote/${id}?idx=${idx}`, 'POST');
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

async function sendComment() {
    const author = document.getElementById('msg-author')?.value?.trim() || 'Anonym';
    const text   = document.getElementById('msg-text')?.value?.trim() || '';
    if (!text) return;
    const res = await api(`/api/project/${encodeURIComponent(_vSlug)}/comment`, 'POST', { author, text });
    if (res.ok) {
        document.getElementById('msg-text').value   = '';
        document.getElementById('msg-sent').style.display     = '';
        document.getElementById('msg-send-btn').style.display = 'none';
    }
}

// ── Writer page ───────────────────────────────────────────────────────────────

var _wSlug = '', _wPin = '', _wType = 0, _wFp = '', _wTimer = null, _wReplyId = null, _wReplyText = '';

function initWriter(slug, pin) {
    _wSlug = slug;
    _wPin  = pin;
    pollWriter();
}

async function pollWriter() {
    try {
        const res = await fetch(`/api/project/${encodeURIComponent(_wSlug)}/writer?pin=${encodeURIComponent(_wPin)}`);
        if (res.ok) {
            const p = await res.json();
            const fp = p.updates.length + '|' + (p.updates[0]?.id || '') + '|' + (p.pendingComments?.length || 0) + '|' + p.streamUrl;
            if (fp !== _wFp) { _wFp = fp; renderWriterFeed(p); renderPending(p); }
            const badge = document.getElementById('pending-badge');
            const n = p.pendingComments?.length || 0;
            if (badge) { badge.textContent = n; badge.style.display = n > 0 ? '' : 'none'; }
            const tBtn = document.getElementById('toggle-btn');
            if (tBtn) tBtn.textContent = p.isActive ? '⏹️ Afslut projekt' : '▶️ Genåbn projekt';
        }
    } catch (e) { /* ignore */ }
    _wTimer = setTimeout(pollWriter, 4000);
}

var _wLastUpdates = [];

function renderWriterFeed(p) {
    const feed = document.getElementById('w-feed');
    if (!feed) return;
    const updates = p.updates || [];
    _wLastUpdates = updates;
    feed.innerHTML = updates.length
        ? updates.map(u => cardHtml(_wSlug, u, true, _wPin)).join('')
        : '<div class="empty-state">Ingen opdateringer endnu</div>';
}

function renderPending(p) {
    const container = document.getElementById('w-pending');
    if (!container) return;
    const msgs = p.pendingComments || [];
    if (!msgs.length) { container.innerHTML = ''; return; }
    let html = '<div class="pending-section"><div class="section-label">💬 Afventende kommentarer</div>';
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

// UpdateType: Text=0, Milestone=1, Breaking=2, Poll=3
var _typeOrder = [0, 1, 2, 3];
var _typePlaceholders = {
    0: 'Hvad sker der?',
    1: 'Hvilken milepæl er nået?',
    2: 'Den store nyhed...',
    3: 'Hvad vil du spørge brugerne om?'
};

function selectType(t) {
    _wType = t;
    document.querySelectorAll('.type-btn').forEach((btn, i) => btn.classList.toggle('type-active', _typeOrder[i] === t));
    document.getElementById('poll-section').style.display = (t === 3) ? '' : 'none';
    const ta = document.getElementById('w-text');
    if (ta) ta.placeholder = _typePlaceholders[t] || 'Hvad sker der?';
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

async function updateStatus() {
    const text = document.getElementById('w-header')?.value?.trim();
    if (!text) return;
    const res = await api(`/api/project/${encodeURIComponent(_wSlug)}/status?pin=${encodeURIComponent(_wPin)}`, 'POST', { text });
    if (res.ok) {
        const v = document.getElementById('w-header-val');
        const c = document.getElementById('w-current-header');
        if (v) v.textContent = text;
        if (c) c.style.display = '';
        toast('✅ Status opdateret');
    } else { toast('Fejl', false); }
}

async function updateStreamUrl() {
    const text = document.getElementById('w-streamurl')?.value?.trim() || '';
    const res = await api(`/api/project/${encodeURIComponent(_wSlug)}/stream-url?pin=${encodeURIComponent(_wPin)}`, 'POST', { text });
    toast(res.ok ? '✅ Live-link opdateret' : 'Fejl', res.ok);
}

function startReply(id) {
    const target = _wLastUpdates.find(u => u.id === id);
    const text = target ? target.text : '';
    _wReplyId = id;
    _wReplyText = text;
    const banner = document.getElementById('reply-banner');
    const bannerText = document.getElementById('reply-banner-text');
    if (bannerText) bannerText.textContent = '💬 Svarer til: "' + text.slice(0, 60) + '"';
    if (banner) banner.style.display = 'flex';
    document.getElementById('w-text')?.focus();
    writerTab('write');
}

function cancelReply() {
    _wReplyId = null;
    _wReplyText = '';
    const banner = document.getElementById('reply-banner');
    if (banner) banner.style.display = 'none';
}

async function postUpdate() {
    const text = document.getElementById('w-text')?.value?.trim() || '';
    if (!text) return;

    const update = { type: _wType, text, pollOptions: [], replyToId: _wReplyId, replyToText: _wReplyId ? _wReplyText : null };
    if (_wType === 3) {
        document.querySelectorAll('.poll-opt-input').forEach(o => {
            if (o.value.trim()) update.pollOptions.push({ text: o.value.trim(), votes: 0 });
        });
        if (update.pollOptions.length < 2) { toast('Tilføj mindst 2 valgmuligheder', false); return; }
    }

    const btn = document.getElementById('post-btn');
    if (btn) { btn.disabled = true; btn.textContent = 'Poster...'; }

    try {
        const res = await api(`/api/project/${encodeURIComponent(_wSlug)}/update?pin=${encodeURIComponent(_wPin)}`, 'POST', update);
        if (res.ok) {
            document.getElementById('w-text').value = '';
            selectType(0);
            cancelReply();
            toast('✅ Postet');
            clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
        } else { toast('Fejl ved post', false); }
    } catch (e) { toast('Netværksfejl', false); }
    if (btn) { btn.disabled = false; btn.textContent = 'Post opdatering'; }
}

async function starUpdate(id) {
    await api(`/api/project/${encodeURIComponent(_wSlug)}/star/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function writerDelete(id) {
    if (!confirm('Slet denne opdatering?')) return;
    await api(`/api/project/${encodeURIComponent(_wSlug)}/delete/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function approveMsg(id) {
    await api(`/api/project/${encodeURIComponent(_wSlug)}/approve/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function rejectMsg(id) {
    await api(`/api/project/${encodeURIComponent(_wSlug)}/reject/${id}?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

async function toggleActive() {
    await api(`/api/project/${encodeURIComponent(_wSlug)}/toggle?pin=${encodeURIComponent(_wPin)}`, 'POST');
    clearTimeout(_wTimer); _wFp = ''; setTimeout(pollWriter, 300);
}

// ── Admin page ────────────────────────────────────────────────────────────────

var _aPin = '';

function initAdmin(pin) {
    _aPin = pin;
    loadProjects();
}

async function loadProjects() {
    try {
        const res = await fetch(`/api/admin/projects?pin=${encodeURIComponent(_aPin)}`);
        if (!res.ok) return;
        const projects = await res.json();
        const container = document.getElementById('a-events');
        if (!container) return;
        if (!projects.length) { container.innerHTML = '<div class="empty-state">Ingen projekter endnu</div>'; return; }
        container.innerHTML = projects.map(p => `
            <div class="admin-event-row">
                <span class="admin-event-emoji">${esc(p.emoji)}</span>
                <div class="admin-event-info">
                    <div class="admin-event-name">${esc(p.name)}</div>
                    <div class="admin-event-meta">/${esc(p.slug)} · PIN: ${esc(p.writerPin)} · ${p.updates.length} opdateringer</div>
                </div>
                <div class="admin-event-actions">
                    <a href="/w/${esc(p.slug)}?pin=${esc(p.writerPin)}" class="ghost-btn">✏️</a>
                    <a href="/p/${esc(p.slug)}" class="ghost-btn">👁️</a>
                    <button class="ghost-btn ${p.isActive ? '' : 'danger'}" onclick="toggleProject('${p.slug}')">
                        ${p.isActive ? '🟢' : '🔴'}
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

async function createProject() {
    const name      = document.getElementById('a-name')?.value?.trim();
    const emoji     = document.getElementById('a-emoji')?.value || '🚀';
    const slug      = document.getElementById('a-slug')?.value?.trim().toLowerCase();
    const writerPin = document.getElementById('a-pin-val')?.value?.trim();
    const errEl     = document.getElementById('a-error');

    if (!name || !slug || !writerPin) {
        if (errEl) { errEl.textContent = 'Udfyld alle felter'; errEl.style.display = ''; }
        return;
    }
    if (errEl) errEl.style.display = 'none';

    const res = await api(`/api/admin/projects?pin=${encodeURIComponent(_aPin)}`, 'POST', { name, emoji, slug, writerPin });
    if (res.ok) {
        document.getElementById('a-name').value    = '';
        document.getElementById('a-slug').value    = '';
        document.getElementById('a-pin-val').value = '';
        toast('✅ Projekt oprettet');
        loadProjects();
    } else if (res.status === 409) {
        if (errEl) { errEl.textContent = 'Slug er allerede i brug'; errEl.style.display = ''; }
    } else {
        if (errEl) { errEl.textContent = 'Fejl'; errEl.style.display = ''; }
    }
}

async function toggleProject(slug) {
    await api(`/api/admin/toggle/${encodeURIComponent(slug)}?pin=${encodeURIComponent(_aPin)}`, 'POST');
    loadProjects();
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
