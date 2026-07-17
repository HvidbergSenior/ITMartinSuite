window.studio = (function() {

    var _mediaRecorder = null;
    var _chunks = [];
    var _micStream = null;
    var _camStream = null;
    var _songKey = null;
    var _videoMode = false;
    var _mixAudios = [];
    var _mixVolume = 0.5; // default lower than full - reduces speaker bleed into the mic when no headphones are used
    var _audioCtx = null;
    var _micGainNode = null;
    var _micGain = 1.5; // default boost - "record me higher"

    // ── Recording ──────────────────────────────────────────────────────────────

    function startRecording(songKey, videoMode) {
        _songKey = songKey;
        _videoMode = !!videoMode;
        _chunks = [];

        // Explicitly OFF: these are WebRTC voice-call constraints (tuned for
        // laptop mics in noisy rooms), and on a real condenser/USB mic they
        // actively hurt quality - pumping/gating artifacts that read as
        // "background noise", and a processed, degraded vocal tone. The
        // correct way to avoid backing-track bleed during overdub is
        // headphones (see the hint next to the "optag med sangen" button),
        // not browser-side audio processing on the mic signal.
        var micConstraints = {
            echoCancellation: false,
            noiseSuppression: false,
            autoGainControl: false
        };
        var constraints = _videoMode
            ? { audio: micConstraints, video: { width: { ideal: 1280 }, height: { ideal: 720 } } }
            : { audio: micConstraints, video: false };

        return navigator.mediaDevices.getUserMedia(constraints)
            .then(function(stream) {
                _micStream = stream;

                if (_videoMode) {
                    var preview = document.getElementById("rec-preview");
                    if (preview) {
                        preview.srcObject = stream;
                        preview.muted = true;
                        preview.play().catch(function() {});
                    }
                }

                // A plain linear gain boost on the raw mic signal - not the
                // same thing as the AGC/noise-suppression left off above.
                // Those are adaptive/dynamic processing that degrades tone;
                // this is just "make the whole recorded signal louder",
                // which is what "record me higher" actually needs.
                _audioCtx = _audioCtx || new (window.AudioContext || window.webkitAudioContext)();
                var micSource = _audioCtx.createMediaStreamSource(new MediaStream(stream.getAudioTracks()));
                _micGainNode = _audioCtx.createGain();
                _micGainNode.gain.value = _micGain;
                var dest = _audioCtx.createMediaStreamDestination();
                micSource.connect(_micGainNode).connect(dest);

                var recordStream = _videoMode
                    ? new MediaStream(stream.getVideoTracks().concat(dest.stream.getAudioTracks()))
                    : dest.stream;

                var mimeType = _videoMode ? "video/webm;codecs=vp8,opus" : "audio/webm";
                if (!MediaRecorder.isTypeSupported(mimeType)) {
                    mimeType = "video/webm";
                }
                if (!MediaRecorder.isTypeSupported(mimeType)) {
                    mimeType = "";
                }

                // Default Opus bitrate leans low (voice-call territory) -
                // 192kbps gives a real condenser mic's vocal recording
                // noticeably more headroom/clarity than the browser default.
                var opts = mimeType ? { mimeType: mimeType, audioBitsPerSecond: 192000 } : {};
                _mediaRecorder = new MediaRecorder(recordStream, opts);

                _mediaRecorder.ondataavailable = function(e) {
                    if (e.data && e.data.size > 0) _chunks.push(e.data);
                };

                _mediaRecorder.start(500);
                console.log("Recording started", _videoMode ? "video" : "audio", mimeType);
            })
            .catch(function(err) {
                console.error("Media access failed", err.name, err.message);
                throw err;
            });
    }

    function stopMix() {
        _mixAudios.forEach(function(a) { try { a.pause(); a.currentTime = 0; } catch(e) {} });
        _mixAudios = [];
    }

    // Boosts your recorded mic level (voice + guitar picked up together, e.g.
    // by a HyperX QuadCast) - takes effect immediately if a recording is
    // already running, and carries over as the default for the next one.
    function setMicGain(gain) {
        _micGain = Math.max(0.1, Math.min(4, gain));
        if (_micGainNode) _micGainNode.gain.value = _micGain;
    }

    // Volume of the reference/backing track only - this plays through your own
    // speakers/headphones for timing, it is never part of the recorded file
    // (recording is mic-only, see startRecording above). Turning this down
    // makes it easier to hear your own playing while performing; it does not
    // change the balance of anything already in the recording.
    function setMixVolume(vol) {
        _mixVolume = Math.max(0, Math.min(1, vol));
        _mixAudios.forEach(function(a) { a.volume = _mixVolume; });
    }

    function playMix(urls) {
        stopMix();
        _mixAudios = urls.map(function(url) {
            var a = new Audio(url);
            a.volume = _mixVolume;
            a.play().catch(function(e) { console.error("Mix playback failed", e); });
            return a;
        });
    }

    function startOverdub(songKey, playUrl, videoMode) {
        stopMix();
        var playback = new Audio(playUrl);
        playback.volume = _mixVolume;
        playback.play().catch(function(e) { console.error("Overdub playback failed", e); });
        _mixAudios = [playback];
        return startRecording(songKey, videoMode);
    }

    function stopRecording() {
        stopMix();
        return new Promise(function(resolve) {
            if (!_mediaRecorder) { resolve(); return; }

            // Capture before nulling to avoid the null reference in onstop
            var recorder = _mediaRecorder;
            var capturedMime = recorder.mimeType || (_videoMode ? "video/webm" : "audio/webm");
            var capturedKey = _songKey;
            _mediaRecorder = null;

            // Stop video preview
            var preview = document.getElementById("rec-preview");
            if (preview) preview.srcObject = null;

            recorder.onstop = function() {
                var blob = new Blob(_chunks, { type: capturedMime });
                _chunks = [];

                if (_micStream) {
                    _micStream.getTracks().forEach(function(t) { t.stop(); });
                    _micStream = null;
                }

                if (!capturedKey || blob.size === 0) { resolve(); return; }

                fetch("/api/recording/" + encodeURIComponent(capturedKey), {
                    method: "POST",
                    headers: { "Content-Type": capturedMime },
                    body: blob
                })
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    console.log("Recording saved", data.path);
                    resolve();
                })
                .catch(function(err) {
                    console.error("Upload failed", err);
                    resolve();
                });
            };

            recorder.stop();
        });
    }

    // ── Camera (for photo capture) ─────────────────────────────────────────────

    function startCamera(videoId) {
        var video = document.getElementById(videoId);
        if (!video) return Promise.reject(new Error("Video element not found: " + videoId));

        stopCamera();

        var constraintSets = [
            { video: { facingMode: { ideal: "environment" }, width: { ideal: 1920 }, height: { ideal: 1080 } }, audio: false },
            { video: { facingMode: "environment" }, audio: false },
            { video: true, audio: false }
        ];

        function tryNext(i) {
            if (i >= constraintSets.length) {
                return Promise.reject(new Error("Could not access camera"));
            }
            return _getUserMedia(constraintSets[i]).then(function(stream) {
                _camStream = stream;
                video.srcObject = stream;
                video.setAttribute("autoplay", "");
                video.setAttribute("muted", "");
                video.setAttribute("playsinline", "");
                video.setAttribute("webkit-playsinline", "");
                video.autoplay = true;
                video.muted = true;
                video.playsInline = true;
                return video.play().catch(function() {});
            }).catch(function(e) {
                console.warn("Camera attempt " + i + " failed", e.name);
                return tryNext(i + 1);
            });
        }

        return tryNext(0);
    }

    function stopCamera() {
        if (_camStream) {
            _camStream.getTracks().forEach(function(t) { t.stop(); });
            _camStream = null;
        }
    }

    function capturePhoto(videoId) {
        var video = document.getElementById(videoId);
        if (!video) return "";

        return new Promise(function(resolve) {
            setTimeout(function() {
                var canvas = document.createElement("canvas");
                var w = video.videoWidth || 640;
                var h = video.videoHeight || 480;

                var MAX = 1600;
                if (w > MAX || h > MAX) {
                    var r = Math.min(MAX / w, MAX / h);
                    w = Math.round(w * r);
                    h = Math.round(h * r);
                }

                canvas.width = w;
                canvas.height = h;
                canvas.getContext("2d").drawImage(video, 0, 0, w, h);

                var base64 = canvas.toDataURL("image/jpeg", 0.88);
                var stripped = base64.replace("data:image/jpeg;base64,", "");
                resolve(stripped);
            }, 300);
        });
    }

    function _getUserMedia(constraints) {
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            return navigator.mediaDevices.getUserMedia(constraints);
        }
        var legacy = navigator.getUserMedia || navigator.webkitGetUserMedia || navigator.mozGetUserMedia;
        if (legacy) {
            return new Promise(function(resolve, reject) {
                legacy.call(navigator, constraints, resolve, reject);
            });
        }
        return Promise.reject(new Error("getUserMedia not supported"));
    }

    return {
        startRecording: startRecording,
        stopRecording: stopRecording,
        startOverdub: startOverdub,
        playMix: playMix,
        stopMix: stopMix,
        setMixVolume: setMixVolume,
        setMicGain: setMicGain,
        startCamera: startCamera,
        stopCamera: stopCamera,
        capturePhoto: capturePhoto
    };

})();
