// The player is entirely client-side now: queue, current index, play/pause/
// next/prev/auto-advance, and the now-playing bar's text all live here, not
// in Blazor state. The Blazor Server circuit in this environment has proven
// unreliable enough (see git history/comments in PlayerBar.razor's prior
// version) that anything actually driving playback can't depend on a round
// trip through it - only browsing/search still use Blazor, since those can
// tolerate an occasional slow render.
function getAudio() {
    return document.getElementById('mainAudio');
}

window.playerInterop = {
    queue: [],
    index: -1,
    lyricsOpen: false,
    lyricsLines: [],
    lyricsForUrl: null,
    lyricsActiveIndex: -1,

    init() {
        const audio = getAudio();
        if (!audio) return;
        audio.addEventListener('ended', () => this.next());
        audio.addEventListener('timeupdate', () => { this.updateProgress(); this._updateLyricsHighlight(); });
        audio.addEventListener('loadedmetadata', () => this.updateProgress());
    },

    toggleLyrics() {
        this.lyricsOpen = !this.lyricsOpen;
        const panel = document.getElementById('lyricsPanel');
        if (panel) panel.style.display = this.lyricsOpen ? 'block' : 'none';
        if (this.lyricsOpen) this._loadLyricsForCurrentTrack();
    },

    async _loadLyricsForCurrentTrack() {
        const track = this.queue[this.index];
        const panel = document.getElementById('lyricsPanel');
        if (!track || !panel) return;
        if (this.lyricsForUrl === track.url) return; // already loaded for this track

        panel.innerHTML = '<div class="lyrics-empty">Henter sangtekst…</div>';
        this.lyricsLines = [];
        this.lyricsForUrl = track.url;
        this.lyricsActiveIndex = -1;

        try {
            const resp = await fetch(`/api/lyrics?title=${encodeURIComponent(track.title)}&artist=${encodeURIComponent(track.artist)}`);
            const data = await resp.json();
            if (this.lyricsForUrl !== track.url) return; // track changed while fetching

            if (data.lines && data.lines.length) {
                this.lyricsLines = data.lines;
                panel.innerHTML = data.lines.map((l, i) => `<div class="lyrics-line" data-i="${i}">${escapeHtml(l.text || ' ')}</div>`).join('');
            } else if (data.plain) {
                panel.innerHTML = `<div class="lyrics-line">${escapeHtml(data.plain).replace(/\n/g, '<br>')}</div>`;
            } else {
                panel.innerHTML = '<div class="lyrics-empty">Ingen sangtekst fundet</div>';
            }
        } catch {
            panel.innerHTML = '<div class="lyrics-empty">Ingen sangtekst fundet</div>';
        }
    },

    _updateLyricsHighlight() {
        if (!this.lyricsOpen || !this.lyricsLines.length) return;
        const audio = getAudio();
        if (!audio) return;
        const t = audio.currentTime;
        let active = -1;
        for (let i = 0; i < this.lyricsLines.length; i++) {
            if (this.lyricsLines[i].t <= t) active = i; else break;
        }
        if (active === this.lyricsActiveIndex) return;
        this.lyricsActiveIndex = active;

        const panel = document.getElementById('lyricsPanel');
        if (!panel) return;
        panel.querySelectorAll('.lyrics-line.active').forEach(el => el.classList.remove('active'));
        const el = panel.querySelector(`[data-i="${active}"]`);
        if (el) {
            el.classList.add('active');
            el.scrollIntoView({ block: 'center', behavior: 'smooth' });
        }
    },

    // data-queue on the clicked element is a JSON array of {url,title,artist,album};
    // data-index is this element's position in that array.
    playQueueAt(queueJson, index) {
        try {
            this.queue = JSON.parse(queueJson);
        } catch {
            return;
        }
        this.index = index;
        this._playCurrent();
    },

    next() {
        if (this.index + 1 >= this.queue.length) return;
        this.index++;
        this._playCurrent();
    },

    previous() {
        if (this.index <= 0) return;
        this.index--;
        this._playCurrent();
    },

    togglePlay() {
        const audio = getAudio();
        if (!audio) return;
        if (audio.paused) audio.play().catch(() => {});
        else audio.pause();
        this.updatePlayButton();
    },

    seekTo(seconds) {
        const audio = getAudio();
        if (audio) audio.currentTime = seconds;
    },

    _playCurrent() {
        const audio = getAudio();
        const track = this.queue[this.index];
        if (!audio || !track) return;
        audio.src = track.url;
        audio.play().catch(() => {});
        this._renderBar(track);
    },

    _renderBar(track) {
        const bar = document.getElementById('playerBar');
        if (!bar) return;
        bar.style.display = 'flex';
        const title = document.getElementById('barTitle');
        const artist = document.getElementById('barArtist');
        if (title) title.textContent = track.title;
        if (artist) artist.textContent = track.album ? `${track.artist} · ${track.album}` : track.artist;

        document.getElementById('barPrev')?.toggleAttribute('disabled', this.index <= 0);
        document.getElementById('barNext')?.toggleAttribute('disabled', this.index + 1 >= this.queue.length);

        document.querySelectorAll('.track-row.playing').forEach(el => el.classList.remove('playing'));
        document.querySelector(`[data-track-url="${cssEscape(track.url)}"]`)?.classList.add('playing');

        this.updatePlayButton();
        if (this.lyricsOpen) this._loadLyricsForCurrentTrack();
    },

    updatePlayButton() {
        const audio = getAudio();
        const btn = document.getElementById('barPlayPause');
        if (btn && audio) btn.textContent = audio.paused ? '▶' : '⏸';
    },

    updateProgress() {
        const audio = getAudio();
        if (!audio) return;
        const cur = document.getElementById('barCurrentTime');
        const dur = document.getElementById('barDuration');
        const seek = document.getElementById('barSeek');
        if (cur) cur.textContent = formatTime(audio.currentTime);
        if (dur) dur.textContent = formatTime(audio.duration || 0);
        if (seek) {
            seek.max = audio.duration || 1;
            seek.value = audio.currentTime;
        }
    },
};

function formatTime(seconds) {
    if (!isFinite(seconds) || seconds < 0) seconds = 0;
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return `${m}:${s.toString().padStart(2, '0')}`;
}

function cssEscape(s) {
    return s.replace(/["\\]/g, '\\$&');
}

function escapeHtml(s) {
    const div = document.createElement('div');
    div.textContent = s;
    return div.innerHTML;
}

// A single delegated listener, added once here (not through Blazor), means
// the very first play() per click fires synchronously inside the actual
// click - Chrome's autoplay policy silently blocks play() calls that happen
// after an async round trip loses the "real user gesture" credit.
document.addEventListener('click', (e) => {
    const queueEl = e.target.closest('[data-queue]');
    if (queueEl) {
        window.playerInterop.playQueueAt(queueEl.dataset.queue, parseInt(queueEl.dataset.index, 10));
        return;
    }
    if (e.target.closest('#barPlayPause')) { window.playerInterop.togglePlay(); return; }
    if (e.target.closest('#barPrev')) { window.playerInterop.previous(); return; }
    if (e.target.closest('#barNext')) { window.playerInterop.next(); return; }
    if (e.target.closest('#barLyrics')) { window.playerInterop.toggleLyrics(); return; }
});

document.addEventListener('input', (e) => {
    if (e.target.id === 'barSeek') window.playerInterop.seekTo(parseFloat(e.target.value));
});

document.addEventListener('DOMContentLoaded', () => window.playerInterop.init());
if (document.readyState !== 'loading') window.playerInterop.init();
