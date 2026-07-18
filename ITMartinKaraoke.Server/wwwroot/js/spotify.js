window.spotifyPlayer = (function () {

    var _player = null;
    var _deviceId = null;
    var _ready = null;

    function loadSdk() {
        if (window.Spotify) return Promise.resolve();
        return new Promise(function (resolve) {
            window.onSpotifyWebPlaybackSDKReady = resolve;
            var tag = document.createElement("script");
            tag.src = "https://sdk.scdn.co/spotify-player.js";
            document.head.appendChild(tag);
        });
    }

    function ensureConnected() {
        if (_ready) return _ready;

        _ready = loadSdk().then(function () {
            return new Promise(function (resolve, reject) {
                _player = new Spotify.Player({
                    name: "ITMartin Karaoke",
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

    function setVolume(vol) {
        return ensureConnected().then(function () {
            if (_player) return _player.setVolume(Math.max(0, Math.min(1, vol)));
        });
    }

    function getState() {
        if (!_player) return Promise.resolve(null);
        return _player.getCurrentState().then(function (state) {
            if (!state) return null;
            return { positionMs: state.position, durationMs: state.duration, paused: state.paused };
        });
    }

    var _lyricSyncTimer = null;
    var _lyricSyncScrollHandler = null;
    var _lastManualScroll = 0;
    var MANUAL_SCROLL_PAUSE_MS = 2500;

    function startLyricSync(containerId) {
        stopLyricSync();
        var container = document.getElementById(containerId);
        if (!container) return;

        var lines = Array.prototype.slice.call(container.querySelectorAll("[data-time]"));
        if (lines.length === 0) return;

        _lastManualScroll = 0;
        _lyricSyncScrollHandler = function () { _lastManualScroll = Date.now(); };
        window.addEventListener("wheel", _lyricSyncScrollHandler, { passive: true });
        window.addEventListener("touchmove", _lyricSyncScrollHandler, { passive: true });

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
                    lines[j].classList.toggle("active", j === activeIndex);
                }
                var recentlyScrolledByHand = (Date.now() - _lastManualScroll) < MANUAL_SCROLL_PAUSE_MS;
                if (activeIndex >= 0 && !recentlyScrolledByHand) {
                    lines[activeIndex].scrollIntoView({ block: "center", behavior: "smooth" });
                }
            });
        }, 300);
    }

    function stopLyricSync() {
        if (_lyricSyncTimer) { clearInterval(_lyricSyncTimer); _lyricSyncTimer = null; }
        if (_lyricSyncScrollHandler) {
            window.removeEventListener("wheel", _lyricSyncScrollHandler);
            window.removeEventListener("touchmove", _lyricSyncScrollHandler);
            _lyricSyncScrollHandler = null;
        }
    }

    return {
        ensureConnected: ensureConnected,
        playTrack: playTrack,
        pause: pause,
        resume: resume,
        setVolume: setVolume,
        getState: getState,
        startLyricSync: startLyricSync,
        stopLyricSync: stopLyricSync
    };

})();
