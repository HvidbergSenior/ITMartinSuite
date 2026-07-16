window.spotifyPlayer = (function () {

    var _player = null;
    var _deviceId = null;
    var _ready = null; // Promise, resolves once the SDK connects and gives us a device id

    function loadSdk() {
        if (window.Spotify) return Promise.resolve();
        return new Promise(function (resolve) {
            window.onSpotifyWebPlaybackSDKReady = resolve;
            var tag = document.createElement("script");
            tag.src = "https://sdk.scdn.co/spotify-player.js";
            document.head.appendChild(tag);
        });
    }

    // Idempotent - safe to call every time the user opens a page that might
    // want to play a Spotify track; only actually connects once.
    function ensureConnected() {
        if (_ready) return _ready;

        _ready = loadSdk().then(function () {
            return new Promise(function (resolve, reject) {
                _player = new Spotify.Player({
                    name: "ITMartin Studio",
                    getOAuthToken: function (cb) {
                        fetch("/api/spotify/token")
                            .then(function (r) { return r.json(); })
                            .then(function (data) { cb(data.accessToken); })
                            .catch(function () { cb(""); });
                    },
                    volume: 0.8
                });

                _player.addListener("ready", function (e) {
                    _deviceId = e.device_id;
                    resolve(_deviceId);
                });
                _player.addListener("not_ready", function () { _deviceId = null; });
                _player.addListener("initialization_error", function (e) { reject(e); });
                _player.addListener("authentication_error", function (e) { reject(e); });
                _player.addListener("account_error", function (e) { reject(e); });

                _player.connect();
            });
        });

        return _ready;
    }

    // Starts playback of a track on this browser tab's Connect device -
    // requires Spotify Premium (Web Playback SDK limitation, not ours).
    function playTrack(trackId) {
        return ensureConnected().then(function (deviceId) {
            return fetch("/api/spotify/token")
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    return fetch("https://api.spotify.com/v1/me/player/play?device_id=" + deviceId, {
                        method: "PUT",
                        headers: {
                            "Authorization": "Bearer " + data.accessToken,
                            "Content-Type": "application/json"
                        },
                        body: JSON.stringify({ uris: ["spotify:track:" + trackId] })
                    });
                });
        });
    }

    function pause() {
        if (_player) _player.pause();
    }

    function resume() {
        if (_player) _player.resume();
    }

    // Returns { positionMs, durationMs, paused } or null if nothing's loaded -
    // polled client-side to drive lyric-line highlighting.
    function getState() {
        if (!_player) return Promise.resolve(null);
        return _player.getCurrentState().then(function (state) {
            if (!state) return null;
            return { positionMs: state.position, durationMs: state.duration, paused: state.paused };
        });
    }

    // ── Lyric highlighting ────────────────────────────────────────────────
    // Pure client-side polling + DOM class toggling, deliberately not routed
    // through Blazor - a 3-4x/second round-trip over the SignalR circuit for
    // something this frequent would be wasteful and laggy, especially given
    // this app's circuit has to survive a reverse proxy (Tailscale Serve).

    var _lyricSyncTimer = null;

    function startLyricSync(containerId) {
        stopLyricSync();
        var container = document.getElementById(containerId);
        if (!container) return;

        var lines = Array.prototype.slice.call(container.querySelectorAll("[data-time]"));
        if (lines.length === 0) return;

        _lyricSyncTimer = setInterval(function () {
            getState().then(function (state) {
                if (!state) return;
                var positionSec = state.positionMs / 1000;

                var activeIndex = -1;
                for (var i = 0; i < lines.length; i++) {
                    if (parseFloat(lines[i].dataset.time) <= positionSec) activeIndex = i;
                    else break;
                }

                for (var j = 0; j < lines.length; j++) {
                    lines[j].classList.toggle("lyric-line--active", j === activeIndex);
                }
                if (activeIndex >= 0) {
                    lines[activeIndex].scrollIntoView({ block: "center", behavior: "smooth" });
                }
            });
        }, 300);
    }

    function stopLyricSync() {
        if (_lyricSyncTimer) { clearInterval(_lyricSyncTimer); _lyricSyncTimer = null; }
    }

    return {
        ensureConnected: ensureConnected,
        playTrack: playTrack,
        pause: pause,
        resume: resume,
        getState: getState,
        startLyricSync: startLyricSync,
        stopLyricSync: stopLyricSync
    };

})();
