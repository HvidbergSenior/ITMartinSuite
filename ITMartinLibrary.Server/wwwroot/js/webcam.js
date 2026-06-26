window.webcam = {

    video: null,
    stream: null,

    async start() {
        try {
            this.video = document.getElementById("video");

            if (!this.video)
                throw new Error("VIDEO ELEMENT NOT FOUND");

            this.stop();

            // Try progressively simpler constraints — old devices reject 4K or strict facingMode
            var constraintSets = [
                { video: { facingMode: { ideal: "environment" }, width: { ideal: 1920 }, height: { ideal: 1080 } }, audio: false },
                { video: { facingMode: "environment" }, audio: false },
                { video: true, audio: false }
            ];

            this.stream = null;
            var lastError = null;
            for (var i = 0; i < constraintSets.length; i++) {
                try {
                    this.stream = await this._getUserMedia(constraintSets[i]);
                    break;
                } catch (e) {
                    lastError = e;
                    console.warn("Camera attempt " + i + " failed, trying simpler constraints", e.name, e.message);
                }
            }
            if (!this.stream) throw lastError || new Error("Could not access camera");

            // Optional enhancements — use old-style null checks, not ?. (Chrome 63 compat)
            var track = this.stream.getVideoTracks()[0];
            if (track) {
                var capabilities = track.getCapabilities ? track.getCapabilities() : null;
                var settings = track.getSettings ? track.getSettings() : null;
                console.log("TRACK CAPABILITIES", capabilities);
                console.log("TRACK SETTINGS", settings);

                if (capabilities && capabilities.torch) {
                    try { await track.applyConstraints({ advanced: [{ torch: true }] }); console.log("TORCH ON"); } catch (e) {}
                }
                if (capabilities && capabilities.focusMode) {
                    try { await track.applyConstraints({ advanced: [{ focusMode: "continuous" }] }); } catch (e) {}
                }
            }

            this.video.srcObject = this.stream;
            this.video.autoplay  = true;
            this.video.muted     = true;
            this.video.playsInline = true;
            // Set as DOM attributes too — needed on some old Android WebViews
            this.video.setAttribute("autoplay", "");
            this.video.setAttribute("muted", "");
            this.video.setAttribute("playsinline", "");
            this.video.setAttribute("webkit-playsinline", "");

            await this.video.play();
            console.log("CAMERA READY", this.video.videoWidth, "x", this.video.videoHeight);
        } catch (err) {
            console.error("CAMERA FAILED", err.name, err.message);
            throw err;
        }
    },

    // Normalises getUserMedia across modern and legacy APIs (old Huawei Android WebView)
    _getUserMedia: function(constraints) {
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
            return navigator.mediaDevices.getUserMedia(constraints);
        }
        var legacyGUM = navigator.getUserMedia ||
                        navigator.webkitGetUserMedia ||
                        navigator.mozGetUserMedia;
        if (legacyGUM) {
            return new Promise(function(resolve, reject) {
                legacyGUM.call(navigator, constraints, resolve, reject);
            });
        }
        return Promise.reject(new Error("getUserMedia not supported on this device"));
    },

    async capture() {
        if (!this.video || !this.stream)
            throw new Error("Camera not started");

        // Brief pause for autofocus to settle
        await new Promise(function(x) { setTimeout(x, 500); });

        var w = this.video.videoWidth;
        var h = this.video.videoHeight;
        if (!w || !h) throw new Error("Video not ready");

        // Cap at 1536px longest side — enough for book cover text, keeps payload under 1MB
        var MAX = 1536;
        if (w > MAX || h > MAX) {
            var ratio = Math.min(MAX / w, MAX / h);
            w = Math.round(w * ratio);
            h = Math.round(h * ratio);
        }

        var canvas = document.createElement("canvas");
        canvas.width  = w;
        canvas.height = h;

        var ctx = canvas.getContext("2d");
        ctx.drawImage(this.video, 0, 0, w, h);

        console.log("CAPTURED", w, "x", h);

        return { image: canvas.toDataURL("image/jpeg", 0.88) };
    },

    stop: function() {
        try {
            if (!this.stream) return;
            this.stream.getTracks().forEach(function(t) { t.stop(); });
            this.stream = null;
            console.log("CAMERA STOPPED");
        } catch (e) {
            console.error("STOP CAMERA FAILED", e);
        }
    },

    speak: function(text) {
        if (!window.speechSynthesis) return;
        try {
            var u = new SpeechSynthesisUtterance(text);
            u.lang = 'da-DK';
            u.rate = 0.9;
            speechSynthesis.cancel();
            speechSynthesis.speak(u);
        } catch (e) {}
    },

    flash: function() {
        var wrap = document.querySelector('.shelf-camera-wrap');
        if (!wrap) return;
        var f = document.createElement('div');
        f.style.cssText = 'position:absolute;inset:0;background:white;pointer-events:none;z-index:20;opacity:0.8;transition:opacity .35s ease-out;';
        wrap.appendChild(f);
        // double rAF ensures browser paints before transition starts
        requestAnimationFrame(function() {
            requestAnimationFrame(function() { f.style.opacity = '0'; });
        });
        setTimeout(function() { if (f.parentNode) f.remove(); }, 400);
    }
};

window.barcodeScanner = {
    stream: null,
    animFrame: null,
    lastCode: null,
    lastTime: 0,

    async start(videoId, dotNetRef) {
        var video = document.getElementById(videoId);
        if (!video) return;
        try {
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: 'environment', width: { ideal: 1280 } }
            });
            video.srcObject = this.stream;
            await video.play();
            this.scan(video, dotNetRef);
        } catch (e) { console.error('barcodeScanner.start', e); }
    },

    scan: function(video, dotNetRef) {
        if (!('BarcodeDetector' in window)) {
            console.warn('BarcodeDetector not supported');
            return;
        }
        var detector = new BarcodeDetector({ formats: ['ean_13', 'ean_8', 'code_128', 'upc_a'] });
        var self = this;
        var loop = async function() {
            if (!self.stream) return;
            try {
                var barcodes = await detector.detect(video);
                if (barcodes.length > 0) {
                    var code = barcodes[0].rawValue;
                    var now = Date.now();
                    if (code !== self.lastCode || now - self.lastTime > 3000) {
                        self.lastCode = code;
                        self.lastTime = now;
                        await dotNetRef.invokeMethodAsync('OnBarcodeDetected', code);
                    }
                }
            } catch (e) {}
            self.animFrame = requestAnimationFrame(loop);
        };
        this.animFrame = requestAnimationFrame(loop);
    },

    stop: function() {
        if (this.animFrame) cancelAnimationFrame(this.animFrame);
        this.animFrame = null;
        if (this.stream) { this.stream.getTracks().forEach(function(t) { t.stop(); }); this.stream = null; }
        this.lastCode = null;
    }
};

window.downloadFile = function(filename, mimeType, base64) {
    var a = document.createElement('a');
    a.href = 'data:' + mimeType + ';base64,' + base64;
    a.download = filename;
    a.click();
};
