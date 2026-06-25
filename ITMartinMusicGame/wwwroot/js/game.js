window.game = (function () {

    var _audio = null;
    var _recorder = null;
    var _chunks = [];
    var _stream = null;
    var _recognition = null;
    var _transcript = "";
    var _photoTimer = null;
    var _clipTimer = null;
    var _dotnet = null; // DotNetObjectReference for callbacks

    // ── Audio playback ─────────────────────────────────────────────────────────

    function playClip(url, durationSec, dotnetRef) {
        _dotnet = dotnetRef;
        stopAudio();
        _audio = new Audio(url);
        _audio.preload = "auto";
        _audio.volume = 1.0;

        _audio.play().catch(function (e) {
            console.error("Audio play failed", e);
        });

        // Auto-stop after durationSec
        _clipTimer = setTimeout(function () {
            stopAudio();
            if (_dotnet) _dotnet.invokeMethodAsync("OnClipEnded");
        }, durationSec * 1000);
    }

    function stopAudio() {
        clearTimeout(_clipTimer);
        if (_audio) {
            _audio.pause();
            _audio.src = "";
            _audio = null;
        }
    }

    function restartClip(url, dotnetRef) {
        _dotnet = dotnetRef;
        stopAudio();
        _audio = new Audio(url);
        _audio.volume = 1.0;
        _audio.play().catch(function (e) { console.error("Restart failed", e); });
    }

    // ── Performance: camera + mic + speech recognition ─────────────────────────

    function startPerformance(videoId, dotnetRef) {
        _dotnet = dotnetRef;
        _transcript = "";
        _chunks = [];

        var constraints = {
            audio: true,
            video: { width: { ideal: 1280 }, height: { ideal: 720 }, facingMode: "user" }
        };

        return navigator.mediaDevices.getUserMedia(constraints)
            .then(function (stream) {
                _stream = stream;

                // Show camera preview
                var vid = document.getElementById(videoId);
                if (vid) {
                    vid.srcObject = stream;
                    vid.muted = true;
                    vid.play().catch(function () { });
                }

                // MediaRecorder
                var mime = "video/webm;codecs=vp8,opus";
                if (!MediaRecorder.isTypeSupported(mime)) mime = "video/webm";
                if (!MediaRecorder.isTypeSupported(mime)) mime = "";
                var opts = mime ? { mimeType: mime } : {};
                _recorder = new MediaRecorder(stream, opts);
                _recorder.ondataavailable = function (e) {
                    if (e.data && e.data.size > 0) _chunks.push(e.data);
                };
                _recorder.start(500);

                // Speech recognition
                var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
                if (SR) {
                    _recognition = new SR();
                    _recognition.continuous = true;
                    _recognition.interimResults = false;
                    _recognition.lang = "da-DK";
                    _recognition.onresult = function (e) {
                        for (var i = e.resultIndex; i < e.results.length; i++) {
                            if (e.results[i].isFinal)
                                _transcript += " " + e.results[i][0].transcript;
                        }
                    };
                    _recognition.onerror = function (e) {
                        console.warn("Speech recognition error:", e.error);
                    };
                    _recognition.start();
                }

                // Take photo at 10 seconds
                _photoTimer = setTimeout(function () {
                    var photo = captureFrame(videoId);
                    if (_dotnet && photo) _dotnet.invokeMethodAsync("OnPhotoCapture", photo);
                }, 10000);
            })
            .catch(function (err) {
                console.error("Camera/mic failed:", err.name, err.message);
                // Fall back to audio only
                return navigator.mediaDevices.getUserMedia({ audio: true, video: false })
                    .then(function (stream) {
                        _stream = stream;
                        _recorder = new MediaRecorder(stream);
                        _recorder.ondataavailable = function (e) {
                            if (e.data && e.data.size > 0) _chunks.push(e.data);
                        };
                        _recorder.start(500);
                        startSpeechOnly();
                    });
            });
    }

    function startSpeechOnly() {
        var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SR) return;
        _recognition = new SR();
        _recognition.continuous = true;
        _recognition.interimResults = false;
        _recognition.lang = "da-DK";
        _recognition.onresult = function (e) {
            for (var i = e.resultIndex; i < e.results.length; i++) {
                if (e.results[i].isFinal) _transcript += " " + e.results[i][0].transcript;
            }
        };
        _recognition.start();
    }

    function stopPerformance() {
        clearTimeout(_photoTimer);
        if (_recognition) { try { _recognition.stop(); } catch (e) { } _recognition = null; }

        return new Promise(function (resolve) {
            if (!_recorder) { stopStream(); resolve({ recording: "", transcript: _transcript.trim(), hasVideo: false }); return; }

            var rec = _recorder;
            var hasVideo = rec.mimeType && rec.mimeType.startsWith("video");
            _recorder = null;

            rec.onstop = function () {
                var mime = rec.mimeType || (hasVideo ? "video/webm" : "audio/webm");
                var blob = new Blob(_chunks, { type: mime });
                _chunks = [];
                stopStream();

                if (blob.size === 0) { resolve({ recording: "", transcript: _transcript.trim(), hasVideo: hasVideo }); return; }

                var reader = new FileReader();
                reader.onload = function () {
                    var b64 = reader.result.split(",")[1];
                    resolve({ recording: b64, transcript: _transcript.trim(), hasVideo: hasVideo });
                };
                reader.readAsDataURL(blob);
            };
            rec.stop();
        });
    }

    function captureFrame(videoId) {
        var vid = document.getElementById(videoId);
        if (!vid || !vid.videoWidth) return null;
        var canvas = document.createElement("canvas");
        var w = Math.min(vid.videoWidth, 800);
        var h = Math.round(vid.videoHeight * (w / vid.videoWidth));
        canvas.width = w; canvas.height = h;
        canvas.getContext("2d").drawImage(vid, 0, 0, w, h);
        return canvas.toDataURL("image/jpeg", 0.82).split(",")[1];
    }

    function stopStream() {
        if (_stream) { _stream.getTracks().forEach(function (t) { t.stop(); }); _stream = null; }
        var vid = document.getElementById("perf-preview");
        if (vid) vid.srcObject = null;
    }

    // ── Playback of recordings ─────────────────────────────────────────────────

    function playRecording(b64, mimeType) {
        var el = document.createElement(mimeType.startsWith("video") ? "video" : "audio");
        el.src = "data:" + mimeType + ";base64," + b64;
        el.controls = true;
        el.style.cssText = "position:fixed;bottom:20px;left:50%;transform:translateX(-50%);z-index:999;max-width:90vw;border-radius:12px;box-shadow:0 4px 24px #000a";
        document.body.appendChild(el);
        el.play();
        el.onended = function () { document.body.removeChild(el); };
    }

    return {
        playClip: playClip,
        stopAudio: stopAudio,
        restartClip: restartClip,
        startPerformance: startPerformance,
        stopPerformance: stopPerformance,
        playRecording: playRecording,
        captureFrame: captureFrame
    };
})();
