// Bibliotek's own music player. One <audio> element lives for the whole visit (outside
// Blazor's markup, so page changes don't touch it), and the next track is put on it
// straight from 'ended'. That is what keeps an iPhone playing with the screen locked -
// Jellyfin's web player first asks the server about the next track, and a locked
// iPhone never lets it start. Buttons with data-play-album="<jellyfin id>" start an album.
(function () {
    var audio = null, ui = null;
    var album = null, pos = 0, marked = {}, startedAt = 0;

    function src(i) { return "/api/afspil/album/" + album.id + "/spor/" + i; }

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text) e.textContent = text;
        return e;
    }

    function btn(cls, text, label, onClick) {
        var b = el("button", cls, text);
        b.type = "button";
        b.setAttribute("aria-label", label);
        b.addEventListener("click", onClick);
        return b;
    }

    function time(s) {
        if (!isFinite(s) || s < 0) s = 0;
        s = Math.floor(s);
        return Math.floor(s / 60) + ":" + ("0" + (s % 60)).slice(-2);
    }

    function build() {
        audio = new Audio();
        audio.preload = "auto";

        var bar = el("div", "player");
        bar.hidden = true;
        var list = el("ol", "player-list");
        list.hidden = true;

        var row = el("div", "player-row");
        var cover = el("img", "player-cover");
        cover.alt = "";
        var text = el("div", "player-text");
        var title = el("div", "player-title");
        var sub = el("div", "player-sub");
        text.append(title, sub);
        text.addEventListener("click", function () { list.hidden = !list.hidden; });
        var play = btn("player-btn player-play", "▶", "Afspil / pause", function () {
            if (audio.paused) audio.play(); else audio.pause();
        });
        row.append(
            cover, text,
            btn("player-btn", "⏮", "Forrige nummer", prev),
            play,
            btn("player-btn", "⏭", "Næste nummer", next),
            btn("player-btn player-close", "✕", "Luk afspilleren", close));

        var seekRow = el("div", "player-seek");
        var now = el("span", "player-time", "0:00");
        var seek = el("input");
        seek.type = "range";
        seek.min = 0; seek.max = 1000; seek.value = 0;
        seek.setAttribute("aria-label", "Spol");
        var dur = el("span", "player-time", "0:00");
        seek.addEventListener("input", function () {
            if (isFinite(audio.duration)) audio.currentTime = audio.duration * seek.value / 1000;
        });
        seekRow.append(now, seek, dur);

        var hint = el("div", "player-hint", "Tryk på titlen for at se alle numre.");
        bar.append(list, row, seekRow, hint);
        document.body.appendChild(bar);
        ui = { bar: bar, list: list, cover: cover, title: title, sub: sub, play: play, seek: seek, now: now, dur: dur, hint: hint };

        audio.addEventListener("play", function () { ui.play.textContent = "⏸"; state("playing"); });
        audio.addEventListener("pause", function () { ui.play.textContent = "▶"; state("paused"); });
        audio.addEventListener("ended", function () {
            markPlayed();
            if (pos + 1 < album.tracks.length) go(pos + 1);
            else { pos = 0; audio.src = src(0); show(); }   // album done: ready at track 1, paused
        });
        audio.addEventListener("timeupdate", tick);
        audio.addEventListener("error", function () {
            if (!album || !audio.getAttribute("src")) return;
            ui.hint.textContent = "Nummer " + (pos + 1) + " kunne ikke afspilles – springer videre.";
            if (pos + 1 < album.tracks.length) setTimeout(function () { go(pos + 1); }, 1500);
        });

        if ("mediaSession" in navigator) {
            var ms = navigator.mediaSession;
            ms.setActionHandler("play", function () { audio.play(); });
            ms.setActionHandler("pause", function () { audio.pause(); });
            ms.setActionHandler("previoustrack", prev);
            ms.setActionHandler("nexttrack", next);
            try {
                ms.setActionHandler("seekto", function (d) { audio.currentTime = d.seekTime; });
            } catch (e) { /* older Safari has no seekto */ }
        }
    }

    function state(s) { if ("mediaSession" in navigator) navigator.mediaSession.playbackState = s; }

    // Must run inside the tap: src + play() here, the track list is fetched afterwards.
    function start(albumId, index) {
        if (!audio) build();
        album = { id: albumId, name: "", artist: "", cover: "", tracks: [] };
        pos = index;
        marked = {};
        audio.src = src(index);
        audio.play().catch(function () { });
        startedAt = Date.now();
        ui.title.textContent = "Henter…";
        ui.sub.textContent = "";
        ui.hint.textContent = "Tryk på titlen for at se alle numre.";
        ui.bar.hidden = false;
        document.body.classList.add("has-player");
        fetch("/api/afspil/album/" + albumId, { credentials: "same-origin" })
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (a) {
                if (album.id !== albumId) return;   // another album was started meanwhile
                album = a;
                renderList();
                show();
            })
            .catch(function () { ui.title.textContent = "Kunne ikke hente albummet"; });
    }

    function go(i) {
        pos = i;
        audio.src = src(i);
        audio.play().catch(function () { });
        startedAt = Date.now();
        show();
    }

    function next() { if (album && pos + 1 < album.tracks.length) go(pos + 1); }

    function prev() {
        if (!album) return;
        if (audio.currentTime > 3 || pos === 0) audio.currentTime = 0;
        else go(pos - 1);
    }

    function close() {
        audio.pause();
        audio.removeAttribute("src");
        audio.load();
        album = null;
        ui.bar.hidden = true;
        document.body.classList.remove("has-player");
        if ("mediaSession" in navigator) navigator.mediaSession.metadata = null;
    }

    function show() {
        var t = album.tracks[pos];
        if (!t) return;
        ui.title.textContent = t.name;
        ui.sub.textContent = t.artist + " · " + album.name;
        ui.cover.src = album.cover || "";
        ui.cover.hidden = !album.cover;
        Array.prototype.forEach.call(ui.list.children, function (li, i) { li.classList.toggle("on", i === pos); });
        if ("mediaSession" in navigator && window.MediaMetadata) {
            navigator.mediaSession.metadata = new MediaMetadata({
                title: t.name,
                artist: t.artist,
                album: album.name,
                artwork: album.cover ? [{ src: album.cover, sizes: "512x512", type: "image/jpeg" }] : []
            });
        }
    }

    function renderList() {
        ui.list.textContent = "";
        album.tracks.forEach(function (t, i) {
            var li = el("li");
            li.append(el("span", "player-li-name", t.name), el("span", "player-time", time(t.seconds)));
            li.addEventListener("click", function () { go(i); });
            ui.list.appendChild(li);
        });
    }

    function tick() {
        var d = audio.duration, c = audio.currentTime;
        if (!isFinite(d) || d <= 0) return;
        ui.seek.value = Math.round(c / d * 1000);
        ui.now.textContent = time(c);
        ui.dur.textContent = time(d);
        if (c > d / 2) markPlayed();   // counts as heard, like Jellyfin's own rule of thumb
        if ("mediaSession" in navigator && navigator.mediaSession.setPositionState && Date.now() - startedAt > 1000) {
            try { navigator.mediaSession.setPositionState({ duration: d, position: Math.min(c, d), playbackRate: 1 }); }
            catch (e) { }
        }
    }

    // Tell Jellyfin the track was played, once per track per album start.
    function markPlayed() {
        var t = album && album.tracks[pos];
        if (!t || marked[t.id]) return;
        marked[t.id] = true;
        fetch("/api/afspil/spillet/" + t.id, { method: "POST", credentials: "same-origin", keepalive: true }).catch(function () { });
    }

    document.addEventListener("click", function (e) {
        var b = e.target.closest && e.target.closest("[data-play-album]");
        if (!b) return;
        e.preventDefault();
        start(b.getAttribute("data-play-album"), parseInt(b.getAttribute("data-play-track") || "0", 10));
    });
})();
