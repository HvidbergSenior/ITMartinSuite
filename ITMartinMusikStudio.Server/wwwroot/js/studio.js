window.studio = (function() {

    var _mediaRecorder = null;
    var _chunks = [];
    var _micStream = null;
    var _camStream = null;
    var _songKey = null;

    // ── Recording ──────────────────────────────────────────────────────────────

    function startRecording(songKey) {
        _songKey = songKey;
        _chunks = [];

        return navigator.mediaDevices.getUserMedia({ audio: true, video: false })
            .then(function(stream) {
                _micStream = stream;

                var mimeType = "audio/webm";
                if (!MediaRecorder.isTypeSupported(mimeType)) {
                    mimeType = "audio/ogg";
                }
                if (!MediaRecorder.isTypeSupported(mimeType)) {
                    mimeType = "";
                }

                var opts = mimeType ? { mimeType: mimeType } : {};
                _mediaRecorder = new MediaRecorder(stream, opts);

                _mediaRecorder.ondataavailable = function(e) {
                    if (e.data && e.data.size > 0) {
                        _chunks.push(e.data);
                    }
                };

                _mediaRecorder.start(500);
                console.log("Recording started", mimeType);
            })
            .catch(function(err) {
                console.error("Mic access failed", err.name, err.message);
                throw err;
            });
    }

    function stopRecording() {
        return new Promise(function(resolve) {
            if (!_mediaRecorder) { resolve(); return; }

            _mediaRecorder.onstop = function() {
                var blob = new Blob(_chunks, { type: _mediaRecorder.mimeType || "audio/webm" });
                _chunks = [];

                if (_micStream) {
                    _micStream.getTracks().forEach(function(t) { t.stop(); });
                    _micStream = null;
                }

                if (!_songKey || blob.size === 0) { resolve(); return; }

                fetch("/api/recording/" + encodeURIComponent(_songKey), {
                    method: "POST",
                    headers: { "Content-Type": blob.type },
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

            _mediaRecorder.stop();
            _mediaRecorder = null;
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
        startCamera: startCamera,
        stopCamera: stopCamera,
        capturePhoto: capturePhoto
    };

})();
